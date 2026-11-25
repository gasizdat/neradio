using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Media;
using Android.Media.Session;
using Android.Net.Wifi;
using Android.OS;
using Neradio.Core.Playback;
using Neradio.Core.Stations;

namespace Neradio.App;

// The media session, its notification and the foreground service follow the player. The session
// stays active after a stop, so the notification and the headset buttons can play the station
// again: it's created once, for the app's lifetime, and not by the service.
internal sealed class MediaControls
{
    public const string PlayAction = "io.github.gasizdat.neradio.PLAY";
    public const string StopAction = "io.github.gasizdat.neradio.STOP";

    private const string Channel = "playback";
    private const int NotificationId = 1;
    private const int LogoSize = 512;

    private readonly Lock _gate = new();
    private readonly Context _context;
    private readonly Player _player;
    private readonly HttpClient _http;
    private readonly MediaSession _session;
    private readonly NotificationManager _notifications;
    private readonly PowerManager.WakeLock _wakeLock;
    private readonly WifiManager.WifiLock _wifiLock;
    private PlaybackService? _service;
    // StartForegroundService was called, and the service hasn't started yet.
    private bool _starting;
    private Station? _logoStation;
    private Bitmap? _logo;

    public MediaControls(Player player, HttpClient http)
    {
        _context = Android.App.Application.Context;
        _player = player;
        _http = http;
        _notifications = (NotificationManager)_context.GetSystemService(Context.NotificationService)!;
        var channel = new NotificationChannel(Channel, "Playback", NotificationImportance.Low);
        _notifications.CreateNotificationChannel(channel);

        var power = (PowerManager)_context.GetSystemService(Context.PowerService)!;
        _wakeLock = power.NewWakeLock(WakeLockFlags.Partial, "neradio:playback")!;
        var wifi = (WifiManager)_context.GetSystemService(Context.WifiService)!;
        // Android 14 replaces this mode with one that works only with the screen on. Before 14 it
        // keeps Wi-Fi awake with the screen off.
#pragma warning disable CA1422
        _wifiLock = wifi.CreateWifiLock(Android.Net.WifiMode.FullHighPerf, "neradio:playback")!;
#pragma warning restore CA1422

        _session = new MediaSession(_context, "neradio");
        _session.SetCallback(new Callback(this));
        _session.Active = true;
        player.StateChanged += Update;
        player.TitleChanged += _ => Update();
    }

    public void Play()
    {
        if (_player.Station is { } station)
            _player.Play(station);
    }

    public void Stop() => _player.Stop(StopKind.User);

    // The service calls this when it starts. Android ends the app unless it enters the foreground
    // in time, even if playback stopped in the meantime.
    public void Started(PlaybackService service)
    {
        lock (_gate)
        {
            _starting = false;
            var notification = Notice(_player.State);
            service.StartForeground(NotificationId, notification, Android.Content.PM.ForegroundService.TypeMediaPlayback);
            if (IsActive(_player.State))
                _service = service;
            else
                Leave(service);
        }
    }

    private static bool IsActive(PlayerState state) =>
        state is PlayerState.Connecting or PlayerState.Playing or PlayerState.Reconnecting or PlayerState.Interrupted;

    private void Update()
    {
        lock (_gate)
        {
            var state = _player.State;
            var station = _player.Station;
            if (station is null)
                return;
            if (station != _logoStation)
            {
                _logoStation = station;
                _logo = null;
                _ = LoadLogoAsync(station);
            }

            _session.SetMetadata(Metadata());
            _session.SetPlaybackState(SessionState(state));
            HoldLocks(state is PlayerState.Connecting or PlayerState.Playing or PlayerState.Reconnecting);

            // Android may refuse to start a foreground service from the background, as after a
            // call, so the service stays in the foreground until playback stops.
            var active = IsActive(state);
            if (active && _service is null && !_starting)
            {
                _starting = true;
                var intent = new Intent(_context, typeof(PlaybackService));
                _context.StartForegroundService(intent);
                return;
            }
            if (!active && _service is { } service)
            {
                _service = null;
                Leave(service);
            }
            if (!_starting)
                _notifications.Notify(NotificationId, Notice(state));
        }
    }

    // The notification stays, so it can play the station again.
    private static void Leave(PlaybackService service)
    {
        service.StopForeground(StopForegroundFlags.Detach);
        service.StopSelf();
    }

