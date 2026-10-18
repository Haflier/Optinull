namespace Optinull.Problems.Vrp;

/// <summary>
/// Which customers each vehicle serves, in order. Every route starts and ends at the depot,
/// which is not listed. Vehicles that stay home have no route.
/// </summary>
public sealed class VrpPlan
{
    private readonly int[][] _routes;

    public VrpPlan(IEnumerable<IEnumerable<int>> routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        _routes = routes
            .Select(route => route.ToArray())
            .Where(route => route.Length > 0)
            .ToArray();

        if (_routes.Length == 0)
            throw new ArgumentException("A plan needs at least one route.", nameof(routes));
    }

    public IReadOnlyList<IReadOnlyList<int>> Routes => _routes;

    public int RouteCount => _routes.Length;

    public int CustomerCount => _routes.Sum(route => route.Length);
}
