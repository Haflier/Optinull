using Optinull.Application.Optimization;

namespace Optinull.Application.Commands;

public abstract record BotCommand;

public sealed record HelpCommand : BotCommand;

/// <summary>
/// Solve a built-in benchmark, or "custom" with the instance text that
/// followed the command.
/// </summary>
public sealed record SolveCommand(
    string Benchmark,
    JobShopSolverKind Solver,
    string? InstanceText = null) : BotCommand;

public sealed record CancelCommand : BotCommand;

public sealed record InvalidCommand(string Message) : BotCommand;
