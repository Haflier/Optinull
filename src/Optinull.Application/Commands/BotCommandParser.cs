using System.Globalization;
using Optinull.Application.Optimization;

namespace Optinull.Application.Commands;

public static class BotCommandParser
{
    public const string Usage =
        "Usage: /solve <problem> [sa|ga] [iterations=N] [seed=N] [key=value ...]";

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
            case "/help":
                return new HelpCommand();

            case "/solve":
                return ParseSolve(parts, body);

            case "/cancel":
                return new CancelCommand();

            default:
                return new InvalidCommand("Unknown command. Send /help to see what I can do.");
        }
    }

    // /solve <problem> then, in any order: a solver (sa|ga) and key=value pairs.
    private static BotCommand ParseSolve(string[] parts, string? body)
    {
        if (parts.Length < 2)
            return new InvalidCommand(Usage);

        SolverKind? solver = null;
        int? iterations = null;
        int? seed = null;
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var token in parts.Skip(2))
        {
            var equals = token.IndexOf('=');

            if (equals < 0)
            {
                var parsed = ParseSolver(token);

                if (parsed is null)
                {
                    return new InvalidCommand(
                        $"Unknown option '{token}'. Use sa or ga, or key=value. {Usage}");
                }

                if (solver is not null && solver != parsed)
                    return new InvalidCommand($"Choose one solver, sa or ga. {Usage}");

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
            else if (!parameters.TryAdd(key, value))
            {
                return new InvalidCommand($"{key} was given twice.");
            }
        }

        return new SolveCommand(
            parts[1].ToLowerInvariant(),
            solver ?? SolverKind.SimulatedAnnealing,
            string.IsNullOrWhiteSpace(body) ? null : body,
            iterations,
            seed,
            parameters.Count == 0 ? null : parameters);
    }

    private static SolverKind? ParseSolver(string token) =>
        token.ToLowerInvariant() switch
        {
            "sa" => SolverKind.SimulatedAnnealing,
            "ga" => SolverKind.GeneticAlgorithm,
            _ => null
        };
}
