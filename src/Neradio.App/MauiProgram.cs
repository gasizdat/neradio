using Neradio.Core;
using Neradio.Core.Playback;
using Neradio.Engine.Bass;

namespace Neradio.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Services.AddMauiBlazorWebView();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif
        builder.Services.AddNeradioCore();
        builder.Services.AddNeradioEngineBass();
#if ANDROID
        builder.Services.AddSingleton<MediaControls>();
#endif

        var app = builder.Build();
        // From the start, as a headset button can play the station before any window opens.
        var player = app.Services.GetRequiredService<Player>();
        Connectivity.Current.ConnectivityChanged += (_, _) => player.NetworkChanged();
        return app;
    }
}
