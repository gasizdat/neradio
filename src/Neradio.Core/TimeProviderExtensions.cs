namespace Neradio.Core;

internal static class TimeProviderExtensions
{
    // Task.Delay finishes a cancelled delay on the thread pool. This one finishes it on the thread
    // that cancels, so a stop or a network change has taken effect by the time it returns.
    public static async Task DelayAsync(this TimeProvider time, TimeSpan delay, CancellationToken cancellation)
    {
        var done = new TaskCompletionSource();
        using var timer = time.CreateTimer(_ => done.TrySetResult(), null, delay, Timeout.InfiniteTimeSpan);
        using var registration = cancellation.Register(() => done.TrySetCanceled(cancellation));
        await done.Task.ConfigureAwait(false);
    }
}
