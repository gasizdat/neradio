using Android.App;
using Android.Runtime;

namespace Neradio.App;

[Application]
public class MainApplication(IntPtr handle, JniHandleOwnership ownership) : MauiApplication(handle, ownership)
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    // The media session is there from the start, so the headset buttons reach the app.
    public override void OnCreate()
    {
        base.OnCreate();
        IPlatformApplication.Current!.Services.GetRequiredService<MediaControls>();
    }
}
