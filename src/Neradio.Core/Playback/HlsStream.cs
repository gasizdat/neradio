namespace Neradio.Core.Playback;

// An HLS stream across its connections: each segment is downloaded once, in order, so a reconnect
// goes on after the last segment downloaded.
internal sealed class HlsStream(TimeProvider time)
{
    private const string ContentType = "audio/aac";
    private const int ChunkSize = 16 * 1024;

    // What the engine's buffer holds: starting this far back fills it at once.
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(60);

    private long? _next;

    // Returns when the playlist ends or stops being one. The text is the playlist at the URL.
    public async Task ReadAsync(HttpClient http, Uri url, string text, Connection connection)
    {
        var token = connection.Token;
        var loadedAt = time.GetTimestamp();
        if (HlsPlaylist.Variant(url, text) is { } variant)
        {
            url = variant;
            loadedAt = time.GetTimestamp();
            text = await Playlists.DownloadAsync(http, url, token).ConfigureAwait(false) ?? "";
        }

        while (HlsPlaylist.Read(url, text) is { } playlist)
        {
            if (playlist.IsFragmentedMp4)
            {
                connection.Reject();
                return;
            }

            connection.AllowFor(playlist.TargetDuration);
            var oldest = playlist.Segments.FirstOrDefault();
            if (oldest is not null && (_next is null || _next < oldest.Sequence))
                _next = LiveStart(playlist.Segments);
            var fresh = playlist.Segments
                .Where(segment => segment.Sequence >= _next)
                .ToArray();
            foreach (var segment in fresh)
            {
                var data = await DownloadAsync(http, segment.Url, connection).ConfigureAwait(false);
                var audio = Audio(data);
                if (audio is null)
                {
                    connection.Reject();
                    return;
                }
                if (connection.Audio is null)
                    connection.Open(ContentType);
                await connection.WriteAsync(audio).ConfigureAwait(false);
                _next = segment.Sequence + 1;
            }
            if (playlist.HasEnded)
                return;

            // RFC 8216, 6.3.4: after a playlist that changed, wait for its last segment's duration;
            // after one that didn't, half the target duration. Both count from when it began loading.
            var wait = fresh.Length > 0 ? playlist.Segments[^1].Duration : playlist.TargetDuration / 2;
            var due = wait - time.GetElapsedTime(loadedAt);
            if (due > TimeSpan.Zero)
                await time.DelayAsync(due, token).ConfigureAwait(false);
            loadedAt = time.GetTimestamp();
            text = await Playlists.DownloadAsync(http, url, token).ConfigureAwait(false) ?? "";
        }
    }

    private static long LiveStart(HlsSegment[] segments)
    {
        var start = segments[^1].Sequence;
        var total = TimeSpan.Zero;
        for (var at = segments.Length - 1; at >= 0; at--)
        {
            total += segments[at].Duration;
            if (total > Window)
                break;
            start = segments[at].Sequence;
        }
        return start;
    }

    private static async Task<byte[]> DownloadAsync(HttpClient http, Uri url, Connection connection)
    {
        var token = connection.Token;
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var body = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using var segment = new MemoryStream();
        var buffer = new byte[ChunkSize];
        int read;
        while ((read = await body.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
        {
            connection.Received();
            segment.Write(buffer, 0, read);
        }
        return segment.ToArray();
    }

    // MPEG-TS, or packed audio: ADTS after an ID3 tag (RFC 8216, 3.4). Null when there's no AAC.
    private static byte[]? Audio(byte[] segment)
    {
        if (segment.Length > 0 && segment[0] == MpegTs.SyncByte)
            return MpegTs.Aac(segment);

        ReadOnlySpan<byte> audio = segment;
        while (audio.Length >= 10 && audio.StartsWith("ID3"u8))
        {
            var size = (audio[6] << 21) | (audio[7] << 14) | (audio[8] << 7) | audio[9];
            var footer = (audio[5] & 0x10) != 0 ? 10 : 0;
            var end = Math.Min(audio.Length, 10 + size + footer);
            audio = audio[end..];
        }
        return audio.ToArray();
    }
}
