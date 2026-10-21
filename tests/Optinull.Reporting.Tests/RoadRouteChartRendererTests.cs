using Optinull.Reporting.Charts;

namespace Optinull.Reporting.Tests;

public sealed class RoadRouteChartRendererTests
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47];

    private static readonly RoutePoint Depot = new(13.40, 52.50);

    [Fact]
    public void Render_ReturnsPng()
    {
        var bytes = new RoadRouteChartRenderer().Render(
            Depot,
            [
                new RoadChartRoute(
                    [Depot, new(13.41, 52.51), new(13.42, 52.52), Depot],
                    [new(13.41, 52.51), new(13.42, 52.52)],
                    "Truck 1"),
                new RoadChartRoute([Depot, new(13.38, 52.49), Depot], [new(13.38, 52.49)], "Truck 2")
            ],
            "Routes");

        Assert.True(bytes.Length > 1000);
        Assert.Equal(PngSignature, bytes.Take(4).ToArray());
    }

    [Fact]
    public void Render_WithoutRoutes_Throws() =>
        Assert.Throws<ArgumentException>(() => new RoadRouteChartRenderer().Render(Depot, [], "Routes"));
}
