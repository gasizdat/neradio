namespace Neradio.Core.Stations;

public sealed class Station
{
    // Set later only by KnownStations, when a station without an ID matches an entry with one.
    public Guid? Id { get; internal set; }
    public required string Name { get; init; }
    public Uri? Logo { get; init; }
    public required Uri PrimaryStream { get; init; }
    public Uri[] AlternateStreams { get; init; } = [];
    public string[] Genres { get; init; } = [];
    public string? Country { get; init; }
    public string? Language { get; init; }
    public int? Bitrate { get; init; }
}
