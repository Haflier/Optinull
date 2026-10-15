#!/usr/bin/env bash
# Automatic per-problem solver settings (your values override them), settings shown in results, tuning sweeps.
set -e
[ -f Optinull.sln ] || { echo "Run this in the repo root"; exit 1; }
mkdir -p "src/Optinull.Application"
cat > "src/Optinull.Application/AssemblyInfo.cs" <<'OPTINULL_EOF'
using System.Runtime.CompilerServices;

// The tests check the automatic-settings rules directly, without running a solver for each case.
[assembly: InternalsVisibleTo("Optinull.Application.Tests")]
OPTINULL_EOF
mkdir -p "src/Optinull.Application/Optimization"
cat > "src/Optinull.Application/Optimization/SolverTuning.cs" <<'OPTINULL_EOF'
using System.Globalization;

namespace Optinull.Application.Optimization;

/// <summary>Raised when a tuning combination cannot work, with a message meant for the chat.</summary>
public sealed class TuningException : Exception
{
    public TuningException(string message) : base(message)
    {
    }
}

/// <summary>
/// Solver settings a user may change from the chat. Null keeps the solver's own default.
/// Every setting is independent of the problem's scale: the temperature is a multiple of
/// the problem's usual starting temperature, and the rest are counts or fractions.
/// </summary>
public sealed record SolverTuning
{
    public static SolverTuning None { get; } = new();

    // Simulated annealing
    public double? Temperature { get; init; }
    public double? Final { get; init; }
    public int? Moves { get; init; }

    // Genetic algorithm
    public int? Population { get; init; }
    public double? Mutation { get; init; }
    public int? Elite { get; init; }
    public int? Tournament { get; init; }
    public int? LocalSearch { get; init; }

    // Iterated local search
    public int? Perturbation { get; init; }

    public bool IsEmpty =>
        Temperature is null && Final is null && Moves is null &&
        Population is null && Mutation is null && Elite is null &&
        Tournament is null && LocalSearch is null &&
        Perturbation is null;

    /// <summary>The settings that were changed, for example "temperature=2, mutation=0.3".</summary>
    public string Describe(bool tagSolvers = false)
    {
        var parts = new List<string>();

        void Add(string name, double? value, string tag)
        {
            if (value is { } number)
            {
                var text = $"{name}={number.ToString("0.########", CultureInfo.InvariantCulture)}";

                parts.Add(tagSolvers ? $"{text} ({tag})" : text);
            }
        }

        Add("temperature", Temperature, "SA");
        Add("final", Final, "SA");
        Add("moves", Moves, "SA");
        Add("population", Population, "GA");
        Add("mutation", Mutation, "GA");
        Add("elite", Elite, "GA");
        Add("tournament", Tournament, "GA");
        Add("localsearch", LocalSearch, "GA");
        Add("perturbation", Perturbation, "ILS");

        return string.Join(", ", parts);
    }

    // ---- Automatic settings -------------------------------------------------------------
    // Whatever the user did not set is chosen here from the problem and the budget, and
    // whatever the user did set is used as given. The rules are written so that at the
    // default budget they give the same values the solvers have always had.

    /// <summary>Temperature levels the default schedule has: ln(1e-5) / ln(0.95), rounded.</summary>
    private const double DefaultTemperatureLevels = 222.0;

    /// <summary>
    /// Generations the genetic algorithm should at least get, so a small budget is not
    /// spent on evaluating a population that never has time to evolve.
    /// </summary>
    private const int MinimumGenerations = 20;

    /// <summary>Simulated annealing: the problem's usual start scaled by the user's temperature.</summary>
    internal AnnealingSettings Annealing(double usualStartTemperature, int budget)
    {
        // The same number of temperature levels at any budget (the default schedule has
        // about 222), so a small budget gets few moves per level and a large one many.
        var autoMoves = Math.Clamp((int)Math.Round(budget / DefaultTemperatureLevels), 5, 1000);

        var moves = Moves ?? autoMoves;
        var final = Final ?? AnnealingBudget.DefaultFinalRatio;

        return new AnnealingSettings(
            usualStartTemperature * (Temperature ?? 1.0),
            moves,
            final,
            string.Join(", ", new[]
            {
                Mark("temperature", Temperature ?? 1.0, Temperature is not null),
                Mark("final", final, Final is not null),
                Mark("moves", moves, Moves is not null)
            }));
    }

    /// <summary>
    /// The genetic settings. The problem supplies its own defaults; the population is shrunk
    /// for a small budget, and the elite and tournament follow the population.
    /// </summary>
    internal GeneticSettings Genetic(
        int defaultPopulation,
        double defaultMutation,
        int defaultTournament,
        int defaultLocalSearch,
        int budget)
    {
        var localSearch = LocalSearch ?? defaultLocalSearch;

        // A child costs one evaluation plus its local search tries.
        var affordable = budget / (MinimumGenerations * (localSearch + 1));

        var population = Population ?? Math.Clamp(affordable, Math.Min(10, defaultPopulation), defaultPopulation);
        var elite = Elite ?? Math.Min(population, Math.Max(2, population / 25));
        var tournament = Tournament ?? Math.Min(defaultTournament, population);
        var mutation = Mutation ?? defaultMutation;

        // These depend on each other, and the automatic values differ per problem and budget,
        // so they can only be checked here.
        if (elite > population)
            throw new TuningException($"elite ({elite}) cannot be more than population ({population}).");

        if (tournament > population)
            throw new TuningException($"tournament ({tournament}) cannot be more than population ({population}).");

        return new GeneticSettings(
            population,
            elite,
            mutation,
            tournament,
            localSearch,
            string.Join(", ", new[]
            {
                Mark("population", population, Population is not null),
                Mark("elite", elite, Elite is not null),
                Mark("mutation", mutation, Mutation is not null),
                Mark("tournament", tournament, Tournament is not null),
                Mark("localsearch", localSearch, LocalSearch is not null)
            }));
    }

    /// <summary>
    /// Iterated local search: roughly one random move per 20 parts of the problem, between
    /// 3 and 8. Every benchmark up to about 60 parts keeps the long-standing 3.
    /// </summary>
    internal PerturbationSettings PerturbationFor(int problemSize)
    {
        var size = Perturbation ?? Math.Clamp((int)Math.Round(problemSize / 20.0), 3, 8);

        return new PerturbationSettings(size, Mark("perturbation", size, Perturbation is not null));
    }

    private static string Mark(string name, double value, bool chosenByUser) =>
        $"{name}={value.ToString("0.########", CultureInfo.InvariantCulture)} " +
        (chosenByUser ? "(yours)" : "(auto)");
}

internal readonly record struct AnnealingSettings(
    double InitialTemperature,
    int Moves,
    double FinalRatio,
    string Applied);

internal readonly record struct GeneticSettings(
    int Population,
    int Elite,
    double Mutation,
    int Tournament,
    int LocalSearch,
    string Applied);

internal readonly record struct PerturbationSettings(int Size, string Applied);

/// <summary>One setting of a solver, for parsing, validation and the help text.</summary>
public sealed record TuningKey(
    string Name,
    SolverKind Solver,
    string Description,
    double Min,
    double Max,
    bool Whole,
    string Default);

