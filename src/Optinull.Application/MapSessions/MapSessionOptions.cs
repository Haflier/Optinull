namespace Optinull.Application.MapSessions;

/// <summary>Where the map is served and how long a map link stays usable.</summary>
public sealed class MapSessionOptions
{
    /// <summary>
    /// The address the user's browser reaches the map web app at. It must be reachable from the
    /// user's phone or computer, so use the public https address when the app is deployed.
    /// </summary>
    public string PublicBaseUrl { get; init; } = "http://localhost:5080";

    /// <summary>How long a link works if no problem is ever submitted through it.</summary>
    public TimeSpan UnusedLifetime { get; init; } = TimeSpan.FromMinutes(30);

    /// <summary>How long a link, and the result behind it, stays open once a problem was submitted.</summary>
    public TimeSpan ResultLifetime { get; init; } = TimeSpan.FromMinutes(30);

    /// <summary>Checks the settings and returns the start of every link: the base address plus "#s=".</summary>
    internal string LinkPrefix()
    {
        if (!Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                $"MapSessions:PublicBaseUrl must be an absolute http or https address, but is '{PublicBaseUrl}'.");
        }

        if (UnusedLifetime <= TimeSpan.Zero)
            throw new ArgumentException("MapSessions:UnusedLifetime must be greater than zero.");

        if (ResultLifetime <= TimeSpan.Zero)
            throw new ArgumentException("MapSessions:ResultLifetime must be greater than zero.");

        var path = uri.GetLeftPart(UriPartial.Path);

        if (!path.EndsWith('/'))
            path += "/";

        // The token goes after "#": browsers never send the fragment to the server, so the
        // token stays out of access logs, proxies and Referer headers.
        return path + "#s=";
    }
}
