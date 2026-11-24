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

    private readonly Lock _gate = new();
    private Session? _session;
    // The connection whose audio plays, or played last and may still play from its buffer.
    private Connection? _audible;

    public PlayerState State { get; private set; }
    public Station? Station { get; private set; }

    public void Play(Station station)
    {
        var session = new Session(station);
        CancellationTokenSource? previous;
        lock (_gate)
        {
            previous = End();
            _session = session;
            Station = station;
            State = PlayerState.Connecting;
        }
        previous?.Cancel();
        _ = RunAsync(session);
    }

    // The station stays selected, and Play connects to its live broadcast again.
    public void Stop()
    {
        CancellationTokenSource? run;
        lock (_gate)
        {
            run = End();
            State = PlayerState.Stopped;
        }
        run?.Cancel();
    }

    // Stops the audio. The run it returns is cancelled once out of the lock: its awaits end on
    // this thread, and take the lock again.
    private CancellationTokenSource? End()
    {
        var session = _session;
        _session = null;
        session?.Attempt?.Audio?.Dispose();
        _audible?.Audio?.Dispose();
        _audible = null;
        return session?.Run;
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
                    token.ThrowIfCancellationRequested();
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
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }

    // Tries each stream once, without waits, until one plays. Returns how long its audio played
    // before its connection was lost; null when none played.
    private async Task<TimeSpan?> RoundAsync(Session session, CancellationToken token)
    {
        foreach (var url in session.RoundOrder())
        {
            if (session.Skipped.Contains(url))
                continue;
            var played = await AttemptAsync(session, url, token).ConfigureAwait(false);
            if (played is not null)
                return played;
        }
        return null;
    }

    private async Task<TimeSpan?> AttemptAsync(Session session, Uri url, CancellationToken token)
    {
        using var connection = new Connection(engine, time, token);
        connection.Playing += () => OnPlaying(session, connection);
        lock (_gate)
        {
            token.ThrowIfCancellationRequested();
            session.Attempt = connection;
        }
        try
        {
            await IcyStream.ReadAsync(http, url, connection).ConfigureAwait(false);
        }
        catch (Exception e) when (e is HttpRequestException or IOException or OperationCanceledException)
        {
        }

        lock (_gate)
        {
            session.Attempt = null;
            // Lost while it played, its audio plays on from the buffer until another connection's plays.
            if (connection != _audible)
                connection.Audio?.Dispose();
            token.ThrowIfCancellationRequested();
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
        lock (_gate)
        {
            if (_session != session || session.Attempt != connection)
                return;
            connection.PlayingSince = time.GetUtcNow();
            // The engine plays a new stream only once the one before it has run out.
            _audible?.Audio?.Dispose();
            _audible = connection;
            State = PlayerState.Playing;
        }
    }

    // One station's playback, from Play until it stops.
    private sealed class Session(Station station)
    {
        public CancellationTokenSource Run { get; } = new();
        public Uri[] Streams { get; } = [station.PrimaryStream, .. station.AlternateStreams];
        public HashSet<Uri> Skipped { get; } = [];
        public Uri? LastPlayed { get; set; }
        // Rounds since the waits started over.
        public int Rounds { get; set; }
        public Connection? Attempt { get; set; }

        // The stream that played last first, then the others in the station's order.
        public IEnumerable<Uri> RoundOrder() =>
            LastPlayed is null ? Streams : Streams.Where(url => url != LastPlayed).Prepend(LastPlayed);
    }
}
