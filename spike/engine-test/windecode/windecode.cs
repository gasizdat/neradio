#:package ManagedBass@4.0.2
#:package ManagedBass.Fx@4.0.2
#:package ManagedBass.Mix@4.0.2
#:package NAudio.Wasapi@2.2.1
#:property TargetFramework=net10.0-windows
#:property PublishAot=false
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using ManagedBass;
using ManagedBass.Fx;
using ManagedBass.Mix;
using NAudio.MediaFoundation;

// Tries the Windows decode path for AAC (#8). BASS opens AAC through Media Foundation only once
// 1 MiB has arrived, and HLS AAC not at all. Here the app downloads the stream itself, takes the
// ICY titles out, cuts the rest into ADTS frames and decodes them with Windows' own AAC decoder.
// HLS segments are MPEG-TS, unpacked to ADTS first. The audio goes into a BASS push stream in a
// mixer with the equalizer, as in the test app. Muted: the mixer still runs and is measured.
//
// dotnet run windecode.cs -- <folder with the x64 BASS DLLs> <URL> [seconds]
if (args.Length < 2)
{
    Console.WriteLine("usage: dotnet run windecode.cs -- <folder with the x64 BASS DLLs> <URL> [seconds]");
    return;
}
var bassFolder = args[0];
var url = new Uri(args[1]);
var seconds = args.Length > 2 ? int.Parse(args[2]) : 20;
foreach (var assembly in new[] { typeof(Bass).Assembly, typeof(BassMix).Assembly, typeof(BassFx).Assembly })
    NativeLibrary.SetDllImportResolver(assembly, (name, _, _) => NativeLibrary.Load(Path.Combine(bassFolder, $"{name}.dll")));

// The test app's start level: 75% of a 5000 ms buffer.
const double StartLevel = 3.75;
var clock = Stopwatch.StartNew();
void Report(string line) => Console.WriteLine($"{clock.Elapsed.TotalSeconds,6:F2} s  {line}");

if (!Bass.Init(-1, 48000))
{
    Report($"BASS init failed: {Bass.LastError}");
    return;
}
Bass.GlobalStreamVolume = 0;
MediaFoundationApi.Startup();
var mixer = BassMix.CreateMixerStream(48000, 2, BassFlags.Float | BassFlags.MixerNonStop);
var eq = Bass.ChannelSetFX(mixer, EffectType.PeakEQ, 0);
var cut = new PeakEQParameters { fCenter = 125, fBandwidth = 1, fGain = -12, lChannel = FXChannelFlags.All };
Bass.FXSetParameters(eq, cut);
var startedAt = -1.0;
var stalls = 0;
SyncProcedure stalled = (_, _, data, _) =>
{
    if (data == 0 && startedAt >= 0)
        Interlocked.Increment(ref stalls);
};
Bass.ChannelPlay(mixer);

AacDecoder? decoder = null;
var push = 0;
var bytesPerSecond = 0;
long decodedBytes = 0;
string? title = null;
var titles = 0;

void OnFrame(byte[] data, int offset, int length)
{
    decoder ??= new AacDecoder(data.AsSpan(offset, 7));
    decoder.Decode(data, offset, length, (pcm, count) =>
    {
        if (decodedBytes == 0)
        {
            // Only now: the decoder settles its output format with the first frames it decodes.
            var format = decoder.IsFloat ? BassFlags.Float : BassFlags.Default;
            push = Bass.CreateStream(decoder.Rate, decoder.Channels, BassFlags.Decode | format, StreamProcedureType.Push);
            BassMix.MixerAddChannel(mixer, push, BassFlags.MixerChanNoRampin);
            BassMix.ChannelSetSync(push, SyncFlags.Stalled | SyncFlags.Mixtime, 0, stalled);
            bytesPerSecond = decoder.Rate * decoder.Channels * (decoder.IsFloat ? 4 : 2);
            Report($"first PCM pushed into BASS: {decoder.Describe()}");
        }
        Bass.StreamPutData(push, pcm, count);
        decodedBytes += count;
        if (startedAt < 0 && decodedBytes >= StartLevel * bytesPerSecond)
        {
            startedAt = clock.Elapsed.TotalSeconds;
            Report($"{StartLevel} s of audio buffered: the test app would start playing here");
        }
    });
}

void OnTitle(string text)
{
    titles++;
    if (text == title)
        return;
    title = text;
    Report($"title: {text}");
}

