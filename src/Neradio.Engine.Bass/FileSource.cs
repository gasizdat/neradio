using System.Runtime.InteropServices;
using ManagedBass;
using Extensions = ManagedBass.Extensions;

namespace Neradio.Engine.Bass;

// A stream BASS decodes itself: MP3, Ogg Vorbis, Opus, and AAC on Android. The bytes go into a
// push stream, whose buffer BASS sizes to hold 60 seconds of audio.
internal sealed class FileSource : MixerSource
{
    // BASS holds these as raw function pointers, so they must live as long as the channel.
    private readonly FileProcedures _procedures;
    private readonly SyncProcedure _oggChanged;
    // What's written before the channel exists, and what didn't fit in its buffer then. BASS
    // reads the start of it to find the format.
    private readonly Queue<byte[]> _pending = new();
    private readonly SemaphoreSlim _arrived = new(0);
    // How much of the first pending array BASS has taken.
    private int _readFromNext;
    private bool _isCreating;

    public FileSource(BassEngine engine)
        : base(engine)
    {
        _procedures = new FileProcedures
        {
            Close = _ => { },
            Length = _ => 0,
            Read = Read,
            Seek = (_, _) => false,
        };
        _oggChanged = (_, channel, _, _) =>
        {
            var title = OggTitle(channel);
            if (title is not null)
                RaiseTitleChanged(title);
        };
    }

    public override long Decoded
    {
        get
        {
            var position = ManagedBass.Bass.StreamGetFilePosition(Channel, FileStreamPosition.Current);
            return Math.Max(position, 0);
        }
    }

    // The buffer's size is BASS's estimate of 60 seconds of the stream, which it makes even for
    // Opus, whose bitrate it doesn't report.
    protected override double BufferedSeconds
    {
        get
        {
            var buffered = ManagedBass.Bass.StreamGetFilePosition(Channel, FileStreamPosition.Buffer);
            var size = ManagedBass.Bass.StreamGetFilePosition(Channel, FileStreamPosition.End);
            return (double)buffered / size * BufferSeconds;
        }
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellation)
    {
        while (true)
        {
            lock (Gate)
            {
                if (IsDisposed)
                    return;
                if (Channel == 0)
                {
                    _pending.Enqueue(data.ToArray());
                    _arrived.Release();
                    StartCreating();
                    return;
                }
                PutPending();
                if (_pending.Count == 0)
                {
                    var put = Put(data.Span);
                    data = data[put..];
                }
            }
            CheckReady();
            if (data.IsEmpty)
                return;
            await Task.Delay(Retry, cancellation).ConfigureAwait(false);
        }
    }

    protected override void Release() => _arrived.Release();

    // Creating the channel blocks until BASS has read enough to know the format.
    private void StartCreating()
    {
        if (_isCreating)
            return;
        _isCreating = true;
        var thread = new Thread(Create) { IsBackground = true, Name = "BASS stream" };
        thread.Start();
    }

    private void Create()
    {
        var channel = ManagedBass.Bass.CreateStream(StreamSystem.BufferPush, BassFlags.Decode | BassFlags.Float, _procedures);
        string? title;
        lock (Gate)
        {
            if (IsDisposed)
            {
                ManagedBass.Bass.StreamFree(channel);
                return;
            }
            if (channel == 0)
            {
                RaiseUnsupported();
                return;
            }
            Attach(channel);
            ManagedBass.Bass.ChannelSetSync(channel, SyncFlags.OggChange | SyncFlags.Mixtime, 0, _oggChanged);
            // HLS starts with up to 60 seconds of audio, which may not all fit. The next write
            // puts the rest first.
            PutPending();
            title = OggTitle(channel);
        }
        // An Ogg change sync fires only for the streams after the first.
        if (title is not null)
            RaiseTitleChanged(title);
        CheckReady();
    }

    // BASS's thread, while the channel is created. Only Dispose makes it return 0, which BASS
    // takes for the end of the data.
    private int Read(IntPtr buffer, int length, IntPtr user)
    {
        while (true)
        {
            lock (Gate)
            {
                if (IsDisposed)
                    return 0;
                if (_pending.TryPeek(out var next))
                {
                    var read = Math.Min(length, next.Length - _readFromNext);
                    Marshal.Copy(next, _readFromNext, buffer, read);
                    _readFromNext += read;
                    if (_readFromNext == next.Length)
                    {
                        _pending.Dequeue();
                        _readFromNext = 0;
                    }
                    return read;
                }
            }
            _arrived.Wait();
        }
    }

    // Under the gate, once the channel exists.
    private void PutPending()
    {
        while (_pending.TryPeek(out var next))
        {
            var rest = next.AsSpan(_readFromNext);
            _readFromNext += Put(rest);
            if (_readFromNext < next.Length)
                return;
            _pending.Dequeue();
            _readFromNext = 0;
        }
    }

    private unsafe int Put(ReadOnlySpan<byte> data)
    {
        fixed (byte* start = data)
        {
            var put = ManagedBass.Bass.StreamPutFileData(Channel, (IntPtr)start, data.Length);
            return Math.Max(put, 0);
        }
    }

    private static string? OggTitle(int channel)
    {
        var tags = ManagedBass.Bass.ChannelGetTags(channel, TagType.OGG);
        if (tags == IntPtr.Zero)
            return null;
        var comments = Extensions.ExtractMultiStringUtf8(tags);
        var artist = Comment(comments, "ARTIST");
        var title = Comment(comments, "TITLE");
        return artist is null ? title : $"{artist} - {title}";
    }

    private static string? Comment(string[] comments, string key)
    {
        var prefix = key + "=";
        var comment = comments.FirstOrDefault(c => c.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        return comment?[prefix.Length..];
    }
}
