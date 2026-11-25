using System.Runtime.InteropServices;
using ManagedBass;
using Neradio.Core.Playback;

namespace Neradio.Engine.Bass;

// AAC on Windows: decoded by Windows' own decoder as it's written, and its audio pushed into the
// mixer. The audio waits there to play, so the buffer is counted in decoded audio.
internal sealed class AacSource(BassEngine engine) : MixerSource(engine)
{
    // Data that holds no ADTS frame by then isn't AAC.
    private const int FramelessLimit = 64 * 1024;

    private readonly AdtsFramer _framer = new();
    // After each write: how many bytes of audio had been pushed, and how many had been written.
    private readonly Queue<(long Pushed, long Written)> _writes = new();
    private AacDecoder? _decoder;
    private int _bytesPerSecond;
    private long _pushed;
    private long _written;
    private long _decoded;

    public override long Decoded
    {
        get
        {
            lock (Gate)
            {
                var played = _pushed - Queued;
                while (_writes.TryPeek(out var write) && write.Pushed <= played)
                {
                    _writes.Dequeue();
                    _decoded = write.Written;
                }
                return _decoded;
            }
        }
    }

    protected override double BufferedSeconds => _bytesPerSecond == 0 ? 0 : (double)Queued / _bytesPerSecond;

    // Audio pushed that the mixer hasn't taken yet.
    private long Queued => Math.Max(ManagedBass.Bass.StreamPutData(Channel, IntPtr.Zero, 0), 0);

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellation)
    {
        lock (Gate)
        {
            if (IsDisposed)
                return;
            try
            {
                var frames = _framer.Push(data.Span);
                foreach (var frame in frames)
                    Decode(frame);
            }
            catch (COMException)
            {
                RaiseUnsupported();
                return;
            }
            _written += data.Length;
            _writes.Enqueue((_pushed, _written));
            if (_pushed == 0 && _written >= FramelessLimit)
                RaiseUnsupported();
        }
        CheckReady();
        while (BufferedSeconds >= BufferSeconds)
            await Task.Delay(Retry, cancellation).ConfigureAwait(false);
    }

    private void Decode(byte[] frame)
    {
        _decoder ??= new AacDecoder(frame);
        var pcm = _decoder.Decode(frame);
        foreach (var audio in pcm)
        {
            if (Channel == 0)
                Create();
            ManagedBass.Bass.StreamPutData(Channel, audio, audio.Length);
            _pushed += audio.Length;
        }
    }

    // The decoder has settled its output format once it produces audio.
    private void Create()
    {
        var sampleSize = _decoder!.IsFloat ? 4 : 2;
        var format = _decoder.IsFloat ? BassFlags.Float : BassFlags.Default;
        _bytesPerSecond = _decoder.Rate * _decoder.Channels * sampleSize;
        var channel = ManagedBass.Bass.CreateStream(_decoder.Rate, _decoder.Channels, BassFlags.Decode | format, StreamProcedureType.Push);
        Attach(channel);
    }
}
