using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace Neradio.App;

// Keeps the app running in the background while a station plays, reconnects or is interrupted.
[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeMediaPlayback)]
public sealed class PlaybackService : Service
{
    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        var controls = IPlatformApplication.Current!.Services.GetRequiredService<MediaControls>();
        controls.Started(this);
        return StartCommandResult.NotSticky;
    }
}
