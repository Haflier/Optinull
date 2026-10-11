using Optinull.Application.Optimization;
using Optinull.Problems.JobShop.Benchmarks;
using Optinull.Problems.Knapsack;
using Optinull.Problems.Tsp;

namespace Optinull.Application.Tests.Wizard;

public sealed class AllSolversServiceTests
{
    private sealed class ListProgress : IProgress<double>
    {
        private readonly List<double> _values = [];

        public IReadOnlyList<double> Values
        {
            get { lock (_values) return _values.ToArray(); }
        }

        public void Report(double value)
        {
            lock (_values)
                _values.Add(value);
        }
    }

    [Fact]
    public void Tsp_Greedy_IsTheNearestNeighbourTour()
    {
        var problem = TspBenchmarkInstances.RandomCities(30, seed: 3);

        var result = new TspSolveService().Solve(problem, SolverKind.Greedy);

        Assert.Equal("Greedy", result.SolverName);
        Assert.Equal(result.GreedyLength, result.Length, 6);
        Assert.True(result.Search.EvaluationCount >= 1);
    }

    [Fact]
    public void Tsp_HillClimbing_ReturnsAValidTour()
    {
        var problem = TspBenchmarkInstances.RandomCities(25, seed: 3);

        var result = new TspSolveService().Solve(problem, SolverKind.HillClimbing, seed: 2);

        Assert.Equal("Hill Climbing", result.SolverName);
        Assert.Equal(Enumerable.Range(0, 25), result.Tour.Cities.OrderBy(city => city));
        Assert.Equal(TspEvaluator.Evaluate(problem, result.Tour).ObjectiveValue, result.Length, 6);
    }

    [Fact]
    public void Knapsack_Greedy_MatchesTheBaseline_AndHillClimbingFits()
    {
        var problem = KnapsackInstances.Random(40, seed: 4);
        var service = new KnapsackSolveService();

        var greedy = service.Solve(problem, SolverKind.Greedy);
        var climbing = service.Solve(problem, SolverKind.HillClimbing, seed: 3);

        Assert.Equal(greedy.GreedyValue, (double)greedy.Value);
        Assert.True(climbing.Weight <= problem.Capacity);
        Assert.True(climbing.Value <= KnapsackExactSolver.Optimum(problem));
    }

    [Theory]
    [InlineData(SolverKind.Greedy)]
    [InlineData(SolverKind.HillClimbing)]
    public void JobShop_Ft06_FastSolversGiveAConsistentSchedule(SolverKind kind)
    {
        var problem = JobShopBenchmarkInstances.Ft06();

        var result = new JobShopSolveService().Solve(problem, kind, seed: 1);

        Assert.True(result.Makespan >= 55);
        Assert.Equal(result.Makespan, result.Schedule.Makespan);
        Assert.Equal(36, result.Schedule.Operations.Count);
    }

    [Theory]
    [InlineData(SolverKind.SimulatedAnnealing)]
    [InlineData(SolverKind.GeneticAlgorithm)]
    public void Progress_GrowsToTheBudget_ForTheBudgetedSolvers(SolverKind kind)
    {
        var progress = new ListProgress();
        var problem = TspBenchmarkInstances.RandomCities(20, seed: 1);

        new TspSolveService().Solve(problem, kind, seed: 1, iterations: 1000, progress: progress);

        var values = progress.Values;

        Assert.NotEmpty(values);
        Assert.All(values, value => Assert.InRange(value, 0.0, 1.0));
        Assert.Equal(values.OrderBy(value => value), values);
        Assert.True(values[^1] >= 0.9, $"Last progress was {values[^1]}.");
    }

    [Theory]
    [InlineData(SolverKind.Greedy)]
    [InlineData(SolverKind.HillClimbing)]
    public void Progress_IsNotReported_ForSolversWithoutABudget(SolverKind kind)
    {
        var progress = new ListProgress();
        var problem = TspBenchmarkInstances.RandomCities(20, seed: 1);

        new TspSolveService().Solve(problem, kind, seed: 1, progress: progress);

        Assert.Empty(progress.Values);
    }
}
