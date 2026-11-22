namespace UiTest;

// The same made-up stations in both apps, so the two screens draw the same content.
public sealed record Station(string Id, string Name, string Meta, string Initials, int Hue)
{
    // The demo's initials tile in its light theme: hsl(hue 42% 84%).
    public Color Tile => Color.FromHsla(Hue / 360.0, 0.42, 0.84);
}

public static class Stations
{
    static readonly string[] Names =
    [
        "Harbor Lights FM", "Late Jazz", "Baltic News 24", "Kaktus Radio", "Tiny Waves", "Radio Sol", "Fjord Classical",
        "Northern Valley Community Radio", "Orbit Lo-Fi", "Old Town Oldies", "Lossless Lounge", "Open Mic Talk", "Kitchen Radio", "Dune Beats",
    ];
    static readonly string[] Genres = ["Electronic", "Jazz", "News", "Rock", "Ambient", "Latin", "Classical", "Community", "Lo-fi", "Oldies", "Lounge", "Talk", "Pop", "Folk"];
    static readonly string[] Countries = ["Latvia", "France", "Lithuania", "Germany", "Netherlands", "Spain", "Norway", "Finland", "Japan", "Poland", "Italy", "Ireland"];
    static readonly string[] Formats = ["MP3 128 kbps", "HE-AAC 64 kbps", "Ogg Vorbis 192 kbps", "Opus 48 kbps", "AAC 128 kbps", "AAC 256 kbps", "HLS AAC 96 kbps"];

    public static string[] AllGenres { get; } = [.. Genres.Order()];

    public static Station[] All { get; } = [.. Enumerable.Range(0, 100).Select(Make)];

    static Station Make(int i)
    {
        var name = i < Names.Length ? Names[i] : $"{Names[i % Names.Length]} {i / Names.Length + 1}";
        var meta = $"{Genres[i % Genres.Length]} · {Countries[i % Countries.Length]} · {Formats[i % Formats.Length]}";
        var words = name.Split(' ').Where(w => w is not ("FM" or "Radio")).Take(2);
        var initials = string.Concat(words.Select(w => char.ToUpperInvariant(w[0])));
        return new Station($"s{i}", name, meta, initials, i * 47 % 360);
    }
}
