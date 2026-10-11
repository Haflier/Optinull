using System.Diagnostics.CodeAnalysis;
using Optinull.Application.Optimization;
using Optinull.Reporting.Charts;

namespace Optinull.Application.Problems;

public sealed record ReportImage(string FileName, string Caption, byte[] Png);

/// <summary>The charts of one finished run, and an optional closing message.</summary>
public sealed record SolveReport(
    string SolverName,
    IReadOnlyList<ReportImage> Images,
    string? Summary = null);

/// <summary>
/// What one solver achieved. Rendering the charts is deferred, so a comparison
/// of several solvers only draws the ones it shows.
/// </summary>
public sealed record SolverOutcome(
    SolverKind Solver,
    string SolverName,
    double Score,
    TimeSpan Elapsed,
    int Evaluations,
    ConvergenceSeries Convergence,
    Func<IReadOnlyList<ReportImage>> Render,
    double? Baseline = null);

/// <summary>One concrete problem that can solve itself and describe the result.</summary>
public interface IProblemInstance
{
    /// <summary>Short name used in file names, for example "ft06" or "custom".</summary>
    string Name { get; }

    string Description { get; }

    /// <summary>What the score is called, for example "Makespan".</summary>
    string ScoreLabel { get; }

    bool HigherIsBetter { get; }

    SolverOutcome Run(SolverKind solver, SolveOptions options, CancellationToken cancellationToken);
}

public static class ProblemInstanceExtensions
{
    /// <summary>Runs one solver and renders its charts.</summary>
    public static SolveReport Solve(
        this IProblemInstance instance,
        SolverKind solver,
        SolveOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var outcome = instance.Run(solver, options, cancellationToken);

        return new SolveReport(
            outcome.SolverName,
            outcome.Render(),
            Summary(instance, outcome));
    }

    private static string Summary(IProblemInstance instance, SolverOutcome outcome)
    {
        var text = $"Optimization completed.\n{instance.ScoreLabel}: {outcome.Score:0.##}";

        if (outcome.Solver != SolverKind.Greedy &&
            outcome.Baseline is { } baseline && baseline != 0)
        {
            var improvement = instance.HigherIsBetter
                ? (outcome.Score - baseline) / baseline
                : (baseline - outcome.Score) / baseline;

            text += $"\nImprovement over greedy: {improvement:P1}";
        }

        return text;
    }
}

/// <summary>A numeric key=value setting a module can generate an instance from.</summary>
public sealed record ProblemParameter(
    string Name,
    string Description,
    int Min,
    int Max,
    int Default);

/// <summary>
/// A ready-made choice in the /optimize wizard: either a built-in instance
/// (Target is its name) or a random one (Target is the problem id, plus Settings).
/// </summary>
public sealed record DatasetPreset(
    string Key,
    string Label,
    string Target,
    IReadOnlyDictionary<string, string>? Settings = null);

/// <summary>A kind of problem the bot can solve: built-in instances and a text format.</summary>
public interface IProblemModule
{
    /// <summary>Used in commands, for example "jobshop".</summary>
    string Id { get; }

    string Title { get; }

    IReadOnlyList<string> BuiltInNames { get; }

    string CustomInputHelp { get; }

    /// <summary>Settings for a generated instance, for example cities=30.</summary>
    IReadOnlyList<ProblemParameter> Parameters { get; }

    /// <summary>The Small / Medium / Large choices of the wizard.</summary>
    IReadOnlyList<DatasetPreset> Presets { get; }

    bool TryGetBuiltIn(
        string name,
        [NotNullWhen(true)] out IProblemInstance? instance);

    bool TryParseCustom(
        string? text,
        [NotNullWhen(true)] out IProblemInstance? instance,
        out string? error);

    /// <summary>Builds a random instance from key=value settings; missing ones use defaults.</summary>
    bool TryGenerate(
        IReadOnlyDictionary<string, string> values,
        int seed,
        [NotNullWhen(true)] out IProblemInstance? instance,
        out string? error);
}
