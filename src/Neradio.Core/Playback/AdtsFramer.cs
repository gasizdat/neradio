namespace Neradio.Core.Playback;

// Cuts AAC in ADTS into whole frames, which a decoder takes one at a time. Data before a sync
// word is skipped.
internal sealed class AdtsFramer
{
    private const int HeaderSize = 7;

    private byte[] _data = [];

    public byte[][] Push(ReadOnlySpan<byte> data)
    {
        byte[] joined = [.. _data, .. data];
        var frames = new List<byte[]>();
        var at = 0;
        while (at + HeaderSize <= joined.Length)
        {
            // ADTS header fields, ISO/IEC 13818-7.
            var header = joined.AsSpan(at);
            var hasSyncWord = header[0] == 0xFF && (header[1] & 0xF0) == 0xF0;
            var layer = (header[1] >> 1) & 0x03;
            var frameLength = ((header[3] & 0x03) << 11) | (header[4] << 3) | (header[5] >> 5);
            if (!hasSyncWord || layer != 0 || frameLength < HeaderSize)
            {
                at++;
                continue;
            }
            if (at + frameLength > joined.Length)
                break;
            frames.Add(joined[at..(at + frameLength)]);
            at += frameLength;
        }
        _data = joined[at..];
        return frames.ToArray();
    }
}
