using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using ManagedBass;
using ManagedBass.Fx;
using ManagedBass.Mix;
using Extensions = ManagedBass.Extensions;

namespace EngineTest;

// Plays one stream URL through a mixer. A new connection waits paused in the mixer and takes
// over at the end of the old one's buffered audio, without a gap; the equalizer and the
// spectrum stay on the mixer across reconnects. Reconnecting is a simplified #7: one URL,
// #7's waits, and a restart on network changes.
//
// All state lives on one thread. BASS calls syncs on its mixer thread while holding the
// mixer's lock, so a sync that waited for a lock held around a mixer call would deadlock.
// Syncs only post work here, except the switch itself, which has to happen at mix time.
sealed class Player
{
    static readonly int[] Waits = [1, 2, 4, 8, 16, 30];
    static readonly float[] Bands = [31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000];
    static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

    // After the fields above: static fields initialize in order, and the constructor uses them.
    public static readonly Player Instance = new();

    // BASS holds these as raw function pointers, so they must live as long as the player.
    readonly DownloadProcedure _downloaded;
    readonly SyncProcedure _downloadEnded;
    readonly SyncProcedure _bufferEnded;
    readonly SyncProcedure _stalled;
    readonly SyncProcedure _icyTitle;
    readonly SyncProcedure _oggTitle;
    readonly SyncProcedure _segmentStarted;
    readonly DSPProcedure _silenceMeter;

    readonly BlockingCollection<Action> _work = [];
    readonly ConcurrentDictionary<int, ConcurrentQueue<string>> _status = [];
    readonly ConcurrentDictionary<int, byte[]> _heads = [];
    readonly Recorder _recorder = new();
    readonly Timer _ticker;
    readonly int _mixer;
    readonly int _eq;
    readonly int _hlsPlugin;

    Source? _current;
    Source? _next;
    volatile int _currentHandle;
    volatile int _nextHandle;
    volatile int _request;
    int _wait = -1;
    CancellationTokenSource? _delay;
    string? _url;
    string? _station;
    string? _lastTitle;
    int _ticks;

    // Written by the silence meter on the mixer thread.
    long _frames;
    long _silentFrames;
    long _silentRun;
    long _waitingSince;
    string? _waitingFor;

    sealed class Source(int handle, int request, bool isHls)
    {
        public int Handle { get; } = handle;
        public int Request { get; } = request;
        public bool IsHls { get; } = isHls;
        public DateTime ConnectedAt { get; } = DateTime.UtcNow;
        public DateTime LastSegmentAt { get; set; } = DateTime.UtcNow;
        public double SegmentSeconds { get; set; } = 10;
        public bool Lost { get; set; }
        public bool SwitchAtOnce { get; set; }

        // Paused in the mixer until its download buffer holds PreBuffer percent.
        public bool Gated { get; set; } = true;
        public long GatedSince { get; set; } = Stopwatch.GetTimestamp();
    }

    public string State { get; private set; } = "stopped";
    public string? Title { get; private set; }
    public string? Format { get; private set; }
    public int NetBuffer { get; set; } = 5000;
    public int PreBuffer { get; set; } = 75;
    public static string SpectrumRate { get; set; } = "-";

    public bool Recording
    {
        get => _recorder.On;
        set => _recorder.On = value;
    }

    public event Action? Changed;

    Player()
    {
        _downloaded = OnDownloaded;
        _downloadEnded = (_, channel, _, _) => Post(() => DownloadEnded(channel));
        _bufferEnded = (_, channel, _, _) => BufferRanOut(channel, "ended");
        _stalled = (_, channel, data, _) =>
        {
            if (data == 0)
                BufferRanOut(channel, "stalled");
            else
                Post(() => Log.Write("buffer", $"source {channel} resumed"));
        };
        _icyTitle = OnIcyTitle;
        _oggTitle = OnOggTitle;
        _segmentStarted = OnSegmentStarted;
        _silenceMeter = MeasureSilence;

        var thread = new Thread(Run) { IsBackground = true, Name = "player" };
        thread.Start();

        Log.Write("start", SystemMedia.Describe());
        if (!Bass.Init(-1, 48000))
            Log.Write("start", $"BASS init failed: {Bass.LastError}");
        Bass.NetTimeOut = 10000;
        Bass.NetReadTimeOut = 10000;
        // For unattended desk runs: the volume applies after the mixer's DSP, so measurements hold.
        if (Environment.GetEnvironmentVariable("ENGINETEST_MUTE") == "1")
            Bass.GlobalStreamVolume = 0;
        var opus = LoadPlugin("bassopus");
        var hls = LoadPlugin("basshls");
        _hlsPlugin = hls.Handle;
        Log.Write("start", $"BASS {Bass.Version}, BASSmix {BassMix.Version}, BASS_FX {BassFx.Version}, BASSOPUS {opus.Version}, BASSHLS {hls.Version}");

        _mixer = BassMix.CreateMixerStream(48000, 2, BassFlags.Float | BassFlags.MixerNonStop);
        Bass.ChannelSetDSP(_mixer, _silenceMeter);
        _eq = Bass.ChannelSetFX(_mixer, EffectType.PeakEQ, 0);
        for (var band = 0; band < Bands.Length; band++)
            SetGain(band, 0);

        Connectivity.Current.ConnectivityChanged += (_, e) => NetworkChanged(e);
        _ticker = new Timer(_ => Post(Tick), null, 100, 100);
    }

