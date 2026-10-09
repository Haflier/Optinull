using Optinull.Application.Optimization;
using Optinull.Problems.JobShop;
using Optinull.Reporting.Charts;

namespace Optinull.Application.Reporting;

public sealed record JobShopReport(byte[] GanttPng, byte[] ConvergencePng);

/// <summary>Renders the two PNG charts for a solved job shop.</summary>
public sealed class JobShopReportService
{
    private readonly GanttChartRenderer _gantt = new();
    private readonly ConvergenceChartRenderer _convergence = new();

    public JobShopReport Render(JobShopProblem problem, JobShopSolveResult result)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(result);

        var gantt = _gantt.Render(
            ReportMapper.ToGanttBars(result.Schedule),
            ReportMapper.ToMachineLabels(problem.MachineCount),
            $"{result.SolverName}: makespan {result.Makespan:0}");

        var convergence = _convergence.Render(
            [ReportMapper.ToConvergenceSeries(result.SolverName, result.Search)],
            $"Convergence, {result.SolverName}");

        return new JobShopReport(gantt, convergence);
    }
}
