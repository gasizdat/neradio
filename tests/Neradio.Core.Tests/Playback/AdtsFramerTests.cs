using Neradio.Core.Playback;

namespace Neradio.Core.Tests.Playback;

public sealed class AdtsFramerTests
{
    private readonly AdtsFramer _framer = new();

    [Fact]
    public void WholeFramesInOrder()
    {
        var first = Frame(1, 100);
        var second = Frame(2, 200);

        var frames = _framer.Push([.. first, .. second]);

        Assert.Equal([first, second], frames);
    }

    [Fact]
    public void FrameSplitAcrossWrites()
    {
        var frame = Frame(1, 100);

        var before = _framer.Push(frame.AsSpan(0, 3));
        var middle = _framer.Push(frame.AsSpan(3, 50));
        var after = _framer.Push(frame.AsSpan(53));

        Assert.Empty(before);
        Assert.Empty(middle);
        Assert.Equal([frame], after);
    }

    [Fact]
    public void DataBeforeASyncWordIsSkipped()
    {
        var frame = Frame(1, 100);
        byte[] junk = [0xFF, 0x00, 0x12, 0xFF];

        var frames = _framer.Push([.. junk, .. frame]);

        Assert.Equal([frame], frames);
    }

    // A header that claims less than its own size can't be a frame.
    [Fact]
    public void HeaderTooShortForItselfIsSkipped()
    {
        var frame = Frame(1, 100);
        var broken = Frame(2, 7);
        broken[4] = 0;
        broken[5] = 0x20;

        var frames = _framer.Push([.. broken, .. frame]);

        Assert.Equal([frame], frames);
    }

    // An ADTS header with the frame's length, then the payload.
    private static byte[] Frame(byte value, int length)
    {
        var frame = Enumerable.Repeat(value, length).ToArray();
        frame[0] = 0xFF;
        frame[1] = 0xF1;
        frame[2] = 0x50;
        frame[3] = (byte)(0x80 | (length >> 11));
        frame[4] = (byte)(length >> 3);
        frame[5] = (byte)((length & 0x07) << 5 | 0x1F);
        frame[6] = 0xFC;
        return frame;
    }
}
