using System.Text;
using Neradio.Core.Playback;

namespace Neradio.Core.Tests.Playback;

public sealed class IcyTitleTests
{
    [Fact]
    public void TitleAmongOtherFields()
    {
        var block = Block("StreamTitle='Artist A - Song 1';StreamUrl='https://a.example';"u8);

        var title = IcyTitle.Read(block);

        Assert.Equal("Artist A - Song 1", title);
    }

    [Fact]
    public void TitleWithAQuote()
    {
        var block = Block("StreamTitle='Guns N' Roses - Patience';"u8);

        var title = IcyTitle.Read(block);

        Assert.Equal("Guns N' Roses - Patience", title);
    }

    [Fact]
    public void EmptyTitle()
    {
        var block = Block("StreamTitle='';"u8);

        var title = IcyTitle.Read(block);

        Assert.Equal("", title);
    }

    [Fact]
    public void BlockWithoutATitle()
    {
        var block = Block("StreamUrl='https://a.example';"u8);

        var title = IcyTitle.Read(block);

        Assert.Null(title);
    }

    // UTF-8, else Windows-1252

    [Fact]
    public void Utf8Title()
    {
        var block = Block("StreamTitle='Мумий Тролль - Утекай';"u8);

        var title = IcyTitle.Read(block);

        Assert.Equal("Мумий Тролль - Утекай", title);
    }

    [Fact]
    public void Windows1252Title()
    {
        byte[] text = [.. "StreamTitle='"u8, 0x93, .. "Caf"u8, 0xE9, 0x94, 0x20, 0x96, .. " Live';"u8];
        var block = Block(text);

        var title = IcyTitle.Read(block);

        Assert.Equal("“Café” – Live", title);
    }

    // Padded with NULs to a multiple of 16 bytes, as servers send it.
    private static byte[] Block(ReadOnlySpan<byte> text)
    {
        var block = new byte[(text.Length + 15) / 16 * 16];
        text.CopyTo(block);
        return block;
    }
}
