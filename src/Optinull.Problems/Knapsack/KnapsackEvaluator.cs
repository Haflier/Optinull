using Optinull.Domain.Evaluation;

namespace Optinull.Problems.Knapsack;

public static class KnapsackEvaluator
{
    public static (long Value, long Weight) Totals(
        KnapsackProblem problem,
        KnapsackSelection selection)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(selection);

        if (selection.Count != problem.ItemCount)
        {
            throw new ArgumentException(
                $"The selection has {selection.Count} items but the problem has {problem.ItemCount}.",
                nameof(selection));
        }

        long value = 0;
        long weight = 0;

        for (var i = 0; i < selection.Count; i++)
        {
            if (!selection[i])
                continue;

            value += problem.Items[i].Value;
            weight += problem.Items[i].Weight;
        }

        return (value, weight);
    }

    /// <summary>The objective is the total value; going over capacity is infeasible.</summary>
    public static EvaluationResult Evaluate(KnapsackProblem problem, KnapsackSelection selection)
    {
        var (value, weight) = Totals(problem, selection);
        var excess = Math.Max(0, weight - problem.Capacity);

        return new EvaluationResult(
            objectiveValue: value,
            isFeasible: excess == 0,
            penalty: excess);
    }
}

/// <summary>Dynamic programming over the capacity: the true optimum, used to grade the heuristics.</summary>
public static class KnapsackExactSolver
{
    public static long Optimum(KnapsackProblem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);

        var best = new long[problem.Capacity + 1];

        foreach (var item in problem.Items)
        {
            for (var capacity = problem.Capacity; capacity >= item.Weight; capacity--)
            {
                best[capacity] = Math.Max(
                    best[capacity],
                    best[capacity - item.Weight] + item.Value);
            }
        }

        return best[problem.Capacity];
    }
}
