namespace EngineTest;

// Saves the stream's bytes as they're downloaded, starting a new file at each title change:
// at ICY metadata for MP3 and AAC, and at the first page of each new Ogg stream for Ogg.
sealed class Recorder
{
    readonly object _gate = new();
    FileStream? _file;
    string? _title;
    bool _on;

    public static string Folder { get; } = Path.Combine(Log.Folder, "Recordings");

    public bool On
    {
        get
        {
            lock (_gate)
                return _on;
        }
        set
        {
            lock (_gate)
            {
                _on = value;
                Close();
            }
            Log.Write("record", value ? $"on, into {Folder}" : "off");
        }
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        lock (_gate)
        {
            if (!_on)
                return;
            var newStream = OggStreamStart(data);
            if (newStream >= 0 && _file is not null)
            {
                _file.Write(data[..newStream]);
                Close();
                data = data[newStream..];
            }
            _file ??= Open(data);
            _file.Write(data);
        }
    }

    public void Split(string? title)
    {
        lock (_gate)
        {
            _title = title;
            Close();
        }
    }

    FileStream Open(ReadOnlySpan<byte> first)
    {
        Directory.CreateDirectory(Folder);
        var invalid = Path.GetInvalidFileNameChars();
        var title = new string((_title ?? "untitled").Select(c => invalid.Contains(c) ? '_' : c).Take(80).ToArray());
        var path = Path.Combine(Folder, $"{DateTime.Now:yyyyMMdd-HHmmss} {title}{Extension(first)}");
        Log.Write("record", $"new file {Path.GetFileName(path)}");
        return new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
    }

    void Close()
    {
        _file?.Dispose();
        _file = null;
    }

    static string Extension(ReadOnlySpan<byte> data) =>
        data.StartsWith("OggS"u8) ? ".ogg"
        : data.Length > 1 && data[0] == 0xFF && (data[1] & 0xF6) == 0xF0 ? ".aac"
        : ".mp3";

    // A page with the "beginning of stream" flag starts a new logical stream: in a chained Ogg
    // radio stream, that's the next track.
    static int OggStreamStart(ReadOnlySpan<byte> data)
    {
        for (var at = data.IndexOf("OggS"u8); at >= 0 && at + 5 < data.Length; )
        {
            if ((data[at + 5] & 0x02) != 0)
                return at;
            var next = data[(at + 4)..].IndexOf("OggS"u8);
            at = next < 0 ? -1 : at + 4 + next;
        }
        return -1;
    }
}
