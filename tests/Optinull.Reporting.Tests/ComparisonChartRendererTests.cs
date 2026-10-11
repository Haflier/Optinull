using Optinull.Reporting.Charts;

namespace Optinull.Reporting.Tests;

public sealed class ComparisonChartRendererTests
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47];

    [Fact]
    public void Render_ReturnsPng()
    {
        ComparisonBar[] bars =
        [
            new("Greedy", 260, false),
            new("Hill Climbing", 211, false),
            new("Simulated Annealing", 194, false),
            new("Genetic Algorithm", 184, true)
        ];

        var bytes = new ComparisonChartRenderer().Render(bars, "ft06: Makespan by solver", "Makespan");

        Assert.True(bytes.Length > 1000);
        Assert.Equal(PngSignature, bytes.Take(4).ToArray());
    }

    [Fact]
    public void Render_WithOneBar_StillWorks()
    {
        var bytes = new ComparisonChartRenderer().Render([new("Greedy", 5, true)], "t", "v");

        Assert.Equal(PngSignature, bytes.Take(4).ToArray());
    }

    [Fact]
    public void Render_WithoutBars_Throws() =>
        Assert.Throws<ArgumentException>(() => new ComparisonChartRenderer().Render([], "t", "v"));
}
