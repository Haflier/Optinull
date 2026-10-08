using Optinull.Domain.Evaluation;
using Optinull.Optimization.Comparison;

namespace Optinull.Optimization.Search;

public sealed record NamedSearchResult<TSolution>(
    string SolverName,
    SearchResult<TSolution> Result);

public sealed record RankedSearchResult<TSolution>(
    int Rank,
    NamedSearchResult<TSolution> Entry);

public sealed class SearchComparison
{
    public IReadOnlyList<NamedSearchResult<TSolution>> Compare<TSolution>(
        ISearchProblem<TSolution> problem,
        IEnumerable<(string Name, ISearchSolver<TSolution> Solver)> solvers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(solvers);

        var results = new List<NamedSearchResult<TSolution>>();

        foreach (var (name, solver) in solvers)
        {
            ArgumentNullException.ThrowIfNull(solver);

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "Solver name cannot be empty.",
                    nameof(solvers));
            }

            cancellationToken.ThrowIfCancellationRequested();

            results.Add(
                new NamedSearchResult<TSolution>(
                    name,
                    solver.Solve(problem, cancellationToken)));
        }

        return results;
    }

    /// <summary>Best first. Equal results share a rank (1, 1, 3).</summary>
    public IReadOnlyList<RankedSearchResult<TSolution>> Rank<TSolution>(
        ISearchProblem<TSolution> problem,
        IEnumerable<NamedSearchResult<TSolution>> results)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(results);

        var comparer = new SolutionComparer(problem.ObjectiveType);

        var ordered = results
            .OrderBy(
                entry => entry.Result.Evaluation,
                Comparer<EvaluationResult>.Create((a, b) =>
                    comparer.IsBetter(a, b) ? -1 :
                    comparer.IsBetter(b, a) ? 1 : 0))
            .ToList();

        var ranked = new List<RankedSearchResult<TSolution>>();

        for (var i = 0; i < ordered.Count; i++)
        {
            var rank = i + 1;

            if (i > 0)
            {
                var previous = ordered[i - 1].Result.Evaluation;
                var current = ordered[i].Result.Evaluation;

                if (!comparer.IsBetter(previous, current) &&
                    !comparer.IsBetter(current, previous))
                {
                    rank = ranked[i - 1].Rank;
                }
            }

            ranked.Add(new RankedSearchResult<TSolution>(rank, ordered[i]));
        }

        return ranked;
    }
}