/// <summary>The settings users can change, and how a chat command becomes a <see cref="SolverTuning"/>.</summary>
public static class TuningCatalog
{
    public static IReadOnlyList<TuningKey> Keys { get; } =
    [
        new("temperature", SolverKind.SimulatedAnnealing, "start temperature, times the usual one", 0.01, 100, false, "1"),
        new("final", SolverKind.SimulatedAnnealing, "end temperature, as a fraction of the start", 0.00000001, 0.5, false, "0.00001"),
        new("moves", SolverKind.SimulatedAnnealing, "moves tried at each temperature", 1, 10_000, true, "90"),
        new("population", SolverKind.GeneticAlgorithm, "individuals per generation", 2, 500, true, "50, job shop 30"),
        new("mutation", SolverKind.GeneticAlgorithm, "chance that a child is mutated", 0, 1, false, "0.2"),
        new("elite", SolverKind.GeneticAlgorithm, "best individuals kept unchanged", 0, 100, true, "2"),
        new("tournament", SolverKind.GeneticAlgorithm, "contestants per parent choice", 1, 50, true, "3"),
        new("localsearch", SolverKind.GeneticAlgorithm, "local search tries per child", 0, 200, true, "10, job shop 30"),
        new("perturbation", SolverKind.IteratedLocalSearch, "random moves between restarts", 1, 50, true, "3")
    ];

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["temp"] = "temperature",
        ["end"] = "final",
        ["pop"] = "population",
        ["mut"] = "mutation",
        ["ls"] = "localsearch",
        ["perturb"] = "perturbation"
    };

    /// <summary>The setting's own name for a key or its short form; null if it is not a tuning setting.</summary>
    public static string? Canonical(string key)
    {
        var lower = key.ToLowerInvariant();

        if (Keys.Any(candidate => candidate.Name == lower))
            return lower;

        return Aliases.TryGetValue(lower, out var name) ? name : null;
    }

    /// <summary>The allowed range for the help text, for example "0.01-100".</summary>
    public static string Range(TuningKey key) => $"{Show(key.Min)}-{Show(key.Max)}";

    public static string Code(SolverKind solver) =>
        solver switch
        {
            SolverKind.SimulatedAnnealing => "sa",
            SolverKind.GeneticAlgorithm => "ga",
            SolverKind.IteratedLocalSearch => "ils",
            SolverKind.HillClimbing => "hc",
            _ => "greedy"
        };

    /// <summary>
    /// Turns the settings found in a command into a tuning. With a named solver they must all
    /// belong to it; with "all" each goes to its own solver.
    /// </summary>
    public static bool TryBuild(
        IReadOnlyDictionary<string, string> given,
        SolverKind? solver,
        bool compare,
        out SolverTuning? tuning,
        out string? error)
    {
        tuning = null;
        error = null;

        if (given.Count == 0)
            return true;

        var effective = solver ?? SolverKind.SimulatedAnnealing;
        var built = SolverTuning.None;

        foreach (var (name, raw) in given)
        {
            var key = Keys.First(candidate => candidate.Name == name);

            if (!compare)
            {
                if (effective is SolverKind.Greedy or SolverKind.HillClimbing)
                {
                    error = $"{SolverNames.Display(effective)} has no settings to tune.";
                    return false;
                }

                if (key.Solver != effective)
                {
                    error = solver is null
                        ? $"{name} tunes {SolverNames.Display(key.Solver)}; add {Code(key.Solver)} to the command."
                        : $"{name} tunes {SolverNames.Display(key.Solver)}, not {SolverNames.Display(effective)}.";
                    return false;
                }
            }

            if (!TryRead(key, raw, out var number))
            {
                error = key.Whole
                    ? $"{name} must be a whole number from {Show(key.Min)} to {Show(key.Max)}."
                    : $"{name} must be a number from {Show(key.Min)} to {Show(key.Max)}.";
                return false;
            }

            built = Apply(built, name, number);
        }

        tuning = built;
        return true;
    }

    private static bool TryRead(TuningKey key, string raw, out double number)
    {
        if (key.Whole)
        {
            var ok = int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var whole);
            number = whole;

            return ok && number >= key.Min && number <= key.Max;
        }

        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out number) &&
               double.IsFinite(number) &&
               number >= key.Min &&
               number <= key.Max;
    }

    private static SolverTuning Apply(SolverTuning tuning, string name, double number) =>
        name switch
        {
            "temperature" => tuning with { Temperature = number },
            "final" => tuning with { Final = number },
            "moves" => tuning with { Moves = (int)number },
            "population" => tuning with { Population = (int)number },
            "mutation" => tuning with { Mutation = number },
            "elite" => tuning with { Elite = (int)number },
            "tournament" => tuning with { Tournament = (int)number },
            "localsearch" => tuning with { LocalSearch = (int)number },
            "perturbation" => tuning with { Perturbation = (int)number },
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };

    private static string Show(double value) =>
        value.ToString("0.########", CultureInfo.InvariantCulture);
}
OPTINULL_EOF
mkdir -p "src/Optinull.Application/Optimization"
cat > "src/Optinull.Application/Optimization/TspSolveService.cs" <<'OPTINULL_EOF'
using Optinull.Optimization.Randomness;
using Optinull.Optimization.Search;
using Optinull.Problems.Tsp;

namespace Optinull.Application.Optimization;

public sealed record TspSolveResult(
    string SolverName,
    TspTour Tour,
    double Length,
    double GreedyLength,
    SearchResult<TspTour> Search,
    string? Settings = null);

/// <summary>
/// Solves a TSP with the chosen solver at a fixed evaluation budget. The settings
/// are untuned starting points; the temperature is scaled to the instance because
/// tour lengths differ wildly between instances.
/// </summary>
public sealed class TspSolveService
{
    public const int EvaluationBudget = SolveOptions.DefaultBudget;

    public TspSolveResult Solve(
        TspProblem problem,
        SolverKind kind = SolverKind.SimulatedAnnealing,
        int seed = SolveOptions.DefaultSeed,
        CancellationToken cancellationToken = default,
        int? iterations = null,
        IProgress<double>? progress = null,
        SolverTuning? tuning = null)
    {
        ArgumentNullException.ThrowIfNull(problem);

        var searchProblem = new TspSearchProblem(problem);
        var random = new RandomSource(seed);
        var tune = tuning ?? SolverTuning.None;

        // Only the budgeted solvers have a meaningful progress.
        var budgeted = kind is SolverKind.SimulatedAnnealing
            or SolverKind.GeneticAlgorithm
            or SolverKind.IteratedLocalSearch;
        var tracked = new ProgressTrackingProblem<TspTour>(
            searchProblem,
            iterations ?? EvaluationBudget,
            budgeted ? progress : null);

        var greedyLength = TspEvaluator
            .Evaluate(problem, searchProblem.CreateGreedy(searchProblem.Evaluate))
            .ObjectiveValue;

        string name;
        string? settings = null;
        SearchResult<TspTour> search;

        switch (kind)
        {
            case SolverKind.Greedy:
                name = SolverNames.Display(kind);
                search = new GreedyBaseline<TspTour>().Solve(tracked, cancellationToken);
                break;

            case SolverKind.HillClimbing:
                name = SolverNames.Display(kind);
                search = new HillClimbing<TspTour>(random).Solve(tracked, cancellationToken);
                break;

            case SolverKind.IteratedLocalSearch:
                var perturb = tune.PerturbationFor(problem.CityCount);

                settings = perturb.Applied;
                name = SolverNames.Display(kind);
                search = new IteratedLocalSearch<TspTour>(
                        perturbationSize: perturb.Size,
                        iterationCount: int.MaxValue,
                        random: random,
                        maxEvaluations: iterations ?? EvaluationBudget)
                    .Solve(tracked, cancellationToken);
                break;

            case SolverKind.SimulatedAnnealing:
            {
                // Start at the average edge of the nearest-neighbour tour and end
                // 100,000 times colder: 225 steps of 90 moves, about 20,000 evaluations.
                var start = greedyLength / problem.CityCount;

                if (start <= 0)
                    start = 1;

                var sa = tune.Annealing(start, iterations ?? EvaluationBudget);

                settings = sa.Applied;

                name = SolverNames.Display(kind);
                search = new SimulatedAnnealing<TspTour>(
                        initialTemperature: sa.InitialTemperature,
                        coolingRate: AnnealingBudget.CoolingRate(iterations, sa.Moves, sa.FinalRatio),
                        iterationsPerTemperature: sa.Moves,
                        minimumTemperature: sa.InitialTemperature * sa.FinalRatio,
                        random: random)
                    .Solve(tracked, cancellationToken);
                break;
            }

            case SolverKind.GeneticAlgorithm:
                var ga = tune.Genetic(50, 0.2, 3, 10, iterations ?? EvaluationBudget);

                settings = ga.Applied;

                name = SolverNames.Display(kind);
                search = new GeneticAlgorithm<TspTour>(
                        populationSize: ga.Population,
                        generations: 1_000_000,
                        eliteCount: ga.Elite,
                        mutationRate: ga.Mutation,
                        tournamentSize: ga.Tournament,
                        localSearchTries: ga.LocalSearch,
                        maxEvaluations: iterations ?? EvaluationBudget,
                        random: random)
                    .Solve(tracked, cancellationToken);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }

        return new TspSolveResult(
            name,
            search.Best,
            search.Evaluation.ObjectiveValue,
            greedyLength,
            search,
            settings);
    }
}
OPTINULL_EOF
mkdir -p "src/Optinull.Application/Optimization"
cat > "src/Optinull.Application/Optimization/KnapsackSolveService.cs" <<'OPTINULL_EOF'
using Optinull.Optimization.Randomness;
using Optinull.Optimization.Search;
using Optinull.Problems.Knapsack;

namespace Optinull.Application.Optimization;

public sealed record KnapsackSolveResult(
    string SolverName,
    KnapsackSelection Selection,
    long Value,
    long Weight,
    double GreedyValue,
    SearchResult<KnapsackSelection> Search,
    string? Settings = null);

/// <summary>
/// Solves a knapsack with the chosen solver at a fixed evaluation budget. The settings
/// are untuned starting points; the temperature is scaled to the item values.
/// </summary>
public sealed class KnapsackSolveService
{
    public const int EvaluationBudget = SolveOptions.DefaultBudget;

