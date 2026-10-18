using Optinull.Domain.Evaluation;
using Optinull.Domain.Objectives;
using Optinull.Optimization.Randomness;
using Optinull.Optimization.Search;

namespace Optinull.Problems.Vrp;

/// <summary>
/// Adapts a routing problem to the generic solvers. Every plan it creates respects the vehicle
/// capacity and the number of vehicles: plans are built that way and every move keeps them so,
/// because the solvers never accept a plan that breaks a limit. The moves are relocating a
/// customer (to any route, including an unused vehicle), swapping two customers, and reversing a
/// stretch of one route. The genetic algorithm recombines the customers' order and cuts it into
/// routes again.
/// </summary>
public sealed class VrpSearchProblem :
    IRecombinableProblem<VrpPlan>,
    IConstructiveProblem<VrpPlan>
{
    private const int MoveAttempts = 30;

    private readonly VrpProblem _problem;

    public VrpSearchProblem(VrpProblem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);

        _problem = problem;
    }

    public ObjectiveType ObjectiveType => ObjectiveType.Minimize;

    public EvaluationResult Evaluate(VrpPlan solution) => VrpEvaluator.Evaluate(_problem, solution);

    // ---- Construction ------------------------------------------------------------------

    public VrpPlan CreateRandom(IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);

        var n = _problem.CustomerCount;
        var order = Enumerable.Range(0, n).ToArray();

        // Fisher-Yates shuffle.
        for (var i = order.Length - 1; i > 0; i--)
        {
            var j = random.Next(0, i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }

        var slots = EmptySlots();
        var loads = new int[slots.Count];

        foreach (var customer in order)
        {
            var demand = _problem.Customers[customer].Demand;

            var fits = Enumerable
                .Range(0, slots.Count)
                .Where(slot => loads[slot] + demand <= _problem.Capacity)
                .ToList();

            // A random placement can strand a customer; packing the loads always works.
            if (fits.Count == 0)
                return PackedPlan();

            var pick = fits[random.Next(0, fits.Count)];

            slots[pick].Add(customer);
            loads[pick] += demand;
        }

        return new VrpPlan(slots);
    }

    /// <summary>
    /// Nearest neighbour with capacity: from wherever the vehicle is, go to the closest customer
    /// that still fits; when none does, start the next vehicle.
    /// </summary>
    public VrpPlan CreateGreedy(Func<VrpPlan, EvaluationResult> evaluate)
    {
        ArgumentNullException.ThrowIfNull(evaluate);

        var n = _problem.CustomerCount;
        var served = new bool[n];
        var remaining = n;
        var routes = new List<List<int>>();

        while (remaining > 0)
        {
            var route = new List<int>();
            var load = 0;
            var at = -1;

            while (true)
            {
                var next = -1;
                var nearest = double.PositiveInfinity;

                for (var customer = 0; customer < n; customer++)
                {
                    if (served[customer] || load + _problem.Customers[customer].Demand > _problem.Capacity)
                        continue;

                    var distance = at < 0 ? _problem.FromDepot(customer) : _problem.Distance(at, customer);

                    if (distance < nearest)
                    {
                        nearest = distance;
                        next = customer;
                    }
                }

                if (next < 0)
                    break;

                route.Add(next);
                served[next] = true;
                remaining--;
                load += _problem.Customers[next].Demand;
                at = next;
            }

            routes.Add(route);
        }

        // Too many vehicles for the fleet: fall back to packing the loads.
        return routes.Count > _problem.Vehicles ? PackedPlan() : new VrpPlan(routes);
    }

    // The loads packed into as few vehicles as first-fit decreasing manages, which the problem
    // guarantees is within the fleet; each vehicle visits its customers nearest first.
    private VrpPlan PackedPlan()
    {
        var demands = _problem.Customers.Select(customer => customer.Demand).ToArray();
        var bins = VrpBinPacking.FirstFitDecreasing(demands, _problem.Capacity);

        return new VrpPlan(bins.Select(NearestFirst));
    }

    private List<int> NearestFirst(List<int> customers)
    {
        var left = new List<int>(customers);
        var route = new List<int>();
        var at = -1;

        while (left.Count > 0)
        {
            var best = 0;

            for (var i = 1; i < left.Count; i++)
            {
                var candidate = at < 0 ? _problem.FromDepot(left[i]) : _problem.Distance(at, left[i]);
                var current = at < 0 ? _problem.FromDepot(left[best]) : _problem.Distance(at, left[best]);

                if (candidate < current)
                    best = i;
            }

            at = left[best];
            route.Add(at);
            left.RemoveAt(best);
        }

        return route;
    }

    // ---- Moves ----------------------------------------------------------------------------

    public VrpPlan RandomNeighbor(VrpPlan solution, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(solution);
        ArgumentNullException.ThrowIfNull(random);

        for (var attempt = 0; attempt < MoveAttempts; attempt++)
        {
            var slots = Slots(solution);
            var kind = random.Next(0, 10);

            var moved = kind < 5
                ? TryRelocate(slots, random)
                : kind < 8
                    ? TrySwap(slots, random)
                    : TryReverse(slots, random);

            if (moved)
                return new VrpPlan(slots);
        }

        // Nothing could move (for example a single customer): the plan stays as it is.
        return solution;
    }

    public IEnumerable<VrpPlan> Neighbors(VrpPlan solution)
    {
        ArgumentNullException.ThrowIfNull(solution);

        var routes = solution.Routes;
        var loads = routes.Select(route => VrpEvaluator.RouteLoad(_problem, route)).ToArray();
        var canOpenRoute = routes.Count < _problem.Vehicles;

        // Reverse a stretch of one route.
        for (var r = 0; r < routes.Count; r++)
        {
            for (var i = 0; i < routes[r].Count - 1; i++)
            {
                for (var j = i + 1; j < routes[r].Count; j++)
                {
                    var slots = Slots(solution);

                    slots[r].Reverse(i, j - i + 1);

                    yield return new VrpPlan(slots);
                }
            }
        }

        // Move one customer anywhere else, including onto an unused vehicle.
        for (var r = 0; r < routes.Count; r++)
        {
            for (var p = 0; p < routes[r].Count; p++)
            {
                var customer = routes[r][p];
                var demand = _problem.Customers[customer].Demand;

                var targets = routes.Count + (canOpenRoute ? 1 : 0);

                for (var t = 0; t < targets; t++)
                {
                    var targetLength = t < routes.Count ? routes[t].Count : 0;
                    var after = t == r ? targetLength - 1 : targetLength;

                    if (t != r && t < routes.Count && loads[t] + demand > _problem.Capacity)
                        continue;

                    for (var q = 0; q <= after; q++)
                    {
                        // Putting a customer back where it was changes nothing.
                        if (t == r && q == p)
                            continue;

                        var slots = Slots(solution);

                        slots[r].RemoveAt(p);
                        slots[t].Insert(q, customer);

                        yield return new VrpPlan(slots);
                    }
                }
            }
        }

        // Swap two customers of different routes.
        for (var r1 = 0; r1 < routes.Count - 1; r1++)
        {
            for (var r2 = r1 + 1; r2 < routes.Count; r2++)
            {
                for (var p1 = 0; p1 < routes[r1].Count; p1++)
                {
                    for (var p2 = 0; p2 < routes[r2].Count; p2++)
                    {
                        var d1 = _problem.Customers[routes[r1][p1]].Demand;
                        var d2 = _problem.Customers[routes[r2][p2]].Demand;

                        if (loads[r1] - d1 + d2 > _problem.Capacity || loads[r2] - d2 + d1 > _problem.Capacity)
                            continue;

                        var slots = Slots(solution);

                        (slots[r1][p1], slots[r2][p2]) = (slots[r2][p2], slots[r1][p1]);

                        yield return new VrpPlan(slots);
                    }
                }
            }
        }
    }

    // ---- Recombination ----------------------------------------------------------------------

    /// <summary>
    /// Order crossover on the customers' order, then the order is cut into routes again. If that
    /// order cannot be cut to fit the fleet, the child is simply the first parent.
    /// </summary>
    public VrpPlan Crossover(VrpPlan first, VrpPlan second, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        ArgumentNullException.ThrowIfNull(random);

        var a = first.Routes.SelectMany(route => route).ToArray();
        var b = second.Routes.SelectMany(route => route).ToArray();

        if (a.Length != b.Length)
            throw new ArgumentException("Parents must serve the same customers.");

        var n = a.Length;
        var left = random.Next(0, n);
        var right = random.Next(0, n);

        if (left > right)
            (left, right) = (right, left);

        var child = new int[n];
        var used = new bool[_problem.CustomerCount];

        for (var i = left; i <= right; i++)
        {
            child[i] = a[i];
            used[a[i]] = true;
        }

        // The rest comes from the second parent, in its order, starting after the copied part.
        var write = (right + 1) % n;

        for (var step = 0; step < n; step++)
        {
            var customer = b[(right + 1 + step) % n];

            if (used[customer])
                continue;

            child[write] = customer;
            used[customer] = true;
            write = (write + 1) % n;
        }

        return VrpSplitter.Split(_problem, child) ?? first;
    }

    public VrpPlan Mutate(VrpPlan solution, IRandomSource random) => RandomNeighbor(solution, random);

    // ---- Helpers -----------------------------------------------------------------------------

    private List<List<int>> EmptySlots() =>
        Enumerable.Range(0, _problem.Vehicles).Select(_ => new List<int>()).ToList();

    // One list per vehicle, so a move can also put a customer on a vehicle that was not used.
    private List<List<int>> Slots(VrpPlan plan)
    {
        var slots = plan.Routes.Select(route => route.ToList()).ToList();

        while (slots.Count < _problem.Vehicles)
            slots.Add([]);

        return slots;
    }

    private int LoadOf(List<int> route) => VrpEvaluator.RouteLoad(_problem, route);

    private (int Route, int Position) Locate(List<List<int>> slots, int index)
    {
        for (var r = 0; r < slots.Count; r++)
        {
            if (index < slots[r].Count)
                return (r, index);

            index -= slots[r].Count;
        }

        throw new ArgumentOutOfRangeException(nameof(index));
    }

    private bool TryRelocate(List<List<int>> slots, IRandomSource random)
    {
        var (r, p) = Locate(slots, random.Next(0, _problem.CustomerCount));
        var customer = slots[r][p];
        var t = random.Next(0, slots.Count);

        slots[r].RemoveAt(p);

        var q = random.Next(0, slots[t].Count + 1);

        if (t == r && q == p)
            return false;

        if (t != r && LoadOf(slots[t]) + _problem.Customers[customer].Demand > _problem.Capacity)
            return false;

        slots[t].Insert(q, customer);

        return true;
    }

    private bool TrySwap(List<List<int>> slots, IRandomSource random)
    {
        if (_problem.CustomerCount < 2)
            return false;

        var first = random.Next(0, _problem.CustomerCount);
        var second = random.Next(0, _problem.CustomerCount);

        if (first == second)
            return false;

        var (r1, p1) = Locate(slots, first);
        var (r2, p2) = Locate(slots, second);

        if (r1 != r2)
        {
            var d1 = _problem.Customers[slots[r1][p1]].Demand;
            var d2 = _problem.Customers[slots[r2][p2]].Demand;

            if (LoadOf(slots[r1]) - d1 + d2 > _problem.Capacity ||
                LoadOf(slots[r2]) - d2 + d1 > _problem.Capacity)
            {
                return false;
            }
        }

        (slots[r1][p1], slots[r2][p2]) = (slots[r2][p2], slots[r1][p1]);

        return true;
    }

    private bool TryReverse(List<List<int>> slots, IRandomSource random)
    {
        var candidates = Enumerable.Range(0, slots.Count).Where(r => slots[r].Count >= 2).ToList();

        if (candidates.Count == 0)
            return false;

        var route = slots[candidates[random.Next(0, candidates.Count)]];
        var i = random.Next(0, route.Count - 1);
        var j = random.Next(i + 1, route.Count);

        route.Reverse(i, j - i + 1);

        return true;
    }
}
