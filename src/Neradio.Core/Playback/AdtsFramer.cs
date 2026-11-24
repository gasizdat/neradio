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
            var header = joined.AsSpan(at);
            var size = ((header[3] & 0x03) << 11) | (header[4] << 3) | (header[5] >> 5);
            var isSync = header[0] == 0xFF && (header[1] & 0xF6) == 0xF0;
            if (!isSync || size < HeaderSize)
            {
                at++;
                continue;
            }
            if (at + size > joined.Length)
                break;
            frames.Add(joined[at..(at + size)]);
            at += size;
        }
        _data = joined[at..];
        return frames.ToArray();
    }
}
