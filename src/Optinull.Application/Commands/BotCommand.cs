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
    string? InstanceText = null,
    int? Iterations = null,
    int? Seed = null,
    IReadOnlyDictionary<string, string>? Parameters = null) : BotCommand
{
    private static readonly IReadOnlyDictionary<string, string> None =
        new Dictionary<string, string>();

    /// <summary>Problem settings such as cities=30; never null.</summary>
    public IReadOnlyDictionary<string, string> ParameterValues => Parameters ?? None;
}

public sealed record CancelCommand : BotCommand;

public sealed record InvalidCommand(string Message) : BotCommand;
