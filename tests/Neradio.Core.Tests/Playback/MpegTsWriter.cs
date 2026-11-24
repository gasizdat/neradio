namespace Neradio.Core.Tests.Playback;

// Builds HLS segments in MPEG-TS the way encoders lay them out: a PAT and a PMT, an ID3 stream
// beside the audio, a PCR in an adaptation field, PES packets split across TS packets, and
// stuffing in the last packet of each.
static class MpegTsWriter
{
    public const byte Aac = 0x0F;
    public const byte Mp3 = 0x03;

    private const int PacketSize = 188;
    private const int AudioPid = 0x101;
    private const int Id3Pid = 0x102;
    private const int PmtPid = 0x1000;

    public static byte[] Segment(params byte[][] frames) => Segment(Aac, frames);

    public static byte[] Segment(byte audioType, params byte[][] frames)
    {
        using var segment = new MemoryStream();
        byte[] pat =
        [
            0x00, 0x00, 0xB0, 13, 0x00, 0x01, 0xC1, 0x00, 0x00,
            0x00, 0x01, 0xE0 | (PmtPid >> 8), PmtPid & 0xFF,
            0, 0, 0, 0,
        ];
        Packet(segment, 0, startsUnit: true, pat, withPcr: false);
        byte[] pmt =
        [
            0x00, 0x02, 0xB0, 23, 0x00, 0x01, 0xC1, 0x00, 0x00,
            0xE0 | (AudioPid >> 8), AudioPid & 0xFF, 0xF0, 0x00,
            0x15, 0xE0 | (Id3Pid >> 8), Id3Pid & 0xFF, 0xF0, 0x00,
            audioType, 0xE0 | (AudioPid >> 8), AudioPid & 0xFF, 0xF0, 0x00,
            0, 0, 0, 0,
        ];
        Packet(segment, PmtPid, startsUnit: true, pmt, withPcr: false);
        Pes(segment, Id3Pid, 0xBD, "ID3 timed metadata"u8.ToArray());
        foreach (var frame in frames)
            Pes(segment, AudioPid, 0xC0, frame);
        return segment.ToArray();
    }

    private static void Pes(MemoryStream segment, int pid, byte streamId, byte[] data)
    {
        var length = 3 + 5 + data.Length;
        byte[] pes = [0x00, 0x00, 0x01, streamId, (byte)(length >> 8), (byte)length, 0x80, 0x80, 0x05, 0x21, 0x00, 0x01, 0x00, 0x01, .. data];
        var withPcr = pid == AudioPid;
        for (var at = 0; at < pes.Length;)
        {
            var room = PacketSize - 4 - (withPcr ? 8 : 0);
            var take = Math.Min(room, pes.Length - at);
            Packet(segment, pid, startsUnit: at == 0, pes[at..(at + take)], withPcr);
            at += take;
            withPcr = false;
        }
    }

    // PSI sections are padded in the payload; PES packets with an adaptation field.
    private static void Packet(MemoryStream segment, int pid, bool startsUnit, byte[] payload, bool withPcr)
    {
        var room = PacketSize - 4 - payload.Length;
        var stuffing = Enumerable.Repeat((byte)0xFF, Math.Max(0, room - 2)).ToArray();
        byte[] field;
        if (pid == 0 || pid == PmtPid)
        {
            var padding = Enumerable.Repeat((byte)0xFF, room).ToArray();
            payload = [.. payload, .. padding];
            field = [];
        }
        else if (withPcr)
        {
            var pcrStuffing = Enumerable.Repeat((byte)0xFF, room - 8).ToArray();
            field = [(byte)(room - 1), 0x10, 0, 0, 0, 0, 0, 0, .. pcrStuffing];
        }
        else if (room == 0)
        {
            field = [];
        }
        else if (room == 1)
        {
            field = [0];
        }
        else
        {
            field = [(byte)(room - 1), 0x00, .. stuffing];
        }
        var control = field.Length > 0 ? 0x30 : 0x10;
        byte[] packet = [0x47, (byte)((startsUnit ? 0x40 : 0) | (pid >> 8)), (byte)pid, (byte)control, .. field, .. payload];
        segment.Write(packet);
    }
}
