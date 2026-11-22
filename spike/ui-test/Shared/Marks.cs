namespace UiTest;

// Startup milestones, measured from the process start. On Android they go to logcat under the
// tag UIMARK; on Windows to marks.txt next to the executable.
public static class Marks
{
    public static void Write(string milestone)
    {
#if ANDROID
        var elapsed = Android.OS.SystemClock.UptimeMillis() - Android.OS.Process.StartUptimeMillis;
        Android.Util.Log.Info("UIMARK", $"{milestone} {elapsed} ms");
#else
        var start = System.Diagnostics.Process.GetCurrentProcess().StartTime;
        var elapsed = (DateTime.Now - start).TotalMilliseconds;
        var file = Path.Combine(AppContext.BaseDirectory, "marks.txt");
        File.AppendAllText(file, $"{milestone} {elapsed:F0} ms{Environment.NewLine}");
#endif
    }
}
