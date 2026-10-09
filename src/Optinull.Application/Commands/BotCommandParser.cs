using Optinull.Application.Optimization;

namespace Optinull.Application.Commands;

public static class BotCommandParser
{
    public const string Usage = "Usage: /solve <benchmark> [sa|ga]";

    public static BotCommand Parse(string? text)
    {
        var parts = (text ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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
                return ParseSolve(parts);

            default:
                return new InvalidCommand("Unknown command. Send /help to see what I can do.");
        }
    }

    private static BotCommand ParseSolve(string[] parts)
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

        return new SolveCommand(parts[1].ToLowerInvariant(), solver);
    }
}
