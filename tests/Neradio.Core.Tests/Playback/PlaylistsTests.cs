using Neradio.Core.Playback;

namespace Neradio.Core.Tests.Playback;

public sealed class PlaylistsTests
{
    private static readonly Uri M3u = new("https://lists.example/radio/station.m3u");

    [Theory]
    [InlineData("https://lists.example/station.pls")]
    [InlineData("https://lists.example/STATION.M3U")]
    [InlineData("https://lists.example/station.m3u8?token=1")]
    public void PlaylistUrl(string url)
    {
        var uri = new Uri(url);

        Assert.True(Playlists.IsPlaylist(uri));
    }

    [Theory]
    [InlineData("https://lists.example/live")]
    [InlineData("https://lists.example/live.mp3")]
    [InlineData("https://lists.example/live?format=.pls")]
    public void StreamUrl(string url)
    {
        var uri = new Uri(url);

        Assert.False(Playlists.IsPlaylist(uri));
    }

    [Fact]
    public void RelativeEntriesAreResolvedAgainstThePlaylist()
    {
        var entries = Playlists.Entries(M3u, "live.mp3\n/other/live.aac\n");

        Uri[] expected = [new("https://lists.example/radio/live.mp3"), new("https://lists.example/other/live.aac")];
        Assert.Equal(expected, entries);
    }

    [Fact]
    public void WindowsLineEndings()
    {
        var entries = Playlists.Entries(M3u, "#EXTM3U\r\nhttps://a.example/live\r\n");

        Assert.Equal([new Uri("https://a.example/live")], entries);
    }
}
