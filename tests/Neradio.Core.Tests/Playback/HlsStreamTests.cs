using System.Text;
using Microsoft.Extensions.Time.Testing;
using Neradio.Core.Playback;

namespace Neradio.Core.Tests.Playback;

public sealed class HlsStreamTests
{
    private static readonly Uri Master = new("https://hls.example/live.m3u8");
    private static readonly Uri Media = new("https://hls.example/high.m3u8");

    private readonly FakeTimeProvider _time = new();
    private readonly FakeEngine _engine = new();
    private readonly FakeHttp _http;
    private readonly HttpClient _client;
    private readonly HlsStream _hls;
    private readonly CancellationTokenSource _session = new();
    private readonly Connection _connection;

    public HlsStreamTests()
    {
        _http = new FakeHttp(_time);
        _client = new HttpClient(_http);
        _hls = new HlsStream(_time);
        _connection = new Connection(_engine, _time, _session.Token);
    }

    // Variants

    [Fact]
    public void VariantWithTheHighestBandwidth()
    {
        var master = """
            #EXTM3U
            #EXT-X-STREAM-INF:AVERAGE-BANDWIDTH=300000,BANDWIDTH=64000,CODECS="mp4a.40.5"
            low.m3u8
            #EXT-X-STREAM-INF:BANDWIDTH=128000,CODECS="mp4a.40.2"
            high.m3u8
            #EXT-X-STREAM-INF:BANDWIDTH=96000
            mid.m3u8
            """;
        Serve(100, 100);

        var reading = Read(Master, master);

        Assert.False(reading.IsCompleted);
        Assert.Equal([Media, Segment(100)], _http.Requested);
    }

    // Where a connection starts

    [Fact]
    public void StartsUpTo60SecondsBack()
    {
        Serve(100, 109);

        Read(100, 109);

        Assert.Equal("[104][105][106][107][108][109]", Played());
    }

    [Fact]
    public void ShortPlaylistIsTakenWhole()
    {
        Serve(100, 102);

        Read(100, 102);

        Assert.Equal("[100][101][102]", Played());
    }

    // Each segment once, in order

    [Fact]
    public void NewSegmentsOnly()
    {
        Serve(100, 105);
        Read(100, 105);

        Serve(101, 106);
        _time.Advance(TimeSpan.FromSeconds(10));
        Serve(103, 108);
        _time.Advance(TimeSpan.FromSeconds(10));

        Assert.Equal("[100][101][102][103][104][105][106][107][108]", Played());
    }

    [Fact]
    public void ReloadsAfterTheLastSegmentOrHalfTheTargetDuration()
    {
        Serve(100, 105);
        Read(100, 105);

        _time.Advance(TimeSpan.FromSeconds(9.9));
        Assert.Equal(0, MediaRequests());
        _time.Advance(TimeSpan.FromSeconds(0.1));
        Assert.Equal(1, MediaRequests());
        _time.Advance(TimeSpan.FromSeconds(4.9));
        Assert.Equal(1, MediaRequests());
        _time.Advance(TimeSpan.FromSeconds(0.1));
        Assert.Equal(2, MediaRequests());
    }

    [Fact]
    public void EndedPlaylist()
    {
        Serve(100, 101);

        var playlist = Playlist(100, 101) + "\n#EXT-X-ENDLIST";
        var reading = Read(Media, playlist);

        Assert.True(reading.IsCompletedSuccessfully);
        Assert.Equal("[100][101]", Played());
    }

    // Segment formats

    [Fact]
    public void PackedAudioWithoutItsId3Tag()
    {
        _http.Text(Media, Playlist(100, 100));
        byte[] packed = [.. "ID3"u8, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 1, 2, 3, .. "[100]"u8];
        _http.Bytes(Segment(100), packed, "audio/aac");

        Read(100, 100);

        Assert.Equal("[100]", Played());
    }

    [Fact]
    public void FragmentedMp4IsUnsupported()
    {
        var playlist = """
            #EXTM3U
            #EXT-X-TARGETDURATION:10
            #EXT-X-MAP:URI="init.mp4"
            #EXTINF:10.0,
            high/100.m4s
            """;

        var reading = Read(Media, playlist);

        Assert.True(reading.IsCompleted);
        Assert.True(_connection.IsUnsupported);
        Assert.Empty(_engine.Streams);
    }

