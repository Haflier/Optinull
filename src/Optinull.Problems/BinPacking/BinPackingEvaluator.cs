using Optinull.Domain.Evaluation;

namespace Optinull.Problems.BinPacking;

public static class BinPackingEvaluator
{
    public static long[] Loads(BinPackingProblem problem, BinPackingPlan plan)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(plan);

        if (plan.Count != problem.ItemCount)
        {
            throw new ArgumentException(
                $"The plan has {plan.Count} items but the problem has {problem.ItemCount}.",
                nameof(plan));
        }

        var loads = new long[plan.BinCount];

        for (var item = 0; item < plan.Count; item++)
            loads[plan[item]] += problem.Sizes[item];

        return loads;
    }

    /// <summary>
    /// The objective is the number of bins plus a fraction below one that is smaller when the
    /// bins are more unevenly filled: fewer bins always wins, and among equal counts a plan
    /// that is closer to emptying a bin scores better. That gives local search a slope to follow.
    /// A bin over capacity is infeasible, with the overflow as the penalty.
    /// </summary>
    public static EvaluationResult Evaluate(BinPackingProblem problem, BinPackingPlan plan)
    {
        var loads = Loads(problem, plan);

        long overflow = 0;
        double squares = 0;

        foreach (var load in loads)
        {
            overflow += Math.Max(0, load - problem.Capacity);

            var fill = Math.Min(load, problem.Capacity) / (double)problem.Capacity;
            squares += fill * fill;
        }

        var bins = loads.Length;

        return new EvaluationResult(
            objectiveValue: bins + 1 - squares / bins,
            isFeasible: overflow == 0,
            penalty: overflow);
    }
}
