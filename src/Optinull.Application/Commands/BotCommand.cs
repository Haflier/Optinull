using Optinull.Application.Optimization;

namespace Optinull.Application.Commands;

public abstract record BotCommand;

public sealed record HelpCommand : BotCommand;

/// <summary>
/// Target is a built-in instance name (ft06, circle20, ...) or a problem id
/// (jobshop, tsp) when the instance text follows the command.
/// </summary>
public sealed record SolveCommand(
    string Target,
    SolverKind Solver,
    string? InstanceText = null) : BotCommand;

public sealed record CancelCommand : BotCommand;

public sealed record InvalidCommand(string Message) : BotCommand;
