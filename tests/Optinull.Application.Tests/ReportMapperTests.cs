using Optinull.Application.Reporting;
using Optinull.Domain.Evaluation;
using Optinull.Optimization.Search;
using Optinull.Problems.JobShop;

namespace Optinull.Application.Tests;

public sealed class ReportMapperTests
{
    [Fact]
    public void ToGanttBars_MapsMachineToRowAndJobToColorAndLabel()
    {
        var schedule = new JobShopSchedule(
        [
            new ScheduledOperation(1, 0, 1, 4, 0),
            new ScheduledOperation(0, 0, 0, 5, 0),
            new ScheduledOperation(1, 1, 0, 7, 5)
        ]);

        var bars = ReportMapper.ToGanttBars(schedule);

        Assert.Equal(3, bars.Count);

        // Ordered by machine, then start time.
        Assert.Equal((0, 0.0, 5.0, 0, "J0"),
            (bars[0].Row, bars[0].Start, bars[0].End, bars[0].ColorIndex, bars[0].Label));
        Assert.Equal((0, 5.0, 12.0, 1, "J1"),
            (bars[1].Row, bars[1].Start, bars[1].End, bars[1].ColorIndex, bars[1].Label));
        Assert.Equal((1, 0.0, 4.0, 1, "J1"),
            (bars[2].Row, bars[2].Start, bars[2].End, bars[2].ColorIndex, bars[2].Label));
    }

    [Fact]
    public void ToMachineLabels_NumbersMachinesFromZero() =>
        Assert.Equal(["Machine 0", "Machine 1", "Machine 2"],
            ReportMapper.ToMachineLabels(3));

    [Fact]
    public void ToMachineLabels_NonPositiveCount_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ReportMapper.ToMachineLabels(0));

    [Fact]
    public void ToConvergenceSeries_CopiesPointsAndFinalEvaluationCount()
    {
        var result = new SearchResult<int>(
            Best: 0,
            Evaluation: new EvaluationResult(93, true, 0),
            EvaluationCount: 2000,
            Elapsed: TimeSpan.FromSeconds(1),
            Convergence: [new ConvergencePoint(1, 120), new ConvergencePoint(900, 93)]);

        var series = ReportMapper.ToConvergenceSeries("SA", result);

        Assert.Equal("SA", series.Name);
        Assert.Equal(2000, series.FinalEvaluations);
        Assert.Equal(2, series.Points.Count);
        Assert.Equal(900, series.Points[1].Evaluations);
        Assert.Equal(93, series.Points[1].Objective);
    }
}