    private void HoldLocks(bool hold)
    {
        if (hold && !_wakeLock.IsHeld)
        {
            _wakeLock.Acquire();
            _wifiLock.Acquire();
        }
        else if (!hold && _wakeLock.IsHeld)
        {
            _wakeLock.Release();
            _wifiLock.Release();
        }
    }

    private MediaMetadata Metadata()
    {
        var station = _player.Station!;
        var builder = new MediaMetadata.Builder()
            .PutString(MediaMetadata.MetadataKeyTitle, _player.Title ?? station.Name)!
            .PutString(MediaMetadata.MetadataKeyArtist, station.Name)!;
        if (_logo is not null)
            builder.PutBitmap(MediaMetadata.MetadataKeyArt, _logo);
        return builder.Build()!;
    }

    // Headset buttons toggle through PlayPause: a press stops a station that plays or connects,
    // and plays a stopped one.
    private static PlaybackState SessionState(PlayerState state)
    {
        var (shown, actions) = state switch
        {
            PlayerState.Playing => (PlaybackStateCode.Playing, PlaybackState.ActionPause | PlaybackState.ActionStop),
            PlayerState.Connecting or PlayerState.Reconnecting => (PlaybackStateCode.Buffering, PlaybackState.ActionPause | PlaybackState.ActionStop),
            PlayerState.Interrupted => (PlaybackStateCode.Paused, PlaybackState.ActionStop),
            _ => (PlaybackStateCode.Stopped, PlaybackState.ActionPlay),
        };
        return new PlaybackState.Builder()
            .SetState(shown, PlaybackState.PlaybackPositionUnknown, 1f)!
            .SetActions(actions | PlaybackState.ActionPlayPause)!
            .Build()!;
    }

    private Notification Notice(PlayerState state)
    {
        var station = _player.Station!;
        var active = IsActive(state);
        var open = new Intent(_context, typeof(MainActivity));
        var content = PendingIntent.GetActivity(_context, 0, open, PendingIntentFlags.Immutable);
        // Android 13 and later draw the buttons from the session; earlier versions from these.
        var action = active
            ? Button(Android.Resource.Drawable.IcMediaPause, "Stop", StopAction)
            : Button(Android.Resource.Drawable.IcMediaPlay, "Play", PlayAction);
        var style = new Notification.MediaStyle()
            .SetMediaSession(_session.SessionToken)!
            .SetShowActionsInCompactView(0);
        var builder = new Notification.Builder(_context, Channel)
            .SetSmallIcon(Android.Resource.Drawable.IcMediaPlay)!
            .SetContentTitle(station.Name)!
            .SetContentText(_player.Title)!
            .SetContentIntent(content)!
            .SetOngoing(active)!
            .SetVisibility(NotificationVisibility.Public)!
            .AddAction(action)!
            .SetStyle(style)!;
        if (_logo is not null)
            builder.SetLargeIcon(_logo);
        return builder.Build()!;
    }

    private Notification.Action Button(int icon, string title, string name)
    {
        var intent = new Intent(_context, typeof(NotificationButtonReceiver)).SetAction(name);
        var pending = PendingIntent.GetBroadcast(_context, 0, intent, PendingIntentFlags.Immutable);
        var image = Android.Graphics.Drawables.Icon.CreateWithResource(_context, icon);
        var label = new Java.Lang.String(title);
        var builder = new Notification.Action.Builder(image, label, pending);
        return builder.Build()!;
    }

    // A station without a logo, or one that can't be loaded, shows without one.
    private async Task LoadLogoAsync(Station station)
    {
        if (station.Logo is not { } url)
            return;
        try
        {
            var bytes = await _http.GetByteArrayAsync(url).ConfigureAwait(false);
            var decoded = await BitmapFactory.DecodeByteArrayAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            if (decoded is null)
                return;
            // The session's metadata crosses to the system in one binder call, which a large bitmap
            // overflows.
            var scale = Math.Min(1f, (float)LogoSize / Math.Max(decoded.Width, decoded.Height));
            var width = (int)(decoded.Width * scale);
            var height = (int)(decoded.Height * scale);
            var logo = Bitmap.CreateScaledBitmap(decoded, width, height, true);
            lock (_gate)
            {
                if (station != _logoStation)
                    return;
                _logo = logo;
            }
            Update();
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
        }
    }

    private sealed class Callback(MediaControls controls) : MediaSession.Callback
    {
        public override void OnPlay() => controls.Play();
        public override void OnPause() => controls.Stop();
        public override void OnStop() => controls.Stop();
    }
}