var framer = new AdtsFramer(OnFrame);
using var http = new HttpClient();
using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
var status = Task.Run(async () =>
{
    var fft = new float[1024];
    while (!stop.IsCancellationRequested)
    {
        await Task.Delay(2000);
        var queued = push == 0 ? 0 : Bass.StreamPutData(push, IntPtr.Zero, 0);
        var played = Bass.ChannelBytes2Seconds(mixer, Bass.ChannelGetPosition(mixer));
        var spectrum = Bass.ChannelGetData(mixer, fft, (int)DataFlags.FFT2048) > 0 && fft.Max() > 0;
        var buffered = bytesPerSecond == 0 ? 0 : (double)queued / bytesPerSecond;
        Report($"decoded {(bytesPerSecond == 0 ? 0 : (double)decodedBytes / bytesPerSecond):F1} s, buffered {buffered:F1} s, mixer out {played:F1} s, spectrum {(spectrum ? "live" : "silent")}");
    }
});
try
{
    if (url.AbsolutePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase))
        await PlayHls(http, url, new TsDemuxer(framer.Push, OnTitle), Report, stop.Token);
    else
        await PlayIcy(http, url, framer.Push, OnTitle, Report, stop.Token);
}
catch (OperationCanceledException)
{
}
await status;
Report($"done: start level after {startedAt:F2} s; stalls after the start: {stalls}; titles seen: {titles}");
Bass.Free();

static async Task PlayIcy(HttpClient http, Uri url, Action<byte[], int, int> audio, Action<string> title, Action<string> report, CancellationToken cancel)
{
    using var request = new HttpRequestMessage(HttpMethod.Get, url);
    request.Headers.Add("Icy-MetaData", "1");
    using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel);
    var metaInt = response.Headers.TryGetValues("icy-metaint", out var values) ? int.Parse(values.First()) : 0;
    report($"HTTP {(int)response.StatusCode}, {response.Content.Headers.ContentType}, icy-metaint {metaInt}");
    await using var body = await response.Content.ReadAsStreamAsync(cancel);
    var buffer = new byte[16 * 1024];
    var untilMeta = metaInt;
    while (true)
    {
        var want = metaInt == 0 ? buffer.Length : Math.Min(buffer.Length, untilMeta);
        var read = await body.ReadAsync(buffer.AsMemory(0, want), cancel);
        if (read == 0)
            return;
        audio(buffer, 0, read);
        untilMeta -= read;
        if (metaInt == 0 || untilMeta > 0)
            continue;
        var length = new byte[1];
        await body.ReadExactlyAsync(length, cancel);
        var meta = new byte[length[0] * 16];
        await body.ReadExactlyAsync(meta, cancel);
        var match = Regex.Match(Encoding.UTF8.GetString(meta), "StreamTitle='(.*?)';");
        if (match.Success)
            title(match.Groups[1].Value);
        untilMeta = metaInt;
    }
}

// Starts two segments behind live, then fetches each new segment once.
static async Task PlayHls(HttpClient http, Uri url, TsDemuxer demuxer, Action<string> report, CancellationToken cancel)
{
    var playlist = await http.GetStringAsync(url, cancel);
    if (playlist.Contains("#EXT-X-STREAM-INF"))
    {
        var variant = Lines(playlist).First(l => !l.StartsWith('#'));
        url = new Uri(url, variant);
        report($"variant: {url}");
    }
    long next = -1;
    while (true)
    {
        var lines = Lines(await http.GetStringAsync(url, cancel)).ToArray();
        var sequence = long.Parse(lines.First(l => l.StartsWith("#EXT-X-MEDIA-SEQUENCE:"))[22..]);
        var target = int.Parse(lines.First(l => l.StartsWith("#EXT-X-TARGETDURATION:"))[22..]);
        var segments = lines.Where(l => !l.StartsWith('#')).ToArray();
        if (next < 0)
            next = sequence + Math.Max(0, segments.Length - 2);
        for (var i = (int)Math.Max(0, next - sequence); i < segments.Length; i++)
        {
            var segment = new Uri(url, segments[i]);
            var data = await http.GetByteArrayAsync(segment, cancel);
            demuxer.Push(data);
            next = sequence + i + 1;
        }
        await Task.Delay(target * 500, cancel);
    }
}

static IEnumerable<string> Lines(string text) => text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0);

// Cuts a byte stream into whole ADTS frames, resyncing on the 12-bit sync word.
sealed class AdtsFramer(Action<byte[], int, int> frame)
{
    byte[] _data = new byte[64 * 1024];
    int _count;

    public void Push(byte[] data, int offset, int length)
    {
        if (_count + length > _data.Length)
            Array.Resize(ref _data, Math.Max(_data.Length * 2, _count + length));
        Buffer.BlockCopy(data, offset, _data, _count, length);
        _count += length;
        var position = 0;
        while (position + 7 <= _count)
        {
            if (_data[position] != 0xFF || (_data[position + 1] & 0xF6) != 0xF0)
            {
                position++;
                continue;
            }
            var size = ((_data[position + 3] & 0x03) << 11) | (_data[position + 4] << 3) | (_data[position + 5] >> 5);
            if (size < 7)
            {
                position++;
                continue;
            }
            if (position + size > _count)
                break;
            frame(_data, position, size);
            position += size;
        }
        Buffer.BlockCopy(_data, position, _data, 0, _count - position);
        _count -= position;
    }
}