    public KnapsackSolveResult Solve(
        KnapsackProblem problem,
        SolverKind kind = SolverKind.SimulatedAnnealing,
        int seed = SolveOptions.DefaultSeed,
        CancellationToken cancellationToken = default,
        int? iterations = null,
        IProgress<double>? progress = null,
        SolverTuning? tuning = null)
    {
        ArgumentNullException.ThrowIfNull(problem);

        var searchProblem = new KnapsackSearchProblem(problem);
        var random = new RandomSource(seed);
        var tune = tuning ?? SolverTuning.None;

        var budgeted = kind is SolverKind.SimulatedAnnealing
            or SolverKind.GeneticAlgorithm
            or SolverKind.IteratedLocalSearch;
        var tracked = new ProgressTrackingProblem<KnapsackSelection>(
            searchProblem,
            iterations ?? EvaluationBudget,
            budgeted ? progress : null);

        var greedyValue = KnapsackEvaluator
            .Evaluate(problem, searchProblem.CreateGreedy(searchProblem.Evaluate))
            .ObjectiveValue;

        string name;
        string? settings = null;
        SearchResult<KnapsackSelection> search;

        switch (kind)
        {
            case SolverKind.Greedy:
                name = SolverNames.Display(kind);
                search = new GreedyBaseline<KnapsackSelection>().Solve(tracked, cancellationToken);
                break;

            case SolverKind.HillClimbing:
                name = SolverNames.Display(kind);
                search = new HillClimbing<KnapsackSelection>(random).Solve(tracked, cancellationToken);
                break;

            case SolverKind.IteratedLocalSearch:
                var perturb = tune.PerturbationFor(problem.ItemCount);

                settings = perturb.Applied;
                name = SolverNames.Display(kind);
                search = new IteratedLocalSearch<KnapsackSelection>(
                        perturbationSize: perturb.Size,
                        iterationCount: int.MaxValue,
                        random: random,
                        maxEvaluations: iterations ?? EvaluationBudget)
                    .Solve(tracked, cancellationToken);
                break;

            case SolverKind.SimulatedAnnealing:
            {
                // A move changes the value by about one item's value, so start there.
                var start = Math.Max(1.0, problem.Items.Average(item => (double)item.Value));

                var sa = tune.Annealing(start, iterations ?? EvaluationBudget);

                settings = sa.Applied;

                name = SolverNames.Display(kind);
                search = new SimulatedAnnealing<KnapsackSelection>(
                        initialTemperature: sa.InitialTemperature,
                        coolingRate: AnnealingBudget.CoolingRate(iterations, sa.Moves, sa.FinalRatio),
                        iterationsPerTemperature: sa.Moves,
                        minimumTemperature: sa.InitialTemperature * sa.FinalRatio,
                        random: random)
                    .Solve(tracked, cancellationToken);
                break;
            }

            case SolverKind.GeneticAlgorithm:
                var ga = tune.Genetic(50, 0.2, 3, 10, iterations ?? EvaluationBudget);

                settings = ga.Applied;

                name = SolverNames.Display(kind);
                search = new GeneticAlgorithm<KnapsackSelection>(
                        populationSize: ga.Population,
                        generations: 1_000_000,
                        eliteCount: ga.Elite,
                        mutationRate: ga.Mutation,
                        tournamentSize: ga.Tournament,
                        localSearchTries: ga.LocalSearch,
                        maxEvaluations: iterations ?? EvaluationBudget,
                        random: random)
                    .Solve(tracked, cancellationToken);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }

        var (value, weight) = KnapsackEvaluator.Totals(problem, search.Best);

        return new KnapsackSolveResult(name, search.Best, value, weight, greedyValue, search, settings);
    }
}
OPTINULL_EOF
mkdir -p "src/Optinull.Application/Optimization"
cat > "src/Optinull.Application/Optimization/JobShopSolveService.cs" <<'OPTINULL_EOF'
using Optinull.Optimization.Randomness;
using Optinull.Optimization.Search;
using Optinull.Problems.JobShop;

namespace Optinull.Application.Optimization;

public sealed record JobShopSolveResult(
    string SolverName,
    JobShopSchedule Schedule,
    double Makespan,
    SearchResult<JobSequence> Search,
    string? Settings = null);

/// <summary>
/// Solves a job shop with the chosen solver. Simulated annealing is the
/// default: it beat the genetic algorithm on FT10 at an equal budget.
/// </summary>
public sealed class JobShopSolveService
{
    /// <summary>The start temperature simulated annealing has always used here.</summary>
    private const double UsualStartTemperature = 100.0;

    public JobShopSolveResult Solve(
        JobShopProblem problem,
        SolverKind kind = SolverKind.SimulatedAnnealing,
        int seed = SolveOptions.DefaultSeed,
        CancellationToken cancellationToken = default,
        int? iterations = null,
        IProgress<double>? progress = null,
        SolverTuning? tuning = null)
    {
        ArgumentNullException.ThrowIfNull(problem);

        var random = new RandomSource(seed);
        var budget = iterations ?? SolveOptions.DefaultBudget;
        var tune = tuning ?? SolverTuning.None;

        string name;
        string? settings = null;
        SearchResult<JobSequence> search;

        switch (kind)
        {
            case SolverKind.Greedy:
                name = SolverNames.Display(kind);
                search = new GreedyBaseline<JobSequence>()
                    .Solve(
                        new ProgressTrackingProblem<JobSequence>(
                            new JobShopSearchProblem(problem), budget, null),
                        cancellationToken);
                break;

            case SolverKind.HillClimbing:
                name = SolverNames.Display(kind);
                search = new HillClimbing<JobSequence>(random)
                    .Solve(
                        new ProgressTrackingProblem<JobSequence>(
                            new JobShopSearchProblem(problem), budget, null),
                        cancellationToken);
                break;

            case SolverKind.IteratedLocalSearch:
                var perturb = tune.PerturbationFor(problem.Jobs.Sum(job => job.Operations.Count));

                settings = perturb.Applied;
                name = SolverNames.Display(kind);
                search = new IteratedLocalSearch<JobSequence>(
                        perturbationSize: perturb.Size,
                        iterationCount: int.MaxValue,
                        random: random,
                        maxEvaluations: budget)
                    .Solve(
                        new ProgressTrackingProblem<JobSequence>(
                            new JobShopSearchProblem(problem), budget, progress),
                        cancellationToken);
                break;

            case SolverKind.SimulatedAnnealing:
                var sa = tune.Annealing(UsualStartTemperature, budget);

                settings = sa.Applied;

                name = SolverNames.Display(kind);
                search = new SimulatedAnnealing<JobSequence>(
                        initialTemperature: sa.InitialTemperature,
                        coolingRate: AnnealingBudget.CoolingRate(iterations, sa.Moves, sa.FinalRatio),
                        iterationsPerTemperature: sa.Moves,
                        minimumTemperature: sa.InitialTemperature * sa.FinalRatio,
                        random: random)
                    .Solve(
                        new ProgressTrackingProblem<JobSequence>(
                            new JobShopSearchProblem(problem), budget, progress),
                        cancellationToken);
                break;

            case SolverKind.GeneticAlgorithm:
                var ga = tune.Genetic(
                    JobShopGeneticAlgorithmPreset.PopulationSize,
                    JobShopGeneticAlgorithmPreset.MutationRate,
                    JobShopGeneticAlgorithmPreset.TournamentSize,
                    JobShopGeneticAlgorithmPreset.LocalSearchTries,
                    budget);

                settings = ga.Applied;

                name = SolverNames.Display(kind);
                search = JobShopGeneticAlgorithmPreset
                    .CreateSolver(
                        budget,
                        random,
                        ga.Population,
                        ga.Elite,
                        ga.Mutation,
                        ga.Tournament,
                        ga.LocalSearch)
                    .Solve(
                        new ProgressTrackingProblem<JobSequence>(
                            JobShopGeneticAlgorithmPreset.CreateProblem(problem), budget, progress),
                        cancellationToken);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }

        var schedule = new JobShopScheduleDecoder().Decode(problem, search.Best);

        return new JobShopSolveResult(
            name,
            schedule,
            search.Evaluation.ObjectiveValue,
            search,
            settings);
    }
}
OPTINULL_EOF
mkdir -p "src/Optinull.Application/Problems"
cat > "src/Optinull.Application/Problems/IProblemModule.cs" <<'OPTINULL_EOF'
using System.Diagnostics.CodeAnalysis;
using Optinull.Application.Optimization;
using Optinull.Reporting.Charts;

namespace Optinull.Application.Problems;

public sealed record ReportImage(string FileName, string Caption, byte[] Png);

/// <summary>The charts of one finished run, and an optional closing message.</summary>
public sealed record SolveReport(
    string SolverName,
    IReadOnlyList<ReportImage> Images,
    string? Summary = null);

/// <summary>
/// What one solver achieved. Rendering the charts is deferred, so a comparison
/// of several solvers only draws the ones it shows.
/// </summary>
public sealed record SolverOutcome(
    SolverKind Solver,
    string SolverName,
    double Score,
    TimeSpan Elapsed,
    int Evaluations,
    ConvergenceSeries Convergence,
    Func<IReadOnlyList<ReportImage>> Render,
    double? Baseline = null,
    string? Settings = null);

/// <summary>One concrete problem that can solve itself and describe the result.</summary>
public interface IProblemInstance
{
    /// <summary>Short name used in file names, for example "ft06" or "custom".</summary>
    string Name { get; }

    string Description { get; }

    /// <summary>What the score is called, for example "Makespan".</summary>
    string ScoreLabel { get; }

    bool HigherIsBetter { get; }

    SolverOutcome Run(SolverKind solver, SolveOptions options, CancellationToken cancellationToken);
}

public static class ProblemInstanceExtensions
{
    /// <summary>Runs one solver and renders its charts.</summary>
    public static SolveReport Solve(
        this IProblemInstance instance,
        SolverKind solver,
        SolveOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var outcome = instance.Run(solver, options, cancellationToken);

        return new SolveReport(
            outcome.SolverName,
            outcome.Render(),
            Summary(instance, outcome));
    }

    private static string Summary(IProblemInstance instance, SolverOutcome outcome)
    {
        var text = $"Optimization completed.\n{instance.ScoreLabel}: {outcome.Score:0.##}";

        if (outcome.Solver != SolverKind.Greedy &&
            outcome.Baseline is { } baseline && baseline != 0)
        {
            var improvement = instance.HigherIsBetter
                ? (outcome.Score - baseline) / baseline
                : (baseline - outcome.Score) / baseline;

            text += $"\nImprovement over greedy: {improvement:P1}";
        }

        // Everything the solver used: what the engine chose for this problem is marked (auto),
        // and what the user set is marked (yours).
        if (!string.IsNullOrEmpty(outcome.Settings))
            text += $"\nSettings: {outcome.Settings}";

        return text;
    }
}