    public void Play(string station, string url) => Post(() =>
    {
        _station = station;
        _url = url;
        _lastTitle = null;
        _wait = -1;
        Log.Write("play", $"{station}: {url} (buffer {NetBuffer} ms, pre-buffer {PreBuffer}%)");
        Start("user played");
    });

    public void PlayAgain(string by) => Post(() =>
    {
        Log.Write("play", $"again, by {by}");
        if (_url is null || _current is not null || State == "connecting")
            return;
        _wait = -1;
        Start(by);
    });

    public void Stop(string by) => Post(() =>
    {
        Log.Write("stop", $"by {by}");
        Native.BASS_StreamCancel(_request);
        DropConnections();
        Bass.ChannelPause(_mixer);
        _waitingFor = null;
        SetState("stopped");
    });

    public void SetGain(int band, double gain)
    {
        var parameters = new PeakEQParameters { lBand = band, fCenter = Bands[band], fBandwidth = 1, fGain = (float)gain, lChannel = FXChannelFlags.All };
        Bass.FXSetParameters(_eq, parameters);
    }

    public bool GetSpectrum(float[] fft) => Bass.ChannelGetData(_mixer, fft, (int)DataFlags.FFT2048) > 0;

    void Start(string reason)
    {
        DropConnections();
        Bass.ChannelPlay(_mixer);
        WaitForAudio(reason);
        SetState("connecting");
        Connect(reason, switchAtOnce: true);
    }

    void NetworkChanged(ConnectivityChangedEventArgs e)
    {
        var profiles = string.Join(",", e.ConnectionProfiles);
        Post(() =>
        {
            Log.Write("network", $"changed: access {e.NetworkAccess}, profiles [{profiles}]");
            if (_url is null || State == "stopped" || e.NetworkAccess != NetworkAccess.Internet)
                return;
            _wait = -1;
            WaitForAudio("network change");
            Connect("network change", switchAtOnce: true);
        });
    }

    // switchAtOnce: the new connection replaces the old one as soon as it's ready, even if
    // audio still arrives on the old one. Otherwise it waits for the old one's buffer to run out.
    void Connect(string reason, bool switchAtOnce)
    {
        _delay?.Cancel();
        var previous = _request;
        if (previous != 0)
            Native.BASS_StreamCancel(previous);
        var request = ++_request;
        var wait = _wait < 0 ? 0 : Waits[Math.Min(_wait, Waits.Length - 1)];
        _wait++;
        Log.Write("connect", $"#{request}: {reason}; wait {wait} s");
        _delay = new CancellationTokenSource();
        var url = _url!;
        var delay = Task.Delay(TimeSpan.FromSeconds(wait), _delay.Token);
        delay.ContinueWith(_ => Open(request, url, switchAtOnce), TaskContinuationOptions.OnlyOnRanToCompletion);
    }

    // Runs on a pool thread: creating a stream blocks until it connects or fails.
    void Open(int request, string url, bool switchAtOnce)
    {
        Bass.NetBufferLength = NetBuffer;
        Bass.NetPreBuffer = PreBuffer;
        var clock = Stopwatch.StartNew();
        var flags = BassFlags.Decode | BassFlags.Float | BassFlags.StreamDownloadBlocks | BassFlags.StreamStatus;
        var handle = Bass.CreateStream(url, 0, flags, _downloaded, request);
        var error = Bass.LastError;
        var elapsed = clock.ElapsedMilliseconds;
        Post(() => Opened(request, handle, error, elapsed, switchAtOnce));
    }

