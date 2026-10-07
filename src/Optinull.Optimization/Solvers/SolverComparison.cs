using Optinull.Domain.Objectives;
using Optinull.Domain.Problems;
using Optinull.Optimization.Comparison;

namespace Optinull.Optimization.Solvers;

public sealed class SolverComparison
{
    public IReadOnlyList<SolverResult> Compare(
        OptimizationProblem problem,
        IEnumerable<(string Name, IOptimizationSolver Solver)> solvers)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(solvers);

        var results = new List<SolverResult>();

        foreach (var (name, solver) in solvers)
        {
            ArgumentNullException.ThrowIfNull(solver);

            var stopwatch =
                System.Diagnostics.Stopwatch.StartNew();

            var result = solver.Solve(problem);

            stopwatch.Stop();

            results.Add(
                new SolverResult(
                    name,
                    result,
                    stopwatch.Elapsed));
        }

        return results;
    }

    public IReadOnlyList<RankedSolverResult> Rank(
        OptimizationProblem problem,
        IEnumerable<SolverResult> results)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(results);

        if (problem.Objective is null)
        {
            throw new InvalidOperationException(
                "Cannot rank results for a problem without an objective.");
        }

        var resultList = results.ToList();

        var comparer = new SolutionComparer(
            problem.Objective.Type);

        var ordered = resultList
            .OrderByDescending(
                result => result.Result.Evaluation,
                new EvaluationComparer(comparer))
            .ToList();

        var ranked = new List<RankedSolverResult>();

        for (var i = 0; i < ordered.Count; i++)
        {
            var rank = CalculateRank(
                ordered,
                i,
                comparer);

            ranked.Add(
                new RankedSolverResult(
                    rank,
                    ordered[i]));
        }

        return ranked;
    }

    private static int CalculateRank(
        IReadOnlyList<SolverResult> ordered,
        int index,
        ISolutionComparer comparer)
    {
        if (index == 0)
            return 1;

        var previous = ordered[index - 1]
            .Result
            .Evaluation;

        var current = ordered[index]
            .Result
            .Evaluation;

        // If neither result is better than the other,
        // they have the same objective quality and therefore tie.
        if (!comparer.IsBetter(previous, current) &&
            !comparer.IsBetter(current, previous))
        {
            return CalculateRank(
                ordered,
                index - 1,
                comparer);
        }

        return index + 1;
    }

    private sealed class EvaluationComparer
        : IComparer<
            Optinull.Domain.Evaluation.EvaluationResult>
    {
        private readonly ISolutionComparer _solutionComparer;

        public EvaluationComparer(
            ISolutionComparer solutionComparer)
        {
            _solutionComparer = solutionComparer;
        }

        public int Compare(
            Optinull.Domain.Evaluation.EvaluationResult? x,
            Optinull.Domain.Evaluation.EvaluationResult? y)
        {
            if (ReferenceEquals(x, y))
                return 0;

            if (x is null)
                return -1;

            if (y is null)
                return 1;

            if (_solutionComparer.IsBetter(x, y))
                return 1;

            if (_solutionComparer.IsBetter(y, x))
                return -1;

            return 0;
        }
    }
}
