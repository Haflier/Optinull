using Optinull.Domain.Problems;
using Optinull.Domain.Solutions;
using Optinull.Optimization.Randomness;
using Optinull.Optimization.Search;

namespace Optinull.Optimization.Tests;

public sealed class GeneticAlgorithmSearchTests
{
    // maximize x + y, both integers in [0, 10]: optimum is 20.
    private static OptimizationSearchProblem CreateProblem()
    {
        var problem = new OptimizationProblem("max-sum");
        var x = problem.AddIntegerVariable("x", 0, 10);
        var y = problem.AddIntegerVariable("y", 0, 10);
        problem.Maximize(x + y);

        return new OptimizationSearchProblem(problem);
    }

    [Fact]
    public void Solve_Maximize_FindsOptimum()
    {
        var result = new GeneticAlgorithm<Solution>(
                generations: 50,
                random: new RandomSource(4))
            .Solve(CreateProblem());

        Assert.Equal(20, result.Evaluation.ObjectiveValue);
    }

    [Fact]
    public void Solve_SameSeed_IsReproducible()
    {
        var first = new GeneticAlgorithm<Solution>(random: new RandomSource(9))
            .Solve(CreateProblem());

        var second = new GeneticAlgorithm<Solution>(random: new RandomSource(9))
            .Solve(CreateProblem());

        Assert.Equal(first.EvaluationCount, second.EvaluationCount);
        Assert.Equal(first.Convergence, second.Convergence);
    }

    [Fact]
    public void Solve_CountsOnlyNewEvaluations()
    {
        // population + generations * (population - elite)
        var result = new GeneticAlgorithm<Solution>(
                populationSize: 20,
                generations: 10,
                eliteCount: 2,
                random: new RandomSource(1))
            .Solve(CreateProblem());

        Assert.Equal(20 + 10 * 18, result.EvaluationCount);
    }

    [Fact]
    public void Solve_WithCancelledToken_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            new GeneticAlgorithm<Solution>(random: new RandomSource(1))
                .Solve(CreateProblem(), cts.Token));
    }

    [Fact]
    public void Solve_ThroughInterface_WithoutRecombination_Throws()
    {
        ISearchSolver<int> solver = new GeneticAlgorithm<int>();

        Assert.Throws<InvalidOperationException>(() =>
            solver.Solve(new NoRecombinationProblem()));
    }

    [Theory]
    [InlineData(0, 10, 1, 0.1, 3)]
    [InlineData(10, 0, 1, 0.1, 3)]
    [InlineData(10, 10, 11, 0.1, 3)]
    [InlineData(10, 10, 1, 1.5, 3)]
    [InlineData(10, 10, 1, 0.1, 0)]
    public void Constructor_RejectsInvalidArguments(
        int populationSize,
        int generations,
        int eliteCount,
        double mutationRate,
        int tournamentSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GeneticAlgorithm<Solution>(
                populationSize,
                generations,
                eliteCount,
                mutationRate,
                tournamentSize));
    }

    // A problem with search but no crossover/mutation.
    private sealed class NoRecombinationProblem : ISearchProblem<int>
    {
        public Optinull.Domain.Objectives.ObjectiveType ObjectiveType =>
            Optinull.Domain.Objectives.ObjectiveType.Minimize;

        public int CreateRandom(IRandomSource random) => 0;

        public Optinull.Domain.Evaluation.EvaluationResult Evaluate(int solution) =>
            new(solution, true, 0);

        public int RandomNeighbor(int solution, IRandomSource random) => solution;

        public IEnumerable<int> Neighbors(int solution) => [];
    }
}
