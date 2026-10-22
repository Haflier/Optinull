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

    [Fact]
    public void Render_WithTiles_DrawsThemAndTheCredit()
    {
        var routes = new[]
        {
            new RoadChartRoute([Depot, new(13.41, 52.51), Depot], [new(13.41, 52.51)], "Truck 1")
        };

        var viewport = MapViewport.Fit(routes.SelectMany(route => route.Line));

        // Any valid PNG will do as a tile.
        var tile = new RoadRouteChartRenderer().Render(Depot, routes, "tile");

        var plain = new RoadRouteChartRenderer().Render(Depot, routes, "Routes", viewport);

        var withTiles = new RoadRouteChartRenderer().Render(
            Depot,
            routes,
            "Routes",
            viewport,
            viewport.Tiles().ToDictionary(t => t, _ => tile),
            "\u00a9 OpenStreetMap contributors");

        Assert.Equal(PngSignature, withTiles.Take(4).ToArray());
        Assert.NotEqual(plain, withTiles);
    }

    [Fact]
    public void Render_WithATileThatIsNotAnImage_StillRenders()
    {
        var routes = new[] { new RoadChartRoute([Depot, new(13.41, 52.51)], [new(13.41, 52.51)], "Truck 1") };
        var viewport = MapViewport.Fit(routes.SelectMany(route => route.Line));

        var bytes = new RoadRouteChartRenderer().Render(
            Depot,
            routes,
            "Routes",
            viewport,
            viewport.Tiles().ToDictionary(t => t, _ => new byte[] { 1, 2, 3 }),
            "credit");

        Assert.Equal(PngSignature, bytes.Take(4).ToArray());
    }
}
