using Optinull.Application.Optimization;

namespace Optinull.Application.Commands;

public abstract record BotCommand;

public sealed record HelpCommand : BotCommand;

public sealed record SolveCommand(
    string Benchmark,
    JobShopSolverKind Solver) : BotCommand;

public sealed record InvalidCommand(string Message) : BotCommand;

public sealed record CancelCommand : BotCommand;