/// <summary>A numeric key=value setting a module can generate an instance from.</summary>
public sealed record ProblemParameter(
    string Name,
    string Description,
    int Min,
    int Max,
    int Default);

/// <summary>
/// A ready-made choice in the /optimize wizard: either a built-in instance
/// (Target is its name) or a random one (Target is the problem id, plus Settings).
/// </summary>
public sealed record DatasetPreset(
    string Key,
    string Label,
    string Target,
    IReadOnlyDictionary<string, string>? Settings = null);

/// <summary>A kind of problem the bot can solve: built-in instances and a text format.</summary>
public interface IProblemModule
{
    /// <summary>Used in commands, for example "jobshop".</summary>
    string Id { get; }

    string Title { get; }

    IReadOnlyList<string> BuiltInNames { get; }

    string CustomInputHelp { get; }

    /// <summary>Settings for a generated instance, for example cities=30.</summary>
    IReadOnlyList<ProblemParameter> Parameters { get; }

    /// <summary>The Small / Medium / Large choices of the wizard.</summary>
    IReadOnlyList<DatasetPreset> Presets { get; }

    bool TryGetBuiltIn(
        string name,
        [NotNullWhen(true)] out IProblemInstance? instance);

    /// <summary>Starts the guided, step-by-step way to enter your own problem.</summary>
    IProblemDialog CreateDialog();

    bool TryParseCustom(
        string? text,
        [NotNullWhen(true)] out IProblemInstance? instance,
        out string? error);

    /// <summary>Builds a random instance from key=value settings; missing ones use defaults.</summary>
    bool TryGenerate(
        IReadOnlyDictionary<string, string> values,
        int seed,
        [NotNullWhen(true)] out IProblemInstance? instance,
        out string? error);
}
OPTINULL_EOF
mkdir -p "src/Optinull.Application/Problems"
cat > "src/Optinull.Application/Problems/TspModule.cs" <<'OPTINULL_EOF'
using System.Diagnostics.CodeAnalysis;
using Optinull.Application.Optimization;
using Optinull.Application.Reporting;
using Optinull.Problems.Tsp;
using Optinull.Reporting.Charts;

namespace Optinull.Application.Problems;

public sealed class TspModule : IProblemModule
{
    public string Id => "tsp";

    public string Title => "traveling salesman";

    public IReadOnlyList<string> BuiltInNames { get; } = ["circle20", "grid8", "random50"];

    public string CustomInputHelp
    {
        get
        {
            var limits = TspInputLimits.Default;

            return
                "one city per line as 'x y' (an optional leading id is ignored; " +
                "decimals use a dot). Example:\n0 0\n10 0\n10 10\n0 10\n" +
                $"Limit: {limits.MaxCities} cities.";
        }
    }

    public IReadOnlyList<ProblemParameter> Parameters { get; } =
    [
        new("cities", "number of random cities", 3, TspInputLimits.Default.MaxCities, 20)
    ];

    public IReadOnlyList<DatasetPreset> Presets { get; } =
    [
        new("small", "Small: 15 random cities", "tsp",
            new Dictionary<string, string> { ["cities"] = "15" }),
        new("medium", "Medium: random50 (50 cities)", "random50"),
        new("large", "Large: 100 random cities", "tsp",
            new Dictionary<string, string> { ["cities"] = "100" })
    ];

    public IProblemDialog CreateDialog() => TspDialog.Create(this);

    public bool TryGenerate(
        IReadOnlyDictionary<string, string> values,
        int seed,
        [NotNullWhen(true)] out IProblemInstance? instance,
        out string? error)
    {
        instance = null;

        if (!ProblemParameterReader.TryRead(Parameters, values, out var read, out error))
            return false;

        var cities = read["cities"];

        instance = new TspInstance(
            $"tsp{cities}",
            $"random tour, {cities} cities (seed {seed})",
            TspBenchmarkInstances.RandomCities(cities, seed),
            knownOptimum: null);

        return true;
    }

    public bool TryGetBuiltIn(
        string name,
        [NotNullWhen(true)] out IProblemInstance? instance)
    {
        switch (name.Trim().ToLowerInvariant())
        {
            case "circle20":
                instance = new TspInstance(
                    "circle20",
                    "circle20 (20 cities on a circle)",
                    TspBenchmarkInstances.Circle(20),
                    TspBenchmarkInstances.CircleOptimum(20));
                return true;

            case "grid8":
                instance = new TspInstance(
                    "grid8",
                    "grid8 (8 x 8 grid of cities)",
                    TspBenchmarkInstances.Grid(8),
                    TspBenchmarkInstances.GridOptimum(8));
                return true;

            case "random50":
                instance = new TspInstance(
                    "random50",
                    "random50 (50 random cities)",
                    TspBenchmarkInstances.RandomCities(50, seed: 7),
                    knownOptimum: null);
                return true;

            default:
                instance = null;
                return false;
        }
    }

    public bool TryParseCustom(
        string? text,
        [NotNullWhen(true)] out IProblemInstance? instance,
        out string? error)
    {
        instance = null;

        if (!TspTextParser.TryParse(text, TspInputLimits.Default, out var problem, out error))
            return false;

        instance = new TspInstance(
            "custom",
            $"custom tour, {problem!.CityCount} cities",
            problem,
            knownOptimum: null);

        return true;
    }
}

internal sealed class TspInstance : IProblemInstance
{
    private readonly TspProblem _problem;
    private readonly double? _knownOptimum;

    public TspInstance(
        string name,
        string description,
        TspProblem problem,
        double? knownOptimum)
    {
        Name = name;
        Description = description;
        _problem = problem;
        _knownOptimum = knownOptimum;
    }

    public string Name { get; }

    public string Description { get; }

    public string ScoreLabel => "Tour length";

    public bool HigherIsBetter => false;

    public SolverOutcome Run(
        SolverKind solver,
        SolveOptions options,
        CancellationToken cancellationToken)
    {
        var result = new TspSolveService().Solve(
            _problem,
            solver,
            options.Seed,
            cancellationToken,
            options.Iterations,
            options.Progress,
            options.Tuning);

        return new SolverOutcome(
            solver,
            result.SolverName,
            result.Length,
            result.Search.Elapsed,
            result.Search.EvaluationCount,
            ReportMapper.ToConvergenceSeries(result.SolverName, result.Search),
            () => RenderImages(result),
            Baseline: result.GreedyLength,
            Settings: result.Settings);
    }

    private IReadOnlyList<ReportImage> RenderImages(TspSolveResult result)
    {
        var route = new RouteChartRenderer().Render(
            _problem.Cities.Select(city => new RoutePoint(city.X, city.Y)).ToList(),
            result.Tour.Cities,
            $"{result.SolverName}: length {result.Length:0.0}");

        var convergence = new ConvergenceChartRenderer().Render(
            [ReportMapper.ToConvergenceSeries(result.SolverName, result.Search)],
            $"Convergence, {result.SolverName}");

        return
        [
            new ReportImage($"{Name}-route.png", Caption(result), route),
            new ReportImage($"{Name}-convergence.png", "Convergence", convergence)
        ];
    }

    private string Caption(TspSolveResult result)
    {
        var prefix = $"{Name} | {result.SolverName} | tour length {result.Length:0.0}";

        if (_knownOptimum is { } optimum)
        {
            var gap = (result.Length - optimum) / optimum;

            return $"{prefix} (optimal {optimum:0.0}, gap {gap:P1})";
        }

        var change = (result.GreedyLength - result.Length) / result.GreedyLength;

        return $"{prefix} (nearest neighbour {result.GreedyLength:0.0}, " +
               $"{Math.Abs(change):P1} {(change >= 0 ? "shorter" : "longer")})";
    }
}
OPTINULL_EOF
mkdir -p "src/Optinull.Application/Problems"
cat > "src/Optinull.Application/Problems/KnapsackModule.cs" <<'OPTINULL_EOF'
using System.Diagnostics.CodeAnalysis;
using Optinull.Application.Optimization;
using Optinull.Application.Reporting;
using Optinull.Problems.Knapsack;
using Optinull.Reporting.Charts;

namespace Optinull.Application.Problems;

public sealed class KnapsackModule : IProblemModule
{
    public string Id => "knapsack";

    public string Title => "0/1 knapsack";

    public IReadOnlyList<string> BuiltInNames { get; } = ["knap20", "knap100"];

    public string CustomInputHelp
    {
        get
        {
            var limits = KnapsackInputLimits.Default;

            return
                "first line: the capacity; then one item per line as 'value weight' " +
                "(whole numbers). Example:\n50\n60 10\n100 20\n120 30\n" +
                $"Limits: {limits.MaxItems} items, capacity up to {limits.MaxCapacity}.";
        }
    }

    public IReadOnlyList<ProblemParameter> Parameters { get; } =
    [
        new("items", "number of random items", 3, KnapsackInputLimits.Default.MaxItems, 30)
    ];

