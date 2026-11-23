using System.Text;
using Neradio.Core.Stations;

namespace Neradio.Core.Tests.Stations;

public sealed class StationListReaderTests
{
    private const string Id = "5f0c2b7e-3d4a-4c1b-9e8f-0a1b2c3d4e5f";

    // Station list document

    [Fact]
    public void ValidListLoadsItsStations()
    {
        var stations = ReadEntries(
            """{"name": "One", "streams": ["https://a.example/live"]}""",
            """{"name": "Two", "streams": ["https://b.example/live"]}""",
            """{"name": "Three", "streams": ["https://c.example/live"]}""");

        Assert.Equal(["One", "Two", "Three"], stations.Select(station => station.Name));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""[{"name": "One", "streams": ["https://a.example/live"]}]""")]
    [InlineData("""{"version": 1}""")]
    [InlineData("""{"version": 1, "stations": {"name": "One"}}""")]
    public void FileWithoutAStationsArrayIsRejected(string json)
    {
        Assert.False(TryRead(json, out var stations));
        Assert.Empty(stations);
    }

    // Format version

    [Fact]
    public void NewerVersionIsRejected()
    {
        var json = """{"version": 2, "stations": [{"name": "One", "streams": ["https://a.example/live"]}]}""";

        Assert.False(TryRead(json, out var stations));
        Assert.Empty(stations);
    }

    [Theory]
    [InlineData("""{"stations": [{"name": "One", "streams": ["https://a.example/live"]}]}""")]
    [InlineData("""{"version": "1", "stations": [{"name": "One", "streams": ["https://a.example/live"]}]}""")]
    [InlineData("""{"version": 1.5, "stations": [{"name": "One", "streams": ["https://a.example/live"]}]}""")]
    public void MissingOrNonIntegerVersionIsRejected(string json)
    {
        Assert.False(TryRead(json, out var stations));
        Assert.Empty(stations);
    }

    // Required station fields

    [Fact]
    public void PrimaryAndAlternateStreams()
    {
        var station = ReadEntry("""{"name": "One", "streams": ["https://a.example/live", "https://b.example/live"]}""");

        Assert.Equal(new Uri("https://a.example/live"), station.PrimaryStream);
        Assert.Equal([new Uri("https://b.example/live")], station.AlternateStreams);
    }

    // Optional station fields

    [Fact]
    public void EntryWithOnlyTheRequiredFields()
    {
        var station = ReadEntry("""{"name": "One", "streams": ["https://a.example/live"]}""");

        Assert.Null(station.Id);
        Assert.Null(station.Logo);
        Assert.Empty(station.Genres);
        Assert.Null(station.Country);
        Assert.Null(station.Language);
        Assert.Null(station.Bitrate);
    }

    [Fact]
    public void EntryWithEveryField()
    {
        var station = ReadEntry($$"""
            {
                "id": "{{Id}}",
                "name": "One",
                "logo": "https://a.example/logo.png",
                "streams": ["https://a.example/live"],
                "genres": ["jazz", "blues"],
                "country": "US",
                "language": "en",
                "bitrate": 128
            }
            """);

        Assert.Equal(new Guid(Id), station.Id);
        Assert.Equal(new Uri("https://a.example/logo.png"), station.Logo);
        Assert.Equal(["jazz", "blues"], station.Genres);
        Assert.Equal("US", station.Country);
        Assert.Equal("en", station.Language);
        Assert.Equal(128, station.Bitrate);
    }

    [Fact]
    public void CountryInLowerCase()
    {
        var station = ReadEntry("""{"name": "One", "streams": ["https://a.example/live"], "country": "us"}""");

        Assert.Equal("US", station.Country);
    }

    [Fact]
    public void GenresWithUnusableElements()
    {
        var station = ReadEntry("""{"name": "One", "streams": ["https://a.example/live"], "genres": ["jazz", "", 5]}""");

        Assert.Equal(["jazz"], station.Genres);
    }

    // Unknown fields are ignored

    [Fact]
    public void EntryWithAnUnknownField()
    {
        var station = ReadEntry("""{"name": "One", "streams": ["https://a.example/live"], "homepage": "https://a.example"}""");

        Assert.Equal("One", station.Name);
        Assert.Equal(new Uri("https://a.example/live"), station.PrimaryStream);
        Assert.Empty(station.AlternateStreams);
        Assert.Null(station.Logo);
    }

    [Fact]
    public void DocumentWithAnUnknownField()
    {
        var json = """{"version": 1, "source": "catalog", "stations": [{"name": "One", "streams": ["https://a.example/live"]}]}""";

        Assert.True(TryRead(json, out var stations));
        Assert.Single(stations);
    }

    // Unusable entries are skipped

    [Fact]
    public void EntryWithoutAName()
    {
        var stations = ReadEntries(
            """{"name": "One", "streams": ["https://a.example/live"]}""",
            """{"streams": ["https://b.example/live"]}""",
            """{"name": "Three", "streams": ["https://c.example/live"]}""");

        Assert.Equal(["One", "Three"], stations.Select(station => station.Name));
    }

    [Theory]
    [InlineData("""{"name": "One", "streams": ["ftp://a.example/live"]}""")]
    [InlineData("""{"name": " ", "streams": ["https://a.example/live"]}""")]
    [InlineData("""{"name": "One", "streams": []}""")]
    [InlineData("\"One\"")]
    public void UnusableEntryIsSkipped(string entry)
    {
        var stations = ReadEntries(entry);

        Assert.Empty(stations);
    }

    [Fact]
    public void UnusableStreamAmongUsableOnes()
    {
        var station = ReadEntry("""{"name": "One", "streams": ["not a url", "https://a.example/live"]}""");

        Assert.Equal(new Uri("https://a.example/live"), station.PrimaryStream);
        Assert.Empty(station.AlternateStreams);
    }

    // Invalid optional values are ignored

    [Fact]
    public void InvalidLogo()
    {
        var station = ReadEntry("""{"name": "One", "streams": ["https://a.example/live"], "logo": "ftp://a.example/logo.png"}""");

        Assert.Null(station.Logo);
    }

    [Fact]
    public void InvalidBitrate()
    {
        var station = ReadEntry("""{"name": "One", "streams": ["https://a.example/live"], "bitrate": -5}""");

        Assert.Null(station.Bitrate);
    }

    // Station IDs are UUIDs

    [Theory]
    [InlineData("42")]
    [InlineData("{5f0c2b7e-3d4a-4c1b-9e8f-0a1b2c3d4e5f}")]
    public void IdThatIsNotTheHyphenatedUuidLoadsAsNoId(string id)
    {
        var station = ReadEntry($$"""{"id": "{{id}}", "name": "One", "streams": ["https://a.example/live"]}""");

        Assert.Null(station.Id);
    }

    // Each station appears once per list

    [Fact]
    public void SameIdTwice()
    {
        var stations = ReadEntries(
            $$"""{"id": "{{Id}}", "name": "First", "streams": ["https://a.example/live"]}""",
            $$"""{"id": "{{Id}}", "name": "Second", "streams": ["https://b.example/live"]}""");

        var station = Assert.Single(stations);
        Assert.Equal("First", station.Name);
    }

    [Fact]
    public void TwoEntriesWithoutIdsSharingAStream()
    {
        var stations = ReadEntries(
            """{"name": "First", "streams": ["https://a.example/live"]}""",
            """{"name": "Second", "streams": ["https://b.example/live", "http://a.example/live"]}""");

        var station = Assert.Single(stations);
        Assert.Equal("First", station.Name);
        Assert.Empty(station.AlternateStreams);
    }

    [Fact]
    public void EarlierEntryWithoutAnId()
    {
        var stations = ReadEntries(
            """{"name": "First", "streams": ["https://a.example/live"]}""",
            $$"""{"id": "{{Id}}", "name": "Second", "streams": ["https://a.example/live"]}""");

        var station = Assert.Single(stations);
        Assert.Equal("First", station.Name);
        Assert.Equal(new Guid(Id), station.Id);
    }

    private static Station ReadEntry(string entry)
    {
        var stations = ReadEntries(entry);
        return Assert.Single(stations);
    }

    private static Station[] ReadEntries(params string[] entries)
    {
        var list = string.Join(",", entries);
        Assert.True(TryRead($$"""{"version": 1, "stations": [{{list}}]}""", out var stations));
        return stations;
    }

    private static bool TryRead(string json, out Station[] stations)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        using var stream = new MemoryStream(bytes);
        return StationListReader.TryRead(stream, out stations);
    }
}