    void Opened(int request, int handle, Errors error, long elapsed, bool switchAtOnce)
    {
        var status = _status.TryRemove(request, out var lines) ? lines.ToArray() : [];
        _heads.TryRemove(request, out var head);
        var http = status.LastOrDefault(s => s.StartsWith("HTTP") || s.StartsWith("ICY"));
        var type = status.LastOrDefault(s => s.StartsWith("Content-Type", StringComparison.OrdinalIgnoreCase));
        if (request != _request || State == "stopped")
        {
            Log.Write("connect", $"#{request}: superseded after {elapsed} ms");
            if (handle != 0)
                Bass.StreamFree(handle);
            return;
        }
        if (handle == 0)
        {
            Failed(request, error, elapsed, http, type, switchAtOnce);
            return;
        }

        var info = Bass.ChannelGetInfo(handle);
        var isHls = info.Plugin == _hlsPlugin;
        var bitrate = Bass.ChannelGetAttribute(handle, ChannelAttribute.Bitrate);
        Format = $"{info.ChannelType} {info.Frequency} Hz {info.Channels} ch {bitrate:F0} kbps{Aac(head, info.Frequency)}";
        Log.Write("connect", $"#{request}: connected in {elapsed} ms: {Format}; {http}; {type}");
        foreach (var tag in TagLines(handle))
            Log.Write("tags", tag);

        var source = new Source(handle, request, isHls);
        Bass.ChannelSetSync(handle, SyncFlags.Downloaded | SyncFlags.Mixtime, 0, _downloadEnded);
        Bass.ChannelSetSync(handle, SyncFlags.MetadataReceived | SyncFlags.Mixtime, 0, _icyTitle);
        Bass.ChannelSetSync(handle, SyncFlags.OggChange | SyncFlags.Mixtime, 0, _oggTitle);
        Bass.ChannelSetSync(handle, SyncFlags.HlsSegement | SyncFlags.Mixtime, 0, _segmentStarted);
        BassMix.MixerAddChannel(_mixer, handle, BassFlags.MixerChanPause | BassFlags.MixerChanNoRampin);
        // Mixer syncs can only be set on a channel that's already in the mixer.
        BassMix.ChannelSetSync(handle, SyncFlags.End | SyncFlags.Mixtime, 0, _bufferEnded);
        BassMix.ChannelSetSync(handle, SyncFlags.Stalled | SyncFlags.Mixtime, 0, _stalled);
        var stillPlaying = _current is { Gated: false } && Native.BASS_Mixer_ChannelIsActive(_current.Handle) == PlaybackState.Playing;
        if (stillPlaying)
        {
            Free(_next);
            _next = source;
            _nextHandle = handle;
            source.SwitchAtOnce = switchAtOnce;
            var when = switchAtOnce ? "once its own buffer fills" : "when that buffer runs out";
            Log.Write("connect", $"#{request}: #{_current!.Request} still plays its buffered audio; #{request} takes over {when}");
        }
        else
        {
            DropConnections();
            _current = source;
            _currentHandle = handle;
        }
        ShowTitle(handle, "connect");
        SetState("playing");
    }

    void Failed(int request, Errors error, long elapsed, string? http, string? type, bool switchAtOnce)
    {
        var page = type?.Contains("text/", StringComparison.OrdinalIgnoreCase) == true;
        var kind = error == Errors.FileFormat && !page ? "format not supported" : "failed connection";
        Log.Write("connect", $"#{request}: failed after {elapsed} ms: {error} ({(int)error}); {http ?? "no HTTP status"}; {type ?? "no content type"} => {kind}");
        if (kind == "format not supported")
        {
            DropConnections();
            Bass.ChannelPause(_mixer);
            SetState("format not supported");
            return;
        }
        SetState("reconnecting");
        Connect("retry after a failed connection", switchAtOnce);
    }

    void DownloadEnded(int channel)
    {
        if (channel != _current?.Handle)
            return;
        var played = DateTime.UtcNow - _current.ConnectedAt;
        Log.Write("lost", $"#{_current.Request}: download ended after {played.TotalSeconds:F0} s");
        Lose(played);
    }

    void Lose(TimeSpan played)
    {
        _current!.Lost = true;
        if (played.TotalSeconds >= 60)
            _wait = -1;
        SetState("reconnecting");
        Connect("lost connection", switchAtOnce: false);
    }

