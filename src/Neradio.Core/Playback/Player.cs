using Neradio.Core.Stations;

namespace Neradio.Core.Playback;

// Plays one station at a time and keeps it playing: it connects in rounds over the station's
// streams, with longer and longer waits between them, until playback stops.
public sealed class Player(HttpClient http, TimeProvider time, IAudioEngine engine)
{
    // Before the second round and each one after it. The last wait repeats.
    private static readonly TimeSpan[] Waits =
    [
        TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4),
        TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(16), TimeSpan.FromSeconds(30),
    ];
    // Audio that plays this long starts the waits over.
    private static readonly TimeSpan Settled = TimeSpan.FromSeconds(60);
    // How often decoding is checked on, for the ICY titles waiting for their audio.
    private static readonly TimeSpan TitlePoll = TimeSpan.FromMilliseconds(250);

    private readonly Lock _gate = new();
    private Session? _session;
    // The connection whose audio plays, or played last and may still play from its buffer.
    private Connection? _audible;
    private ITimer? _titlePoll;
    // A title that repeats this one isn't a title change, even after a reconnect.
    private string? _lastTitle;

    public PlayerState State { get; private set; }
    public Station? Station { get; private set; }
    public string? Title { get; private set; }

    // Raised outside the player's lock, on any thread.
    public event Action<string>? TitleChanged;

    public void Play(Station station)
    {
        CancellationTokenSource? previous;
        Session session;
        lock (_gate)
        {
            previous = End();
            _lastTitle = null;
            session = Start(station);
        }
        previous?.Cancel();
        _ = RunAsync(session);
    }

    // The station stays selected, and Play connects to its live broadcast again. An interruption
    // counts only while the station plays or connects; any other stop ends one.
    public void Stop(StopKind kind)
    {
        CancellationTokenSource? run;
        lock (_gate)
        {
            if (kind == StopKind.Interruption && _session is null)
                return;
            run = End();
            State = kind == StopKind.Interruption ? PlayerState.Interrupted : PlayerState.Stopped;
        }
        run?.Cancel();
    }

    // The station plays again as if the user had played it, but a repeat of its last title still
    // isn't a title change.
    public void EndInterruption()
    {
        Session session;
        lock (_gate)
        {
            if (State != PlayerState.Interrupted)
                return;
            session = Start(Station!);
        }
        _ = RunAsync(session);
    }

    // The old connection would hang until it timed out, so it's dropped even while audio still
    // arrives on it. Its buffered audio plays on.
    public void NetworkChanged()
    {
        Session? session;
        CancellationTokenSource old;
        lock (_gate)
        {
            session = _session;
            if (session is null)
                return;
            if (session.Attempt != _audible)
                session.Attempt?.Audio?.Dispose();
            old = session.Restart();
            if (State == PlayerState.Playing)
                State = PlayerState.Reconnecting;
        }
        old.Cancel();
        _ = RunAsync(session);
    }

    // From the primary stream, with the waits started over.
    private Session Start(Station station)
    {
        var session = new Session(station);
        _session = session;
        Station = station;
        State = PlayerState.Connecting;
        _titlePoll ??= time.CreateTimer(_ => ShowReachedTitles(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _titlePoll.Change(TitlePoll, TitlePoll);
        return session;
    }

    // Stops the audio. The run it returns is cancelled once out of the lock: its awaits end on
    // the cancelling thread, and take the lock again.
    private CancellationTokenSource? End()
    {
        var session = _session;
        _session = null;
        session?.Attempt?.Audio?.Dispose();
        _audible?.Audio?.Dispose();
        _audible = null;
        Title = null;
        _titlePoll?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        return session?.Run;
    }

    // A stop or a network change has ended the run, though its token may not be cancelled yet.
    private void ThrowIfEnded(Session session, CancellationToken token)
    {
        if (_session != session || session.Run.Token != token)
            throw new OperationCanceledException(token);
    }

    private async Task RunAsync(Session session)
    {
        var token = session.Run.Token;
        try
        {
            while (true)
            {
                if (session.Rounds > 0)
                {
                    var wait = Waits[Math.Min(session.Rounds, Waits.Length) - 1];
                    await time.DelayAsync(wait, token).ConfigureAwait(false);
                }
                var played = await RoundAsync(session, token).ConfigureAwait(false);
                lock (_gate)
                {
                    ThrowIfEnded(session, token);
                    if (session.Streams.All(session.Skipped.Contains))
                    {
                        End();
                        State = PlayerState.Unsupported;
                        return;
                    }
                    session.Rounds = played >= Settled ? 0 : session.Rounds + 1;
                    State = PlayerState.Reconnecting;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    // Tries each stream once, without waits, until one plays: a playlist's streams in its place.
    // Returns how long its audio played before its connection was lost; null when none played.
    private async Task<TimeSpan?> RoundAsync(Session session, CancellationToken token)
    {
        var tried = new HashSet<Uri>();
        foreach (var url in session.RoundOrder())
        {
            var played = Playlists.IsPlaylist(url)
                ? await PlaylistAsync(session, url, tried, token).ConfigureAwait(false)
                : await StreamAsync(session, url, tried, token).ConfigureAwait(false);
            if (played is not null)
                return played;
        }
        return null;
    }

    // Downloaded each time a round reaches it. A playlist that can't be downloaded within the
    // limit, or lists no usable stream, is a stream that failed to connect.
    private async Task<TimeSpan?> PlaylistAsync(Session session, Uri url, HashSet<Uri> tried, CancellationToken token)
    {
        if (session.Skipped.Contains(url) || !tried.Add(url))
            return null;

        var text = await DownloadPlaylistAsync(url, token).ConfigureAwait(false);
        if (text is null)
            return null;
        if (Playlists.IsHls(text))
        {
            var hls = session.Hls(url, time);
            return await AttemptAsync(session, url, connection => hls.ReadAsync(http, url, text, connection), token).ConfigureAwait(false);
        }

        var streams = Playlists.Entries(url, text);
        foreach (var stream in streams)
        {
            var played = await StreamAsync(session, stream, tried, token).ConfigureAwait(false);
            if (played is not null)
                return played;
        }
        if (streams.Length > 0 && streams.All(session.Skipped.Contains))
            session.Skipped.Add(url);
        return null;
    }

    private async Task<string?> DownloadPlaylistAsync(Uri url, CancellationToken token)
    {
        using var limit = new CancellationTokenSource(Connection.Limit, time);
        using var download = CancellationTokenSource.CreateLinkedTokenSource(token, limit.Token);
        try
        {
            return await Playlists.DownloadAsync(http, url, download.Token).ConfigureAwait(false);
        }
        catch (Exception e) when (e is HttpRequestException or IOException or OperationCanceledException)
        {
            token.ThrowIfCancellationRequested();
            return null;
        }
    }

    private Task<TimeSpan?> StreamAsync(Session session, Uri url, HashSet<Uri> tried, CancellationToken token)
    {
        if (session.Skipped.Contains(url) || !tried.Add(url))
            return Task.FromResult<TimeSpan?>(null);
        return AttemptAsync(session, url, connection => IcyStream.ReadAsync(http, url, connection), token);
    }

    private async Task<TimeSpan?> AttemptAsync(Session session, Uri url, Func<Connection, Task> read, CancellationToken token)
    {
        using var connection = new Connection(engine, time, token);
        connection.Playing += () => OnPlaying(session, connection);
        connection.TitleChanged += title => OnOggTitle(connection, title);
        lock (_gate)
        {
            ThrowIfEnded(session, token);
            session.Attempt = connection;
        }
        try
        {
            await read(connection).ConfigureAwait(false);
        }
        catch (Exception e) when (e is HttpRequestException or IOException or OperationCanceledException)
        {
        }

        lock (_gate)
        {
            if (session.Attempt == connection)
                session.Attempt = null;
            // Lost while it played, its audio plays on from the buffer until another connection's plays.
            if (connection != _audible)
                connection.Audio?.Dispose();
            ThrowIfEnded(session, token);
            if (connection.IsUnsupported)
                session.Skipped.Add(url);
            if (connection.PlayingSince is not { } since)
                return null;
            session.LastPlayed = url;
            return time.GetUtcNow() - since;
        }
    }

    private void OnPlaying(Session session, Connection connection)
    {
        string[] changes;
        lock (_gate)
        {
            if (_session != session || session.Attempt != connection)
                return;
            connection.PlayingSince = time.GetUtcNow();
            // The engine plays a new stream only once the one before it has run out, so all of
            // that one's audio has been decoded.
            var rest = _audible?.ReachedTitles(long.MaxValue) ?? [];
            changes = Show(rest);
            _audible?.Audio?.Dispose();
            _audible = connection;
            State = PlayerState.Playing;
        }
        RaiseTitleChanged(changes);
    }

    // Shows each title in turn. Returns those that count as title changes.
    private string[] Show(IEnumerable<string> titles)
    {
        var changes = new List<string>();
        foreach (var title in titles)
        {
            var trimmed = title.Trim();
            Title = trimmed.Length == 0 ? null : trimmed;
            if (Title is null || Title == _lastTitle)
                continue;
            _lastTitle = Title;
            changes.Add(Title);
        }
        return changes.ToArray();
    }

    private void RaiseTitleChanged(string[] changes)
    {
        foreach (var change in changes)
            TitleChanged?.Invoke(change);
    }

    private void ShowReachedTitles()
    {
        string[] changes;
        lock (_gate)
        {
            var decoded = _audible?.Audio?.Decoded ?? 0;
            var reached = _audible?.ReachedTitles(decoded) ?? [];
            changes = Show(reached);
        }
        RaiseTitleChanged(changes);
    }

    private void OnOggTitle(Connection connection, string title)
    {
        string[] changes;
        lock (_gate)
        {
            if (connection != _audible)
                return;
            changes = Show([title]);
        }
        RaiseTitleChanged(changes);
    }

    // One station's playback, from Play until it stops.
    private sealed class Session(Station station)
    {
        private readonly Dictionary<Uri, HlsStream> _hls = [];

        public CancellationTokenSource Run { get; private set; } = new();
        public Uri[] Streams { get; } = [station.PrimaryStream, .. station.AlternateStreams];
        public HashSet<Uri> Skipped { get; } = [];
        public Uri? LastPlayed { get; set; }
        // Rounds since the waits started over.
        public int Rounds { get; set; }
        public Connection? Attempt { get; set; }

        // The stream that played last first, even one a playlist listed, then the others in the
        // station's order.
        public IEnumerable<Uri> RoundOrder() =>
            LastPlayed is null ? Streams : Streams.Where(url => url != LastPlayed).Prepend(LastPlayed);

        // Kept across reconnects, so that one goes on after the last segment downloaded.
        public HlsStream Hls(Uri url, TimeProvider time)
        {
            if (!_hls.TryGetValue(url, out var hls))
            {
                hls = new HlsStream(time);
                _hls.Add(url, hls);
            }
            return hls;
        }

        // Starts the waits over, in a new run. Returns the old run, to cancel.
        public CancellationTokenSource Restart()
        {
            var old = Run;
            Run = new CancellationTokenSource();
            Rounds = 0;
            return old;
        }
    }
}