    public bool TryGetBuiltIn(
        string name,
        [NotNullWhen(true)] out IProblemInstance? instance)
    {
        switch (name.Trim().ToLowerInvariant())
        {
            case "knap20":
                instance = new KnapsackInstance(
                    "knap20",
                    "knap20 (20 items)",
                    KnapsackInstances.Random(20, seed: 11));
                return true;

            case "knap100":
                instance = new KnapsackInstance(
                    "knap100",
                    "knap100 (100 items)",
                    KnapsackInstances.Random(100, seed: 11));
                return true;

            default:
                instance = null;
                return false;
        }
    }

    public bool TryParseCustom(
        string? text,
        [NotNullWhen(true)] out IProblemInstance? instance,
        out string? error)
    {
        instance = null;

        if (!KnapsackTextParser.TryParse(text, KnapsackInputLimits.Default, out var problem, out error))
            return false;

        instance = new KnapsackInstance(
            "custom",
            $"custom knapsack, {problem!.ItemCount} items, capacity {problem.Capacity}",
            problem);

        return true;
    }

    public IReadOnlyList<DatasetPreset> Presets { get; } =
    [
        new("small", "Small: knap20 (20 items)", "knap20"),
        new("medium", "Medium: 50 random items", "knapsack",
            new Dictionary<string, string> { ["items"] = "50" }),
        new("large", "Large: knap100 (100 items)", "knap100")
    ];

    public IProblemDialog CreateDialog() => KnapsackDialog.Create(this);

    public bool TryGenerate(
        IReadOnlyDictionary<string, string> values,
        int seed,
        [NotNullWhen(true)] out IProblemInstance? instance,
        out string? error)
    {
        instance = null;

        if (!ProblemParameterReader.TryRead(Parameters, values, out var read, out error))
            return false;

        var items = read["items"];
        var problem = KnapsackInstances.Random(items, seed);

        instance = new KnapsackInstance(
            $"knapsack{items}",
            $"random knapsack, {items} items, capacity {problem.Capacity} (seed {seed})",
            problem);

        return true;
    }
}

internal sealed class KnapsackInstance : IProblemInstance
{
    private readonly KnapsackProblem _problem;

    public KnapsackInstance(string name, string description, KnapsackProblem problem)
    {
        Name = name;
        Description = description;
        _problem = problem;
    }

    public string Name { get; }

    public string Description { get; }

    public string ScoreLabel => "Value";

    public bool HigherIsBetter => true;

    public SolverOutcome Run(
        SolverKind solver,
        SolveOptions options,
        CancellationToken cancellationToken)
    {
        var result = new KnapsackSolveService().Solve(
            _problem,
            solver,
            options.Seed,
            cancellationToken,
            options.Iterations,
            options.Progress,
            options.Tuning);

        return new SolverOutcome(
            solver,
            result.SolverName,
            result.Value,
            result.Search.Elapsed,
            result.Search.EvaluationCount,
            ReportMapper.ToConvergenceSeries(result.SolverName, result.Search),
            () => RenderImages(result),
            Baseline: result.GreedyValue,
            Settings: result.Settings);
    }

    private IReadOnlyList<ReportImage> RenderImages(KnapsackSolveResult result)
    {
        var points = _problem.Items
            .Select((item, index) => new KnapsackPoint(item.Weight, item.Value, result.Selection[index]))
            .ToList();

        var items = new KnapsackChartRenderer().Render(
            points,
            $"{result.SolverName}: value {result.Value}, weight {result.Weight} of {_problem.Capacity}");

        var convergence = new ConvergenceChartRenderer().Render(
            [ReportMapper.ToConvergenceSeries(result.SolverName, result.Search)],
            $"Convergence, {result.SolverName}");

        return
        [
            new ReportImage($"{Name}-items.png", Caption(result), items),
            new ReportImage($"{Name}-convergence.png", "Convergence", convergence)
        ];
    }

    private string Caption(KnapsackSolveResult result)
    {
        // The optimum is cheap to compute exactly at these sizes.
        var optimum = KnapsackExactSolver.Optimum(_problem);
        var gap = (optimum - result.Value) / (double)optimum;

        return $"{Name} | {result.SolverName} | value {result.Value}, weight {result.Weight}/{_problem.Capacity} " +
               $"(optimal {optimum}, gap {gap:P1}; greedy {result.GreedyValue:0})";
    }
}
OPTINULL_EOF
mkdir -p "src/Optinull.Application/Problems"
cat > "src/Optinull.Application/Problems/JobShopModule.cs" <<'OPTINULL_EOF'
using System.Diagnostics.CodeAnalysis;
using Optinull.Application.Optimization;
using Optinull.Application.Reporting;
using Optinull.Problems.JobShop;
using Optinull.Problems.JobShop.Benchmarks;
using Optinull.Problems.JobShop.Parsing;

namespace Optinull.Application.Problems;

public sealed class JobShopModule : IProblemModule
{
    public string Id => "jobshop";

    public string Title => "job shop scheduling";

    public IReadOnlyList<string> BuiltInNames => JobShopBenchmarkCatalog.Names;

    public string CustomInputHelp
    {
        get
        {
            var limits = JobShopInputLimits.Default;

            return
                "first line: jobs and machines; then one line per job with " +
                "'machine time' pairs, machines numbered from 0. Example:\n" +
                "3 3\n0 3 1 2 2 2\n0 2 2 1 1 4\n1 4 2 3 0 1\n" +
                $"Limits: {limits.MaxJobs} jobs, {limits.MaxMachines} machines, " +
                $"{limits.MaxOperations} operations.";
        }
    }

    public IReadOnlyList<ProblemParameter> Parameters { get; } =
    [
        new("jobs", "number of random jobs", 1, JobShopInputLimits.Default.MaxJobs, 6),
        new("machines", "number of machines", 1, JobShopInputLimits.Default.MaxMachines, 6)
    ];

    public IReadOnlyList<DatasetPreset> Presets { get; } =
    [
        new("small", "Small: 5 jobs x 4 machines", "jobshop",
            new Dictionary<string, string> { ["jobs"] = "5", ["machines"] = "4" }),
        new("medium", "Medium: ft06 (6 x 6)", "ft06"),
        new("large", "Large: ft10 (10 x 10)", "ft10")
    ];

    public IProblemDialog CreateDialog() => JobShopDialog.Create(this);

    public bool TryGenerate(
        IReadOnlyDictionary<string, string> values,
        int seed,
        [NotNullWhen(true)] out IProblemInstance? instance,
        out string? error)
    {
        instance = null;

        if (!ProblemParameterReader.TryRead(Parameters, values, out var read, out error))
            return false;

        var jobs = read["jobs"];
        var machines = read["machines"];

        if (jobs * machines > JobShopInputLimits.Default.MaxOperations)
        {
            error = $"jobs x machines must be at most {JobShopInputLimits.Default.MaxOperations} operations.";
            return false;
        }

        instance = new JobShopInstance(
            $"jobshop{jobs}x{machines}",
            $"random job shop, {jobs} jobs x {machines} machines (seed {seed})",
            JobShopRandomInstances.Generate(jobs, machines, seed),
            knownOptimum: null);

        return true;
    }

    public bool TryGetBuiltIn(
        string name,
        [NotNullWhen(true)] out IProblemInstance? instance)
    {
        if (JobShopBenchmarkCatalog.TryGet(name, out var benchmark))
        {
            instance = new JobShopInstance(
                benchmark.Name,
                benchmark.Name,
                benchmark.Problem,
                benchmark.KnownOptimum);
            return true;
        }

        instance = null;
        return false;
    }

    public bool TryParseCustom(
        string? text,
        [NotNullWhen(true)] out IProblemInstance? instance,
        out string? error)
    {
        instance = null;

        if (!JobShopTextParser.TryParse(
                text,
                JobShopInputLimits.Default,
                out var problem,
                out error))
        {
            return false;
        }

        instance = new JobShopInstance(
            "custom",
            $"custom job shop, {problem!.Jobs.Count} jobs x {problem.MachineCount} machines",
            problem,
            knownOptimum: null);

        return true;
    }
}

internal sealed class JobShopInstance : IProblemInstance
{
    private readonly JobShopProblem _problem;
    private readonly double? _knownOptimum;

    public JobShopInstance(
        string name,
        string description,
        JobShopProblem problem,
        double? knownOptimum)
    {
        Name = name;
        Description = description;
        _problem = problem;
        _knownOptimum = knownOptimum;
    }

    public string Name { get; }

    public string Description { get; }

    public string ScoreLabel => "Makespan";

    public bool HigherIsBetter => false;

    public SolverOutcome Run(
        SolverKind solver,
        SolveOptions options,
        CancellationToken cancellationToken)
    {
        var result = new JobShopSolveService().Solve(
            _problem,
            solver,
            options.Seed,
            cancellationToken,
            options.Iterations,
            options.Progress,
            options.Tuning);

        return new SolverOutcome(
            solver,
            result.SolverName,
            result.Makespan,
            result.Search.Elapsed,
            result.Search.EvaluationCount,
            ReportMapper.ToConvergenceSeries(result.SolverName, result.Search),
            () => RenderImages(result),
            Settings: result.Settings);
    }

    private IReadOnlyList<ReportImage> RenderImages(JobShopSolveResult result)
    {
        var report = new JobShopReportService().Render(_problem, result);

        return
        [
            new ReportImage($"{Name}-gantt.png", Caption(result), report.GanttPng),
            new ReportImage($"{Name}-convergence.png", "Convergence", report.ConvergencePng)
        ];
    }

