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
        if (Argument("--url") is { } url)
            DebugPlayback.Play(url, Argument("--logo"));
#endif
    }
#if DEBUG

    private static string? Argument(string name)
    {
        var arguments = Environment.GetCommandLineArgs();
        var at = Array.IndexOf(arguments, name);
        return at >= 0 && at + 1 < arguments.Length ? arguments[at + 1] : null;
    }
#endif
}
