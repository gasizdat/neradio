using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Neradio.Core.Tests.Playback;

// Answers by URL. A URL without an answer refuses the connection.
sealed class FakeHttp(TimeProvider time) : HttpMessageHandler
{
    private readonly Dictionary<Uri, Func<CancellationToken, Task<HttpResponseMessage>>> _answers = [];

    public List<(Uri Url, DateTimeOffset At)> Requests { get; } = [];
    public Dictionary<Uri, List<FakeBody>> Bodies { get; } = [];
    public HttpRequestMessage? LastRequest { get; private set; }

    public IEnumerable<Uri> Requested => Requests.Select(request => request.Url);

    // Each request gets a new body, for the test to send data on.
    public void Stream(Uri url, string contentType = "audio/mpeg", int metaInt = 0)
    {
        Bodies[url] = [];
        _answers[url] = _ =>
        {
            var body = new FakeBody();
            Bodies[url].Add(body);
            var response = Response(HttpStatusCode.OK, contentType, body);
            if (metaInt > 0)
            {
                var interval = metaInt.ToString();
                response.Headers.Add("icy-metaint", interval);
            }
            return Task.FromResult(response);
        };
    }

    public void Text(Uri url, string text, string contentType = "audio/x-mpegurl")
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        Bytes(url, bytes, contentType);
    }

    public void Bytes(Uri url, byte[] bytes, string contentType = "video/mp2t") =>
        _answers[url] = _ =>
        {
            var body = new MemoryStream(bytes);
            var response = Response(HttpStatusCode.OK, contentType, body);
            return Task.FromResult(response);
        };

    public void Status(Uri url, HttpStatusCode status) =>
        _answers[url] = _ =>
        {
            var body = new MemoryStream();
            var response = Response(status, "text/html", body);
            return Task.FromResult(response);
        };

    // The server accepts the connection and never answers.
    public void Hang(Uri url) =>
        _answers[url] = cancellation =>
        {
            var answer = new TaskCompletionSource<HttpResponseMessage>();
            cancellation.Register(() => answer.TrySetCanceled(cancellation));
            return answer.Task;
        };

    public void Refuse(Uri url) => _answers.Remove(url);

    public FakeBody LastBody(Uri url) => Bodies[url][^1];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri!;
        // As SocketsHttpHandler does.
        if (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps)
            throw new NotSupportedException($"The '{url.Scheme}' scheme is not supported.");
        var now = time.GetUtcNow();
        Requests.Add((url, now));
        LastRequest = request;
        if (!_answers.TryGetValue(url, out var answer))
            throw new HttpRequestException("Connection refused");
        return answer(cancellationToken);
    }

    private static HttpResponseMessage Response(HttpStatusCode status, string contentType, Stream body)
    {
        var response = new HttpResponseMessage(status) { Content = new StreamContent(body) };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return response;
    }
}

// A response body that gets its data when the test sends it.
sealed class FakeBody : Stream
{
    private readonly Queue<byte> _data = new();
    private bool _hasEnded;
    private bool _hasFailed;
    private TaskCompletionSource<int>? _reader;
    private Memory<byte> _readerBuffer;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public void Send(byte[] data)
    {
        foreach (var value in data)
            _data.Enqueue(value);
        Wake();
    }

    // The server closes the connection.
    public void End()
    {
        _hasEnded = true;
        Wake();
    }

    // The connection breaks.
    public void Fail()
    {
        _hasFailed = true;
        Wake();
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_data.Count > 0 || _hasEnded)
            return ValueTask.FromResult(Take(buffer));
        if (_hasFailed)
            return ValueTask.FromException<int>(new IOException("Connection reset"));

        var reader = new TaskCompletionSource<int>();
        _reader = reader;
        _readerBuffer = buffer;
        cancellationToken.Register(() => reader.TrySetCanceled(cancellationToken));
        return new ValueTask<int>(reader.Task);
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    // The reader's continuation runs inline, and may read again: it's taken off before it's woken.
    private void Wake()
    {
        var reader = _reader;
        if (reader is null || reader.Task.IsCompleted)
            return;
        _reader = null;
        if (_hasFailed && _data.Count == 0)
        {
            reader.TrySetException(new IOException("Connection reset"));
            return;
        }
        var read = Take(_readerBuffer);
        reader.TrySetResult(read);
    }

    private int Take(Memory<byte> buffer)
    {
        var count = Math.Min(buffer.Length, _data.Count);
        var span = buffer.Span;
        for (var at = 0; at < count; at++)
            span[at] = _data.Dequeue();
        return count;
    }
}