    // Mix time: the switch happens in the same mix as the end of the old buffer. A stalled
    // HLS stream never ends, so running dry counts too.
    void BufferRanOut(int channel, string how)
    {
        var next = channel == _currentHandle ? _nextHandle : 0;
        if (next != 0)
            BassMix.ChannelFlags(next, BassFlags.Default, BassFlags.MixerChanPause);
        Post(() => BufferRanOut(channel, how, next));
    }

    void BufferRanOut(int channel, string how, int next)
    {
        if (channel != _current?.Handle)
        {
            Log.Write("buffer", $"source {channel} {how}");
            return;
        }
        var old = _current;
        if (next != 0 && next == _next?.Handle)
        {
            Log.Write("switch", $"#{old.Request}'s buffer {how}; #{_next.Request} took over in the same mix, its buffer {Level(_next):F0}%");
            _next.Gated = false;
            _current = _next;
            _currentHandle = next;
            _next = null;
            _nextHandle = 0;
            Free(old);
        }
        else if (how == "ended")
        {
            Log.Write("switch", $"#{old.Request}'s buffer ended; silence until a connection is ready");
            _current = null;
            _currentHandle = 0;
            Free(old);
        }
        else
        {
            BassMix.ChannelFlags(old.Handle, BassFlags.MixerChanPause, BassFlags.MixerChanPause);
            old.Gated = true;
            old.GatedSince = Stopwatch.GetTimestamp();
            Log.Write("buffer", $"#{old.Request}'s buffer is empty: silence until it refills to {PreBuffer}%");
        }
    }

    void CheckGates()
    {
        if (_next is { SwitchAtOnce: true } next && Ready(next))
        {
            var old = _current!;
            Log.Write("switch", $"#{next.Request} took over from #{old.Request} at once");
            _current = next;
            _currentHandle = next.Handle;
            _next = null;
            _nextHandle = 0;
            Free(old);
            Release(next);
        }
        if (_current is { Gated: true } current && Ready(current))
            Release(current);
    }

    bool Ready(Source source) => source.Lost || Level(source) >= PreBuffer;

    static void Release(Source source)
    {
        source.Gated = false;
        BassMix.ChannelFlags(source.Handle, BassFlags.Default, BassFlags.MixerChanPause);
        var waited = Stopwatch.GetElapsedTime(source.GatedSince);
        Log.Write("buffer", $"#{source.Request} plays: buffer {Level(source):F0}% after {waited.TotalMilliseconds:F0} ms");
    }

    // BASS's own pre-buffering only holds back BASS_ChannelPlay; a decoding source in a mixer
    // plays whatever has arrived. So the gate measures the download buffer the same way.
    static double Level(Source source)
    {
        var buffered = Bass.StreamGetFilePosition(source.Handle, FileStreamPosition.Buffer);
        var size = Bass.StreamGetFilePosition(source.Handle, FileStreamPosition.End);
        return buffered < 0 || size <= 0 ? 100 : 100.0 * buffered / size;
    }

    void OnDownloaded(IntPtr buffer, int length, IntPtr user)
    {
        var request = (int)user;
        if (buffer == IntPtr.Zero)
            return;
        if (length == 0)
        {
            var lines = _status.GetOrAdd(request, _ => new ConcurrentQueue<string>());
            foreach (var line in Extensions.ExtractMultiStringAnsi(buffer))
                lines.Enqueue(line);
            return;
        }
        unsafe
        {
            var data = new ReadOnlySpan<byte>((void*)buffer, length);
            _heads.TryAdd(request, data[..Math.Min(length, 64)].ToArray());
            if (request == _request)
                _recorder.Write(data);
        }
    }

    void OnIcyTitle(int sync, int channel, int data, IntPtr user)
    {
        var meta = ReadString(Bass.ChannelGetTags(channel, TagType.META));
        var title = meta is null ? null : ParseIcy(meta);
        _recorder.Split(title);
        Post(() => TitleArrived(channel, title, $"ICY {meta}"));
    }

    void OnOggTitle(int sync, int channel, int data, IntPtr user)
    {
        var title = OggTitle(channel);
        Post(() => TitleArrived(channel, title, "Ogg comments"));
    }

    void OnSegmentStarted(int sync, int channel, int data, IntPtr user)
    {
        var extinf = Extensions.PtrToStringUtf8(Bass.ChannelGetTags(channel, (TagType)0x14000));
        Post(() =>
        {
            var source = channel == _current?.Handle ? _current : channel == _next?.Handle ? _next : null;
            if (source is null)
                return;
            var gap = DateTime.UtcNow - source.LastSegmentAt;
            source.LastSegmentAt = DateTime.UtcNow;
            var duration = extinf?.Split(',')[0];
            if (double.TryParse(duration, System.Globalization.CultureInfo.InvariantCulture, out var seconds))
                source.SegmentSeconds = seconds;
            if (gap.TotalSeconds > source.SegmentSeconds + 2)
                Log.Write("hls", $"segment after {gap.TotalSeconds:F1} s: EXTINF {extinf}");
        });
    }

