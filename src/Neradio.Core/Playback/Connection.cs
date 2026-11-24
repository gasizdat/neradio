namespace Neradio.Core.Playback;

// One attempt to connect to a stream, and the connection it becomes once its audio plays. Its
// token is cancelled when no data has arrived within the limit, and when the stream's format is
// unsupported. Disposing it leaves its audio playing: that's for its owner to stop.
internal sealed class Connection : IDisposable
{
    public static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

    private readonly IAudioEngine _engine;
    private readonly CancellationTokenSource _stall;
    private readonly CancellationTokenSource _cancel;
    private TimeSpan _limit = Limit;
    private volatile bool _isUnsupported;

    public Connection(IAudioEngine engine, TimeProvider time, CancellationToken cancellation)
    {
        _engine = engine;
        _stall = new CancellationTokenSource(Limit, time);
        _cancel = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _stall.Token);
    }

    public CancellationToken Token => _cancel.Token;
    public IAudioStream? Audio { get; private set; }
    public bool IsUnsupported => _isUnsupported;
    public DateTimeOffset? PlayingSince { get; set; }

    public event Action? Playing;

    public void Open(string contentType)
    {
        Audio = _engine.Open(contentType);
        Audio.Playing += () => Playing?.Invoke();
        Audio.Unsupported += Reject;
    }

    // HLS data arrives a segment at a time, so its limit counts the playlist's target duration too.
    public void AllowFor(TimeSpan targetDuration) => _limit = Limit + targetDuration;

    public void Received() => _stall.CancelAfter(_limit);

    // A full buffer isn't a stall: the limit only runs while the connection waits for data.
    public async ValueTask WriteAsync(ReadOnlyMemory<byte> audio)
    {
        _stall.CancelAfter(Timeout.InfiniteTimeSpan);
        await Audio!.WriteAsync(audio, Token).ConfigureAwait(false);
        _stall.CancelAfter(_limit);
    }

    public void Reject()
    {
        _isUnsupported = true;
        _cancel.Cancel();
    }

    public void Dispose()
    {
        _cancel.Dispose();
        _stall.Dispose();
    }
}
