using Neradio.Core.Stations;

namespace Neradio.Core.Tests.Stations;

public sealed class KnownStationsTests
{
    private static readonly Guid IdX = new("5f0c2b7e-3d4a-4c1b-9e8f-0a1b2c3d4e5f");
    private static readonly Guid IdY = new("a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d");

    private readonly KnownStations _known = new();

    [Fact]
    public void IdMatchWinsOverStreamMatch()
    {
        var withId = _known.Add(Entry(IdX, "https://a.example/live"));
        var withoutId = _known.Add(Entry(null, "https://b.example/live"));

        var match = _known.Add(Entry(IdX, "https://b.example/live"));

        Assert.Same(withId, match);
        Assert.Null(withoutId.Id);
    }

    [Fact]
    public void SameIdDifferentStreams()
    {
        var station = _known.Add(Entry(IdX, "https://a.example/live"));

        var match = _known.Add(Entry(IdX, "https://b.example/live"));

        Assert.Same(station, match);
    }

    [Fact]
    public void DifferentIdsSameStream()
    {
        var station = _known.Add(Entry(IdX, "https://a.example/live"));
        var entry = Entry(IdY, "https://a.example/live");

        var match = _known.Add(entry);

        Assert.Same(entry, match);
        Assert.Equal(IdX, station.Id);
        Assert.Equal(2, _known.Stations.Count());
    }

    [Fact]
    public void EntryAlternateIsTheStationPrimary()
    {
        var station = _known.Add(Entry(null, "https://a.example/live"));

        var match = _known.Add(Entry(null, "https://b.example/live", "https://a.example/live"));

        Assert.Same(station, match);
    }

    [Fact]
    public void EntryWithAnIdStationWithoutOne()
    {
        var station = _known.Add(Entry(null, "https://a.example/live"));

        var match = _known.Add(Entry(IdX, "http://a.example/live/"));

        Assert.Same(station, match);
    }

    [Fact]
    public void OneCandidateSharesThePrimaryStream()
    {
        var sharesPrimary = _known.Add(Entry(null, "https://a.example/live", "https://c.example/live"));
        _known.Add(Entry(null, "https://b.example/live"));

        var match = _known.Add(Entry(null, "https://a.example/live", "https://b.example/live"));

        Assert.Same(sharesPrimary, match);
    }

    [Fact]
    public void NoSingleCandidateSharesThePrimaryStream()
    {
        _known.Add(Entry(null, "https://a.example/live", "https://c.example/live"));
        _known.Add(Entry(null, "https://b.example/live", "https://d.example/live"));
        var entry = Entry(null, "https://e.example/live", "https://c.example/live", "https://d.example/live");

        var match = _known.Add(entry);

        Assert.Same(entry, match);
        Assert.Equal(3, _known.Stations.Count());
    }

    [Fact]
    public void StationAddedByUrlAppearsInTheCatalog()
    {
        var station = _known.Add(Entry(null, "https://a.example/live"));

        var first = _known.Add(Entry(IdX, "https://a.example/live"));
        var later = _known.Add(Entry(IdX, "https://b.example/live"));

        Assert.Same(station, first);
        Assert.Equal(IdX, station.Id);
        Assert.Same(station, later);
    }

    [Fact]
    public void SecondEntryWithADifferentId()
    {
        var station = _known.Add(Entry(null, "https://a.example/live"));
        _known.Add(Entry(IdX, "https://a.example/live"));
        var second = Entry(IdY, "https://a.example/live");

        var match = _known.Add(second);

        Assert.Same(second, match);
        Assert.Equal(IdX, station.Id);
    }

    private static Station Entry(Guid? id, string primary, params string[] alternates)
    {
        var alternateStreams = alternates.Select(url => new Uri(url)).ToArray();
        return new Station
        {
            Id = id,
            Name = primary,
            PrimaryStream = new Uri(primary),
            AlternateStreams = alternateStreams,
        };
    }
}