    void ShowTitle(int handle, string when)
    {
        var meta = ReadString(Bass.ChannelGetTags(handle, TagType.META));
        if (meta is not null)
            TitleArrived(handle, ParseIcy(meta), $"ICY at {when}");
        else
            TitleArrived(handle, OggTitle(handle), $"Ogg comments at {when}");
    }

    void TitleArrived(int channel, string? title, string raw)
    {
        if (channel != _current?.Handle && channel != _next?.Handle)
            return;
        var trimmed = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        var change = trimmed is not null && trimmed != _lastTitle;
        Log.Write("title", $"\"{trimmed}\" {(change ? "CHANGE" : "no change")} ({raw})");
        if (trimmed is not null)
            _lastTitle = trimmed;
        Title = trimmed;
        SystemMedia.Show(true, _station, trimmed);
        Changed?.Invoke();
    }

    void Tick()
    {
        _ticks++;
        CheckGates();
        if (_ticks % 10 != 0)
            return;
        var source = _current;
        if (source is { IsHls: true, Lost: false })
        {
            var silent = DateTime.UtcNow - source.LastSegmentAt;
            if (silent.TotalSeconds > 10 + source.SegmentSeconds)
            {
                Log.Write("lost", $"#{source.Request}: no new HLS segment for {silent.TotalSeconds:F0} s (limit 10 + {source.SegmentSeconds} s)");
                Lose(DateTime.UtcNow - source.ConnectedAt);
            }
        }
        var age = source is null ? 99 : (DateTime.UtcNow - source.ConnectedAt).TotalSeconds;
        if (source is { IsHls: false } && age < 4)
            Log.Write("burst", $"#{source.Request} at {age:F1} s: {Buffered(source)}");
        if (_ticks % 100 == 0 && State != "stopped")
            Heartbeat(source);
        if (_ticks % 6000 == 0 && State == "stopped")
            Log.Write("beat", "stopped");
    }

    void Heartbeat(Source? source)
    {
        var output = Interlocked.Read(ref _frames) / 48000.0;
        var silent = Interlocked.Read(ref _silentFrames) / 48000.0;
        var buffered = source is null ? "no source" : $"#{source.Request} {Buffered(source)}";
        var battery = $"{Battery.Default.ChargeLevel:P0} {Battery.Default.State}";
        Log.Write("beat", $"{State}; output {output:F0} s, silent {silent:F1} s; {buffered}; fft {SpectrumRate}; battery {battery}; network {Connectivity.Current.NetworkAccess}");
    }

    string Buffered(Source source)
    {
        if (source.IsHls)
        {
            var segment = Bass.StreamGetFilePosition(source.Handle, FileStreamPosition.HlsSegment);
            return $"HLS segment {segment}, last {(DateTime.UtcNow - source.LastSegmentAt).TotalSeconds:F0} s ago";
        }
        var downloaded = Bass.StreamGetFilePosition(source.Handle, FileStreamPosition.Download);
        var decoded = Bass.StreamGetFilePosition(source.Handle, FileStreamPosition.Current);
        var kbps = Bass.ChannelGetAttribute(source.Handle, ChannelAttribute.Bitrate);
        var seconds = kbps > 0 ? $"{(downloaded - decoded) * 8 / (kbps * 1000):F1} s" : "? s";
        return $"buffer {Level(source):F0}%, {seconds} ({downloaded - decoded} bytes), {downloaded} downloaded{(source.Gated ? ", gated" : "")}";
    }

    // Mixer thread. Exact zeros come from the mixer having no source with data: silence the
    // listener hears, not quiet music.
    unsafe void MeasureSilence(int handle, int channel, IntPtr buffer, int length, IntPtr user)
    {
        var samples = (float*)buffer;
        var frames = length / 8;
        for (var i = 0; i < frames; i++)
        {
            var silent = samples[2 * i] == 0 && samples[2 * i + 1] == 0;
            if (silent)
            {
                _silentRun++;
                continue;
            }
            if (_silentRun > 4800)
            {
                var run = _silentRun;
                Post(() => Log.Write("silence", $"{run * 1000 / 48000} ms"));
            }
            Interlocked.Add(ref _silentFrames, _silentRun);
            _silentRun = 0;
            var reason = Interlocked.Exchange(ref _waitingFor, null);
            if (reason is not null)
            {
                var waited = Stopwatch.GetElapsedTime(_waitingSince);
                Post(() => Log.Write("audio", $"audible {waited.TotalMilliseconds:F0} ms after {reason}"));
            }
        }
        Interlocked.Add(ref _frames, frames);
    }

