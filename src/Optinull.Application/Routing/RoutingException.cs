namespace Optinull.Application.Routing;

/// <summary>The routing service failed, timed out, or answered something unusable.</summary>
public class RoutingException : Exception
{
    public RoutingException(string message)
        : base(message)
    {
    }

    public RoutingException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

/// <summary>
/// No road connects two of the locations. Indices are positions in the cost matrix:
/// 0 is the depot, customer i is i + 1.
/// </summary>
public sealed class UnreachableRouteException : RoutingException
{
    public UnreachableRouteException(int from, int to)
        : base($"No road route from {Name(from)} to {Name(to)}.")
    {
        From = from;
        To = to;
    }

    public int From { get; }

    public int To { get; }

    private static string Name(int index) => index == 0 ? "the depot" : $"customer {index}";
}
