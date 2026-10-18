using Optinull.Domain.Evaluation;

namespace Optinull.Problems.Vrp;

public static class VrpEvaluator
{
    public static double RouteDistance(VrpProblem problem, IReadOnlyList<int> route)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(route);

        if (route.Count == 0)
            return 0;

        var distance = problem.FromDepot(route[0]) + problem.ToDepot(route[^1]);

        for (var i = 1; i < route.Count; i++)
            distance += problem.Distance(route[i - 1], route[i]);

        return distance;
    }

    public static int RouteLoad(VrpProblem problem, IReadOnlyList<int> route)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(route);

        var load = 0;

        foreach (var customer in route)
            load += problem.Customers[customer].Demand;

        return load;
    }

    public static double TotalDistance(VrpProblem problem, VrpPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return plan.Routes.Sum(route => RouteDistance(problem, route));
    }

    /// <summary>
    /// The objective is the total distance. Overloaded vehicles or too many routes make a plan
    /// infeasible; the search never builds one, but the evaluator does not take that on trust.
    /// </summary>
    public static EvaluationResult Evaluate(VrpProblem problem, VrpPlan plan)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(plan);

        var seen = new bool[problem.CustomerCount];

        foreach (var route in plan.Routes)
        {
            foreach (var customer in route)
            {
                if (customer < 0 || customer >= problem.CustomerCount)
                    throw new ArgumentException($"The plan serves customer {customer}, who does not exist.", nameof(plan));

                if (seen[customer])
                    throw new ArgumentException($"The plan serves customer {customer} twice.", nameof(plan));

                seen[customer] = true;
            }
        }

        if (Array.IndexOf(seen, false) is var missing and >= 0)
            throw new ArgumentException($"The plan never serves customer {missing}.", nameof(plan));

        long overload = 0;

        foreach (var route in plan.Routes)
            overload += Math.Max(0, RouteLoad(problem, route) - problem.Capacity);

        var extraRoutes = Math.Max(0, plan.RouteCount - problem.Vehicles);
        var feasible = overload == 0 && extraRoutes == 0;

        return new EvaluationResult(
            objectiveValue: TotalDistance(problem, plan),
            isFeasible: feasible,
            penalty: overload + (double)extraRoutes * problem.Capacity);
    }
}
