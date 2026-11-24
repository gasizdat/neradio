using Neradio.Core.Playback;

namespace Neradio.Core.Tests.Playback;

sealed class FakeEngine : IAudioEngine
{
    public List<FakeAudioStream> Streams { get; } = [];

    public IAudioStream Open(string contentType)
    {
        var stream = new FakeAudioStream(contentType);
        Streams.Add(stream);
        return stream;
    }
}

sealed class FakeAudioStream(string contentType) : IAudioStream
{
    private readonly MemoryStream _written = new();
    private TaskCompletionSource? _room;

    public string ContentType => contentType;
    public byte[] Written => _written.ToArray();
    public bool IsStopped { get; private set; }
    // While it's set, writes wait for Drain, as they do on a full buffer.
    public bool IsFull { get; set; }
    public long Decoded { get; set; }

    public event Action? Playing;
    public event Action? Unsupported;
    public event Action<string>? TitleChanged;

    public void Play() => Playing?.Invoke();

    public void RejectFormat() => Unsupported?.Invoke();

    public void ChangeTitle(string title) => TitleChanged?.Invoke(title);

    public void Drain()
    {
        IsFull = false;
        _room?.TrySetResult();
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellation)
    {
        if (IsFull)
        {
            var room = new TaskCompletionSource();
            _room = room;
            using var registration = cancellation.Register(() => room.TrySetCanceled(cancellation));
            await room.Task.ConfigureAwait(false);
        }
        _written.Write(data.Span);
    }

    public void Dispose() => IsStopped = true;
}
