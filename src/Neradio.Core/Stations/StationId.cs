namespace Neradio.Core.Stations;

public static class StationId
{
    // "D" is the 36-character hyphenated form only; Guid.TryParse would also take braces and 32 digits.
    public static Guid? Parse(string value) => Guid.TryParseExact(value, "D", out var id) ? id : null;
}
