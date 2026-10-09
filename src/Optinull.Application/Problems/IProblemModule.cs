using System.Diagnostics.CodeAnalysis;
using Optinull.Application.Optimization;

namespace Optinull.Application.Problems;

public sealed record ReportImage(string FileName, string Caption, byte[] Png);

public sealed record SolveReport(string SolverName, IReadOnlyList<ReportImage> Images);

/// <summary>One concrete problem that can solve itself and describe the result.</summary>
public interface IProblemInstance
{
    /// <summary>Short name used in file names, for example "ft06" or "custom".</summary>
    string Name { get; }

    string Description { get; }

    SolveReport Solve(SolverKind solver, int seed, CancellationToken cancellationToken);
}

/// <summary>A kind of problem the bot can solve: built-in instances and a text format.</summary>
public interface IProblemModule
{
    /// <summary>Used in commands, for example "jobshop".</summary>
    string Id { get; }

    string Title { get; }

    IReadOnlyList<string> BuiltInNames { get; }

    string CustomInputHelp { get; }

    bool TryGetBuiltIn(
        string name,
        [NotNullWhen(true)] out IProblemInstance? instance);

    bool TryParseCustom(
        string? text,
        [NotNullWhen(true)] out IProblemInstance? instance,
        out string? error);
}
