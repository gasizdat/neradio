using Microsoft.Extensions.Time.Testing;

namespace Neradio.Core.Tests.Playback;

// A clock the user, or the network, can set: its date and time move apart from the time that
// passes. FakeTimeProvider can't go back.
sealed class SettableClock(FakeTimeProvider time) : TimeProvider
{
    public TimeSpan Shift { get; set; }

    public override long TimestampFrequency => time.TimestampFrequency;
    public override TimeZoneInfo LocalTimeZone => time.LocalTimeZone;

    public override DateTimeOffset GetUtcNow() => time.GetUtcNow() + Shift;

    public override long GetTimestamp() => time.GetTimestamp();

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
        time.CreateTimer(callback, state, dueTime, period);
}
