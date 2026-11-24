using Neradio.Core.Playback;

namespace Neradio.Core.Tests.Playback;

public sealed class MpegTsTests
{
    [Fact]
    public void AacIsTakenOutInOrder()
    {
        var first = Frame(1, 500);
        var second = Frame(2, 100);
        var segment = MpegTsWriter.Segment(first, second);

        var aac = MpegTs.Aac(segment);

        Assert.NotNull(aac);
        Assert.Equal([.. first, .. second], aac);
    }

    // A 14-byte PES header and the frame fill 176 bytes after the PCR, then 184 bytes less the
    // stuffing: none, an adaptation field of just its length byte, and one with a flags byte too.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void StuffingInTheLastPacket(int stuffing)
    {
        var frame = Frame(1, 176 + 184 - 14 - stuffing);
        var segment = MpegTsWriter.Segment(frame);

        var aac = MpegTs.Aac(segment);

        Assert.Equal(frame, aac);
    }

    [Fact]
    public void SegmentWithoutAac()
    {
        var frame = Frame(1, 100);
        var segment = MpegTsWriter.Segment(MpegTsWriter.Mp3, frame);

        var aac = MpegTs.Aac(segment);

        Assert.Null(aac);
    }

    private static byte[] Frame(byte value, int length) => Enumerable.Repeat(value, length).ToArray();
}
