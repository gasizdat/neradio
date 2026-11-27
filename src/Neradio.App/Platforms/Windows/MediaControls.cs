using Neradio.Core.Playback;
using Windows.Media;
using Windows.Media.Playback;
using Windows.Storage.Streams;

namespace Neradio.App;

// Windows' media controls and the media keys follow the player. They come from a Windows media
// player that plays nothing: an app without a package or a window handle has no other way to
// them.
internal sealed class MediaControls
{
    private readonly Lock _gate = new();
    private readonly Player _player;
    // Holds the controls: they go when it's collected.
    private readonly MediaPlayer _host;
    private readonly SystemMediaTransportControls _controls;

    public MediaControls(Player player)
    {
        _player = player;
        _host = new MediaPlayer();
        _host.CommandManager.IsEnabled = false;
        _controls = _host.SystemMediaTransportControls;
        _controls.IsPlayEnabled = true;
        _controls.IsPauseEnabled = true;
        _controls.IsStopEnabled = true;
        _controls.ButtonPressed += Pressed;
        player.StateChanged += Update;
        player.TitleChanged += _ => Update();
    }

    // The play/pause key sends Pause while the controls show Playing, and Play otherwise.
    private void Pressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
    {
        switch (args.Button)
        {
            case SystemMediaTransportControlsButton.Play when _player.Station is { } station:
                _player.Play(station);
                break;
            case SystemMediaTransportControlsButton.Pause or SystemMediaTransportControlsButton.Stop:
                _player.Stop(StopKind.User);
                break;
        }
    }

    private void Update()
    {
        lock (_gate)
        {
            if (_player.Station is not { } station)
                return;
            _controls.IsEnabled = true;
            _controls.PlaybackStatus = Status(_player.State);
            var display = _controls.DisplayUpdater;
            display.Type = MediaPlaybackType.Music;
            display.MusicProperties.Title = _player.Title ?? station.Name;
            display.MusicProperties.Artist = station.Name;
            display.Thumbnail = station.Logo is { } logo ? RandomAccessStreamReference.CreateFromUri(logo) : null;
            display.Update();
        }
    }

    // A station that connects shows as playing, so the key stops it.
    private static MediaPlaybackStatus Status(PlayerState state) => state switch
    {
        PlayerState.Connecting or PlayerState.Playing or PlayerState.Reconnecting => MediaPlaybackStatus.Playing,
        _ => MediaPlaybackStatus.Stopped,
    };
}
