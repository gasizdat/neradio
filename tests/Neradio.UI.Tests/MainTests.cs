using Bunit;

namespace Neradio.UI.Tests;

public sealed class MainTests : BunitContext
{
    [Fact]
    public void ShowsTheAppName()
    {
        var main = Render<Main>();

        var heading = main.Find("h1");
        Assert.Equal("Neradio", heading.TextContent);
    }
}
