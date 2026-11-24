namespace Neradio.Core.Playback;

public enum PlayerState
{
    Stopped,
    Connecting,
    Playing,
    Reconnecting,
    // Stopped because no stream of the station is in a format the app can play.
    Unsupported,
}
