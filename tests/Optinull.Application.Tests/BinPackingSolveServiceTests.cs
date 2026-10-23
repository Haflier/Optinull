using Optinull.Application.Optimization;
using Optinull.Problems.BinPacking;
using Xunit.Abstractions;

namespace Optinull.Application.Tests;

public sealed class BinPackingSolveServiceTests
{
    private readonly ITestOutputHelper _output;

    public BinPackingSolveServiceTests(ITestOutputHelper output) => _output = output;

    private static BinPackingProblem Small() => new([60, 40, 50, 50, 30, 70], capacity: 100);

    [Theory]
    [InlineData(SolverKind.SimulatedAnnealing)]
    [InlineData(SolverKind.IteratedLocalSearch)]
    [InlineData(SolverKind.GeneticAlgorithm)]
    public void SmallProblem_ReachesTheLowerBound_SoItIsOptimal(SolverKind kind)
    {
        var problem = Small();

        var result = new BinPackingSolveService().Solve(problem, kind, seed: 42, iterations: 5000);

        Assert.Equal(problem.LowerBound, result.Bins);
    }

    [Theory]
    [InlineData(SolverKind.Greedy)]
    [InlineData(SolverKind.HillClimbing)]
    [InlineData(SolverKind.IteratedLocalSearch)]
    [InlineData(SolverKind.SimulatedAnnealing)]
    [InlineData(SolverKind.GeneticAlgorithm)]
    public void EverySolver_ReturnsAFeasiblePlan_BetweenTheBoundAndOneBinPerItem(SolverKind kind)
    {
        var problem = BinPackingInstances.Random(40, 100, 10, 50, seed: 11);

        var result = new BinPackingSolveService().Solve(problem, kind, seed: 1, iterations: 2000);

        Assert.True(result.Search.Evaluation.IsFeasible);
        Assert.InRange(result.Bins, problem.LowerBound, problem.ItemCount);

        // The objective is the bin count plus a fraction below one.
        Assert.Equal(result.Bins, (int)Math.Floor(result.Search.Evaluation.ObjectiveValue));
    }

    [Fact]
    public void Greedy_MatchesTheFirstFitDecreasingBaseline()
    {
        var problem = BinPackingInstances.Random(60, 100, 10, 50, seed: 3);

        var result = new BinPackingSolveService().Solve(problem, SolverKind.Greedy);

        Assert.Equal(result.GreedyBins, result.Bins);
    }

    [Fact]
    public void SameSeed_GivesTheSamePacking()
    {
        var problem = BinPackingInstances.Random(40, 100, 10, 50, seed: 11);
        var service = new BinPackingSolveService();

        var first = service.Solve(problem, SolverKind.SimulatedAnnealing, seed: 5, iterations: 2000);
        var second = service.Solve(problem, SolverKind.SimulatedAnnealing, seed: 5, iterations: 2000);

        Assert.Equal(first.Plan.Assignment, second.Plan.Assignment);
    }

    // Prints how far each solver is from the lower bound, to judge the starting temperature.
    [Theory]
    [InlineData(SolverKind.SimulatedAnnealing, 60)]
    [InlineData(SolverKind.GeneticAlgorithm, 60)]
    [InlineData(SolverKind.SimulatedAnnealing, 120)]
    [InlineData(SolverKind.GeneticAlgorithm, 120)]
    public void Diagnostics_BinsAgainstTheLowerBound_AcrossSeeds(SolverKind kind, int items)
    {
        var problem = BinPackingInstances.Random(items, 100, 10, 50, seed: 11);

        _output.WriteLine($"{items} items, lower bound {problem.LowerBound}, {kind}");

        foreach (var seed in new[] { 1, 2, 3, 4, 5 })
        {
            var result = new BinPackingSolveService().Solve(problem, kind, seed);

            _output.WriteLine($"  seed {seed}: {result.Bins} bins (greedy {result.GreedyBins})");

            Assert.InRange(result.Bins, problem.LowerBound, problem.ItemCount);
        }
    }
}