    private string Caption(JobShopSolveResult result)
    {
        var prefix = $"{Name} | {result.SolverName} | makespan {result.Makespan:0}";

        if (_knownOptimum is { } optimum)
        {
            var gap = (result.Makespan - optimum) / optimum;

            return $"{prefix} (optimal {optimum:0}, gap {gap:P1})";
        }

        // The optimum of a custom problem is unknown, so compare with a lower bound.
        var lowerBound = JobShopLowerBound.Compute(_problem);
        var above = (result.Makespan - lowerBound) / lowerBound;

        return $"{prefix} (lower bound {lowerBound:0}, at most {above:P1} above optimal)";
    }
}
OPTINULL_EOF
mkdir -p "tests/Optinull.Application.Tests/Tuning"
cat > "tests/Optinull.Application.Tests/Tuning/AutomaticSettingsTests.cs" <<'OPTINULL_EOF'
using Optinull.Application.Optimization;
using Optinull.Problems.JobShop.Benchmarks;
using Optinull.Problems.Knapsack;
using Optinull.Problems.Tsp;

namespace Optinull.Application.Tests.Tuning;

/// <summary>
/// By default the engine chooses the settings from the problem and the budget; whatever the
/// user sets replaces only that value. At the default budget the automatic values are the
/// ones the solvers have always had.
/// </summary>
public sealed class AutomaticSettingsTests
{
    private static string Tsp(SolverKind kind, int? iterations = null, SolverTuning? tuning = null, int cities = 20) =>
        new TspSolveService().Solve(
            TspBenchmarkInstances.RandomCities(cities, seed: 1), kind, seed: 1,
            iterations: iterations, tuning: tuning).Settings!;

    private static string Knapsack(SolverKind kind, int? iterations = null, int items = 20) =>
        new KnapsackSolveService().Solve(
            KnapsackInstances.Random(items, seed: 1), kind, seed: 1, iterations: iterations).Settings!;

    private static string JobShop(SolverKind kind, int? iterations = null) =>
        new JobShopSolveService().Solve(
            JobShopBenchmarkInstances.Ft06(), kind, seed: 1, iterations: iterations).Settings!;

    [Fact]
    public void AtTheDefaultBudget_TheAutomaticValuesAreTheLongStandingOnes()
    {
        Assert.Equal(
            "temperature=1 (auto), final=0.00001 (auto), moves=90 (auto)",
            Tsp(SolverKind.SimulatedAnnealing));

        Assert.Equal(
            "population=50 (auto), elite=2 (auto), mutation=0.2 (auto), tournament=3 (auto), localsearch=10 (auto)",
            Tsp(SolverKind.GeneticAlgorithm));

        Assert.Equal(
            "population=50 (auto), elite=2 (auto), mutation=0.2 (auto), tournament=3 (auto), localsearch=10 (auto)",
            Knapsack(SolverKind.GeneticAlgorithm));

        // The job shop has its own, FT06-tuned genetic settings.
        Assert.Equal(
            "population=30 (auto), elite=2 (auto), mutation=0.2 (auto), tournament=3 (auto), localsearch=30 (auto)",
            JobShop(SolverKind.GeneticAlgorithm));

        Assert.Equal(
            "temperature=1 (auto), final=0.00001 (auto), moves=90 (auto)",
            JobShop(SolverKind.SimulatedAnnealing));
    }

    [Fact]
    public void TheAnnealingScheduleKeepsItsShape_AtAnyBudget()
    {
        Assert.Contains("moves=5 (auto)", Tsp(SolverKind.SimulatedAnnealing, iterations: 500));
        Assert.Contains("moves=14 (auto)", Tsp(SolverKind.SimulatedAnnealing, iterations: 3_000));
        Assert.Contains("moves=90 (auto)", Tsp(SolverKind.SimulatedAnnealing, iterations: 20_000));
        Assert.Contains("moves=450 (auto)", Tsp(SolverKind.SimulatedAnnealing, iterations: 100_000));
    }

    [Theory]
    [InlineData(500)]
    [InlineData(3_000)]
    [InlineData(20_000)]
    [InlineData(100_000)]
    public void TheAutomaticSchedule_StillFitsTheBudget(int iterations)
    {
        var result = new TspSolveService().Solve(
            TspBenchmarkInstances.RandomCities(20, seed: 1), SolverKind.SimulatedAnnealing, seed: 1,
            iterations: iterations);

        Assert.InRange(result.Search.EvaluationCount, iterations * 0.8, iterations * 1.2);
    }

    [Fact]
    public void ASmallBudget_GetsASmallerPopulation_SoItHasTimeToEvolve()
    {
        Assert.Contains("population=10 (auto)", Tsp(SolverKind.GeneticAlgorithm, iterations: 1_000));
        Assert.Contains("population=22 (auto)", Tsp(SolverKind.GeneticAlgorithm, iterations: 5_000));
        Assert.Contains("population=50 (auto)", Tsp(SolverKind.GeneticAlgorithm, iterations: 20_000));

        // A larger budget does not grow it beyond the problem's default.
        Assert.Contains("population=50 (auto)", Tsp(SolverKind.GeneticAlgorithm, iterations: 100_000));
    }

    [Fact]
    public void TheEliteAndTournament_FollowAnAutomaticPopulation()
    {
        var small = Tsp(SolverKind.GeneticAlgorithm, iterations: 1_000);

        Assert.Contains("population=10 (auto)", small);
        Assert.Contains("elite=2 (auto)", small);
        Assert.Contains("tournament=3 (auto)", small);
    }

    [Fact]
    public void WhatTheUserSets_IsUsedAsGiven_AndTheRestStaysAutomatic()
    {
        var settings = Tsp(
            SolverKind.GeneticAlgorithm,
            tuning: new SolverTuning { Population = 20, Mutation = 0.35 });

        Assert.Contains("population=20 (yours)", settings);
        Assert.Contains("mutation=0.35 (yours)", settings);
        Assert.Contains("elite=2 (auto)", settings);
        Assert.Contains("tournament=3 (auto)", settings);
        Assert.Contains("localsearch=10 (auto)", settings);
    }

    [Fact]
    public void ALargeUserPopulation_IsNotShrunkByTheBudget()
    {
        var settings = Tsp(
            SolverKind.GeneticAlgorithm,
            iterations: 1_000,
            tuning: new SolverTuning { Population = 200 });

        Assert.Contains("population=200 (yours)", settings);
    }

    [Fact]
    public void ASmallUserPopulation_GetsAFittingAutomaticEliteAndTournament()
    {
        var settings = Tsp(
            SolverKind.GeneticAlgorithm,
            tuning: new SolverTuning { Population = 2 });

        Assert.Contains("population=2 (yours)", settings);
        Assert.Contains("elite=2 (auto)", settings);
        Assert.Contains("tournament=2 (auto)", settings);
    }

    [Fact]
    public void AnnealingOverrides_AreMarked_AndCombineWithTheAutomaticMoves()
    {
        var settings = Tsp(
            SolverKind.SimulatedAnnealing,
            iterations: 3_000,
            tuning: new SolverTuning { Temperature = 2 });

        Assert.Equal("temperature=2 (yours), final=0.00001 (auto), moves=14 (auto)", settings);
    }

    [Theory]
    [InlineData(1, 3)]
    [InlineData(20, 3)]
    [InlineData(50, 3)]
    [InlineData(60, 3)]
    [InlineData(100, 5)]
    [InlineData(150, 8)]
    [InlineData(200, 8)]
    [InlineData(100_000, 8)]
    public void ThePerturbationRule_GrowsWithTheProblem_ButStaysBetween3And8(int size, int expected)
    {
        var settings = SolverTuning.None.PerturbationFor(size);

        Assert.Equal(expected, settings.Size);
        Assert.Equal($"perturbation={expected} (auto)", settings.Applied);
    }

    [Fact]
    public void ThePerturbationRule_IsReachedByTheSolver() =>
        Assert.Equal("perturbation=3 (auto)", Tsp(SolverKind.IteratedLocalSearch, iterations: 500, cities: 20));

    [Theory]
    [InlineData(500, 5)]
    [InlineData(1_000, 5)]
    [InlineData(3_000, 14)]
    [InlineData(20_000, 90)]
    [InlineData(100_000, 450)]
    [InlineData(10_000_000, 1000)]
    public void TheAnnealingMovesRule_FollowsTheBudget_WithinItsLimits(int budget, int expected) =>
        Assert.Equal(expected, SolverTuning.None.Annealing(1.0, budget).Moves);

    [Theory]
    [InlineData(100, 10)]
    [InlineData(1_000, 10)]
    [InlineData(5_000, 22)]
    [InlineData(20_000, 50)]
    [InlineData(10_000_000, 50)]
    public void TheGeneticPopulationRule_FollowsTheBudget_NeverAboveTheProblemsDefault(int budget, int expected) =>
        Assert.Equal(expected, SolverTuning.None.Genetic(50, 0.2, 3, 10, budget).Population);

    [Theory]
    [InlineData(10, 2)]
    [InlineData(30, 2)]
    [InlineData(50, 2)]
    [InlineData(100, 4)]
    [InlineData(200, 8)]
    public void TheEliteRule_ScalesWithAnAutomaticPopulation(int population, int expected) =>
        Assert.Equal(
            expected,
            new SolverTuning { Population = population }.Genetic(50, 0.2, 3, 10, 20_000).Elite);

