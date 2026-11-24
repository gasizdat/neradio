using Neradio.Core;
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

        return builder.Build();
    }
}
