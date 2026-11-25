using ManagedBass;
using ManagedBass.Mix;
using Neradio.Core.Playback.Abstractions;

namespace Neradio.Engine.Bass;

// One connection's audio: a decoding channel in the engine's mixer, once there's audio to decode.
internal abstract class MixerSource : IAudioStream
{
    // Q30: playback starts once this much audio is buffered.
    private const double StartSeconds = 4;
    protected const double BufferSeconds = 60;
    // How often a write waiting for room in the buffer tries again.
    protected static readonly TimeSpan Retry = TimeSpan.FromMilliseconds(100);

    // BASS holds it as a raw function pointer, so it must live as long as the channel.
    private readonly SyncProcedure _stalled;
    private readonly BassEngine _engine;
    private volatile bool _isReady;
    private volatile bool _hasRunOut;
    private volatile bool _isDisposed;

    protected MixerSource(BassEngine engine)
    {
        _engine = engine;
        _stalled = (_, _, data, _) =>
        {
            _hasRunOut = data == 0;
            if (_hasRunOut)
                _engine.TakeOver();
        };
    }

    public abstract long Decoded { get; }
    public bool IsReady => _isReady;
    public bool HasRunOut => _hasRunOut;

    public event Action? Playing;
    public event Action? Unsupported;
    public event Action<string>? TitleChanged;

    // Holds the writes and what they change against Dispose.
    protected Lock Gate { get; } = new();
    protected int Channel { get; private set; }
    protected bool IsDisposed => _isDisposed;
    protected abstract double BufferedSeconds { get; }

    public abstract ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellation);

    public void Play()
    {
        BassMix.ChannelFlags(Channel, BassFlags.Default, BassFlags.MixerChanPause);
        Raise(() => Playing?.Invoke());
    }

    public void Dispose()
    {
        lock (Gate)
        {
            if (_isDisposed)
                return;
            _isDisposed = true;
            if (Channel != 0)
                ManagedBass.Bass.StreamFree(Channel);
            Release();
        }
        _engine.Remove(this);
    }

    // Under the gate.
    protected void Attach(int channel)
    {
        Channel = channel;
        _engine.Add(channel);
        // A mixer sync can only be set on a channel that's in the mixer already.
        BassMix.ChannelSetSync(channel, SyncFlags.Stalled | SyncFlags.Mixtime, 0, _stalled);
    }

    // Outside the gate: the core's handlers take its lock, under which it disposes streams.
    protected void CheckReady()
    {
        if (_isReady || BufferedSeconds < StartSeconds)
            return;
        _isReady = true;
        _engine.TakeOver();
    }

    protected void RaiseUnsupported() => Raise(() => Unsupported?.Invoke());

    protected void RaiseTitleChanged(string title) => Raise(() => TitleChanged?.Invoke(title));

    // What Dispose wakes or frees besides the channel, under the gate.
    protected virtual void Release()
    {
    }

    // Never raised on the caller's thread, which can be the mixer's, inside a sync.
    private void Raise(Action raise) => ThreadPool.QueueUserWorkItem(_ =>
    {
        if (!_isDisposed)
            raise();
    });
}
