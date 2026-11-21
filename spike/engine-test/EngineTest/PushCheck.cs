using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using ManagedBass;
using Extensions = ManagedBass.Extensions;

namespace EngineTest;

// Checks the design where the app owns every connection (#7): the app downloads each stream with
// HttpClient, takes the ICY titles out, and pushes the bytes into a BASS user stream, which only
// decodes. BASS's own connections break during a .NET GC on Android; these mustn't.
static class PushCheck
{
    const int Seconds = 8;

    static readonly (string Name, string Url)[] Streams =
    [
        ("MP3", "https://ice1.somafm.com/groovesalad-128-mp3"),
        ("MP3, plain http", "http://ice1.somafm.com/groovesalad-128-mp3"),
        ("AAC-LC", "https://ice1.somafm.com/groovesalad-128-aac"),
        ("HE-AAC", "https://ice1.somafm.com/groovesalad-64-aac"),
        ("Ogg Vorbis, plain http", "http://stream-dc1.radioparadise.com/rp_192m.ogg"),
        ("Opus", "https://st01.sslstream.dlf.de/dlf/01/low/opus/stream.opus?aggregator=web"),
    ];

    public static async Task Run(bool gc)
    {
        Log.Write("push", $"check starts{(gc ? ", with a GC every 50 ms" : "")}");
        using var http = new HttpClient(new SocketsHttpHandler());
        foreach (var (name, url) in Streams)
        {
            // Windows decodes AAC itself (spike/engine-test/windecode): BASS would wait for 1 MiB.
            if (OperatingSystem.IsWindows() && name.Contains("AAC"))
                continue;
            await Task.Run(() => Check(http, name, url, gc));
        }
        Log.Write("push", "check done");
    }

    static void Check(HttpClient http, string name, string url, bool gc)
    {
        using var collector = gc ? new Timer(_ => GC.Collect(), null, 0, 50) : null;
        var clock = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Icy-MetaData", "1");
            using var response = http.Send(request, HttpCompletionOption.ResponseHeadersRead);
            var headersAt = clock.ElapsedMilliseconds;
            var metaInt = response.Headers.TryGetValues("icy-metaint", out var values) ? int.Parse(values.First()) : 0;
            using var body = response.Content.ReadAsStream();
            var icy = new IcyReader(body, metaInt);
            var chunk = new byte[16 * 1024];
            // BASS reads the data it needs to recognise the format through this, on this thread.
            var procs = new FileProcedures
            {
                Close = _ => { },
                Length = _ => 0,
                Read = (buffer, length, _) =>
                {
                    var read = icy.Read(chunk, Math.Min(length, chunk.Length));
                    Marshal.Copy(chunk, 0, buffer, read);
                    return read;
                },
                Seek = (_, _) => false,
            };
            var handle = Bass.CreateStream(StreamSystem.BufferPush, BassFlags.Decode | BassFlags.Float | BassFlags.StreamDownloadBlocks, procs);
            var createdAt = clock.ElapsedMilliseconds;
            var answer = $"HTTP {(int)response.StatusCode} {response.Content.Headers.ContentType} after {headersAt} ms";
            if (handle == 0)
            {
                Log.Write("push", $"{name}: {answer}; BASS failed after {createdAt} ms: {Bass.LastError}");
                return;
            }
            var info = Bass.ChannelGetInfo(handle);
            var bytesPerSecond = info.Frequency * info.Channels * 4;
            var pcm = new byte[64 * 1024];
            long decoded = 0;
            long pushed = 0;
            var firstAudioAt = -1L;
            var until = createdAt + Seconds * 1000;
            while (clock.ElapsedMilliseconds < until)
            {
                var read = icy.Read(chunk, chunk.Length);
                if (read == 0)
                    break;
                for (var offset = 0; offset < read;)
                {
                    var put = PutFileData(handle, chunk, offset, read - offset);
                    offset += Math.Max(put, 0);
                    pushed += Math.Max(put, 0);
                    int got;
                    while ((got = Bass.ChannelGetData(handle, pcm, pcm.Length)) > 0)
                    {
                        decoded += got;
                        if (firstAudioAt < 0)
                            firstAudioAt = clock.ElapsedMilliseconds;
                    }
                }
            }
            var title = icy.Title ?? OggTitle(handle) ?? "none";
            Log.Write("push", $"{name}: {answer}; stream created after {createdAt} ms: {info.ChannelType} {info.Frequency} Hz {info.Channels} ch; first audio after {firstAudioAt} ms; {(double)decoded / bytesPerSecond:F1} s decoded from {pushed / 1024} KB in {Seconds} s; title: {title}");
            Bass.StreamFree(handle);
            GC.KeepAlive(procs);
        }
        catch (Exception e)
        {
            Log.Write("push", $"{name}: failed after {clock.ElapsedMilliseconds} ms: {e.GetType().Name}: {e.Message}");
        }
    }

    static unsafe int PutFileData(int handle, byte[] data, int offset, int length)
    {
        fixed (byte* start = data)
            return Bass.StreamPutFileData(handle, (IntPtr)(start + offset), length);
    }

    static string? OggTitle(int handle)
    {
        var tags = Bass.ChannelGetTags(handle, TagType.OGG);
        if (tags == IntPtr.Zero)
            return null;
        var comments = Extensions.ExtractMultiStringUtf8(tags);
        return comments.FirstOrDefault(c => c.StartsWith("TITLE=", StringComparison.OrdinalIgnoreCase));
    }

    // Reads the audio bytes of an ICY stream, taking out the metadata blocks every metaInt bytes.
    sealed class IcyReader(Stream body, int metaInt)
    {
        int _untilMeta = metaInt;

        public string? Title { get; private set; }

        public int Read(byte[] buffer, int count)
        {
            if (metaInt > 0 && _untilMeta == 0)
            {
                var length = body.ReadByte() * 16;
                var meta = new byte[length];
                body.ReadExactly(meta);
                var match = Regex.Match(Encoding.UTF8.GetString(meta), "StreamTitle='(.*?)';");
                if (match.Success)
                    Title = match.Groups[1].Value;
                _untilMeta = metaInt;
            }
            var want = metaInt > 0 ? Math.Min(count, _untilMeta) : count;
            var read = body.Read(buffer, 0, want);
            _untilMeta -= read;
            return read;
        }
    }
}
