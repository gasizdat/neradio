using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Neradio.Core.Playback;

// Stream URLs given as .pls and .m3u playlists, and HLS playlists.
internal static partial class Playlists
{
    // Far more than a playlist needs, HLS media playlists with a long window included.
    private const int MaxSize = 1024 * 1024;
    private const int ChunkSize = 16 * 1024;

    private static readonly string[] Extensions = [".pls", ".m3u", ".m3u8"];

    public static bool IsPlaylist(Uri url) =>
        Extensions.Any(extension => url.AbsolutePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase));

    // A list of streams has no tags of HLS's own.
    public static bool IsHls(string text) => text.Contains("#EXT-X-", StringComparison.Ordinal);

    // HttpClient throws on any other scheme.
    public static bool IsHttp(Uri url) => url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps;

    // The http and https URLs a .pls or .m3u lists, in its order.
    public static Uri[] Entries(Uri url, string text)
    {
        var lines = text.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0);
        var isPls = url.AbsolutePath.EndsWith(".pls", StringComparison.OrdinalIgnoreCase);
        var entries = isPls ? PlsEntries(lines) : lines.Where(line => !line.StartsWith('#'));
        return entries
            .Select(entry => Uri.TryCreate(url, entry, out var stream) ? stream : null)
            .OfType<Uri>()
            .Where(IsHttp)
            .ToArray();
    }

    // Null when the server answers with an error, or with more than a playlist would hold.
    public static async Task<string?> DownloadAsync(HttpClient http, Uri url, CancellationToken cancellation)
    {
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellation).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return null;

        await using var body = await response.Content.ReadAsStreamAsync(cancellation).ConfigureAwait(false);
        using var text = new MemoryStream();
        var buffer = new byte[ChunkSize];
        int read;
        while ((read = await body.ReadAsync(buffer, cancellation).ConfigureAwait(false)) > 0)
        {
            if (text.Length + read > MaxSize)
                return null;
            text.Write(buffer, 0, read);
        }
        var bytes = text.GetBuffer();
        var content = Encoding.UTF8.GetString(bytes, 0, (int)text.Length);
        return content.TrimStart('﻿');
    }

    // The FileN= entries, in the order of N.
    private static IEnumerable<string> PlsEntries(IEnumerable<string> lines) =>
        lines
            .Select(line => PlsFile().Match(line))
            .Where(match => match.Success)
            .OrderBy(match => int.Parse(match.Groups[1].ValueSpan, CultureInfo.InvariantCulture))
            .Select(match => match.Groups[2].Value);

    [GeneratedRegex("^File(\\d{1,9})=(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex PlsFile();
}
