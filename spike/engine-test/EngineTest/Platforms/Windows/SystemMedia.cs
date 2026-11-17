using Windows.Media;
using Windows.Media.Playback;

namespace EngineTest;

static partial class SystemMedia
{
    // An unpackaged app gets the transport controls through a MediaPlayer that never plays.
    static readonly MediaPlayer Host = CreateHost();

    public static partial string Describe() => $"Windows {Environment.OSVersion.Version}, {Environment.ProcessorCount} cores";

    public static partial void Show(bool active, string? station, string? title)
    {
        var controls = Host.SystemMediaTransportControls;
        controls.PlaybackStatus = active ? MediaPlaybackStatus.Playing : MediaPlaybackStatus.Stopped;
        var display = controls.DisplayUpdater;
        display.Type = MediaPlaybackType.Music;
        display.MusicProperties.Artist = station ?? "";
        display.MusicProperties.Title = title ?? "";
        display.Update();
    }

    static MediaPlayer CreateHost()
    {
        var host = new MediaPlayer();
        host.CommandManager.IsEnabled = false;
        var controls = host.SystemMediaTransportControls;
        controls.IsEnabled = true;
        controls.IsPlayEnabled = true;
        controls.IsPauseEnabled = true;
        controls.IsStopEnabled = true;
        controls.ButtonPressed += (_, e) =>
        {
            Log.Write("media", $"Windows media button {e.Button}");
            if (e.Button == SystemMediaTransportControlsButton.Play)
                Player.Instance.PlayAgain("Windows media controls");
            else
                Player.Instance.Stop("Windows media controls");
        };
        return host;
    }
}
