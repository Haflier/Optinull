namespace Optinull.Problems.Vrp;

/// <summary>
/// Cuts one order of all customers into routes: the cheapest way to split it into at most
/// <see cref="VrpProblem.Vehicles"/> consecutive runs that each fit a vehicle.
/// </summary>
public static class VrpSplitter
{
    /// <returns>The plan, or null when this order cannot be split into that many runs.</returns>
    public static VrpPlan? Split(VrpProblem problem, IReadOnlyList<int> order)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(order);

        var n = order.Count;
        var vehicles = problem.Vehicles;

        // best[k, i]: the shortest way to serve the first i customers with exactly k routes.
        var best = new double[vehicles + 1, n + 1];
        var from = new int[vehicles + 1, n + 1];

        for (var k = 0; k <= vehicles; k++)
        {
            for (var i = 0; i <= n; i++)
                best[k, i] = double.PositiveInfinity;
        }

        best[0, 0] = 0;

        for (var k = 1; k <= vehicles; k++)
        {
            for (var i = 1; i <= n; i++)
            {
                var load = 0;
                var path = 0.0;

                // The last route serves customers j .. i-1.
                for (var j = i - 1; j >= 0; j--)
                {
                    load += problem.Customers[order[j]].Demand;

                    if (load > problem.Capacity)
                        break;

                    if (j < i - 1)
                        path += problem.Distance(order[j], order[j + 1]);

                    var before = best[k - 1, j];

                    if (double.IsPositiveInfinity(before))
                        continue;

                    var cost = before + problem.FromDepot(order[j]) + path + problem.ToDepot(order[i - 1]);

                    if (cost < best[k, i])
                    {
                        best[k, i] = cost;
                        from[k, i] = j;
                    }
                }
            }
        }

        var bestRoutes = 0;
        var bestCost = double.PositiveInfinity;

        for (var k = 1; k <= vehicles; k++)
        {
            if (best[k, n] < bestCost)
            {
                bestCost = best[k, n];
                bestRoutes = k;
            }
        }

        if (bestRoutes == 0)
            return null;

        var routes = new List<List<int>>();
        var end = n;

        for (var k = bestRoutes; k >= 1; k--)
        {
            var start = from[k, end];
            var route = new List<int>();

            for (var t = start; t < end; t++)
                route.Add(order[t]);

            routes.Add(route);
            end = start;
        }

        routes.Reverse();

        return new VrpPlan(routes);
    }
}
