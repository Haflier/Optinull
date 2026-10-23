using Optinull.Domain.Evaluation;
using Optinull.Domain.Objectives;
using Optinull.Optimization.Randomness;
using Optinull.Optimization.Search;
using Optinull.Problems.Vrp;

namespace Optinull.Problems.BinPacking;

/// <summary>
/// Adapts bin packing to the generic solvers. Every solution it creates fits: moves only
/// put an item where there is room. A move takes one item to another bin (or a new one), a
/// swap trades two items of different size. The genetic algorithm inherits whole bins from
/// one parent, keeps the other parent's bins that lost nothing, and packs the rest again
/// (largest first, into the fullest bin that fits).
/// </summary>
public sealed class BinPackingSearchProblem :
    IRecombinableProblem<BinPackingPlan>,
    IConstructiveProblem<BinPackingPlan>
{
    private const double NewBinChance = 0.1;
    private const int Attempts = 20;

    private readonly BinPackingProblem _problem;

    public BinPackingSearchProblem(BinPackingProblem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);

        _problem = problem;
    }

    public ObjectiveType ObjectiveType => ObjectiveType.Minimize;

    public BinPackingPlan CreateRandom(IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);

        var sizes = _problem.Sizes;
        var order = Enumerable.Range(0, sizes.Count).ToArray();

        // Fisher-Yates shuffle.
        for (var i = order.Length - 1; i > 0; i--)
        {
            var j = random.Next(0, i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }

        var assignment = new int[sizes.Count];
        var loads = new List<long>();
        var open = new List<int>();

        foreach (var item in order)
        {
            open.Clear();

            for (var bin = 0; bin < loads.Count; bin++)
            {
                if (loads[bin] + sizes[item] <= _problem.Capacity)
                    open.Add(bin);
            }

            int target;

            if (open.Count == 0)
            {
                loads.Add(0);
                target = loads.Count - 1;
            }
            else
            {
                target = open[random.Next(0, open.Count)];
            }

            assignment[item] = target;
            loads[target] += sizes[item];
        }

        return new BinPackingPlan(assignment);
    }

    public EvaluationResult Evaluate(BinPackingPlan solution) =>
        BinPackingEvaluator.Evaluate(_problem, solution);

    public BinPackingPlan RandomNeighbor(BinPackingPlan solution, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(solution);
        ArgumentNullException.ThrowIfNull(random);

        var sizes = _problem.Sizes;
        var assignment = solution.Assignment.ToArray();
        var loads = Loads(assignment, solution.BinCount);

        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            var item = random.Next(0, assignment.Length);

            if (random.NextDouble() < 0.5)
            {
                var from = assignment[item];
                int target;

                if (random.NextDouble() < NewBinChance)
                {
                    // Opening a bin only makes sense when something stays behind.
                    if (loads[from] == sizes[item])
                        continue;

                    target = solution.BinCount;
                }
                else
                {
                    target = random.Next(0, solution.BinCount);

                    if (target == from || loads[target] + sizes[item] > _problem.Capacity)
                        continue;
                }

                assignment[item] = target;

                return new BinPackingPlan(assignment);
            }

            var other = random.Next(0, assignment.Length);

            if (CanSwap(assignment, loads, item, other))
            {
                (assignment[item], assignment[other]) = (assignment[other], assignment[item]);

                return new BinPackingPlan(assignment);
            }
        }

        // No fitting move was found; staying put is a valid answer.
        return solution;
    }

    public IEnumerable<BinPackingPlan> Neighbors(BinPackingPlan solution)
    {
        ArgumentNullException.ThrowIfNull(solution);

        var sizes = _problem.Sizes;
        var assignment = solution.Assignment.ToArray();
        var loads = Loads(assignment, solution.BinCount);

        for (var item = 0; item < assignment.Length; item++)
        {
            for (var bin = 0; bin < loads.Length; bin++)
            {
                if (bin == assignment[item] || loads[bin] + sizes[item] > _problem.Capacity)
                    continue;

                var moved = (int[])assignment.Clone();
                moved[item] = bin;

                yield return new BinPackingPlan(moved);
            }
        }

        for (var first = 0; first < assignment.Length; first++)
        {
            for (var second = first + 1; second < assignment.Length; second++)
            {
                if (!CanSwap(assignment, loads, first, second))
                    continue;

                var swapped = (int[])assignment.Clone();
                (swapped[first], swapped[second]) = (swapped[second], swapped[first]);

                yield return new BinPackingPlan(swapped);
            }
        }
    }

    public BinPackingPlan Crossover(
        BinPackingPlan first,
        BinPackingPlan second,
        IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        ArgumentNullException.ThrowIfNull(random);

        if (first.Count != second.Count)
            throw new ArgumentException("Parents must have the same number of items.");

        var sizes = _problem.Sizes;
        var child = new int[first.Count];
        var loads = new List<long>();

        Array.Fill(child, -1);

        // Whole bins of the second parent, chosen at random: they fit, as they did there.
        foreach (var bin in second.Bins())
        {
            if (random.NextDouble() >= 0.5)
                continue;

            long load = 0;

            foreach (var item in bin)
            {
                child[item] = loads.Count;
                load += sizes[item];
            }

            loads.Add(load);
        }

        // A bin of the first parent that lost nothing is kept as it is. One that lost items
        // is dissolved: its remaining items are placed again below.
        var free = new List<int>();

        foreach (var bin in first.Bins())
        {
            var left = bin.Where(item => child[item] < 0).ToList();

            if (left.Count != bin.Count)
            {
                free.AddRange(left);
                continue;
            }

            long load = 0;

            foreach (var item in left)
            {
                child[item] = loads.Count;
                load += sizes[item];
            }

            loads.Add(load);
        }

        // Largest first, each into the fullest bin that still has room (a new one if none).
        foreach (var item in free.OrderByDescending(item => sizes[item]).ThenBy(item => item))
        {
            var target = -1;

            for (var bin = 0; bin < loads.Count; bin++)
            {
                if (loads[bin] + sizes[item] <= _problem.Capacity &&
                    (target < 0 || loads[bin] > loads[target]))
                {
                    target = bin;
                }
            }

            if (target < 0)
            {
                loads.Add(0);
                target = loads.Count - 1;
            }

            child[item] = target;
            loads[target] += sizes[item];
        }

        return new BinPackingPlan(child);
    }

    public BinPackingPlan Mutate(BinPackingPlan solution, IRandomSource random) =>
        RandomNeighbor(solution, random);

    /// <summary>First-fit decreasing, the same quick heuristic the vehicle routing uses to size its fleet.</summary>
    public BinPackingPlan CreateGreedy(Func<BinPackingPlan, EvaluationResult> evaluate)
    {
        ArgumentNullException.ThrowIfNull(evaluate);

        var bins = VrpBinPacking.FirstFitDecreasing(_problem.Sizes, _problem.Capacity);

        return BinPackingPlan.FromBins(bins, _problem.ItemCount);
    }

    private long[] Loads(int[] assignment, int binCount)
    {
        var loads = new long[binCount];

        for (var item = 0; item < assignment.Length; item++)
            loads[assignment[item]] += _problem.Sizes[item];

        return loads;
    }

    // Two items in different bins trade places, if both bins still hold them.
    private bool CanSwap(int[] assignment, long[] loads, int first, int second)
    {
        var sizes = _problem.Sizes;
        var a = assignment[first];
        var b = assignment[second];

        if (a == b || sizes[first] == sizes[second])
            return false;

        return loads[a] - sizes[first] + sizes[second] <= _problem.Capacity &&
               loads[b] - sizes[second] + sizes[first] <= _problem.Capacity;
    }
}
