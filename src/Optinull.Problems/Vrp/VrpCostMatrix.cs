namespace Optinull.Problems.Vrp;

/// <summary>
/// Directed travel costs between the depot and the customers. Index 0 is the depot and
/// customer i is index i + 1. The cost [a, b] is the cost of driving from a to b and need not
/// equal [b, a] (one-way streets, turn restrictions). Every cost must be finite and not
/// negative: an unreachable pair is an error, never a free leg.
/// </summary>
public sealed class VrpCostMatrix
{
    private readonly double[,] _cost;

    public VrpCostMatrix(double[,] costs)
    {
        ArgumentNullException.ThrowIfNull(costs);

        var size = costs.GetLength(0);

        if (size < 2 || costs.GetLength(1) != size)
            throw new ArgumentException("A cost matrix must be square and cover the depot and at least one customer.", nameof(costs));

        for (var a = 0; a < size; a++)
        {
            for (var b = 0; b < size; b++)
            {
                if (!double.IsFinite(costs[a, b]) || costs[a, b] < 0)
                {
                    throw new ArgumentException(
                        $"The cost from {Name(a)} to {Name(b)} must be finite and not negative " +
                        "(an unreachable pair cannot be used as a cost).",
                        nameof(costs));
                }
            }
        }

        _cost = (double[,])costs.Clone();
    }

    public int CustomerCount => _cost.GetLength(0) - 1;

    public double FromDepot(int customer) => _cost[0, customer + 1];

    public double ToDepot(int customer) => _cost[customer + 1, 0];

    public double Between(int from, int to) => _cost[from + 1, to + 1];

    /// <summary>Straight-line distances: the synthetic cost model, symmetric by construction.</summary>
    public static VrpCostMatrix Euclidean(double depotX, double depotY, IReadOnlyList<VrpCustomer> customers)
    {
        ArgumentNullException.ThrowIfNull(customers);

        var size = customers.Count + 1;
        var xs = new double[size];
        var ys = new double[size];

        xs[0] = depotX;
        ys[0] = depotY;

        for (var i = 0; i < customers.Count; i++)
        {
            xs[i + 1] = customers[i].X;
            ys[i + 1] = customers[i].Y;
        }

        var costs = new double[size, size];

        for (var a = 0; a < size; a++)
        {
            for (var b = 0; b < size; b++)
                costs[a, b] = Math.Sqrt(Math.Pow(xs[a] - xs[b], 2) + Math.Pow(ys[a] - ys[b], 2));
        }

        return new VrpCostMatrix(costs);
    }

    private static string Name(int index) => index == 0 ? "the depot" : $"customer {index - 1}";
}