    [Fact]
    public void SegmentWithoutAacIsUnsupported()
    {
        _http.Text(Media, Playlist(100, 100));
        var frame = Payload(100);
        var segment = MpegTsWriter.Segment(MpegTsWriter.Mp3, frame);
        _http.Bytes(Segment(100), segment);

        Read(100, 100);

        Assert.True(_connection.IsUnsupported);
        Assert.Empty(_engine.Streams);
    }

    // HLS reconnect

    [Fact]
    public void ReconnectGoesOnAfterTheLastSegment()
    {
        Serve(95, 100);
        Read(95, 100);
        _session.Cancel();
        var before = _http.Requests.Count;

        Serve(98, 105);
        var second = new Connection(_engine, _time, CancellationToken.None);
        var playlist = Playlist(98, 105);
        _hls.ReadAsync(_client, Media, playlist, second);

        Assert.Equal("[101][102][103][104][105]", Played(1));
        var after = _http.Requested.Skip(before);
        Assert.DoesNotContain(Segment(98), after);
        Assert.DoesNotContain(Segment(99), after);
        Assert.DoesNotContain(Segment(100), after);
    }

    // HLS reconnect after a long outage

    [Fact]
    public void ReconnectAfterTheNextSegmentIsGone()
    {
        Serve(95, 100);
        Read(95, 100);
        _session.Cancel();

        Serve(110, 117);
        var second = new Connection(_engine, _time, CancellationToken.None);
        var playlist = Playlist(110, 117);
        _hls.ReadAsync(_client, Media, playlist, second);

        Assert.Equal("[112][113][114][115][116][117]", Played(1));
    }

    // HLS segments

    [Fact]
    public void NewSegmentEveryTargetDuration()
    {
        Serve(100, 105);
        var reading = Read(100, 105);

        for (var next = 106; next < 112; next++)
        {
            Serve(next - 5, next);
            _time.Advance(TimeSpan.FromSeconds(10));
        }

        Assert.False(reading.IsCompleted);
        Assert.EndsWith("[110][111]", Played());
    }

    // Stalled HLS stream

    [Fact]
    public void NoNewSegmentFor10SecondsPlusTheTargetDuration()
    {
        Serve(100, 105);
        var reading = Read(100, 105);

        _time.Advance(TimeSpan.FromSeconds(10));
        _time.Advance(TimeSpan.FromSeconds(5));
        _time.Advance(TimeSpan.FromSeconds(4.9));
        Assert.False(reading.IsCompleted);
        _time.Advance(TimeSpan.FromSeconds(0.1));

        Assert.True(reading.IsCanceled);
    }


    private Task Read(long first, long last)
    {
        var playlist = Playlist(first, last);
        return Read(Media, playlist);
    }

    private Task Read(Uri url, string playlist) => _hls.ReadAsync(_client, url, playlist, _connection);

    private void Serve(long first, long last)
    {
        var playlist = Playlist(first, last);
        _http.Text(Media, playlist, "application/vnd.apple.mpegurl");
        for (var sequence = first; sequence <= last; sequence++)
        {
            var frame = Payload(sequence);
            var segment = MpegTsWriter.Segment(frame);
            _http.Bytes(Segment(sequence), segment);
        }
    }

    private int MediaRequests() => _http.Requested.Count(url => url == Media);

    private string Played(int stream = 0)
    {
        var written = _engine.Streams[stream].Written;
        return Encoding.ASCII.GetString(written);
    }

    private static string Playlist(long first, long last)
    {
        var lines = new List<string> { "#EXTM3U", "#EXT-X-TARGETDURATION:10", $"#EXT-X-MEDIA-SEQUENCE:{first}" };
        for (var sequence = first; sequence <= last; sequence++)
        {
            lines.Add("#EXTINF:10.000,");
            lines.Add($"high/{sequence}.ts");
        }
        return string.Join("\n", lines);
    }

    private static Uri Segment(long sequence) => new($"https://hls.example/high/{sequence}.ts");

    private static byte[] Payload(long sequence) => Encoding.ASCII.GetBytes($"[{sequence}]");
}
