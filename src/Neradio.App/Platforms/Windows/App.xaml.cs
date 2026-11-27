namespace Neradio.App.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);
        IPlatformApplication.Current!.Services.GetRequiredService<MediaControls>();
#if DEBUG
        var arguments = Environment.GetCommandLineArgs();
        var at = Array.IndexOf(arguments, "--url");
        if (at >= 0 && at + 1 < arguments.Length)
            DebugPlayback.Play(arguments[at + 1]);
#endif
    }
}
