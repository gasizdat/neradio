using System.Globalization;
using System.Text.RegularExpressions;

namespace Neradio.Core.Playback;

internal sealed record HlsSegment(long Sequence, TimeSpan Duration, Uri Url);

// A media playlist (RFC 8216), as far as playing its audio needs.
internal sealed partial record HlsPlaylist(TimeSpan TargetDuration, HlsSegment[] Segments, bool IsFragmentedMp4, bool HasEnded)
{
    // The variant with the highest bandwidth, when the text is a master playlist.
    public static Uri? Variant(Uri url, string text)
    {
        Uri? best = null;
        long bestBandwidth = -1;
        long? bandwidth = null;
        foreach (var line in Lines(text))
        {
            if (line.StartsWith("#EXT-X-STREAM-INF:", StringComparison.Ordinal))
            {
                var match = BandwidthAttribute().Match(line);
                bandwidth = match.Success ? long.Parse(match.Groups[1].ValueSpan, CultureInfo.InvariantCulture) : 0;
            }
            else if (bandwidth is { } candidate && !line.StartsWith('#'))
            {
                if (candidate > bestBandwidth && Uri.TryCreate(url, line, out var variant))
                {
                    best = variant;
                    bestBandwidth = candidate;
                }
                bandwidth = null;
            }
        }
        return best;
    }

    // Null when the text isn't a media playlist.
    public static HlsPlaylist? Read(Uri url, string text)
    {
        TimeSpan? target = null;
        long sequence = 0;
        var duration = TimeSpan.Zero;
        var segments = new List<HlsSegment>();
        var isFragmentedMp4 = false;
        var hasEnded = false;
        foreach (var line in Lines(text))
        {
            var colon = line.IndexOf(':');
            var tag = colon < 0 ? line : line[..colon];
            var value = colon < 0 ? "" : line[(colon + 1)..];
            if (tag == "#EXT-X-TARGETDURATION")
            {
                target = int.TryParse(value, CultureInfo.InvariantCulture, out var seconds) ? TimeSpan.FromSeconds(seconds) : null;
            }
            else if (tag == "#EXT-X-MEDIA-SEQUENCE")
            {
                sequence = long.TryParse(value, CultureInfo.InvariantCulture, out var first) ? first : 0;
            }
            else if (tag == "#EXTINF")
            {
                // The duration, then a comma and an optional title.
                var seconds = value.Split(',')[0];
                duration = double.TryParse(seconds, CultureInfo.InvariantCulture, out var length) ? TimeSpan.FromSeconds(length) : TimeSpan.Zero;
            }
            else if (tag == "#EXT-X-MAP")
            {
                isFragmentedMp4 = true;
            }
            else if (tag == "#EXT-X-ENDLIST")
            {
                hasEnded = true;
            }
            else if (!line.StartsWith('#') && Uri.TryCreate(url, line, out var address))
            {
                var segment = new HlsSegment(sequence + segments.Count, duration, address);
                segments.Add(segment);
            }
        }
        return target is { } targetDuration ? new HlsPlaylist(targetDuration, segments.ToArray(), isFragmentedMp4, hasEnded) : null;
    }

    private static IEnumerable<string> Lines(string text) =>
        text.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0);

    // Preceded by ':' or ',' so that AVERAGE-BANDWIDTH doesn't match.
    [GeneratedRegex("[:,]BANDWIDTH=(\\d+)")]
    private static partial Regex BandwidthAttribute();
}
