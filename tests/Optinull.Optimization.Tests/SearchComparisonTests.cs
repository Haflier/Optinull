using Optinull.Domain.Evaluation;
using Optinull.Domain.Objectives;
using Optinull.Optimization.Randomness;
using Optinull.Optimization.Search;

namespace Optinull.Optimization.Tests;

public sealed class SearchComparisonTests
{
    private static List<(string Name, ISearchSolver<int> Solver)> Solvers(
        params (string Name, double Objective)[] entries) =>
        entries
            .Select(entry => (
                entry.Name,
                (ISearchSolver<int>)new FixedSolver(entry.Objective)))
            .ToList();

    [Fact]
    public void Compare_RunsEverySolverInOrder()
    {
        var results = new SearchComparison().Compare(
            new FixedProblem(ObjectiveType.Minimize),
            Solvers(("a", 3), ("b", 1), ("c", 2)));

        Assert.Equal(
            ["a", "b", "c"],
            results.Select(result => result.SolverName));
    }

    [Fact]
    public void Compare_RejectsEmptySolverName()
    {
        Assert.Throws<ArgumentException>(() =>
            new SearchComparison().Compare(
                new FixedProblem(ObjectiveType.Minimize),
                Solvers((" ", 1))));
    }

    [Fact]
    public void Compare_WithCancelledToken_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            new SearchComparison().Compare(
                new FixedProblem(ObjectiveType.Minimize),
                Solvers(("a", 1)),
                cts.Token));
    }

    [Fact]
    public void Rank_Minimize_PlacesLowestObjectiveFirst()
    {
        var problem = new FixedProblem(ObjectiveType.Minimize);
        var comparison = new SearchComparison();

        var ranked = comparison.Rank(
            problem,
            comparison.Compare(problem, Solvers(("a", 30), ("b", 10), ("c", 20))));

        Assert.Equal(["b", "c", "a"], ranked.Select(r => r.Entry.SolverName));
        Assert.Equal([1, 2, 3], ranked.Select(r => r.Rank));
    }

    [Fact]
    public void Rank_Maximize_PlacesHighestObjectiveFirst()
    {
        var problem = new FixedProblem(ObjectiveType.Maximize);
        var comparison = new SearchComparison();

        var ranked = comparison.Rank(
            problem,
            comparison.Compare(problem, Solvers(("a", 30), ("b", 10), ("c", 20))));

        Assert.Equal(["a", "c", "b"], ranked.Select(r => r.Entry.SolverName));
    }

    [Fact]
    public void Rank_EqualObjectives_ShareRank()
    {
        var problem = new FixedProblem(ObjectiveType.Minimize);
        var comparison = new SearchComparison();

        var ranked = comparison.Rank(
            problem,
            comparison.Compare(problem, Solvers(("a", 10), ("b", 10), ("c", 20))));

        Assert.Equal([1, 1, 3], ranked.Select(r => r.Rank));
    }

    private sealed class FixedProblem(ObjectiveType objectiveType) : ISearchProblem<int>
    {
        public ObjectiveType ObjectiveType { get; } = objectiveType;

        public int CreateRandom(IRandomSource random) => 0;

        public EvaluationResult Evaluate(int solution) => new(solution, true, 0);

        public int RandomNeighbor(int solution, IRandomSource random) => solution;

        public IEnumerable<int> Neighbors(int solution) => [];
    }

    private sealed class FixedSolver(double objective) : ISearchSolver<int>
    {
        public SearchResult<int> Solve(
            ISearchProblem<int> problem,
            CancellationToken cancellationToken = default) =>
            new(0, new EvaluationResult(objective, true, 0), 1, TimeSpan.Zero, []);
    }
}
