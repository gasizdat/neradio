namespace Neradio.Core.Playback.Abstractions;

// Decodes and mixes the streams the core downloads.
public interface IAudioEngine
{
    // A stream opened while another one's audio plays stays silent until that audio runs out and
    // about 4 seconds of its own are buffered.
    IAudioStream Open(string contentType);
}
