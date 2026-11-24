using System.Text;

namespace Neradio.Core.Playback;

internal static class Playlists
{
    // Far more than a playlist needs, HLS media playlists with a long window included.
    private const int MaxSize = 1024 * 1024;
    private const int ChunkSize = 16 * 1024;

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
        var content = Encoding.UTF8.GetString(text.GetBuffer(), 0, (int)text.Length);
        return content.TrimStart('﻿');
    }
}
