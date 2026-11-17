using Android.App;
using Android.Content;
using Android.Media;
using Android.Media.Session;
using Android.OS;
using Android.Views;

namespace EngineTest;

// The platform's own MediaSession, so no media library is needed (#8: "which library provides
// the Android media session").
static partial class SystemMedia
{
    static readonly MediaSession Session = CreateSession();

    public static bool Active { get; private set; }

    public static string? Station { get; private set; }

    public static string? Title { get; private set; }

    public static MediaSession.Token Token => Session.SessionToken;

    public static partial string Describe()
    {
        var context = Android.App.Application.Context;
        var power = (PowerManager)context.GetSystemService(Context.PowerService)!;
        var exempt = power.IsIgnoringBatteryOptimizations(context.PackageName);
        return $"{Build.Manufacturer} {Build.Model}, Android {Build.VERSION.Release} (API {(int)Build.VERSION.SdkInt}); battery optimization {(exempt ? "off" : "on")} for this app";
    }

    public static partial void Show(bool active, string? station, string? title)
    {
        Station = station;
        Title = title;
        var metadata = new MediaMetadata.Builder()
            .PutString(MediaMetadata.MetadataKeyTitle, title ?? station ?? "")!
            .PutString(MediaMetadata.MetadataKeyArtist, title is null ? "" : station ?? "")!
            .Build();
        Session.SetMetadata(metadata);
        var actions = PlaybackState.ActionPlay | PlaybackState.ActionPause | PlaybackState.ActionPlayPause | PlaybackState.ActionStop;
        var code = active ? PlaybackStateCode.Playing : PlaybackStateCode.Stopped;
        var state = new PlaybackState.Builder()
            .SetActions(actions)!
            .SetState(code, PlaybackState.PlaybackPositionUnknown, 1)!
            .Build();
        Session.SetPlaybackState(state);
        var wasActive = Active;
        Active = active;
        if (active && !wasActive)
            PlaybackService.Start();
        else if (!active && wasActive)
            PlaybackService.Leave();
        else
            PlaybackService.Refresh();
    }

    static MediaSession CreateSession()
    {
        var session = new MediaSession(Android.App.Application.Context, "neradio-engine-test");
        var mainThread = new Handler(Looper.MainLooper!);
        session.SetCallback(new Callback(), mainThread);
        session.Active = true;
        return session;
    }

    sealed class Callback : MediaSession.Callback
    {
        public override bool OnMediaButtonEvent(Intent mediaButtonIntent)
        {
            var key = (KeyEvent?)mediaButtonIntent.GetParcelableExtra(Intent.ExtraKeyEvent);
            Log.Write("media", $"media button {key?.KeyCode} {key?.Action}");
            return base.OnMediaButtonEvent(mediaButtonIntent);
        }

        public override void OnPlay() => Player.Instance.PlayAgain("media session play");

        public override void OnPause() => Player.Instance.Stop("media session pause");

        public override void OnStop() => Player.Instance.Stop("media session stop");
    }
}
