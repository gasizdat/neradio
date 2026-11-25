using Android.Content;

namespace Neradio.App;

// The notification's buttons before Android 13.
[BroadcastReceiver(Exported = false)]
public sealed class NotificationButtonReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        var controls = IPlatformApplication.Current!.Services.GetRequiredService<MediaControls>();
        switch (intent?.Action)
        {
            case MediaControls.PlayAction:
                controls.Play();
                break;
            case MediaControls.StopAction:
                controls.Stop();
                break;
        }
    }
}
