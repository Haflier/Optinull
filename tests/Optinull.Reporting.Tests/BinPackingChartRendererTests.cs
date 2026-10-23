using Optinull.Reporting.Charts;

namespace Optinull.Reporting.Tests;

public sealed class BinPackingChartRendererTests
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47];

    private static IReadOnlyList<IReadOnlyList<int>> Bins(params int[][] bins) =>
        bins.Select(bin => (IReadOnlyList<int>)bin).ToList();

    [Fact]
    public void Render_ReturnsPng()
    {
        var bytes = new BinPackingChartRenderer().Render(Bins([60, 40], [50, 50], [70, 30]), 100, "Bins");

        Assert.True(bytes.Length > 1000);
        Assert.Equal(PngSignature, bytes.Take(4).ToArray());
    }

    [Fact]
    public void Render_ManyBins_SkipsLabels_AndStillWorks()
    {
        var bins = Enumerable.Range(0, 80).Select(i => (IReadOnlyList<int>)new[] { 30 + i % 20, 20 }).ToList();

        var bytes = new BinPackingChartRenderer().Render(bins, 100, "Many");

        Assert.Equal(PngSignature, bytes.Take(4).ToArray());
    }

    [Fact]
    public void Render_AnOverfullBin_StillWorks()
    {
        var bytes = new BinPackingChartRenderer().Render(Bins([80, 40]), 100, "Over");

        Assert.Equal(PngSignature, bytes.Take(4).ToArray());
    }

    [Fact]
    public void Render_WithoutBins_Throws() =>
        Assert.Throws<ArgumentException>(() => new BinPackingChartRenderer().Render([], 100, "Bins"));

    [Fact]
    public void Render_BadInput_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BinPackingChartRenderer().Render(Bins([5]), 0, "Bins"));
        Assert.Throws<ArgumentException>(() => new BinPackingChartRenderer().Render(Bins([]), 100, "Bins"));
        Assert.Throws<ArgumentException>(() => new BinPackingChartRenderer().Render(Bins([0, 5]), 100, "Bins"));
    }
}
