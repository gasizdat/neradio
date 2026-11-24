using System.Globalization;
using System.Text.RegularExpressions;

namespace Neradio.Core.Playback;

internal sealed record HlsSegment(long Sequence, TimeSpan Duration, Uri Url);

// A media playlist (RFC 8216), as far as playing its audio needs.
internal sealed partial record HlsPlaylist(TimeSpan TargetDuration, HlsSegment[] Segments, bool IsFragmentedMp4, bool HasEnded)
{
    // Longer than any segment lasts, and short enough for the timers that wait for one.
    private const int MaxSeconds = 3600;

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
                var digits = match.Groups[1].ValueSpan;
                bandwidth = match.Success && long.TryParse(digits, CultureInfo.InvariantCulture, out var value) ? value : 0;
            }
            else if (bandwidth is { } candidate && !line.StartsWith('#'))
            {
                if (candidate > bestBandwidth && Uri.TryCreate(url, line, out var variant) && Playlists.IsHttp(variant))
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
            switch (tag)
            {
                case "#EXT-X-TARGETDURATION":
                    var isTarget = int.TryParse(value, CultureInfo.InvariantCulture, out var seconds) && seconds is > 0 and <= MaxSeconds;
                    target = isTarget ? TimeSpan.FromSeconds(seconds) : null;
                    break;
                case "#EXT-X-MEDIA-SEQUENCE":
                    sequence = long.TryParse(value, CultureInfo.InvariantCulture, out var first) ? first : 0;
                    break;
                case "#EXTINF":
                    // The duration, then a comma and an optional title.
                    var number = value.Split(',')[0];
                    var isDuration = double.TryParse(number, CultureInfo.InvariantCulture, out var length) && length is >= 0 and <= MaxSeconds;
                    duration = isDuration ? TimeSpan.FromSeconds(length) : TimeSpan.Zero;
                    break;
                case "#EXT-X-MAP":
                    isFragmentedMp4 = true;
                    break;
                case "#EXT-X-ENDLIST":
                    hasEnded = true;
                    break;
                case var _ when !line.StartsWith('#'):
                    // A segment that can't be downloaded still takes its number.
                    if (Uri.TryCreate(url, line, out var address) && Playlists.IsHttp(address))
                    {
                        var segment = new HlsSegment(sequence, duration, address);
                        segments.Add(segment);
                    }
                    sequence++;
                    break;
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