    [Fact]
    public void ATemperatureTheUserSets_ScalesTheProblemsUsualStart()
    {
        var settings = new SolverTuning { Temperature = 4 }.Annealing(usualStartTemperature: 25, budget: 20_000);

        Assert.Equal(100, settings.InitialTemperature);
        Assert.Equal(90, settings.Moves);
        Assert.Equal(0.00001, settings.FinalRatio);
    }

    [Fact]
    public void ThePerturbation_UsesTheItemCountOfAKnapsack_AndTheOperationsOfAJobShop()
    {
        Assert.Equal("perturbation=3 (auto)", Knapsack(SolverKind.IteratedLocalSearch, iterations: 500, items: 40));
        Assert.Equal("perturbation=5 (auto)", Knapsack(SolverKind.IteratedLocalSearch, iterations: 500, items: 100));
        Assert.Equal("perturbation=3 (auto)", JobShop(SolverKind.IteratedLocalSearch, iterations: 500));
    }

    [Fact]
    public void APerturbationTheUserSets_WinsOverTheAutomaticOne()
    {
        Assert.Equal("perturbation=7 (yours)", new SolverTuning { Perturbation = 7 }.PerturbationFor(1_000).Applied);
        Assert.Equal(
            "perturbation=7 (yours)",
            Tsp(SolverKind.IteratedLocalSearch, iterations: 500, tuning: new SolverTuning { Perturbation = 7 }));
    }

    [Fact]
    public void GreedyAndHillClimbing_HaveNoSettings()
    {
        var problem = TspBenchmarkInstances.RandomCities(20, seed: 1);

        Assert.Null(new TspSolveService().Solve(problem, SolverKind.Greedy).Settings);
        Assert.Null(new TspSolveService().Solve(problem, SolverKind.HillClimbing).Settings);
    }

    [Fact]
    public void ARunIsReproducible_WhenTheEngineChoosesTheSettings()
    {
        var problem = KnapsackInstances.Random(40, seed: 3);
        var service = new KnapsackSolveService();

        foreach (var kind in new[] { SolverKind.SimulatedAnnealing, SolverKind.GeneticAlgorithm, SolverKind.IteratedLocalSearch })
        {
            var first = service.Solve(problem, kind, seed: 5, iterations: 2_000);
            var second = service.Solve(problem, kind, seed: 5, iterations: 2_000);

            Assert.Equal(first.Value, second.Value);
            Assert.Equal(first.Settings, second.Settings);
        }
    }
}
OPTINULL_EOF
mkdir -p "tests/Optinull.Application.Tests/Tuning"
cat > "tests/Optinull.Application.Tests/Tuning/SolverTuningExperiments.cs" <<'OPTINULL_EOF'
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
OPTINULL_EOF
mkdir -p "tests/Optinull.Application.Tests/Tuning"
cat > "tests/Optinull.Application.Tests/Tuning/TuningCommandTests.cs" <<'OPTINULL_EOF'
using Optinull.Application.Commands;
using Optinull.Application.Jobs;
using Optinull.Application.Optimization;

namespace Optinull.Application.Tests.Tuning;

public sealed class TuningCommandTests
{
    private static async Task<(OptimizationJob Job, FakeReplies Replies)> QueueAsync(string text)
    {
        var queue = new JobQueue();
        var replies = new FakeReplies();

        await new BotCommandProcessor(queue).ProcessAsync(text, 1, replies);

        Assert.True(queue.Reader.TryRead(out var job), $"Nothing was queued: {string.Join(" | ", replies.Texts)}");

        return (job, replies);
    }

    [Fact]
    public async Task ASingleRun_ReportsEverySettingItUsed_MarkingWhoChoseIt()
    {
        var (job, replies) = await QueueAsync("/solve knap20 sa iterations=1000 temperature=2 final=0.001");

        await new JobExecutor().ExecuteAsync(job);

        Assert.Equal(JobStatus.Completed, job.Status);

        var summary = replies.Texts[^1];

        Assert.Contains("temperature=2 (yours)", summary);
        Assert.Contains("final=0.001 (yours)", summary);
        Assert.Contains("moves=5 (auto)", summary);
    }

    [Fact]
    public async Task ARunWithoutSettings_StillShowsWhatTheEngineChose()
    {
        var (job, replies) = await QueueAsync("/solve knap20 ga iterations=2000");

        await new JobExecutor().ExecuteAsync(job);

        var summary = replies.Texts[^1];

        Assert.Contains("Settings: population=", summary);
        Assert.Contains("(auto)", summary);
        Assert.DoesNotContain("(yours)", summary);
    }

    [Fact]
    public async Task GreedyAndHillClimbing_HaveNoSettingsToShow()
    {
        var (job, replies) = await QueueAsync("/solve knap20 greedy");

        await new JobExecutor().ExecuteAsync(job);

        Assert.DoesNotContain("Settings:", replies.Texts[^1]);
    }

    [Fact]
    public async Task ACompare_ShowsWhichSolverGotWhichSetting()
    {
        var (job, replies) = await QueueAsync("/compare knap20 iterations=500 mutation=0.3 temperature=2");

        await new JobExecutor().ExecuteAsync(job);

        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Contains("Settings: temperature=2 (SA), mutation=0.3 (GA)", replies.Pngs[0].Caption);
    }

    [Fact]
    public async Task TuningWorksWithAGeneratedInstance_AndAFixedOne()
    {
        var (generated, _) = await QueueAsync("/solve tsp cities=15 ga population=20 mutation=0.4 iterations=500");
        var (fixedOne, _) = await QueueAsync("/solve ft06 ils perturbation=5 iterations=500");

        Assert.Equal(20, generated.Command.Tuning!.Population);
        Assert.Equal(5, fixedOne.Command.Tuning!.Perturbation);
        Assert.Equal("15", generated.Command.ParameterValues["cities"]);
    }

    [Fact]
    public async Task SettingsThatCannotWorkTogether_StopTheJobWithAnExplanation()
    {
        var (job, replies) = await QueueAsync("/solve knap20 ga iterations=500 population=5 elite=9");

        await new JobExecutor().ExecuteAsync(job);

        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Contains("elite (9) cannot be more than population (5)", replies.Texts[^1]);
        Assert.DoesNotContain("Please try again", replies.Texts[^1]);
    }

    [Theory]
    [InlineData("/solve ft06 population=10", "add ga to the command")]
    [InlineData("/solve ft06 ga temperature=2", "temperature tunes Simulated Annealing")]
    [InlineData("/solve ft06 sa temperature=0", "temperature must be a number from 0.01 to 100")]
    [InlineData("/compare ft06 mutation=2", "mutation must be a number from 0 to 1")]
    public async Task BadSettings_AreAnsweredInTheChat_AndNothingIsQueued(string text, string reason)
    {
        var queue = new JobQueue();
        var replies = new FakeReplies();

        await new BotCommandProcessor(queue).ProcessAsync(text, 1, replies);

        Assert.Contains(reason, Assert.Single(replies.Texts));
        Assert.False(queue.Reader.TryRead(out _));
    }

    [Fact]
    public async Task Help_ListsEverySetting_AndStillFitsInOneTelegramMessage()
    {
        var replies = new FakeReplies();

        await new BotCommandProcessor(new JobQueue()).ProcessAsync("/help", 1, replies);

        var help = replies.Texts[0];

        foreach (var key in TuningCatalog.Keys)
            Assert.Contains($"{key.Name}={TuningCatalog.Range(key)}", help);

        Assert.Contains("sa temperature=0.01-100", help);
        Assert.True(help.Length < 4096, $"The help text is {help.Length} characters; Telegram allows 4096.");
    }

    [Fact]
    public void TheUsageHint_MentionsTuning() =>
        Assert.Contains("tuning", BotCommandParser.Usage);
}
OPTINULL_EOF
mkdir -p "tests/Optinull.Application.Tests/Tuning"
cat > "tests/Optinull.Application.Tests/Tuning/TuningSolveTests.cs" <<'OPTINULL_EOF'
using Optinull.Application.Optimization;
using Optinull.Problems.JobShop.Benchmarks;
using Optinull.Problems.Knapsack;
using Optinull.Problems.Tsp;

namespace Optinull.Application.Tests.Tuning;

public sealed class TuningSolveTests
{
    [Theory]
    [InlineData(SolverKind.SimulatedAnnealing)]
    [InlineData(SolverKind.GeneticAlgorithm)]
    [InlineData(SolverKind.IteratedLocalSearch)]
    public void EmptyTuning_ChangesNothing_OnEveryProblem(SolverKind kind)
    {
        var tsp = TspBenchmarkInstances.RandomCities(25, seed: 2);
        var knapsack = KnapsackInstances.Random(30, seed: 2);
        var jobShop = JobShopBenchmarkInstances.Ft06();

        Assert.Equal(
            new TspSolveService().Solve(tsp, kind, seed: 1, iterations: 1000).Length,
            new TspSolveService().Solve(tsp, kind, seed: 1, iterations: 1000, tuning: SolverTuning.None).Length);

        Assert.Equal(
            new KnapsackSolveService().Solve(knapsack, kind, seed: 1, iterations: 1000).Value,
            new KnapsackSolveService().Solve(knapsack, kind, seed: 1, iterations: 1000, tuning: SolverTuning.None).Value);

        Assert.Equal(
            new JobShopSolveService().Solve(jobShop, kind, seed: 1, iterations: 1000).Makespan,
            new JobShopSolveService().Solve(jobShop, kind, seed: 1, iterations: 1000, tuning: SolverTuning.None).Makespan);
    }

