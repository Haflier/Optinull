using System.Globalization;
using Optinull.Application.Optimization;
using Optinull.Problems.JobShop;
using Optinull.Problems.JobShop.Benchmarks;
using Optinull.Problems.Knapsack;
using Optinull.Problems.Tsp;

namespace Optinull.Application.Tests.Tuning;

/// <summary>
/// Manual experiments for choosing the automatic settings. Each one runs a solver over
/// several seeds on a fixed set of problems for a list of values of one setting, and prints
/// the mean result per problem. The first row, "auto", is what the engine chooses today.
/// Read the table, then change the rules in <c>SolverTuning</c> (and the problem defaults in
/// the solve services) where a different value clearly wins on most problems.
///
/// Remove the Skip of one experiment to run it, with
/// dotnet test --filter SolverTuningExperiments --logger "console;verbosity=detailed".
/// A sweep takes about a minute or two, mostly on FT10.
/// </summary>
public sealed class SolverTuningExperiments
{
    private const string Skipped =
        "Manual tuning experiment; remove Skip to run it (a minute or two). Reads: lower is better " +
        "for ft06, ft10 and the TSPs, higher is better for knap100.";

    private const int Seeds = 10;

    private static readonly JobShopProblem Ft06 = JobShopBenchmarkInstances.Ft06();
    private static readonly JobShopProblem Ft10 = JobShopBenchmarkInstances.Ft10();
    private static readonly TspProblem Random50 = TspBenchmarkInstances.RandomCities(50, seed: 7);
    private static readonly TspProblem Grid8 = TspBenchmarkInstances.Grid(8);
    private static readonly KnapsackProblem Knapsack100 = KnapsackInstances.Random(100, seed: 11);

    private delegate double Score(SolverKind kind, SolverTuning tuning, int seed, int? iterations);

    private static readonly (string Name, Score Run)[] Problems =
    [
        ("ft06", (kind, tuning, seed, iterations) =>
            new JobShopSolveService().Solve(Ft06, kind, seed, iterations: iterations, tuning: tuning).Makespan),
        ("ft10", (kind, tuning, seed, iterations) =>
            new JobShopSolveService().Solve(Ft10, kind, seed, iterations: iterations, tuning: tuning).Makespan),
        ("random50", (kind, tuning, seed, iterations) =>
            new TspSolveService().Solve(Random50, kind, seed, iterations: iterations, tuning: tuning).Length),
        ("grid8", (kind, tuning, seed, iterations) =>
            new TspSolveService().Solve(Grid8, kind, seed, iterations: iterations, tuning: tuning).Length),
        ("knap100", (kind, tuning, seed, iterations) =>
            new KnapsackSolveService().Solve(Knapsack100, kind, seed, iterations: iterations, tuning: tuning).Value)
    ];

    private static string Cell(double value) =>
        value.ToString("0.0", CultureInfo.InvariantCulture).PadLeft(11);

    private static void Sweep(
        string title,
        SolverKind kind,
        IReadOnlyList<(string Label, SolverTuning Tuning)> rows,
        int? iterations = null)
    {
        Console.WriteLine();
        Console.WriteLine($"{title}  ({Seeds} seeds, mean of the best result)");
        Console.WriteLine("setting".PadRight(24) + string.Concat(Problems.Select(problem => problem.Name.PadLeft(11))));

        var all = new List<(string Label, SolverTuning Tuning)> { ("auto", SolverTuning.None) };
        all.AddRange(rows);

        foreach (var (label, tuning) in all)
        {
            var cells = Problems.Select(problem =>
                Enumerable.Range(1, Seeds).Average(seed => problem.Run(kind, tuning, seed, iterations)));

            Console.WriteLine(label.PadRight(24) + string.Concat(cells.Select(Cell)));
        }
    }

    private static IReadOnlyList<(string, SolverTuning)> Rows(
        string name,
        double[] values,
        Func<double, SolverTuning> tuning) =>
        values
            .Select(value => (
                $"{name}={value.ToString("0.########", CultureInfo.InvariantCulture)}",
                tuning(value)))
            .ToList();

    // ---- Simulated annealing -----------------------------------------------------------

    [Fact(Skip = Skipped)]
    public void SimulatedAnnealing_Temperature() =>
        Sweep(
            "SA temperature (times the problem's usual start)",
            SolverKind.SimulatedAnnealing,
            Rows("temperature", [0.05, 0.1, 0.25, 0.5, 2, 4, 10], value => new SolverTuning { Temperature = value }));

    [Fact(Skip = Skipped)]
    public void SimulatedAnnealing_FinalTemperature() =>
        Sweep(
            "SA final temperature (fraction of the start)",
            SolverKind.SimulatedAnnealing,
            Rows("final", [0.01, 0.001, 0.0001, 0.000001], value => new SolverTuning { Final = value }));

    [Fact(Skip = Skipped)]
    public void SimulatedAnnealing_MovesPerTemperature() =>
        Sweep(
            "SA moves per temperature",
            SolverKind.SimulatedAnnealing,
            Rows("moves", [10, 30, 150, 300], value => new SolverTuning { Moves = (int)value }));

    // ---- Genetic algorithm -------------------------------------------------------------

    [Fact(Skip = Skipped)]
    public void GeneticAlgorithm_Population() =>
        Sweep(
            "GA population",
            SolverKind.GeneticAlgorithm,
            Rows("population", [10, 20, 30, 80, 120], value => new SolverTuning { Population = (int)value }));

    [Fact(Skip = Skipped)]
    public void GeneticAlgorithm_Mutation() =>
        Sweep(
            "GA mutation",
            SolverKind.GeneticAlgorithm,
            Rows("mutation", [0.02, 0.05, 0.1, 0.4, 0.6], value => new SolverTuning { Mutation = value }));

    [Fact(Skip = Skipped)]
    public void GeneticAlgorithm_LocalSearch() =>
        Sweep(
            "GA local search tries per child",
            SolverKind.GeneticAlgorithm,
            Rows("localsearch", [0, 5, 20, 60, 120], value => new SolverTuning { LocalSearch = (int)value }));

    // ---- Iterated local search ---------------------------------------------------------

    [Fact(Skip = Skipped)]
    public void IteratedLocalSearch_Perturbation() =>
        Sweep(
            "ILS perturbation",
            SolverKind.IteratedLocalSearch,
            Rows("perturbation", [1, 2, 5, 8, 12, 20], value => new SolverTuning { Perturbation = (int)value }));

    // ---- Does the budget rule help? -----------------------------------------------------
    // Compares what the engine chooses for a budget with the fixed values the solvers used
    // before the settings became automatic.

    [Fact(Skip = Skipped)]
    public void Budget_AnnealingMoves_AutoVersusFixed90()
    {
        foreach (var budget in new[] { 2_000, 5_000, 20_000, 60_000 })
        {
            Sweep(
                $"SA at {budget} evaluations: auto moves against a fixed 90",
                SolverKind.SimulatedAnnealing,
                [("moves=90 (old fixed)", new SolverTuning { Moves = 90 })],
                budget);
        }
    }

    [Fact(Skip = Skipped)]
    public void Budget_GeneticPopulation_AutoVersusFixed()
    {
        foreach (var budget in new[] { 2_000, 5_000, 20_000, 60_000 })
        {
            Sweep(
                $"GA at {budget} evaluations: auto population against a fixed 50 and a fixed 30",
                SolverKind.GeneticAlgorithm,
                [
                    ("population=50 (old TSP)", new SolverTuning { Population = 50 }),
                    ("population=30 (old job shop)", new SolverTuning { Population = 30 })
                ],
                budget);
        }
    }
}
