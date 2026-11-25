using Microsoft.Extensions.DependencyInjection;
using Neradio.Core.Playback;
using Neradio.Core.Playback.Abstractions;
using Neradio.Core.Tests.Playback;

namespace Neradio.Core.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    private readonly ServiceProvider _provider;

    public ServiceCollectionExtensionsTests()
    {
        var services = new ServiceCollection();
        services.AddNeradioCore();
        // The app registers the engine.
        services.AddSingleton<IAudioEngine, FakeEngine>();
        _provider = services.BuildServiceProvider();
    }

    [Fact]
    public void TimeIsTheSystemClock()
    {
        var time = _provider.GetRequiredService<TimeProvider>();

        Assert.Same(TimeProvider.System, time);
    }

    [Fact]
    public void OneHttpClientForTheWholeApp()
    {
        var first = _provider.GetRequiredService<HttpClient>();
        var second = _provider.GetRequiredService<HttpClient>();

        Assert.Same(first, second);
    }

    [Fact]
    public void OnePlayerForTheWholeApp()
    {
        var first = _provider.GetRequiredService<Player>();
        var second = _provider.GetRequiredService<Player>();

        Assert.Same(first, second);
    }
}
