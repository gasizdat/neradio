using Neradio.Core.Stations;

namespace Neradio.Core.Tests.Stations;

public sealed class StreamUrlTests
{
    [Theory]
    [InlineData("http://a.example/live", "https://a.example/live")]
    [InlineData("https://A.Example:443/live/", "https://a.example/live")]
    public void SameStreamUrl(string first, string second)
    {
        Assert.Equal(Normalize(first), Normalize(second));
    }

    [Theory]
    [InlineData("https://a.example/live", "https://mirror.a.example/live")]
    [InlineData("https://a.example/live?token=1", "https://a.example/live?token=2")]
    [InlineData("https://a.example/Live", "https://a.example/live")]
    [InlineData("https://a.example:8000/live", "https://a.example/live")]
    [InlineData("http://a.example:443/live", "https://a.example/live")]
    public void DifferentStreamUrls(string first, string second)
    {
        Assert.NotEqual(Normalize(first), Normalize(second));
    }

    private static string Normalize(string url)
    {
        var uri = new Uri(url);
        return StreamUrl.Normalize(uri);
    }
}
