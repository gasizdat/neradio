namespace Neradio.Core.Playback.Abstractions;

// One connection's audio in the engine. Disposing it stops its audio at once; it raises no event
// after that, and drops what's written. The events can come on any thread.
public interface IAudioStream : IDisposable
{
    // How far decoding has got, counted in the bytes written.
    long Decoded { get; }

    // Raised once, when the stream's audio starts to play.
    event Action? Playing;
    event Action? Unsupported;
    // An Ogg stream's title, raised when decoding reaches the track that carries it.
    event Action<string>? TitleChanged;

    // Completes once the stream's buffer, 60 seconds of audio, has room for the data.
    ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellation);
}
