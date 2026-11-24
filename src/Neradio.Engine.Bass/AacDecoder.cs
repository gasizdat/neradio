#if WINDOWS
using System.Runtime.InteropServices;
using NAudio.MediaFoundation;

namespace Neradio.Engine.Bass;

// Windows' own AAC decoder, a Media Foundation transform, fed one ADTS frame at a time. Its
// output format is known only once it has decoded the first frames.
internal sealed class AacDecoder
{
    private static readonly Guid MicrosoftAacDecoder = new("32d186a7-218f-4c75-8876-dd77273a8999");
    private static readonly int[] Rates = [96000, 88200, 64000, 48000, 44100, 32000, 24000, 22050, 16000, 12000, 11025, 8000, 7350];

    private readonly IMFTransform _transform;
    private readonly int _outputSize;

    public AacDecoder(ReadOnlySpan<byte> header)
    {
        MediaFoundationApi.Startup();
        var rate = Rates[(header[2] >> 2) & 0x0F];
        var channels = ((header[2] & 1) << 2) | (header[3] >> 6);
        var type = Type.GetTypeFromCLSID(MicrosoftAacDecoder)!;
        _transform = (IMFTransform)Activator.CreateInstance(type)!;

        var input = MediaFoundationApi.CreateMediaType();
        input.SetGUID(MediaFoundationAttributes.MF_MT_MAJOR_TYPE, MediaTypes.MFMediaType_Audio);
        input.SetGUID(MediaFoundationAttributes.MF_MT_SUBTYPE, AudioSubtypes.MFAudioFormat_AAC);
        input.SetUINT32(MediaFoundationAttributes.MF_MT_AUDIO_SAMPLES_PER_SECOND, rate);
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

    public int Rate { get; private set; }
    public int Channels { get; private set; }
    public bool IsFloat { get; private set; }

    public byte[][] Decode(byte[] frame)
    {
        var buffer = MediaFoundationApi.CreateMemoryBuffer(frame.Length);
        buffer.Lock(out var pointer, out _, out _);
        Marshal.Copy(frame, 0, pointer, frame.Length);
        buffer.Unlock();
        buffer.SetCurrentLength(frame.Length);
        var sample = MediaFoundationApi.CreateSample();
        sample.AddBuffer(buffer);
        _transform.ProcessInput(0, sample, 0);
        Marshal.ReleaseComObject(sample);
        Marshal.ReleaseComObject(buffer);

        var pcm = new List<byte[]>();
        while (Output(pcm))
        {
        }
        return pcm.ToArray();
    }

    // False once the decoder needs more input.
    private bool Output(List<byte[]> pcm)
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
            pcm.Add(bytes);
        }
        else if (!changed && result != MediaFoundationErrors.MF_E_TRANSFORM_NEED_MORE_INPUT)
        {
            Marshal.ThrowExceptionForHR(result);
        }
        Marshal.ReleaseComObject(sample);
        Marshal.ReleaseComObject(buffer);
        return produced || changed;
    }

    private void SetOutput()
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
    private IMFMediaType PickOutput()
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
            var offered = new MediaType(candidate);
            if (offered.SubType == AudioSubtypes.MFAudioFormat_Float)
                return candidate;
        }
    }
}
#endif
