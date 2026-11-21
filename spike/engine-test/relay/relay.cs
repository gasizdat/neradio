#:sdk Microsoft.NET.Sdk.Web

// Forwards a real station and injects the faults #7's scenarios need. On the PC:
//   dotnet run relay.cs            listens on localhost only
//   dotnet run relay.cs -- --lan   listens on the LAN, for the phone; Windows Firewall asks first
// The test app then opens http://<pc-ip>:8765/relay?u=<station url>&... See RUNSHEET.md.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(args.Contains("--lan") ? "http://0.0.0.0:8765" : "http://localhost:8765");
builder.Logging.SetMinimumLevel(LogLevel.Warning);
var app = builder.Build();
var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
var failingUntil = new ConcurrentDictionary<string, DateTime>();
var hlsStarted = new ConcurrentDictionary<string, DateTime>();
var hlsSnapshot = new ConcurrentDictionary<string, string>();

void Log(string s) => Console.WriteLine($"{DateTime.Now:HH:mm:ss.f} {s}");

// close=N   end the response after N seconds
// stall=N   after N seconds keep the connection open but send nothing
// pause=N:S after N seconds send nothing for S seconds, then go on
// down=S    after a close, answer 404 for S seconds
// html=S    after a close, answer with a login page for S seconds
app.MapGet("/relay", async (HttpContext ctx, string u, int? close, int? stall, string? pause, int? down, int? html) =>
{
    var key = ctx.Request.QueryString.Value!;
    var peer = ctx.Connection.RemoteIpAddress;
    if (failingUntil.TryGetValue(key, out var until) && DateTime.UtcNow < until)
    {
        if (html is not null)
        {
            Log($"{peer} login page");
            ctx.Response.ContentType = "text/html";
            await ctx.Response.WriteAsync("<!doctype html><html><body><form>Log in to use this Wi-Fi</form></body></html>");
            return;
        }
        Log($"{peer} 404");
        ctx.Response.StatusCode = 404;
        return;
    }

    using var request = new HttpRequestMessage(HttpMethod.Get, u);
    if (ctx.Request.Headers.TryGetValue("Icy-MetaData", out var icy))
        request.Headers.TryAddWithoutValidation("Icy-MetaData", icy.ToString());
    using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ctx.RequestAborted);
    ctx.Response.StatusCode = (int)response.StatusCode;
    var headers = response.Headers.Concat(response.Content.Headers);
    foreach (var (name, values) in headers.Where(h => h.Key.StartsWith("icy-", StringComparison.OrdinalIgnoreCase) || h.Key == "Content-Type"))
        ctx.Response.Headers[name] = values.ToArray();
    Log($"{peer} connected, upstream {(int)response.StatusCode}");

    // Forwarding at the station's bitrate keeps its connect burst out of the TCP buffers, where
    // it would hide a pause or a stall from the player for as long as the burst lasts.
    var bitrate = response.Headers.TryGetValues("icy-br", out var br) && int.TryParse(br.First().Split(',')[0], out var kbps) ? kbps * 1000 / 8 * 1.02 : 0;
    var pauseParts = pause?.Split(':').Select(int.Parse).ToArray();
    var paused = false;
    var sent = 0L;
    var buffer = new byte[4096];
    var clock = Stopwatch.StartNew();
    await using var upstream = await response.Content.ReadAsStreamAsync(ctx.RequestAborted);
    try
    {
        while (await upstream.ReadAsync(buffer, ctx.RequestAborted) is var n and > 0)
        {
            var t = clock.Elapsed.TotalSeconds;
            if (t >= close)
            {
                var failFor = down ?? html ?? 0;
                failingUntil[key] = DateTime.UtcNow.AddSeconds(failFor);
                Log($"{peer} closing after {t:F0} s; failing for {failFor} s");
                return;
            }
            if (t >= stall)
            {
                Log($"{peer} stalling");
                await Task.Delay(Timeout.Infinite, ctx.RequestAborted);
            }
            if (pauseParts is [var at, var length] && !paused && t >= at)
            {
                paused = true;
                Log($"{peer} pausing {length} s");
                await Task.Delay(TimeSpan.FromSeconds(length), ctx.RequestAborted);
                Log($"{peer} resuming");
            }
            await ctx.Response.Body.WriteAsync(buffer.AsMemory(0, n), ctx.RequestAborted);
            sent += n;
            var ahead = bitrate > 0 ? sent / bitrate - clock.Elapsed.TotalSeconds : 0;
            if (ahead > 0.5)
                await Task.Delay(TimeSpan.FromSeconds(ahead - 0.5), ctx.RequestAborted);
        }
        Log($"{peer} upstream ended");
    }
    catch (OperationCanceledException)
    {
        Log($"{peer} client disconnected after {clock.Elapsed.TotalSeconds:F0} s");
    }
});

// freeze=N:S  N seconds after the first request, serve the same media playlist for S seconds,
//             so no new segment appears; then serve the live playlist again
app.MapGet("/hls", async (HttpContext ctx, string u, string? freeze) =>
{
    var started = hlsStarted.GetOrAdd(u, _ => DateTime.UtcNow);
    var age = (DateTime.UtcNow - started).TotalSeconds;
    var freezeParts = freeze?.Split(':').Select(int.Parse).ToArray();
    var frozen = freezeParts is [var at, var length] && age >= at && age < at + length;
    if (!frozen || !hlsSnapshot.TryGetValue(u, out var playlist))
    {
        var text = await http.GetStringAsync(u, ctx.RequestAborted);
        playlist = Rewrite(text, new Uri(u), freeze);
        hlsSnapshot[u] = playlist;
    }
    Log($"{ctx.Connection.RemoteIpAddress} playlist{(frozen ? " (frozen)" : "")} at {age:F0} s");
    // Without a length the response is chunked, and BASSHLS doesn't recognize a chunked playlist.
    var body = Encoding.UTF8.GetBytes(playlist);
    ctx.Response.ContentType = "application/vnd.apple.mpegurl";
    ctx.Response.ContentLength = body.Length;
    await ctx.Response.Body.WriteAsync(body, ctx.RequestAborted);
});

app.Run();

// Segment URIs become absolute, so segments come straight from the station. Playlist URIs in a
// master playlist stay on the relay, so the media playlist can be frozen.
static string Rewrite(string text, Uri baseUri, string? freeze)
{
    var isMaster = text.Contains("#EXT-X-STREAM-INF");
    string Resolve(string uri)
    {
        var absolute = new Uri(baseUri, uri).ToString();
        var escaped = Uri.EscapeDataString(absolute);
        return isMaster ? $"/hls?u={escaped}&freeze={freeze}" : absolute;
    }
    var lines = text.Split('\n').Select(line => line.TrimEnd('\r'));
    var rewritten = lines.Select(line => line.Length == 0 ? line
        : line.StartsWith('#') ? Regex.Replace(line, "URI=\"([^\"]+)\"", m => $"URI=\"{Resolve(m.Groups[1].Value)}\"")
        : Resolve(line));
    return string.Join('\n', rewritten);
}
