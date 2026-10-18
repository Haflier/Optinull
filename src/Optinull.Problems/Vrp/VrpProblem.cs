namespace Optinull.Problems.Vrp;

public readonly record struct VrpCustomer(double X, double Y, int Demand);

/// <summary>
/// Capacitated vehicle routing: vehicles leave one depot, serve every customer exactly once
/// without carrying more than their capacity, and return. At most <see cref="Vehicles"/> of
/// them may be used. The aim is the shortest total distance.
/// </summary>
public sealed class VrpProblem
{
    private readonly VrpCustomer[] _customers;
    private readonly double[,] _distance;
    private readonly double[] _fromDepot;

    public VrpProblem(
        double depotX,
        double depotY,
        IEnumerable<VrpCustomer> customers,
        int vehicles,
        int capacity)
    {
        ArgumentNullException.ThrowIfNull(customers);

        _customers = customers.ToArray();

        if (_customers.Length == 0)
            throw new ArgumentException("A routing problem needs at least one customer.", nameof(customers));

        if (vehicles <= 0)
            throw new ArgumentOutOfRangeException(nameof(vehicles), "At least one vehicle is needed.");

        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");

        if (!double.IsFinite(depotX) || !double.IsFinite(depotY))
            throw new ArgumentException("The depot needs finite coordinates.");

        foreach (var customer in _customers)
        {
            if (!double.IsFinite(customer.X) || !double.IsFinite(customer.Y))
                throw new ArgumentException("Every customer needs finite coordinates.", nameof(customers));

            if (customer.Demand <= 0)
                throw new ArgumentException("Every demand must be greater than zero.", nameof(customers));

            if (customer.Demand > capacity)
            {
                throw new ArgumentException(
                    $"A demand of {customer.Demand} is more than the capacity {capacity}.",
                    nameof(customers));
            }
        }

        var required = RequiredVehicles(_customers.Select(customer => customer.Demand), capacity);

        if (vehicles < required)
        {
            throw new ArgumentException(
                $"The loads need at least {required} vehicles of capacity {capacity}, " +
                $"but only {vehicles} are available.",
                nameof(vehicles));
        }

        DepotX = depotX;
        DepotY = depotY;
        Vehicles = vehicles;
        Capacity = capacity;

        _fromDepot = _customers
            .Select(customer => Math.Sqrt(Math.Pow(customer.X - depotX, 2) + Math.Pow(customer.Y - depotY, 2)))
            .ToArray();

        _distance = new double[_customers.Length, _customers.Length];

        for (var a = 0; a < _customers.Length; a++)
        {
            for (var b = 0; b < _customers.Length; b++)
            {
                _distance[a, b] = Math.Sqrt(
                    Math.Pow(_customers[a].X - _customers[b].X, 2) +
                    Math.Pow(_customers[a].Y - _customers[b].Y, 2));
            }
        }
    }

    public double DepotX { get; }

    public double DepotY { get; }

    public IReadOnlyList<VrpCustomer> Customers => _customers;

    public int CustomerCount => _customers.Length;

    public int Vehicles { get; }

    public int Capacity { get; }

    public long TotalDemand => _customers.Sum(customer => (long)customer.Demand);

    public double Distance(int from, int to) => _distance[from, to];

    public double FromDepot(int customer) => _fromDepot[customer];

    /// <summary>
    /// A number of vehicles that is certain to be enough for these loads (first-fit decreasing).
    /// The true minimum can be smaller, but never needs more than this.
    /// </summary>
    public static int RequiredVehicles(IEnumerable<int> demands, int capacity) =>
        VrpBinPacking.FirstFitDecreasing(demands.ToArray(), capacity).Count;
}

/// <summary>Packs loads into as few vehicles as a quick heuristic finds.</summary>
public static class VrpBinPacking
{
    /// <summary>Largest load first, each into the first vehicle that still has room.</summary>
    public static List<List<int>> FirstFitDecreasing(IReadOnlyList<int> demands, int capacity)
    {
        ArgumentNullException.ThrowIfNull(demands);

        var bins = new List<List<int>>();
        var loads = new List<int>();

        var order = Enumerable
            .Range(0, demands.Count)
            .OrderByDescending(index => demands[index])
            .ThenBy(index => index);

        foreach (var customer in order)
        {
            if (demands[customer] > capacity)
                throw new ArgumentException($"A demand of {demands[customer]} is more than the capacity {capacity}.");

            var bin = loads.FindIndex(load => load + demands[customer] <= capacity);

            if (bin < 0)
            {
                bins.Add([]);
                loads.Add(0);
                bin = bins.Count - 1;
            }

            bins[bin].Add(customer);
            loads[bin] += demands[customer];
        }

        return bins;
    }
}
