using System.Text;
using System.Text.Unicode;

namespace Neradio.Core.Playback;

// ICY metadata declares no text encoding. A title is read as UTF-8 when it's valid UTF-8, and as
// Windows-1252 otherwise: Windows software writes curly quotes and dashes, which are control
// characters in Latin-1.
internal static class IcyTitle
{
    private static readonly Encoding Windows1252 = CodePagesEncodingProvider.Instance.GetEncoding(1252)!;

    // Null when the block carries no StreamTitle.
    public static string? Read(ReadOnlySpan<byte> metadata)
    {
        var start = metadata.IndexOf("StreamTitle='"u8);
        if (start < 0)
            return null;
        var value = metadata[(start + 13)..];
        var end = value.IndexOf("';"u8);
        if (end < 0)
            return null;
        var title = value[..end];
        return Utf8.IsValid(title) ? Encoding.UTF8.GetString(title) : Windows1252.GetString(title);
    }
}
