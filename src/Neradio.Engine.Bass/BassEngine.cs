using ManagedBass;
using ManagedBass.Mix;
using Neradio.Core.Playback.Abstractions;

namespace Neradio.Engine.Bass;

// Every stream plays through one mixer. A stream joins it paused, and takes over once about 4
// seconds of its audio are buffered and the one before it has run out, in the same mix.
internal sealed class BassEngine : IAudioEngine
{
    // BASS_CONFIG_AM_DISABLE, which ManagedBass doesn't name. 2 leaves Android's codecs only AAC.
    private const Configuration AndroidCodecs = (Configuration)58;
    // Q30's 60 seconds. BASS sizes a push stream's buffer by it.
    private const int BufferMilliseconds = 60_000;

    private readonly int _mixer;
    private MixerSource? _current;
    private MixerSource? _next;

    public BassEngine()
    {
        if (OperatingSystem.IsAndroid())
            ManagedBass.Bass.Configure(AndroidCodecs, 2);
        ManagedBass.Bass.NetBufferLength = BufferMilliseconds;
        ManagedBass.Bass.Init(-1, 48000);
        ManagedBass.Bass.PluginLoad("bassopus");
        _mixer = BassMix.CreateMixerStream(48000, 2, BassFlags.Float | BassFlags.MixerNonStop);
        ManagedBass.Bass.ChannelPlay(_mixer);
    }

    public IAudioStream Open(string contentType)
    {
#if WINDOWS
        MixerSource source = IsAac(contentType) ? new AacSource(this) : new FileSource(this);
#else
        MixerSource source = new FileSource(this);
#endif
        Volatile.Write(ref _next, source);
        return source;
    }

    public void Add(int channel) =>
        BassMix.MixerAddChannel(_mixer, channel, BassFlags.MixerChanPause | BassFlags.MixerChanNoRampin);

    // Also runs on the mixer thread, in a stall sync, so it takes no lock: the switch happens
    // in the mix where the old stream runs out.
    public void TakeOver()
    {
        var next = Volatile.Read(ref _next);
        var current = Volatile.Read(ref _current);
        if (next is not { IsReady: true } || current is { HasRunOut: false })
            return;
        if (Interlocked.CompareExchange(ref _next, null, next) != next)
            return;
        Volatile.Write(ref _current, next);
        next.Play();
    }

    public void Remove(MixerSource source)
    {
        Interlocked.CompareExchange(ref _next, null, source);
        var removed = Interlocked.CompareExchange(ref _current, null, source);
        if (removed == source)
            TakeOver();
    }
#if WINDOWS

    // BASS would hand AAC to Media Foundation and wait for 1 MiB of it.
    private static bool IsAac(string contentType) =>
        contentType.Contains("aac", StringComparison.OrdinalIgnoreCase);
#endif
}
