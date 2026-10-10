using Optinull.Domain.Evaluation;
using Optinull.Domain.Objectives;
using Optinull.Optimization.Randomness;
using Optinull.Optimization.Search;

namespace Optinull.Problems.Knapsack;

/// <summary>
/// Adapts a knapsack to the generic solvers. Every solution it creates fits the
/// capacity: a move that overfills the knapsack evicts random items until it
/// fits again. Moves flip one item; the genetic algorithm uses uniform crossover.
/// </summary>
public sealed class KnapsackSearchProblem :
    IRecombinableProblem<KnapsackSelection>,
    IConstructiveProblem<KnapsackSelection>
{
    private readonly KnapsackProblem _problem;

    public KnapsackSearchProblem(KnapsackProblem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);

        _problem = problem;
    }

    public ObjectiveType ObjectiveType => ObjectiveType.Maximize;

    public KnapsackSelection CreateRandom(IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);

        var count = _problem.ItemCount;
        var order = Enumerable.Range(0, count).ToArray();

        // Fisher-Yates shuffle.
        for (var i = order.Length - 1; i > 0; i--)
        {
            var j = random.Next(0, i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }

        var selected = new bool[count];
        long weight = 0;

        foreach (var item in order)
        {
            var itemWeight = _problem.Items[item].Weight;

            if (weight + itemWeight > _problem.Capacity)
                continue;

            selected[item] = true;
            weight += itemWeight;
        }

        return new KnapsackSelection(selected);
    }

    public EvaluationResult Evaluate(KnapsackSelection solution) =>
        KnapsackEvaluator.Evaluate(_problem, solution);

    public KnapsackSelection RandomNeighbor(KnapsackSelection solution, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(solution);
        ArgumentNullException.ThrowIfNull(random);

        var bits = solution.Flags.ToArray();
        var item = random.Next(0, bits.Length);

        bits[item] = !bits[item];

        // Adding an item may overfill the knapsack; if even an empty one cannot
        // hold it, there is no move here.
        if (bits[item] && !Repair(bits, random, protect: item))
            return solution;

        return new KnapsackSelection(bits);
    }

    public IEnumerable<KnapsackSelection> Neighbors(KnapsackSelection solution)
    {
        ArgumentNullException.ThrowIfNull(solution);

        var items = _problem.Items;
        var weight = KnapsackEvaluator.Totals(_problem, solution).Weight;

        for (var i = 0; i < solution.Count; i++)
        {
            if (solution[i])
                yield return With(solution, remove: i, add: -1);
            else if (weight + items[i].Weight <= _problem.Capacity)
                yield return With(solution, remove: -1, add: i);
        }

        for (var outItem = 0; outItem < solution.Count; outItem++)
        {
            if (!solution[outItem])
                continue;

            for (var inItem = 0; inItem < solution.Count; inItem++)
            {
                if (solution[inItem])
                    continue;

                if (weight - items[outItem].Weight + items[inItem].Weight <= _problem.Capacity)
                    yield return With(solution, remove: outItem, add: inItem);
            }
        }
    }

    public KnapsackSelection Crossover(
        KnapsackSelection first,
        KnapsackSelection second,
        IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        ArgumentNullException.ThrowIfNull(random);

        if (first.Count != second.Count)
            throw new ArgumentException("Parents must have the same length.");

        var bits = new bool[first.Count];

        for (var i = 0; i < bits.Length; i++)
            bits[i] = random.NextDouble() < 0.5 ? first[i] : second[i];

        // Two feasible parents can overfill together; evicting can always succeed.
        Repair(bits, random, protect: -1);

        return new KnapsackSelection(bits);
    }

    public KnapsackSelection Mutate(KnapsackSelection solution, IRandomSource random) =>
        RandomNeighbor(solution, random);

    /// <summary>Best value per unit of weight first, while it still fits.</summary>
    public KnapsackSelection CreateGreedy(Func<KnapsackSelection, EvaluationResult> evaluate)
    {
        ArgumentNullException.ThrowIfNull(evaluate);

        var items = _problem.Items;

        var order = Enumerable
            .Range(0, items.Count)
            .OrderByDescending(i => (double)items[i].Value / items[i].Weight)
            .ThenBy(i => i);

        var selected = new bool[items.Count];
        long weight = 0;

        foreach (var item in order)
        {
            if (weight + items[item].Weight > _problem.Capacity)
                continue;

            selected[item] = true;
            weight += items[item].Weight;
        }

        return new KnapsackSelection(selected);
    }

    private KnapsackSelection With(KnapsackSelection solution, int remove, int add)
    {
        var bits = solution.Flags.ToArray();

        if (remove >= 0)
            bits[remove] = false;

        if (add >= 0)
            bits[add] = true;

        return new KnapsackSelection(bits);
    }

    // Removes random selected items (never the protected one) until the weight fits.
    private bool Repair(bool[] bits, IRandomSource random, int protect)
    {
        var items = _problem.Items;
        long weight = 0;

        for (var i = 0; i < bits.Length; i++)
        {
            if (bits[i])
                weight += items[i].Weight;
        }

        if (weight <= _problem.Capacity)
            return true;

        var removable = Enumerable
            .Range(0, bits.Length)
            .Where(i => bits[i] && i != protect)
            .ToList();

        while (weight > _problem.Capacity)
        {
            if (removable.Count == 0)
                return false;

            var pick = random.Next(0, removable.Count);
            var item = removable[pick];

            removable[pick] = removable[^1];
            removable.RemoveAt(removable.Count - 1);

            bits[item] = false;
            weight -= items[item].Weight;
        }

        return true;
    }
}
