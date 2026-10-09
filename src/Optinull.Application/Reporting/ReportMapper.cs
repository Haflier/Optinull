using Optinull.Optimization.Search;
using Optinull.Problems.JobShop;
using Optinull.Reporting.Charts;

namespace Optinull.Application.Reporting;

/// <summary>Turns solver and problem results into chart inputs.</summary>
public static class ReportMapper
{
    public static IReadOnlyList<GanttBar> ToGanttBars(JobShopSchedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        return schedule.Operations
            .OrderBy(operation => operation.MachineId)
            .ThenBy(operation => operation.StartTime)
            .Select(operation => new GanttBar(
                Row: operation.MachineId,
                Start: operation.StartTime,
                End: operation.EndTime,
                ColorIndex: operation.JobId,
                Label: $"J{operation.JobId}"))
            .ToList();
    }

    public static IReadOnlyList<string> ToMachineLabels(int machineCount)
    {
        if (machineCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(machineCount));

        return Enumerable
            .Range(0, machineCount)
            .Select(machine => $"Machine {machine}")
            .ToList();
    }

    public static ConvergenceSeries ToConvergenceSeries<TSolution>(
        string name,
        SearchResult<TSolution> result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(result);

        return new ConvergenceSeries(
            name,
            result.Convergence
                .Select(point => new ConvergencePointData(
                    point.Evaluations,
                    point.Objective))
                .ToList(),
            result.EvaluationCount);
    }
}
