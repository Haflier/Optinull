using System.Globalization;
using Optinull.Application.Optimization;

namespace Optinull.Application.Commands;

public static class BotCommandParser
{
    public const string Usage =
        "Usage: /solve <problem> [greedy|hc|ils|sa|ga|all] [iterations=N] [seed=N] " +
        "[tuning key=value ...] [key=value ...]";

    private static readonly HashSet<string> IterationKeys =
        new(["iterations", "iteration", "iter", "it"], StringComparer.OrdinalIgnoreCase);

    public static BotCommand Parse(string? text)
    {
        var input = (text ?? string.Empty).Replace("\r", string.Empty);

        // The first line is the command; anything after it is the instance.
        var newline = input.IndexOf('\n');
        var header = newline < 0 ? input : input[..newline];
        var body = newline < 0 ? null : input[(newline + 1)..];

        var parts = header.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0 || !parts[0].StartsWith('/'))
            return new InvalidCommand("Send /help to see what I can do.");

        // In groups Telegram sends "/solve@BotName".
        var command = parts[0].Split('@')[0].ToLowerInvariant();

        switch (command)
        {
            case "/start":
              return new StartCommand();

            case "/help":
                return new HelpCommand();

            case "/optimize":
                return new OptimizeCommand();

            case "/solve":
                return ParseSolve(parts, body, forceCompare: false);

            case "/compare":
                return ParseSolve(parts, body, forceCompare: true);

            case "/cancel":
                return new CancelCommand();

            default:
                return new InvalidCommand("Unknown command. Send /help to see what I can do.");
        }
    }

    // /solve <problem> then, in any order: a solver (greedy|hc|ils|sa|ga|all) and key=value pairs.
    // /compare is /solve with every solver.
    private static BotCommand ParseSolve(string[] parts, string? body, bool forceCompare)
    {
        if (parts.Length < 2)
        {
            return new InvalidCommand(forceCompare
                ? "Usage: /compare <problem> [iterations=N] [seed=N] [key=value ...]"
                : Usage);
        }

        SolverKind? solver = null;
        var compare = forceCompare;
        var onMap = false;
        int? iterations = null;
        int? seed = null;
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var tuningValues = new Dictionary<string, string>();

        foreach (var token in parts.Skip(2))
        {
            var equals = token.IndexOf('=');

            if (equals < 0)
            {
                // "/solve vrp map": the problem is drawn on a map. Repeating it is harmless.
                if (IsMapToken(token))
                {
                    onMap = true;
                    continue;
                }

                if (IsCompareToken(token))
                {
                    if (solver is not null)
                        return new InvalidCommand($"Choose one solver, or all. {Usage}");

                    compare = true;
                    continue;
                }

                var parsed = ParseSolver(token);

                if (parsed is null)
                {
                    return new InvalidCommand(
                        $"Unknown option '{token}'. Use greedy, hc, ils, sa, ga or all, or key=value. {Usage}");
                }

                if (compare)
                {
                    return new InvalidCommand(forceCompare
                        ? "/compare already runs every solver; remove the solver name."
                        : $"Choose one solver, or all. {Usage}");
                }

                if (solver is not null && solver != parsed)
                    return new InvalidCommand($"Choose one solver, or all. {Usage}");

                solver = parsed;
                continue;
            }

            var key = token[..equals].ToLowerInvariant();
            var value = token[(equals + 1)..];

            if (key.Length == 0 || value.Length == 0)
                return new InvalidCommand($"Expected key=value, got '{token}'. {Usage}");

            if (IterationKeys.Contains(key))
            {
                if (iterations is not null)
                    return new InvalidCommand("iterations was given twice.");

                if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ||
                    n < SolveLimits.MinIterations || n > SolveLimits.MaxIterations)
                {
                    return new InvalidCommand(
                        $"iterations must be a whole number from {SolveLimits.MinIterations} " +
                        $"to {SolveLimits.MaxIterations}.");
                }

                iterations = n;
            }
            else if (key == "seed")
            {
                if (seed is not null)
                    return new InvalidCommand("seed was given twice.");

                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var s))
                    return new InvalidCommand("seed must be a whole number.");

                seed = s;
            }
            else if (TuningCatalog.Canonical(key) is { } tuningKey)
            {
                if (!tuningValues.TryAdd(tuningKey, value))
                    return new InvalidCommand($"{tuningKey} was given twice.");
            }
            else if (!parameters.TryAdd(key, value))
            {
                return new InvalidCommand($"{key} was given twice.");
            }
        }

        // The map shows the routes of one algorithm, so there is nothing to compare there.
        if (onMap && compare)
        {
            return new InvalidCommand(forceCompare
                ? "/compare runs every solver, but the map shows one algorithm's routes. " +
                  "Use /solve <problem> map [greedy|hc|ils|sa|ga] instead."
                : "The map shows one algorithm's routes. Choose greedy, hc, ils, sa or ga instead of all.");
        }

        if (!TuningCatalog.TryBuild(tuningValues, solver, compare, out var tuning, out var tuningError))
            return new InvalidCommand(tuningError!);

        return new SolveCommand(
            parts[1].ToLowerInvariant(),
            solver ?? SolverKind.SimulatedAnnealing,
            string.IsNullOrWhiteSpace(body) ? null : body,
            iterations,
            seed,
            parameters.Count == 0 ? null : parameters,
            compare,
            tuning,
            onMap);
    }

    private static bool IsMapToken(string token) =>
        token.Equals("map", StringComparison.OrdinalIgnoreCase);

    private static bool IsCompareToken(string token) =>
        token.Equals("all", StringComparison.OrdinalIgnoreCase) ||
        token.Equals("compare", StringComparison.OrdinalIgnoreCase);

    private static SolverKind? ParseSolver(string token) =>
        token.ToLowerInvariant() switch
        {
            "greedy" => SolverKind.Greedy,
            "hc" => SolverKind.HillClimbing,
            "ils" => SolverKind.IteratedLocalSearch,
            "sa" => SolverKind.SimulatedAnnealing,
            "ga" => SolverKind.GeneticAlgorithm,
            _ => null
        };
}
