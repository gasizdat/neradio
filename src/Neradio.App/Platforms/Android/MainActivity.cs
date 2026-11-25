using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace Neradio.App;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
#if DEBUG
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        PlayDebugUrl(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        PlayDebugUrl(intent);
    }

    private static void PlayDebugUrl(Intent? intent)
    {
        var url = intent?.GetStringExtra("url");
        if (url is not null)
            DebugPlayback.Play(url);
    }
#endif
}
