namespace Optinull.Optimization.Search;

public interface ISearchSolver<TSolution>
{
    SearchResult<TSolution> Solve(
        ISearchProblem<TSolution> problem,
        CancellationToken cancellationToken = default);
}
