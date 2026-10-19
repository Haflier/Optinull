using Optinull.Application.MapSessions;

namespace Optinull.Web.Sessions;

/// <summary>An error answer: a message for the page to show.</summary>
public sealed record ApiError(string Error);

/// <summary>
/// Gatekeeper for every /api request. The request must carry the session token as
/// "Authorization: Bearer ...", and the token must belong to a session that is still open. The
/// token travels in a header, never in the URL, so it does not end up in access logs. The
/// endpoints behind this act only on the session it found, so a request can never reach another
/// user's problem, job or result.
/// </summary>
internal sealed class MapSessionMiddleware(RequestDelegate next)
{
    private const string ItemKey = "Optinull.MapSession";

    public async Task InvokeAsync(HttpContext http, MapSessionService sessions)
    {
        if (!http.Request.Path.StartsWithSegments("/api"))
        {
            await next(http);
            return;
        }

        // The answers hold the user's problem and routes; keep them out of shared caches.
        http.Response.Headers.CacheControl = "no-store";

        var token = ReadBearerToken(http.Request);

        if (token is null)
        {
            await RejectAsync(
                http,
                StatusCodes.Status401Unauthorized,
                "Open this page from the link the Telegram bot sent you.");
            return;
        }

        // A wrong, replaced, closed or expired token all look the same from outside.
        if (!sessions.TryAuthorize(token, out var session))
        {
            await RejectAsync(
                http,
                StatusCodes.Status410Gone,
                "This map link is not valid or has expired. Ask the bot for a new one.");
            return;
        }

        http.Items[ItemKey] = session;

        await next(http);
    }

    /// <summary>The session the middleware authorized for this request.</summary>
    public static MapSession SessionOf(HttpContext http) =>
        http.Items[ItemKey] as MapSession
        ?? throw new InvalidOperationException("The request was not authorized by a map session.");

    private static string? ReadBearerToken(HttpRequest request)
    {
        const string prefix = "Bearer ";

        var header = request.Headers.Authorization.ToString();

        if (!header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return null;

        var token = header[prefix.Length..].Trim();

        return token.Length == 0 ? null : token;
    }

    private static async Task RejectAsync(HttpContext http, int status, string message)
    {
        http.Response.StatusCode = status;

        await http.Response.WriteAsJsonAsync(new ApiError(message));
    }
}
