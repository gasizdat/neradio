using Neradio.Core.Stations;

namespace Neradio.Core.Tests.Stations;

public sealed class StationIdTests
{
    [Fact]
    public void IdsDifferingOnlyInLetterCaseAreEqual()
    {
        var upper = StationId.Parse("5F0C2B7E-3D4A-4C1B-9E8F-0A1B2C3D4E5F");
        var lower = StationId.Parse("5f0c2b7e-3d4a-4c1b-9e8f-0a1b2c3d4e5f");

        Assert.NotNull(upper);
        Assert.Equal(upper, lower);
    }

    [Fact]
    public void IdThatIsNotAUuidIsNoId()
    {
        Assert.Null(StationId.Parse("42"));
    }

    [Theory]
    [InlineData("{5f0c2b7e-3d4a-4c1b-9e8f-0a1b2c3d4e5f}")]
    [InlineData("5f0c2b7e3d4a4c1b9e8f0a1b2c3d4e5f")]
    public void UuidInAnotherFormIsNoId(string value)
    {
        Assert.Null(StationId.Parse(value));
    }
}