// Takes the AAC elementary stream (ADTS) and the ID3 titles out of MPEG-TS segments.
sealed class TsDemuxer(Action<byte[], int, int> audio, Action<string> title)
{
    int _pmtPid = -1;
    int _audioPid = -1;
    int _id3Pid = -1;
    readonly List<byte> _id3 = [];

    public void Push(byte[] data)
    {
        for (var packet = 0; packet + 188 <= data.Length; packet += 188)
            Packet(data, packet);
    }

    void Packet(byte[] d, int p)
    {
        if (d[p] != 0x47)
            return;
        var start = (d[p + 1] & 0x40) != 0;
        var pid = ((d[p + 1] & 0x1F) << 8) | d[p + 2];
        var adaptation = (d[p + 3] >> 4) & 3;
        if (adaptation == 2)
            return;
        var offset = p + 4 + (adaptation == 3 ? 1 + d[p + 4] : 0);
        var end = p + 188;
        if (pid == 0 && start)
        {
            offset += 1 + d[offset];
            var sectionEnd = offset + 3 + (((d[offset + 1] & 0x0F) << 8) | d[offset + 2]) - 4;
            for (var i = offset + 8; i < sectionEnd; i += 4)
                if (((d[i] << 8) | d[i + 1]) != 0)
                    _pmtPid = ((d[i + 2] & 0x1F) << 8) | d[i + 3];
        }
        else if (pid == _pmtPid && start)
        {
            offset += 1 + d[offset];
            var sectionEnd = offset + 3 + (((d[offset + 1] & 0x0F) << 8) | d[offset + 2]) - 4;
            var i = offset + 12 + (((d[offset + 10] & 0x0F) << 8) | d[offset + 11]);
            while (i < sectionEnd)
            {
                var esPid = ((d[i + 1] & 0x1F) << 8) | d[i + 2];
                if (d[i] == 0x0F)
                    _audioPid = esPid;
                else if (d[i] == 0x15)
                    _id3Pid = esPid;
                i += 5 + (((d[i + 3] & 0x0F) << 8) | d[i + 4]);
            }
        }
        else if (pid == _audioPid)
        {
            if (start)
                offset += 9 + d[offset + 8];
            audio(d, offset, end - offset);
        }
        else if (pid == _id3Pid)
        {
            if (start)
            {
                Id3Titles();
                _id3.Clear();
                offset += 9 + d[offset + 8];
            }
            _id3.AddRange(d.AsSpan(offset, end - offset));
        }
    }

    // ID3v2.4 text frames: a 10-byte header with a syncsafe size, then an encoding byte.
    void Id3Titles()
    {
        var tag = _id3.ToArray();
        var artist = Frame(tag, "TPE1");
        var song = Frame(tag, "TIT2");
        if (song is not null)
            title(artist is null ? song : $"{artist} - {song}");
    }

    static string? Frame(byte[] tag, string id)
    {
        var at = tag.AsSpan().IndexOf(Encoding.ASCII.GetBytes(id));
        if (at < 0 || at + 11 > tag.Length)
            return null;
        var size = (tag[at + 4] << 21) | (tag[at + 5] << 14) | (tag[at + 6] << 7) | tag[at + 7];
        var length = Math.Min(size - 1, tag.Length - at - 11);
        var encoding = tag[at + 10] switch { 1 => Encoding.Unicode, 2 => Encoding.BigEndianUnicode, 3 => Encoding.UTF8, _ => Encoding.Latin1 };
        return encoding.GetString(tag, at + 11, length).TrimEnd('\0');
    }
}

// Windows' own AAC decoder, a Media Foundation transform, fed one ADTS frame per sample.
sealed class AacDecoder
{
    static readonly Guid MsAacDecoder = new("32d186a7-218f-4c75-8876-dd77273a8999");
    static readonly int[] Rates = [96000, 88200, 64000, 48000, 44100, 32000, 24000, 22050, 16000, 12000, 11025, 8000, 7350];

    readonly IMFTransform _transform;
    readonly int _headerRate;
    readonly int _outputSize;

    public int Rate { get; private set; }
    public int Channels { get; private set; }
    public bool IsFloat { get; private set; }

