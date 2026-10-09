using Optinull.Application.Optimization;

namespace Optinull.Application.Commands;

public static class BotCommandParser
{
    public const string CustomBenchmark = "custom";

    public const string Usage = "Usage: /solve <benchmark> [sa|ga]";

    public const string CustomUsage =
        "Usage: /solve custom [sa|ga], followed by the instance on the next lines " +
        "(or attach it as a .txt file). Send /help to see the format.";

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

    private static BotCommand ParseSolve(string[] parts, string? body)
    {
        if (parts.Length < 2 || parts.Length > 3)
            return new InvalidCommand(Usage);

        var solver = JobShopSolverKind.SimulatedAnnealing;

        if (parts.Length == 3)
        {
            switch (parts[2].ToLowerInvariant())
            {
                case "sa":
                    solver = JobShopSolverKind.SimulatedAnnealing;
                    break;

                case "ga":
                    solver = JobShopSolverKind.GeneticAlgorithm;
                    break;

                default:
                    return new InvalidCommand(
                        $"Unknown solver '{parts[2]}'. Use sa or ga. {Usage}");
            }
        }

        var benchmark = parts[1].ToLowerInvariant();

        if (benchmark != CustomBenchmark)
            return new SolveCommand(benchmark, solver);

        return string.IsNullOrWhiteSpace(body)
            ? new InvalidCommand(CustomUsage)
            : new SolveCommand(benchmark, solver, body);
    }
}
