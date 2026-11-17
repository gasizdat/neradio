using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics.Drawables;
using Android.Net.Wifi;
using Android.OS;

namespace EngineTest;

// In the foreground while a station plays or reconnects. After a stop it leaves the foreground
// but keeps its notification, so the notification and the headset button can play again.
[Service(ForegroundServiceType = ForegroundService.TypeMediaPlayback, Exported = false)]
sealed class PlaybackService : Service
{
    const string ChannelId = "playback";
    const int NotificationId = 1;
    const string ActionPlay = "com.neradio.enginetest.PLAY";
    const string ActionStop = "com.neradio.enginetest.STOP";

    static PlaybackService? _running;
    PowerManager.WakeLock? _wakeLock;
    WifiManager.WifiLock? _wifiLock;

    public static void Start()
    {
        var context = Android.App.Application.Context;
        var intent = new Intent(context, typeof(PlaybackService));
        try
        {
            context.StartForegroundService(intent);
            Log.Write("android", "foreground service start requested");
        }
        // Android 12 and later refuse some starts from the background; #8 records which.
        catch (Java.Lang.IllegalStateException e)
        {
            Log.Write("android", $"foreground service start refused: {e.GetType().Name}: {e.Message}");
        }
    }

    public static void Leave() => _running?.LeaveForeground();

    public static void Refresh() => _running?.Repost();

    public override IBinder? OnBind(Intent? intent) => null;

    public override void OnCreate()
    {
        base.OnCreate();
        var channel = new NotificationChannel(ChannelId, "Playback", NotificationImportance.Low);
        var manager = (NotificationManager)GetSystemService(NotificationService)!;
        manager.CreateNotificationChannel(channel);
        Log.Write("android", "service created");
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        _running = this;
        var action = intent?.Action;
        Log.Write("android", $"service start command {action ?? "from the player"}");
        if (action == ActionStop)
        {
            Player.Instance.Stop("notification");
            return StartCommandResult.NotSticky;
        }
        EnterForeground();
        if (action == ActionPlay)
            Player.Instance.PlayAgain("notification");
        return StartCommandResult.NotSticky;
    }

    public override void OnDestroy()
    {
        Log.Write("android", "service destroyed");
        _running = null;
        base.OnDestroy();
    }

    void EnterForeground()
    {
        try
        {
            StartForeground(NotificationId, Build(), ForegroundService.TypeMediaPlayback);
        }
        // Android can refuse here too, not only when the service is started.
        catch (Java.Lang.IllegalStateException e)
        {
            Log.Write("android", $"going to the foreground refused: {e.GetType().Name}: {e.Message}");
            return;
        }
        var power = (PowerManager)GetSystemService(PowerService)!;
        _wakeLock ??= power.NewWakeLock(WakeLockFlags.Partial, "neradio:playback")!;
        if (!_wakeLock.IsHeld)
            _wakeLock.Acquire();
        var wifi = (WifiManager)GetSystemService(WifiService)!;
        _wifiLock ??= wifi.CreateWifiLock(Android.Net.WifiMode.FullHighPerf, "neradio:playback")!;
        if (!_wifiLock.IsHeld)
            _wifiLock.Acquire();
        Log.Write("android", "in the foreground; wake lock and Wi-Fi lock held");
    }

    void LeaveForeground()
    {
        StopForeground(StopForegroundFlags.Detach);
        Repost();
        if (_wakeLock?.IsHeld == true)
            _wakeLock.Release();
        if (_wifiLock?.IsHeld == true)
            _wifiLock.Release();
        Log.Write("android", "left the foreground; locks released");
    }

    void Repost()
    {
        var manager = (NotificationManager)GetSystemService(NotificationService)!;
        manager.Notify(NotificationId, Build());
    }

    Notification Build()
    {
        var active = SystemMedia.Active;
        var intent = new Intent(this, typeof(PlaybackService));
        intent.SetAction(active ? ActionStop : ActionPlay);
        var pending = active
            ? PendingIntent.GetService(this, 0, intent, PendingIntentFlags.Immutable)
            : PendingIntent.GetForegroundService(this, 1, intent, PendingIntentFlags.Immutable);
        var icon = Icon.CreateWithResource(this, active ? Android.Resource.Drawable.IcMediaPause : Android.Resource.Drawable.IcMediaPlay);
        var button = new Notification.Action.Builder(icon, active ? "Stop" : "Play", pending).Build();
        var open = PackageManager!.GetLaunchIntentForPackage(PackageName!)!;
        var content = PendingIntent.GetActivity(this, 2, open, PendingIntentFlags.Immutable);
        var style = new Notification.MediaStyle()
            .SetMediaSession(SystemMedia.Token)!
            .SetShowActionsInCompactView(0);
        return new Notification.Builder(this, ChannelId)
            .SetSmallIcon(Android.Resource.Drawable.IcMediaPlay)!
            .SetContentTitle(SystemMedia.Title ?? SystemMedia.Station ?? "neradio engine test")!
            .SetContentText(SystemMedia.Title is null ? "" : SystemMedia.Station)!
            .SetContentIntent(content)!
            .SetStyle(style)!
            .AddAction(button)!
            .SetOngoing(active)!
            .Build();
    }
}
