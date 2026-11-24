using System.Text.Json;

namespace Neradio.Core.Stations;

// Walks a JsonDocument instead of deserializing, so one bad entry or value is skipped where it's
// read and the rest of the list still loads.
public static class StationListReader
{
    private const int SupportedVersion = 1;

    // False when the list is rejected as a whole. The stations come in list order, each station once.
    public static bool TryRead(Stream json, out Station[] stations)
    {
        stations = [];
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return false;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;

            var version = Property(root, "version");
            if (version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number) || number != SupportedVersion)
                return false;

            var entries = Property(root, "stations");
            if (entries.ValueKind != JsonValueKind.Array)
                return false;

            var known = new KnownStations();
            foreach (var element in entries.EnumerateArray())
            {
                var entry = ReadEntry(element);
                if (entry is not null)
                    known.Add(entry);
            }
            stations = known.Stations.ToArray();
            return true;
        }
    }

    private static Station? ReadEntry(JsonElement entry)
    {
        if (entry.ValueKind != JsonValueKind.Object)
            return null;

        var nameValue = Property(entry, "name");
        var name = ReadString(nameValue)?.Trim();
        if (string.IsNullOrEmpty(name))
            return null;

        var streamValues = Property(entry, "streams");
        var streams = ReadArray(streamValues)
            .Select(ReadHttpUrl)
            .OfType<Uri>()
            .ToArray();
        if (streams.Length == 0)
            return null;

        var idValue = Property(entry, "id");
        var id = ReadString(idValue);
        var logoValue = Property(entry, "logo");
        var genreValues = Property(entry, "genres");
        var genres = ReadArray(genreValues)
            .Select(ReadString)
            .OfType<string>()
            .Where(genre => genre.Length > 0)
            .ToArray();
        var countryValue = Property(entry, "country");
        var country = ReadString(countryValue);
        var languageValue = Property(entry, "language");
        var language = ReadString(languageValue);
        var bitrate = Property(entry, "bitrate");

        return new Station
        {
            Id = id is null ? null : StationId.Parse(id),
            Name = name,
            Logo = ReadHttpUrl(logoValue),
            PrimaryStream = streams[0],
            AlternateStreams = streams[1..],
            Genres = genres,
            Country = country is { Length: 2 } && country.All(char.IsAsciiLetter) ? country.ToUpperInvariant() : null,
            Language = string.IsNullOrEmpty(language) ? null : language,
            Bitrate = bitrate.ValueKind == JsonValueKind.Number && bitrate.TryGetInt32(out var kbps) && kbps > 0 ? kbps : null,
        };
    }

    // A missing property comes back as an Undefined element, which every reader below turns away.
    private static JsonElement Property(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) ? value : default;

    private static string? ReadString(JsonElement value) =>
        value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static IEnumerable<JsonElement> ReadArray(JsonElement value) =>
        value.ValueKind == JsonValueKind.Array ? value.EnumerateArray() : [];

    private static Uri? ReadHttpUrl(JsonElement value)
    {
        var text = ReadString(value);
        var isAbsolute = Uri.TryCreate(text, UriKind.Absolute, out var url);
        return isAbsolute && (url!.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps) ? url : null;
    }
}
