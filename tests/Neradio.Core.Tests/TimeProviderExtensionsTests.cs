using Microsoft.Extensions.Time.Testing;

namespace Neradio.Core.Tests;

public sealed class TimeProviderExtensionsTests
{
    private readonly FakeTimeProvider _time = new();

    [Fact]
    public void DelayEndsAfterItsTime()
    {
        var delay = _time.DelayAsync(TimeSpan.FromSeconds(2), CancellationToken.None);

        _time.Advance(TimeSpan.FromSeconds(1.9));
        Assert.False(delay.IsCompleted);
        _time.Advance(TimeSpan.FromSeconds(0.1));

        Assert.True(delay.IsCompletedSuccessfully);
    }

    [Fact]
    public void CancelledDelayHasEndedWhenCancelReturns()
    {
        using var cancellation = new CancellationTokenSource();
        var delay = _time.DelayAsync(TimeSpan.FromSeconds(2), cancellation.Token);

        cancellation.Cancel();

        Assert.True(delay.IsCanceled);
    }
}
