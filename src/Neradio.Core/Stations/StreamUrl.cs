namespace Neradio.Core.Stations;

public static class StreamUrl
{
    // Uri has already lowercased the host and dropped a port that is the default for its scheme.
    // Leaving the scheme out makes http and https compare equal.
    public static string Normalize(Uri url)
    {
        var path = url.AbsolutePath.TrimEnd('/');
        return url.Authority + path + url.Query;
    }
}