    public AacDecoder(ReadOnlySpan<byte> header)
    {
        _headerRate = Rates[(header[2] >> 2) & 0x0F];
        var channels = ((header[2] & 1) << 2) | (header[3] >> 6);
        var type = Type.GetTypeFromCLSID(MsAacDecoder)!;
        _transform = (IMFTransform)Activator.CreateInstance(type)!;

        var input = MediaFoundationApi.CreateMediaType();
        input.SetGUID(MediaFoundationAttributes.MF_MT_MAJOR_TYPE, MediaTypes.MFMediaType_Audio);
        input.SetGUID(MediaFoundationAttributes.MF_MT_SUBTYPE, AudioSubtypes.MFAudioFormat_AAC);
        input.SetUINT32(MediaFoundationAttributes.MF_MT_AUDIO_SAMPLES_PER_SECOND, _headerRate);
        input.SetUINT32(MediaFoundationAttributes.MF_MT_AUDIO_NUM_CHANNELS, channels);
        input.SetUINT32(MediaFoundationAttributes.MF_MT_AAC_PAYLOAD_TYPE, 1);
        // The part of HEAACWAVEINFO after WAVEFORMATEX: wPayloadType 1 (ADTS), the rest zero.
        var userData = new byte[12];
        userData[0] = 1;
        input.SetBlob(MediaFoundationAttributes.MF_MT_USER_DATA, userData, userData.Length);
        _transform.SetInputType(0, input, _MFT_SET_TYPE_FLAGS.None);
        SetOutput();
        _transform.GetOutputStreamInfo(0, out var info);
        _outputSize = Math.Max(info.cbSize, 64 * 1024);
        _transform.ProcessMessage(MFT_MESSAGE_TYPE.MFT_MESSAGE_NOTIFY_BEGIN_STREAMING, IntPtr.Zero);
    }

    public string Describe() => $"ADTS {_headerRate} Hz; decoder output {Rate} Hz, {Channels} ch, {(IsFloat ? "float" : "16-bit")}{(Rate == _headerRate * 2 ? " (HE-AAC)" : "")}";

    public void Decode(byte[] data, int offset, int length, Action<byte[], int> pcm)
    {
        var buffer = MediaFoundationApi.CreateMemoryBuffer(length);
        buffer.Lock(out var pointer, out _, out _);
        Marshal.Copy(data, offset, pointer, length);
        buffer.Unlock();
        buffer.SetCurrentLength(length);
        var sample = MediaFoundationApi.CreateSample();
        sample.AddBuffer(buffer);
        _transform.ProcessInput(0, sample, 0);
        Marshal.ReleaseComObject(sample);
        Marshal.ReleaseComObject(buffer);
        while (Output(pcm))
        {
        }
    }

    bool Output(Action<byte[], int> pcm)
    {
        var buffer = MediaFoundationApi.CreateMemoryBuffer(_outputSize);
        var sample = MediaFoundationApi.CreateSample();
        sample.AddBuffer(buffer);
        var buffers = new[] { new MFT_OUTPUT_DATA_BUFFER { pSample = sample } };
        var result = _transform.ProcessOutput(_MFT_PROCESS_OUTPUT_FLAGS.None, 1, buffers, out _);
        // HE-AAC's implicit SBR shows only in the first frames: the decoder then doubles its rate.
        var changed = result == MediaFoundationErrors.MF_E_TRANSFORM_STREAM_CHANGE;
        if (changed)
            SetOutput();
        var produced = result == 0;
        if (produced)
        {
            buffer.GetCurrentLength(out var length);
            buffer.Lock(out var pointer, out _, out _);
            var bytes = new byte[length];
            Marshal.Copy(pointer, bytes, 0, length);
            buffer.Unlock();
            pcm(bytes, length);
        }
        else if (!changed && result != MediaFoundationErrors.MF_E_TRANSFORM_NEED_MORE_INPUT)
        {
            Marshal.ThrowExceptionForHR(result);
        }
        Marshal.ReleaseComObject(sample);
        Marshal.ReleaseComObject(buffer);
        return produced || changed;
    }

    void SetOutput()
    {
        var output = PickOutput();
        _transform.SetOutputType(0, output, _MFT_SET_TYPE_FLAGS.None);
        _transform.GetOutputCurrentType(0, out var current);
        var chosen = new MediaType(current);
        Rate = chosen.SampleRate;
        Channels = chosen.ChannelCount;
        IsFloat = chosen.SubType == AudioSubtypes.MFAudioFormat_Float;
    }

    // Float if the decoder offers it, else its first type, 16-bit PCM.
    IMFMediaType PickOutput()
    {
        IMFMediaType? first = null;
        for (var index = 0; ; index++)
        {
            IMFMediaType candidate;
            try
            {
                _transform.GetOutputAvailableType(0, index, out candidate);
            }
            catch (COMException e) when (e.HResult == MediaFoundationErrors.MF_E_NO_MORE_TYPES)
            {
                return first!;
            }
            first ??= candidate;
            if (new MediaType(candidate).SubType == AudioSubtypes.MFAudioFormat_Float)
                return candidate;
        }
    }
}
