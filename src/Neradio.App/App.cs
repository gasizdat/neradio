using Microsoft.AspNetCore.Components.WebView.Maui;
using Neradio.UI;

namespace Neradio.App;

public sealed class App : Application
{
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var root = new RootComponent { Selector = "#app", ComponentType = typeof(Main) };
        var webView = new BlazorWebView { HostPage = "wwwroot/index.html" };
        webView.RootComponents.Add(root);
        var page = new ContentPage { Content = webView };
        return new Window(page) { Title = "Neradio" };
    }
}
