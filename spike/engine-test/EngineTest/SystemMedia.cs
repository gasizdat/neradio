namespace EngineTest;

// The system's media controls: the media session, its notification and the foreground service
// on Android; the system media transport controls on Windows.
static partial class SystemMedia
{
    public static partial string Describe();

    public static partial void Show(bool active, string? station, string? title);
}