    [Fact]
    public void TheDefaultAnnealingSchedule_IsUnchanged_WithoutAnIterationCount()
    {
        var problem = TspBenchmarkInstances.RandomCities(20, seed: 1);

        var plain = new TspSolveService().Solve(problem, SolverKind.SimulatedAnnealing, seed: 4);
        var tuned = new TspSolveService().Solve(
            problem, SolverKind.SimulatedAnnealing, seed: 4,
            tuning: new SolverTuning { Temperature = 1, Moves = 90, Final = 0.00001 });

        Assert.Equal(plain.Length, tuned.Length);
        Assert.Equal(plain.Search.EvaluationCount, tuned.Search.EvaluationCount);
    }

    [Theory]
    [InlineData(10, 0.00001)]
    [InlineData(300, 0.00001)]
    [InlineData(90, 0.001)]
    [InlineData(90, 0.4)]
    [InlineData(5, 0.05)]
    public void ACustomSchedule_StillFitsTheEvaluationBudget(int moves, double final)
    {
        var problem = TspBenchmarkInstances.RandomCities(20, seed: 1);

        var result = new TspSolveService().Solve(
            problem, SolverKind.SimulatedAnnealing, seed: 1, iterations: 3000,
            tuning: new SolverTuning { Moves = moves, Final = final });

        // A schedule of whole temperature levels may end a little under or over the budget.
        Assert.InRange(result.Search.EvaluationCount, 2000, 4000);
    }

    [Fact]
    public void Temperature_ReachesTheAnnealer_OnEveryProblem()
    {
        var cold = new SolverTuning { Temperature = 0.01 };
        var hot = new SolverTuning { Temperature = 50 };

        var tsp = TspBenchmarkInstances.RandomCities(50, seed: 7);
        var jobShop = JobShopBenchmarkInstances.Ft10();
        var knapsack = KnapsackInstances.Random(100, seed: 11);

        Assert.NotEqual(
            new TspSolveService().Solve(tsp, SolverKind.SimulatedAnnealing, 1, iterations: 3000, tuning: cold).Length,
            new TspSolveService().Solve(tsp, SolverKind.SimulatedAnnealing, 1, iterations: 3000, tuning: hot).Length);

        Assert.NotEqual(
            new JobShopSolveService().Solve(jobShop, SolverKind.SimulatedAnnealing, 1, iterations: 3000, tuning: cold).Makespan,
            new JobShopSolveService().Solve(jobShop, SolverKind.SimulatedAnnealing, 1, iterations: 3000, tuning: hot).Makespan);

        Assert.NotEqual(
            new KnapsackSolveService().Solve(knapsack, SolverKind.SimulatedAnnealing, 1, iterations: 3000, tuning: cold).Value,
            new KnapsackSolveService().Solve(knapsack, SolverKind.SimulatedAnnealing, 1, iterations: 3000, tuning: hot).Value);
    }

    [Fact]
    public void GeneticSettings_ReachTheGeneticAlgorithm()
    {
        var tsp = TspBenchmarkInstances.RandomCities(50, seed: 7);

        double Length(SolverTuning tuning) =>
            new TspSolveService().Solve(tsp, SolverKind.GeneticAlgorithm, 1, iterations: 3000, tuning: tuning).Length;

        Assert.NotEqual(Length(new SolverTuning { Population = 10 }), Length(new SolverTuning { Population = 100 }));
        Assert.NotEqual(Length(new SolverTuning { Mutation = 0 }), Length(new SolverTuning { Mutation = 0.9 }));
        Assert.NotEqual(Length(new SolverTuning { LocalSearch = 0 }), Length(new SolverTuning { LocalSearch = 50 }));
    }

    [Fact]
    public void JobShopGeneticSettings_ReachThePreset_AndTheScheduleStaysValid()
    {
        var problem = JobShopBenchmarkInstances.Ft06();

        var result = new JobShopSolveService().Solve(
            problem, SolverKind.GeneticAlgorithm, seed: 2, iterations: 2000,
            tuning: new SolverTuning { Population = 10, Mutation = 0.5, Elite = 1, Tournament = 2, LocalSearch = 5 });

        Assert.True(result.Makespan >= 55);
        Assert.Equal(result.Makespan, result.Schedule.Makespan);
        Assert.Equal(36, result.Schedule.Operations.Count);
    }

    [Fact]
    public void Perturbation_ReachesIteratedLocalSearch()
    {
        // Iterated local search finishes its first climb before it looks at the budget, so
        // the instance is small and the budget large enough for several restarts. Both runs
        // may well end on the same best tour, but the restarts that led there differ.
        var tsp = TspBenchmarkInstances.RandomCities(25, seed: 7);

        TspSolveResult Run(int perturbation) =>
            new TspSolveService().Solve(
                tsp, SolverKind.IteratedLocalSearch, 1, iterations: 60_000,
                tuning: new SolverTuning { Perturbation = perturbation });

        var gentle = Run(1);
        var violent = Run(15);

        Assert.False(
            gentle.Length == violent.Length &&
            gentle.Search.EvaluationCount == violent.Search.EvaluationCount,
            "Perturbation 1 and 15 produced the same run, so the setting is not reaching the solver.");
    }

    [Fact]
    public void Perturbation_NeedsRoomForARestart_SoATinyBudgetOnALargeInstanceIgnoresIt()
    {
        // Documents a limit of the solver, not of the tuning: the first climb alone costs more
        // than the whole budget here, so no restart (and no perturbation) ever happens.
        var tsp = TspBenchmarkInstances.RandomCities(50, seed: 7);

        double Length(int perturbation) =>
            new TspSolveService().Solve(
                tsp, SolverKind.IteratedLocalSearch, 1, iterations: 500,
                tuning: new SolverTuning { Perturbation = perturbation }).Length;

        Assert.Equal(Length(1), Length(15));
    }

    [Fact]
    public void SolversWithoutSettings_IgnoreTuning()
    {
        var problem = TspBenchmarkInstances.RandomCities(20, seed: 3);
        var tuning = new SolverTuning { Temperature = 5, Population = 9 };

        foreach (var kind in new[] { SolverKind.Greedy, SolverKind.HillClimbing })
        {
            Assert.Equal(
                new TspSolveService().Solve(problem, kind, seed: 1).Length,
                new TspSolveService().Solve(problem, kind, seed: 1, tuning: tuning).Length);
        }
    }

    [Fact]
    public void ASolverIgnoresTheOtherSolversSettings_WhenAllRunTogether()
    {
        var problem = TspBenchmarkInstances.RandomCities(20, seed: 3);

        Assert.Equal(
            new TspSolveService().Solve(problem, SolverKind.SimulatedAnnealing, seed: 1, iterations: 1000).Length,
            new TspSolveService().Solve(
                problem, SolverKind.SimulatedAnnealing, seed: 1, iterations: 1000,
                tuning: new SolverTuning { Population = 9, Mutation = 0.9, Perturbation = 20 }).Length);
    }

    [Fact]
    public void EliteLargerThanThePopulation_IsRefusedWithAClearMessage()
    {
        var problem = KnapsackInstances.Random(20, seed: 1);

        var error = Assert.Throws<TuningException>(() =>
            new KnapsackSolveService().Solve(
                problem, SolverKind.GeneticAlgorithm, iterations: 500,
                tuning: new SolverTuning { Population = 5, Elite = 9 }));

        Assert.Equal("elite (9) cannot be more than population (5).", error.Message);
    }

    [Fact]
    public void TournamentLargerThanThePopulation_IsRefused()
    {
        var problem = KnapsackInstances.Random(20, seed: 1);

        var error = Assert.Throws<TuningException>(() =>
            new KnapsackSolveService().Solve(
                problem, SolverKind.GeneticAlgorithm, iterations: 500,
                tuning: new SolverTuning { Population = 5, Tournament = 9 }));

        Assert.Contains("tournament (9)", error.Message);
    }

    [Fact]
    public void TheDefaultPopulationDiffersPerProblem_SoTheSameEliteCanBeFineForOneAndNotTheOther()
    {
        var tuning = new SolverTuning { Elite = 40 };

        // At the default budget the job shop's population is 30 and the TSP's is 50.
        var error = Assert.Throws<TuningException>(() =>
            new JobShopSolveService().Solve(
                JobShopBenchmarkInstances.Ft06(), SolverKind.GeneticAlgorithm, tuning: tuning));

        Assert.Equal("elite (40) cannot be more than population (30).", error.Message);

        var result = new TspSolveService().Solve(
            TspBenchmarkInstances.RandomCities(15, seed: 1), SolverKind.GeneticAlgorithm, tuning: tuning);

        Assert.True(result.Length > 0);
    }

    [Fact]
    public void TheCatalog_CoversTheThreeTunableSolvers() =>
        Assert.Equal(
            new[] { SolverKind.SimulatedAnnealing, SolverKind.GeneticAlgorithm, SolverKind.IteratedLocalSearch },
            TuningCatalog.Keys.Select(key => key.Solver).Distinct().ToArray());
}
OPTINULL_EOF
echo "Applied. Now run: dotnet test"
