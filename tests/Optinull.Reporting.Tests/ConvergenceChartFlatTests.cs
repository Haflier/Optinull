using Optinull.Reporting.Charts;

namespace Optinull.Reporting.Tests;

public sealed class ConvergenceChartFlatTests
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47];

    private static ConvergenceSeries Flat(string name, int finalEvaluations) =>
        new(name, [new ConvergencePointData(1, 10.5578840258)], finalEvaluations);

    [Fact]
    public void Render_WhenEverySolverEndsOnTheSameValue_StillProducesAPng()
    {
        var bytes = new ConvergenceChartRenderer().Render(
            [Flat("Greedy", 20_000), Flat("Hill Climbing", 1), Flat("Annealing", 20_000), Flat("Genetic", 20_000)],
            "Convergence, custom");

        Assert.True(bytes.Length > 1000);
        Assert.Equal(PngSignature, bytes.Take(4).ToArray());
    }

    [Fact]
    public void Render_ASingleSinglePointSeries_StillProducesAPng()
    {
        var bytes = new ConvergenceChartRenderer().Render([Flat("Greedy", 1)], "Convergence, greedy");

        Assert.Equal(PngSignature, bytes.Take(4).ToArray());
    }

    [Fact]
    public void Render_ZeroValuedSeries_StillProducesAPng()
    {
        var bytes = new ConvergenceChartRenderer().Render(
            [new("Zero", [new ConvergencePointData(1, 0)], 1000)],
            "Convergence");

        Assert.Equal(PngSignature, bytes.Take(4).ToArray());
    }

    [Fact]
    public void Render_WithSomeSeriesTiedAndSomeNot_StillProducesAPng()
    {
        ConvergenceSeries[] series =
        [
            Flat("Greedy", 20_000),
            new("Annealing", [new ConvergencePointData(1, 14), new ConvergencePointData(500, 10.5578840258)], 20_000),
            Flat("Genetic", 20_000)
        ];

        var bytes = new ConvergenceChartRenderer().Render(series, "Convergence");

        Assert.Equal(PngSignature, bytes.Take(4).ToArray());
    }
}
