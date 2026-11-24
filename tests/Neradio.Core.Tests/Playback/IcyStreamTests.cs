using System.Net;
using System.Text;
using Microsoft.Extensions.Time.Testing;
using Neradio.Core.Playback;

namespace Neradio.Core.Tests.Playback;

public sealed class IcyStreamTests
{
    private static readonly Uri Url = new("https://a.example/live");

    private readonly FakeTimeProvider _time = new();
    private readonly FakeEngine _engine = new();
    private readonly FakeHttp _http;
    private readonly HttpClient _client;
    private readonly Connection _connection;

    public IcyStreamTests()
    {
        _http = new FakeHttp(_time);
        _client = new HttpClient(_http);
        _connection = new Connection(_engine, _time, CancellationToken.None);
    }

    // The request

    [Fact]
    public void AsksForIcyMetadata()
    {
        _http.Stream(Url);

        Read();

        var values = _http.LastRequest!.Headers.GetValues("Icy-MetaData");
        Assert.Equal(["1"], values);
    }

    [Fact]
    public void EngineStreamIsOpenedForTheContentType()
    {
        _http.Stream(Url, "audio/aacp");

        Read();

        var stream = Assert.Single(_engine.Streams);
        Assert.Equal("audio/aacp", stream.ContentType);
    }

    // ICY metadata is taken out of the stream

    [Fact]
    public void MetadataBlocksAreTakenOut()
    {
        _http.Stream(Url, metaInt: 4);
        var reading = Read();

        var body = _http.LastBody(Url);
        body.Send([1, 2, 3, 4, 1, .. Metadata("StreamTitle='A';"), 5, 6, 7, 8, 0, 9]);
        body.End();

        Assert.True(reading.IsCompletedSuccessfully);
        var stream = Assert.Single(_engine.Streams);
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8, 9], stream.Written);
    }

    [Fact]
    public void MetadataSplitAcrossReads()
    {
        _http.Stream(Url, metaInt: 2);
        var reading = Read();

        var body = _http.LastBody(Url);
        byte[] data = [1, 2, 1, .. Metadata("StreamTitle='A';"), 3, 4, 0, 5];
        foreach (var value in data)
            body.Send([value]);
        body.End();

        Assert.True(reading.IsCompletedSuccessfully);
        var stream = Assert.Single(_engine.Streams);
        Assert.Equal([1, 2, 3, 4, 5], stream.Written);
    }

    [Fact]
    public void StreamWithoutMetadata()
    {
        _http.Stream(Url);
        var reading = Read();

        var body = _http.LastBody(Url);
        body.Send([1, 2, 3, 4, 5, 6]);
        body.End();

        Assert.True(reading.IsCompletedSuccessfully);
        var stream = Assert.Single(_engine.Streams);
        Assert.Equal([1, 2, 3, 4, 5, 6], stream.Written);
    }

    // Server answers with an error

    [Fact]
    public void ErrorStatusIsAFailedConnection()
    {
        _http.Status(Url, HttpStatusCode.NotFound);

        var reading = Read();

        Assert.True(reading.IsCompletedSuccessfully);
        Assert.Empty(_engine.Streams);
        Assert.False(_connection.IsUnsupported);
    }

    // Wi-Fi login page

    [Fact]
    public void WebPageIsAFailedConnection()
    {
        _http.Text(Url, "<html><body>Log in to the Wi-Fi</body></html>", "text/html");

        var reading = Read();

        Assert.True(reading.IsCompletedSuccessfully);
        Assert.Empty(_engine.Streams);
        Assert.False(_connection.IsUnsupported);
    }

    // The bytes arriving

    [Fact]
    public void NoDataFor10Seconds()
    {
        _http.Stream(Url);
        var reading = Read();
        _http.LastBody(Url).Send([1]);

        _time.Advance(TimeSpan.FromSeconds(10));

        Assert.True(reading.IsCanceled);
    }

    [Fact]
    public void DataEvery9Seconds()
    {
        _http.Stream(Url);
        var reading = Read();

        var body = _http.LastBody(Url);
        for (var second = 0; second < 60; second += 9)
        {
            body.Send([1]);
            _time.Advance(TimeSpan.FromSeconds(9));
        }

        Assert.False(reading.IsCompleted);
    }

    [Fact]
    public void NoAnswerFor10Seconds()
    {
        _http.Hang(Url);
        var reading = Read();

        _time.Advance(TimeSpan.FromSeconds(10));

        Assert.True(reading.IsCanceled);
    }

    [Fact]
    public void FullBufferIsNotAStall()
    {
        _http.Stream(Url);
        var reading = Read();
        var stream = Assert.Single(_engine.Streams);
        stream.IsFull = true;

        _http.LastBody(Url).Send([1]);
        _time.Advance(TimeSpan.FromSeconds(30));
        stream.Drain();
        _time.Advance(TimeSpan.FromSeconds(9));

        Assert.False(reading.IsCompleted);
        Assert.Equal([1], stream.Written);
    }

    [Fact]
    public void UnsupportedFormatEndsTheConnection()
    {
        _http.Stream(Url);
        var reading = Read();

        _engine.Streams[0].RejectFormat();

        Assert.True(reading.IsCanceled);
        Assert.True(_connection.IsUnsupported);
    }

    private Task Read() => IcyStream.ReadAsync(_client, Url, _connection);

    // The block after a length byte of 1, which counts 16 bytes.
    private static byte[] Metadata(string text)
    {
        var bytes = Encoding.ASCII.GetBytes(text);
        Assert.Equal(16, bytes.Length);
        return bytes;
    }
}
