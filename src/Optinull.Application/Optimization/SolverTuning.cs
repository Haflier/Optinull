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

    /// <summary>The annealing schedule: the problem's usual start, scaled by the user's temperature.</summary>
    internal AnnealingSettings Annealing(double usualStartTemperature) =>
        new(
            usualStartTemperature * (Temperature ?? 1.0),
            Moves ?? AnnealingBudget.IterationsPerTemperature,
            Final ?? AnnealingBudget.DefaultFinalRatio);

    /// <summary>The genetic settings, with the problem's defaults for whatever was not changed.</summary>
    internal GeneticSettings Genetic(
        int defaultPopulation,
        int defaultElite,
        double defaultMutation,
        int defaultTournament,
        int defaultLocalSearch)
    {
        var settings = new GeneticSettings(
            Population ?? defaultPopulation,
            Elite ?? defaultElite,
            Mutation ?? defaultMutation,
            Tournament ?? defaultTournament,
            LocalSearch ?? defaultLocalSearch);

        // These depend on each other, and the defaults differ per problem, so they can only be
        // checked here.
        if (settings.Elite > settings.Population)
        {
            throw new TuningException(
                $"elite ({settings.Elite}) cannot be more than population ({settings.Population}).");
        }

        if (settings.Tournament > settings.Population)
        {
            throw new TuningException(
                $"tournament ({settings.Tournament}) cannot be more than population ({settings.Population}).");
        }

        return settings;
    }
}

internal readonly record struct AnnealingSettings(double InitialTemperature, int Moves, double FinalRatio);

internal readonly record struct GeneticSettings(
    int Population,
    int Elite,
    double Mutation,
    int Tournament,
    int LocalSearch);

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
