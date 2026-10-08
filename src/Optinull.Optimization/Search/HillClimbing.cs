using Optinull.Domain.Evaluation;
using Optinull.Optimization.Randomness;

namespace Optinull.Optimization.Search;

public sealed class HillClimbing<TSolution> : ISearchSolver<TSolution>
{
    private readonly IRandomSource _random;

    public HillClimbing(IRandomSource? random = null)
    {
        _random = random ?? new RandomSource();
    }

    public SearchResult<TSolution> Solve(
        ISearchProblem<TSolution> problem,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(problem);

        return Solve(
            problem,
            problem.CreateRandom(_random),
            cancellationToken);
    }

    public SearchResult<TSolution> Solve(
        ISearchProblem<TSolution> problem,
        TSolution initialSolution,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(problem);

        var run = new SearchRun<TSolution>(problem);

        Climb(run, problem, initialSolution, cancellationToken);

        return run.Complete();
    }

    // Steepest descent. Also used by IteratedLocalSearch, so evaluations
    // and the best solution are tracked on the caller's run.
    internal static (TSolution Solution, EvaluationResult Evaluation) Climb(
        SearchRun<TSolution> run,
        ISearchProblem<TSolution> problem,
        TSolution start,
        CancellationToken cancellationToken)
    {
        var current = start;
        var currentEvaluation = run.Evaluate(current);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var found = false;
            var bestNeighbor = default(TSolution)!;
            var bestEvaluation = currentEvaluation;

            foreach (var neighbor in problem.Neighbors(current))
            {
                var evaluation = run.Evaluate(neighbor);

                if (!found || run.IsBetter(evaluation, bestEvaluation))
                {
                    found = true;
                    bestNeighbor = neighbor;
                    bestEvaluation = evaluation;
                }
            }

            if (!found ||
                !run.IsBetter(bestEvaluation, currentEvaluation))
            {
                return (current, currentEvaluation);
            }

            current = bestNeighbor;
            currentEvaluation = bestEvaluation;
        }
    }
}
