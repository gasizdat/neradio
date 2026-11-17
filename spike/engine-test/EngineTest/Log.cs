using System.Collections.Concurrent;

namespace EngineTest;

// The log is the test's result, so every line reaches the file at once: a run that's killed
// hours in keeps everything up to that moment.
static class Log
{
    static readonly BlockingCollection<string> Pending = [];
    static readonly Queue<string> Recent = new();

    public static string Folder { get; } =
#if ANDROID
        Android.App.Application.Context.GetExternalFilesDir(null)!.AbsolutePath;
#else
        FileSystem.AppDataDirectory;
#endif

    public static string FilePath { get; } = Path.Combine(Folder, $"engine-test-{DateTime.Now:yyyyMMdd-HHmmss}.log");

    public static int Version { get; private set; }

    static Log()
    {
        var writer = new Thread(WriteLines) { IsBackground = true, Name = "log" };
        writer.Start();
    }

    public static void Write(string category, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {category,-8} {message}";
        Pending.Add(line);
        lock (Recent)
        {
            Recent.Enqueue(line);
            if (Recent.Count > 60)
                Recent.Dequeue();
            Version++;
        }
    }

    public static string Tail()
    {
        lock (Recent)
            return string.Join('\n', Recent.Reverse());
    }

    static void WriteLines()
    {
        using var file = new StreamWriter(FilePath, append: true) { AutoFlush = true };
        foreach (var line in Pending.GetConsumingEnumerable())
            file.WriteLine(line);
    }
}
