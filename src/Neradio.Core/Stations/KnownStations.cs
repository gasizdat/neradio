namespace Neradio.Core.Stations;

public sealed class KnownStations
{
    private readonly List<Station> _stations = [];
    private readonly Dictionary<Guid, Station> _byId = [];
    private readonly Dictionary<string, List<Station>> _byStream = new(StringComparer.Ordinal);

    public IEnumerable<Station> Stations => _stations;

    // Returns the known station it matches, or the station itself, now known, when it matches none.
    public Station Add(Station station)
    {
        var match = Find(station);
        if (match is null)
        {
            Index(station);
            return station;
        }

        if (match.Id is null && station.Id is { } id)
        {
            match.Id = id;
            _byId.Add(id, match);
        }
        return match;
    }

    private Station? Find(Station station)
    {
        if (station.Id is { } id && _byId.TryGetValue(id, out var byId))
            return byId;

        // A known station with an ID other than this one's isn't a candidate: when both have IDs, the IDs decide.
        var candidates = StreamKeys(station)
            .SelectMany(key => _byStream.GetValueOrDefault(key, []))
            .Distinct()
            .Where(candidate => station.Id is null || candidate.Id is null)
            .ToArray();
        if (candidates.Length <= 1)
            return candidates.FirstOrDefault();

        var primary = StreamUrl.Normalize(station.PrimaryStream);
        var samePrimary = candidates
            .Where(candidate => StreamUrl.Normalize(candidate.PrimaryStream) == primary)
            .ToArray();
        return samePrimary.Length == 1 ? samePrimary[0] : null;
    }

    private void Index(Station station)
    {
        _stations.Add(station);
        if (station.Id is { } id)
            _byId.Add(id, station);

        foreach (var key in StreamKeys(station))
        {
            if (!_byStream.TryGetValue(key, out var stations))
            {
                stations = [];
                _byStream.Add(key, stations);
            }
            stations.Add(station);
        }
    }

    private static IEnumerable<string> StreamKeys(Station station) =>
        station.AlternateStreams
            .Prepend(station.PrimaryStream)
            .Select(StreamUrl.Normalize)
            .Distinct();
}
