namespace Neradio.Core.Playback;

// Takes the AAC out of an HLS segment in MPEG-TS. Each segment starts with its own PAT and PMT
// (RFC 8216, 3.2), so segments are read one at a time.
internal static class MpegTs
{
    public const byte SyncByte = 0x47;

    private const int PacketSize = 188;
    private const byte AdtsStreamType = 0x0F;

    // The ADTS frames of the segment's AAC stream, or null when it has none.
    public static byte[]? Aac(ReadOnlySpan<byte> segment)
    {
        var pmtPid = -1;
        var aacPid = -1;
        using var aac = new MemoryStream();
        for (var at = 0; at + PacketSize <= segment.Length; at += PacketSize)
        {
            var packet = segment.Slice(at, PacketSize);
            var hasPayload = (packet[3] & 0x10) != 0;
            var hasAdaptation = (packet[3] & 0x20) != 0;
            var payloadAt = hasAdaptation ? 5 + packet[4] : 4;
            if (packet[0] != SyncByte || !hasPayload || payloadAt >= PacketSize)
                continue;

            var startsUnit = (packet[1] & 0x40) != 0;
            var pid = ((packet[1] & 0x1F) << 8) | packet[2];
            var payload = packet[payloadAt..];
            if (pid == 0 && startsUnit)
            {
                var pat = Section(payload);
                pmtPid = PmtPid(pat);
            }
            else if (pid == pmtPid && startsUnit)
            {
                var pmt = Section(payload);
                aacPid = AacPid(pmt);
            }
            else if (pid == aacPid)
            {
                var data = startsUnit ? PesData(payload) : payload;
                aac.Write(data);
            }
        }
        return aacPid < 0 ? null : aac.ToArray();
    }

    // A PSI section starts after its pointer field and ends before its CRC.
    private static ReadOnlySpan<byte> Section(ReadOnlySpan<byte> payload)
    {
        var start = 1 + payload[0];
        if (start + 3 > payload.Length)
            return [];
        var section = payload[start..];
        var end = Math.Min(section.Length, 3 + (((section[1] & 0x0F) << 8) | section[2]) - 4);
        return end < 0 ? [] : section[..end];
    }

    private static int PmtPid(ReadOnlySpan<byte> pat)
    {
        for (var at = 8; at + 4 <= pat.Length; at += 4)
        {
            var program = (pat[at] << 8) | pat[at + 1];
            if (program != 0)
                return ((pat[at + 2] & 0x1F) << 8) | pat[at + 3];
        }
        return -1;
    }

    private static int AacPid(ReadOnlySpan<byte> pmt)
    {
        if (pmt.Length < 12)
            return -1;
        var at = 12 + (((pmt[10] & 0x0F) << 8) | pmt[11]);
        while (at + 5 <= pmt.Length)
        {
            if (pmt[at] == AdtsStreamType)
                return ((pmt[at + 1] & 0x1F) << 8) | pmt[at + 2];
            at += 5 + (((pmt[at + 3] & 0x0F) << 8) | pmt[at + 4]);
        }
        return -1;
    }

    private static ReadOnlySpan<byte> PesData(ReadOnlySpan<byte> payload)
    {
        var start = payload.Length < 9 ? payload.Length : 9 + payload[8];
        return start < payload.Length ? payload[start..] : [];
    }
}
