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
        // Written at once, past the queue: the process is about to end. Without this, a crash
        // and Android killing the app would leave the same log.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            using var file = Open();
            file.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} crash    {e.ExceptionObject}");
        };
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
        using var file = Open();
        foreach (var line in Pending.GetConsumingEnumerable())
            file.WriteLine(line);
    }

    static StreamWriter Open()
    {
        var stream = new FileStream(FilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        return new StreamWriter(stream) { AutoFlush = true };
    }
}
