using Optinull.Reporting.Charts;

namespace Optinull.Reporting.Tests;

public sealed class KnapsackChartRendererTests
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47];

    [Fact]
    public void Render_ReturnsPng()
    {
        KnapsackPoint[] points = [new(10, 60, true), new(20, 100, true), new(30, 120, false)];

        var bytes = new KnapsackChartRenderer().Render(points, "Items");

        Assert.True(bytes.Length > 1000);
        Assert.Equal(PngSignature, bytes.Take(4).ToArray());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Render_WhenAllItemsShareOneState_StillWorks(bool selected)
    {
        KnapsackPoint[] points = [new(10, 60, selected), new(20, 100, selected)];

        var bytes = new KnapsackChartRenderer().Render(points, "Items");

        Assert.Equal(PngSignature, bytes.Take(4).ToArray());
    }

    [Fact]
    public void Render_WithoutItems_Throws() =>
        Assert.Throws<ArgumentException>(() => new KnapsackChartRenderer().Render([], "Items"));
}
