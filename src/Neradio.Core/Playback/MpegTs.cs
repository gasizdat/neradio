namespace Neradio.Core.Playback;

// Takes the AAC out of an HLS segment in MPEG-TS. Each segment starts with its own PAT and PMT
// (RFC 8216, 3.2), so segments are read one at a time. Field names are ISO/IEC 13818-1's.
internal static class MpegTs
{
    public const byte SyncByte = 0x47;

    private const int PacketSize = 188;
    private const byte AdtsStreamType = 0x0F;
    // The bytes of a section before its section_length counts, and its CRC_32 at the end.
    private const int SectionHeaderSize = 3;
    private const int CrcSize = 4;
    // A PAT entry: program_number and its PID. A PMT's fixed fields up to program_info_length,
    // and an elementary stream's fields up to ES_info_length.
    private const int PatEntrySize = 4;
    private const int PmtHeaderSize = 12;
    private const int StreamEntrySize = 5;

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
            var pid = Pid(packet[1], packet[2]);
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
        if (start + SectionHeaderSize > payload.Length)
            return [];
        var section = payload[start..];
        var sectionLength = Length(section[1], section[2]);
        var end = Math.Min(section.Length, SectionHeaderSize + sectionLength - CrcSize);
        return end < 0 ? [] : section[..end];
    }

    private static int PmtPid(ReadOnlySpan<byte> pat)
    {
        for (var at = 8; at + PatEntrySize <= pat.Length; at += PatEntrySize)
        {
            var programNumber = (pat[at] << 8) | pat[at + 1];
            if (programNumber != 0)
                return Pid(pat[at + 2], pat[at + 3]);
        }
        return -1;
    }

    private static int AacPid(ReadOnlySpan<byte> pmt)
    {
        if (pmt.Length < PmtHeaderSize)
            return -1;
        var programInfoLength = Length(pmt[10], pmt[11]);
        var at = PmtHeaderSize + programInfoLength;
        while (at + StreamEntrySize <= pmt.Length)
        {
            var streamType = pmt[at];
            if (streamType == AdtsStreamType)
                return Pid(pmt[at + 1], pmt[at + 2]);
            var esInfoLength = Length(pmt[at + 3], pmt[at + 4]);
            at += StreamEntrySize + esInfoLength;
        }
        return -1;
    }

    private static ReadOnlySpan<byte> PesData(ReadOnlySpan<byte> payload)
    {
        var start = payload.Length < 9 ? payload.Length : 9 + payload[8];
        return start < payload.Length ? payload[start..] : [];
    }

    // The 13 low bits of two bytes.
    private static int Pid(byte high, byte low) => ((high & 0x1F) << 8) | low;

    // The 12 low bits of two bytes: section_length, program_info_length, ES_info_length.
    private static int Length(byte high, byte low) => ((high & 0x0F) << 8) | low;
}
