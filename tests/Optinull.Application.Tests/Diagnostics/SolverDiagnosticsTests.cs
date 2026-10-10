using Optinull.Application.Optimization;
using Optinull.Optimization.Search;
using Optinull.Problems.Knapsack;
using Optinull.Problems.Tsp;
using Xunit.Abstractions;

namespace Optinull.Application.Tests.Diagnostics;

/// <summary>
/// Runs the solvers over several seeds and prints how they did, so you can see
/// the real numbers (dotnet test --logger "console;verbosity=detailed"). The
/// quality thresholds are deliberately loose; they flag a broken solver, not a
/// slightly weaker one.
/// </summary>
public sealed class SolverDiagnosticsTests
{
    private static readonly int[] Seeds = Enumerable.Range(1, 10).ToArray();

    private readonly ITestOutputHelper _output;

    public SolverDiagnosticsTests(ITestOutputHelper output) => _output = output;

    // ---- Knapsack ----------------------------------------------------------

    [Theory]
    [InlineData(SolverKind.SimulatedAnnealing, 20)]
    [InlineData(SolverKind.GeneticAlgorithm, 20)]
    [InlineData(SolverKind.SimulatedAnnealing, 50)]
    [InlineData(SolverKind.GeneticAlgorithm, 50)]
    [InlineData(SolverKind.SimulatedAnnealing, 100)]
    [InlineData(SolverKind.GeneticAlgorithm, 100)]
    public void Knapsack_GapToTheExactOptimum_AcrossSeeds(SolverKind kind, int items)
    {
        var problem = KnapsackInstances.Random(items, seed: 11);
        var optimum = KnapsackExactSolver.Optimum(problem);
        var gaps = new List<double>();

        _output.WriteLine($"Knapsack, {items} items, capacity {problem.Capacity}, optimum {optimum}, {kind}");

        foreach (var seed in Seeds)
        {
            var result = new KnapsackSolveService().Solve(problem, kind, seed);
            var gap = (optimum - result.Value) / (double)optimum;

            gaps.Add(gap);

            _output.WriteLine(
                $"  seed {seed,2}: value {result.Value,5}  weight {result.Weight,5}  " +
                $"gap {gap,6:P2}  greedy {result.GreedyValue,5:0}  evals {result.Search.EvaluationCount}");

            Assert.True(result.Weight <= problem.Capacity, $"seed {seed} overfilled the knapsack.");
            Assert.True(result.Value <= optimum, $"seed {seed} beat the exact optimum, so the grader or solver is wrong.");
        }

        _output.WriteLine($"  mean gap {gaps.Average():P2}, worst {gaps.Max():P2}");

        Assert.True(gaps.Average() <= 0.06, $"Mean gap {gaps.Average():P2} is above 6%.");
        Assert.True(gaps.Max() <= 0.15, $"Worst gap {gaps.Max():P2} is above 15%.");
    }

    [Theory]
    [InlineData(SolverKind.SimulatedAnnealing)]
    [InlineData(SolverKind.GeneticAlgorithm)]
    public void Knapsack_MoreIterations_AreNotWorseOnAverage(SolverKind kind)
    {
        var problem = KnapsackInstances.Random(100, seed: 11);

        double Mean(int iterations) =>
            Seeds.Average(seed =>
                new KnapsackSolveService().Solve(problem, kind, seed, iterations: iterations).Value);

        var small = Mean(300);
        var large = Mean(20_000);

        _output.WriteLine($"{kind}: mean value {small:0.0} at 300 iterations, {large:0.0} at 20000");

        Assert.True(large >= small);
    }

    [Theory]
    [InlineData(SolverKind.SimulatedAnnealing)]
    [InlineData(SolverKind.GeneticAlgorithm)]
    public void Knapsack_ConvergenceTrace_OnlyImproves_AndEndsAtTheBest(SolverKind kind)
    {
        var problem = KnapsackInstances.Random(50, seed: 4);

        var result = new KnapsackSolveService().Solve(problem, kind, seed: 3);

        AssertTrace(result.Search.Convergence, result.Value, maximize: true);
    }

    // ---- TSP ---------------------------------------------------------------

    [Theory]
    [InlineData(SolverKind.SimulatedAnnealing, "circle20", 0.05, 0.15)]
    [InlineData(SolverKind.GeneticAlgorithm, "circle20", 0.05, 0.15)]
    [InlineData(SolverKind.SimulatedAnnealing, "grid8", 0.30, 0.50)]
    [InlineData(SolverKind.GeneticAlgorithm, "grid8", 1.00, 1.30)]
    public void Tsp_GapToTheKnownOptimum_AcrossSeeds(
        SolverKind kind,
        string instance,
        double allowedMeanGap,
        double allowedWorstGap)
    {
        var (problem, optimum) = instance == "circle20"
            ? (TspBenchmarkInstances.Circle(20), TspBenchmarkInstances.CircleOptimum(20))
            : (TspBenchmarkInstances.Grid(8), TspBenchmarkInstances.GridOptimum(8));

        var gaps = new List<double>();

        _output.WriteLine($"TSP {instance}, optimum {optimum:0.0}, {kind}");

        foreach (var seed in Seeds)
        {
            var result = new TspSolveService().Solve(problem, kind, seed);
            var gap = (result.Length - optimum) / optimum;

            gaps.Add(gap);

            _output.WriteLine(
                $"  seed {seed,2}: length {result.Length,8:0.0}  gap {gap,6:P2}  " +
                $"nearest neighbour {result.GreedyLength,8:0.0}  evals {result.Search.EvaluationCount}");

            AssertValidTour(problem, result);
            Assert.True(result.Length >= optimum - 1e-6, $"seed {seed} beat the known optimum.");
        }

        _output.WriteLine($"  mean gap {gaps.Average():P2}, worst {gaps.Max():P2}");

        Assert.True(gaps.Average() <= allowedMeanGap, $"Mean gap {gaps.Average():P2} is above {allowedMeanGap:P0}.");
        Assert.True(gaps.Max() <= allowedWorstGap, $"Worst gap {gaps.Max():P2} is above {allowedWorstGap:P0}.");
    }

