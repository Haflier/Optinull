using Optinull.Reporting.Charts;

namespace Optinull.Reporting.Tests;

public sealed class ChartRendererTests
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47];

    private static readonly GanttBar[] Bars =
    [
        new(0, 0, 5, 0, "J0"),
        new(0, 5, 12, 1, "J1"),
        new(1, 2, 9, 1, "J1"),
        new(1, 9, 14, 0, "J0")
    ];

    private static readonly string[] Rows = ["Machine 0", "Machine 1"];

    private static readonly ConvergenceSeries[] Series =
    [
        new("Simulated Annealing",
            [new(1, 120), new(40, 100), new(900, 93)],
            FinalEvaluations: 2000),
        new("Genetic Algorithm",
            [new(1, 130), new(500, 105)],
            FinalEvaluations: 2000)
    ];

    private static void AssertPng(byte[] bytes)
    {
        Assert.True(bytes.Length > 1000);
        Assert.Equal(PngSignature, bytes.Take(4).ToArray());
    }

    [Fact]
    public void Gantt_RendersPng() =>
        AssertPng(new GanttChartRenderer().Render(Bars, Rows, "Schedule"));

    [Fact]
    public void Gantt_NoBars_Throws() =>
        Assert.Throws<ArgumentException>(() =>
            new GanttChartRenderer().Render([], Rows, "Schedule"));

    [Fact]
    public void Gantt_BarOutsideRows_Throws() =>
        Assert.Throws<ArgumentException>(() =>
            new GanttChartRenderer().Render([new GanttBar(5, 0, 1, 0, "X")], Rows, "Schedule"));

    [Fact]
    public void Gantt_BarEndingBeforeStart_Throws() =>
        Assert.Throws<ArgumentException>(() =>
            new GanttChartRenderer().Render([new GanttBar(0, 4, 4, 0, "X")], Rows, "Schedule"));

    [Fact]
    public void Convergence_RendersPng() =>
        AssertPng(new ConvergenceChartRenderer().Render(Series, "Convergence"));

    [Fact]
    public void Convergence_NoSeries_Throws() =>
        Assert.Throws<ArgumentException>(() =>
            new ConvergenceChartRenderer().Render([], "Convergence"));

    [Fact]
    public void Convergence_SeriesWithoutPoints_Throws() =>
        Assert.Throws<ArgumentException>(() =>
            new ConvergenceChartRenderer().Render(
                [new ConvergenceSeries("Empty", [], 10)],
                "Convergence"));

    [Fact(Skip = "Manual: writes sample PNGs to /tmp so you can look at them.")]
    public void WriteSamplePngs()
    {
        File.WriteAllBytes(
            "/tmp/optinull-gantt.png",
            new GanttChartRenderer().Render(Bars, Rows, "Schedule, makespan 14"));

        File.WriteAllBytes(
            "/tmp/optinull-convergence.png",
            new ConvergenceChartRenderer().Render(Series, "Convergence"));
    }
}
