namespace Optinull.Optimization.Search;

public sealed class GreedyBaseline<TSolution> : ISearchSolver<TSolution>
{
    public SearchResult<TSolution> Solve(
        ISearchProblem<TSolution> problem,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(problem);

        if (problem is not IConstructiveProblem<TSolution> constructive)
        {
            throw new InvalidOperationException(
                "The greedy baseline needs a problem that implements " +
                "IConstructiveProblem.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var run = new SearchRun<TSolution>(problem);

        var solution = constructive.CreateGreedy(run.Evaluate);

        // Make sure the final solution is recorded even if the
        // heuristic did not evaluate it itself.
        run.Evaluate(solution);

        return run.Complete();
    }
}
