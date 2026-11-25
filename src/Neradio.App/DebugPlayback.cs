#if DEBUG
using Neradio.Core.Playback;
using Neradio.Core.Stations;

namespace Neradio.App;

// For hand checks until the screens exist (#10): plays a stream URL given at launch, and logs the
// player's state and titles to the console, which is logcat on Android.
//   Android: adb shell am start -f 0x20000000 -n io.github.gasizdat.neradio/crc640b41d2c63d2be6b3.MainActivity --es url <URL>
//   Windows: Neradio.App.exe --url <URL>
internal static class DebugPlayback
{
    private static readonly TimeSpan StatePoll = TimeSpan.FromMilliseconds(250);

    private static Player? _player;
    private static ITimer? _statePoll;

    public static void Play(string url)
    {
        if (_player is null)
            Watch();
        var station = new Station { Name = "Hand check", PrimaryStream = new Uri(url) };
        Log($"play {url}");
        _player!.Play(station);
    }

    private static void Watch()
    {
        var player = IPlatformApplication.Current!.Services.GetRequiredService<Player>();
        _player = player;
        player.TitleChanged += title => Log($"title change: {title}");
        var state = player.State;
        _statePoll = TimeProvider.System.CreateTimer(_ =>
        {
            if (player.State == state)
                return;
            state = player.State;
            Log($"state: {state}");
        }, null, StatePoll, StatePoll);
    }

    private static void Log(string line) => Console.WriteLine($"neradio {DateTime.Now:HH:mm:ss.fff} {line}");
}
#endif