    [Theory]
    [InlineData(SolverKind.SimulatedAnnealing)]
    [InlineData(SolverKind.GeneticAlgorithm)]
    public void Tsp_OnRandomCities_StaysCloseToOrBeatsNearestNeighbour(SolverKind kind)
    {
        var problem = TspBenchmarkInstances.RandomCities(50, seed: 7);
        var lengths = new List<double>();
        var greedy = 0.0;

        foreach (var seed in Seeds)
        {
            var result = new TspSolveService().Solve(problem, kind, seed);

            AssertValidTour(problem, result);
            lengths.Add(result.Length);
            greedy = result.GreedyLength;
        }

        _output.WriteLine(
            $"{kind} on random50: mean {lengths.Average():0.0}, best {lengths.Min():0.0}, " +
            $"worst {lengths.Max():0.0}, nearest neighbour {greedy:0.0}");

        // Annealing should beat the heuristic. The genetic algorithm is only held
        // to a loose bound (a random tour is several times longer).
        var allowed = kind == SolverKind.SimulatedAnnealing ? greedy : greedy * 2.0;

        Assert.True(
            lengths.Average() <= allowed,
            $"Mean {lengths.Average():0.0} is above {allowed:0.0}.");
    }

    [Theory]
    [InlineData(SolverKind.SimulatedAnnealing)]
    [InlineData(SolverKind.GeneticAlgorithm)]
    public void Tsp_OnEightCities_FindsTheBruteForceOptimumMostOfTheTime(SolverKind kind)
    {
        var problem = TspBenchmarkInstances.RandomCities(8, seed: 3);
        var optimum = BruteForceTspOptimum(problem);
        var hits = 0;

        foreach (var seed in Seeds)
        {
            var result = new TspSolveService().Solve(problem, kind, seed);

            Assert.True(result.Length >= optimum - 1e-6, $"seed {seed} beat the brute-force optimum.");

            if (result.Length <= optimum + 1e-6)
                hits++;
        }

        _output.WriteLine($"{kind}: optimum {optimum:0.00} found on {hits} of {Seeds.Length} seeds");

        Assert.True(hits >= 7, $"Only {hits} of {Seeds.Length} seeds found the optimum.");
    }

    [Theory]
    [InlineData(SolverKind.SimulatedAnnealing)]
    [InlineData(SolverKind.GeneticAlgorithm)]
    public void Tsp_ConvergenceTrace_OnlyImproves_AndEndsAtTheBest(SolverKind kind)
    {
        var problem = TspBenchmarkInstances.RandomCities(40, seed: 2);

        var result = new TspSolveService().Solve(problem, kind, seed: 5);

        AssertTrace(result.Search.Convergence, result.Length, maximize: false);
    }

    [Fact]
    public void Tsp_GeneticAlgorithm_SameSeedGivesTheSameTour()
    {
        var problem = TspBenchmarkInstances.RandomCities(30, seed: 1);
        var service = new TspSolveService();

        Assert.Equal(
            service.Solve(problem, SolverKind.GeneticAlgorithm, seed: 4).Tour.Cities,
            service.Solve(problem, SolverKind.GeneticAlgorithm, seed: 4).Tour.Cities);
    }

    // ---- Helpers -----------------------------------------------------------

    private static void AssertTrace(
        IReadOnlyList<ConvergencePoint> trace,
        double finalValue,
        bool maximize)
    {
        Assert.NotEmpty(trace);

        for (var i = 1; i < trace.Count; i++)
        {
            Assert.True(trace[i].Evaluations > trace[i - 1].Evaluations);

            if (maximize)
                Assert.True(trace[i].Objective > trace[i - 1].Objective);
            else
                Assert.True(trace[i].Objective < trace[i - 1].Objective);
        }

        Assert.Equal(finalValue, trace[^1].Objective, 6);
    }

    private static void AssertValidTour(TspProblem problem, TspSolveResult result)
    {
        Assert.Equal(
            Enumerable.Range(0, problem.CityCount),
            result.Tour.Cities.OrderBy(city => city));

        Assert.Equal(
            TspEvaluator.Evaluate(problem, result.Tour).ObjectiveValue,
            result.Length,
            6);
    }

    private static double BruteForceTspOptimum(TspProblem problem)
    {
        var count = problem.CityCount;
        var best = double.MaxValue;
        var order = new int[count];
        var used = new bool[count];

        void Extend(int position, double length)
        {
            if (length >= best)
                return;

            if (position == count)
            {
                best = Math.Min(best, length + problem.Distance(order[count - 1], order[0]));
                return;
            }

            for (var city = 1; city < count; city++)
            {
                if (used[city])
                    continue;

                used[city] = true;
                order[position] = city;
                Extend(position + 1, length + problem.Distance(order[position - 1], city));
                used[city] = false;
            }
        }

        order[0] = 0;
        used[0] = true;
        Extend(1, 0);

        return best;
    }
}
