namespace EngineTest;

static class Stations
{
    const string Mp3 = "https://ice1.somafm.com/groovesalad-128-mp3";
    const string HlsMedia = "https://stream.radiofrance.fr/fip/fip_midfi.m3u8?id=radiofrance";

    static readonly (string Name, string Url)[] Direct =
    [
        ("MP3 - SomaFM Groove Salad", Mp3),
        ("MP3, plain http - SomaFM Groove Salad", "http://ice1.somafm.com/groovesalad-128-mp3"),
        ("AAC-LC - SomaFM Groove Salad 128k", "https://ice1.somafm.com/groovesalad-128-aac"),
        ("HE-AAC - SomaFM Groove Salad 64k", "https://ice1.somafm.com/groovesalad-64-aac"),
        ("Ogg Vorbis, plain http - Radio Paradise", "http://stream-dc1.radioparadise.com/rp_192m.ogg"),
        ("Opus - Deutschlandfunk 24k", "https://st01.sslstream.dlf.de/dlf/01/low/opus/stream.opus?aggregator=web"),
        ("HLS AAC - FIP", "https://stream.radiofrance.fr/fip/fip.m3u8?id=radiofrance"),
        ("Unsupported format: FLAC - Radio Paradise", "https://stream.radioparadise.com/mellow-flac"),
        ("HTTP 404", "https://ice1.somafm.com/no-such-station-404"),
        ("Web page (stands in for a Wi-Fi login page)", "https://example.com/"),
    ];

    // Served by spike/engine-test/relay on the PC, which forwards a station and injects faults.
    static readonly (string Name, string Path)[] Relayed =
    [
        ("Relay: MP3, no faults", $"/relay?u={Uri.EscapeDataString(Mp3)}"),
        ("Relay: server closes every 60 s", $"/relay?u={Uri.EscapeDataString(Mp3)}&close=60"),
        ("Relay: stream stalls after 60 s", $"/relay?u={Uri.EscapeDataString(Mp3)}&stall=60"),
        ("Relay: 9 s pause after 60 s", $"/relay?u={Uri.EscapeDataString(Mp3)}&pause=60:9"),
        ("Relay: closes after 60 s, then 404 for 30 s", $"/relay?u={Uri.EscapeDataString(Mp3)}&close=60&down=30"),
        ("Relay: closes after 60 s, then a login page for 30 s", $"/relay?u={Uri.EscapeDataString(Mp3)}&close=60&html=30"),
        ("Relay: HLS, no new segment for 30 s after 60 s", $"/hls?u={Uri.EscapeDataString(HlsMedia)}&freeze=60:30"),
    ];

    public static IEnumerable<string> Names => Direct.Select(s => s.Name).Concat(Relayed.Select(s => s.Name));

    public static (string Name, string Url) At(int index, string relayHost)
    {
        if (index < Direct.Length)
            return Direct[index];
        var (name, path) = Relayed[index - Direct.Length];
        return (name, $"http://{relayHost}:8765{path}");
    }
}