    void WaitForAudio(string reason)
    {
        _waitingSince = Stopwatch.GetTimestamp();
        Volatile.Write(ref _waitingFor, reason);
    }

    void DropConnections()
    {
        _delay?.Cancel();
        Free(_current);
        Free(_next);
        _current = null;
        _next = null;
        _currentHandle = 0;
        _nextHandle = 0;
    }

    static void Free(Source? source)
    {
        if (source is null)
            return;
        BassMix.MixerRemoveChannel(source.Handle);
        Bass.StreamFree(source.Handle);
    }

    void SetState(string state)
    {
        State = state;
        if (state == "stopped" || state == "format not supported")
            Title = null;
        SystemMedia.Show(state is "playing" or "connecting" or "reconnecting", _station, Title);
        Changed?.Invoke();
    }

    void Post(Action action) => _work.Add(action);

    // An exception would end this thread silently, hours into a run; the log has to show it.
    void Run()
    {
        foreach (var action in _work.GetConsumingEnumerable())
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Log.Write("error", e.ToString());
            }
        }
    }

    static (int Handle, string Version) LoadPlugin(string name)
    {
        var handle = Bass.PluginLoad(name);
        if (handle == 0)
            return (0, $"not loaded: {Bass.LastError}");
        var info = Bass.PluginGetInfo(handle);
        return (handle, info.Version.ToString());
    }

    static string? OggTitle(int handle)
    {
        var tags = Bass.ChannelGetTags(handle, TagType.OGG);
        if (tags == IntPtr.Zero)
            return null;
        var comments = Extensions.ExtractMultiStringUtf8(tags);
        string? Value(string key) => comments.FirstOrDefault(c => c.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))?[(key.Length + 1)..];
        var artist = Value("ARTIST");
        var title = Value("TITLE");
        return artist is null ? title : $"{artist} - {title}";
    }

    static string? ParseIcy(string meta)
    {
        var start = meta.IndexOf("StreamTitle='", StringComparison.Ordinal);
        var end = start < 0 ? -1 : meta.IndexOf("';", start + 13, StringComparison.Ordinal);
        return end < 0 ? null : meta[(start + 13)..end];
    }

    // ICY declares no encoding: UTF-8 when the bytes are valid UTF-8, else Latin-1 for the log.
    static string? ReadString(IntPtr text)
    {
        if (text == IntPtr.Zero)
            return null;
        var length = 0;
        while (Marshal.ReadByte(text, length) != 0)
            length++;
        var bytes = new byte[length];
        Marshal.Copy(text, bytes, 0, length);
        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(bytes);
        }
    }

    static IEnumerable<string> TagLines(int handle)
    {
        var extinf = Bass.ChannelGetTags(handle, (TagType)0x14000);
        if (extinf != IntPtr.Zero)
            yield return $"HLS EXTINF {Extensions.PtrToStringUtf8(extinf)}";
        var streaminf = Bass.ChannelGetTags(handle, (TagType)0x14001);
        if (streaminf != IntPtr.Zero)
            yield return $"HLS STREAM-INF {Extensions.PtrToStringUtf8(streaminf)}";
    }

    // An ADTS header with half the decoded sample rate means HE-AAC's implicit SBR signalling.
    static string Aac(byte[]? head, int decodedRate)
    {
        if (head is not [0xFF, var b1, var b2, ..] || (b1 & 0xF6) != 0xF0)
            return "";
        int[] rates = [96000, 88200, 64000, 48000, 44100, 32000, 24000, 22050, 16000, 12000, 11025, 8000, 7350];
        var index = (b2 >> 2) & 0xF;
        var headerRate = index < rates.Length ? rates[index] : 0;
        var profile = headerRate * 2 == decodedRate ? "HE-AAC" : "AAC-LC";
        return $", ADTS {headerRate} Hz => {profile}";
    }

    static class Native
    {
        [DllImport("bass")]
        public static extern bool BASS_StreamCancel(IntPtr user);

        [DllImport("bassmix")]
        public static extern PlaybackState BASS_Mixer_ChannelIsActive(int handle);
    }
}
