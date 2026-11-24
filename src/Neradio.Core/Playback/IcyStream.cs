namespace Neradio.Core.Playback;

// A plain HTTP stream. Asked for ICY metadata, the server puts a metadata block after every
// icy-metaint bytes of audio.
internal static class IcyStream
{
    private const int ChunkSize = 16 * 1024;

    // Returns when the stream ends, or at once when the response isn't audio.
    public static async Task ReadAsync(HttpClient http, Uri url, Connection connection)
    {
        var token = connection.Token;
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Icy-MetaData", "1");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (!response.IsSuccessStatusCode || !IsAudio(contentType))
            return;

        connection.Open(contentType!);
        var metaInt = MetaInt(response);
        await using var body = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        var buffer = new byte[ChunkSize];
        var untilMeta = metaInt;
        while (true)
        {
            var want = metaInt == 0 ? buffer.Length : Math.Min(buffer.Length, untilMeta);
            var space = buffer.AsMemory(0, want);
            var read = await body.ReadAsync(space, token).ConfigureAwait(false);
            if (read == 0)
                return;
            connection.Received();
            var audio = buffer.AsMemory(0, read);
            await connection.WriteAsync(audio).ConfigureAwait(false);
            if (metaInt == 0)
                continue;

            untilMeta -= read;
            if (untilMeta > 0)
                continue;

            var lengthByte = buffer.AsMemory(0, 1);
            await body.ReadExactlyAsync(lengthByte, token).ConfigureAwait(false);
            var metadata = buffer.AsMemory(0, buffer[0] * 16);
            await body.ReadExactlyAsync(metadata, token).ConfigureAwait(false);
            connection.Received();
            untilMeta = metaInt;
        }
    }

    // A web page, such as a Wi-Fi login page, is a failed connection rather than an unsupported format.
    private static bool IsAudio(string? contentType) =>
        contentType is not null
        && (contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)
            || contentType.Equals("application/ogg", StringComparison.OrdinalIgnoreCase));

    private static int MetaInt(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("icy-metaint", out var values))
            return 0;
        var value = values.First();
        return int.TryParse(value, out var metaInt) && metaInt > 0 ? metaInt : 0;
    }
}
