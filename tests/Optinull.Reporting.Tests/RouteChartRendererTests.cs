using Optinull.Reporting.Charts;

namespace Optinull.Reporting.Tests;

public sealed class RouteChartRendererTests
{
    private static readonly RoutePoint[] Points =
    [
        new(0, 0), new(10, 0), new(10, 10), new(0, 10)
    ];

    [Fact]
    public void Render_ReturnsPng()
    {
        var bytes = new RouteChartRenderer().Render(Points, [0, 1, 2, 3], "Route");

        Assert.True(bytes.Length > 1000);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, bytes.Take(4).ToArray());
    }

    [Theory]
    [InlineData(new[] { 0, 1, 2 })]
    [InlineData(new[] { 0, 1, 2, 2 })]
    [InlineData(new[] { 0, 1, 2, 9 })]
    public void Render_OrderThatIsNotAPermutation_Throws(int[] order) =>
        Assert.Throws<ArgumentException>(() =>
            new RouteChartRenderer().Render(Points, order, "Route"));

    [Fact]
    public void Render_FewerThanTwoPoints_Throws() =>
        Assert.Throws<ArgumentException>(() =>
            new RouteChartRenderer().Render([new RoutePoint(0, 0)], [0], "Route"));
}
