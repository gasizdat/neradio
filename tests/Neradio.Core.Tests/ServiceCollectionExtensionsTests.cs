using Microsoft.Extensions.DependencyInjection;

namespace Neradio.Core.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    private readonly ServiceProvider _provider;

    public ServiceCollectionExtensionsTests()
    {
        var services = new ServiceCollection();
        services.AddNeradioCore();
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
}
