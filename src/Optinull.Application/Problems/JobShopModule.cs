using System.Diagnostics.CodeAnalysis;
using Optinull.Application.Optimization;
using Optinull.Application.Reporting;
using Optinull.Problems.JobShop;
using Optinull.Problems.JobShop.Benchmarks;
using Optinull.Problems.JobShop.Parsing;

namespace Optinull.Application.Problems;

public sealed class JobShopModule : IProblemModule
{
    public string Id => "jobshop";

    public string Title => "job shop scheduling";

    public IReadOnlyList<string> BuiltInNames => JobShopBenchmarkCatalog.Names;

    public string CustomInputHelp
    {
        get
        {
            var limits = JobShopInputLimits.Default;

            return
                "first line: jobs and machines; then one line per job with " +
                "'machine time' pairs, machines numbered from 0. Example:\n" +
                "3 3\n0 3 1 2 2 2\n0 2 2 1 1 4\n1 4 2 3 0 1\n" +
                $"Limits: {limits.MaxJobs} jobs, {limits.MaxMachines} machines, " +
                $"{limits.MaxOperations} operations.";
        }
    }

    public bool TryGetBuiltIn(
        string name,
        [NotNullWhen(true)] out IProblemInstance? instance)
    {
        if (JobShopBenchmarkCatalog.TryGet(name, out var benchmark))
        {
            instance = new JobShopInstance(
                benchmark.Name,
                benchmark.Name,
                benchmark.Problem,
                benchmark.KnownOptimum);
            return true;
        }

        instance = null;
        return false;
    }

    public bool TryParseCustom(
        string? text,
        [NotNullWhen(true)] out IProblemInstance? instance,
        out string? error)
    {
        instance = null;

        if (!JobShopTextParser.TryParse(
                text,
                JobShopInputLimits.Default,
                out var problem,
                out error))
        {
            return false;
        }

        instance = new JobShopInstance(
            "custom",
            $"custom job shop, {problem!.Jobs.Count} jobs x {problem.MachineCount} machines",
            problem,
            knownOptimum: null);

        return true;
    }
}

internal sealed class JobShopInstance : IProblemInstance
{
    private readonly JobShopProblem _problem;
    private readonly double? _knownOptimum;

    public JobShopInstance(
        string name,
        string description,
        JobShopProblem problem,
        double? knownOptimum)
    {
        Name = name;
        Description = description;
        _problem = problem;
        _knownOptimum = knownOptimum;
    }

    public string Name { get; }

    public string Description { get; }

    public SolveReport Solve(
        SolverKind solver,
        int seed,
        CancellationToken cancellationToken)
    {
        var result = new JobShopSolveService().Solve(_problem, solver, seed, cancellationToken);
        var report = new JobShopReportService().Render(_problem, result);

        return new SolveReport(
            result.SolverName,
            [
                new ReportImage($"{Name}-gantt.png", Caption(result), report.GanttPng),
                new ReportImage($"{Name}-convergence.png", "Convergence", report.ConvergencePng)
            ]);
    }

    private string Caption(JobShopSolveResult result)
    {
        var prefix = $"{Name} | {result.SolverName} | makespan {result.Makespan:0}";

        if (_knownOptimum is { } optimum)
        {
            var gap = (result.Makespan - optimum) / optimum;

            return $"{prefix} (optimal {optimum:0}, gap {gap:P1})";
        }

        // The optimum of a custom problem is unknown, so compare with a lower bound.
        var lowerBound = JobShopLowerBound.Compute(_problem);
        var above = (result.Makespan - lowerBound) / lowerBound;

        return $"{prefix} (lower bound {lowerBound:0}, at most {above:P1} above optimal)";
    }
}
