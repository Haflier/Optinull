using Optinull.Reporting.Charts;

namespace Optinull.Reporting.Tests;

public sealed class MapViewportTests
{
    private static readonly RoutePoint[] Berlin = [new(13.38, 52.49), new(13.42, 52.52)];

    [Fact]
    public void Fit_PutsEveryPointInsideThePicture()
    {
        var viewport = MapViewport.Fit(Berlin, 1000, 1000);

        foreach (var point in Berlin)
        {
            var (x, y) = viewport.ToPixel(point);

            Assert.InRange(x, 0, 1000);
            Assert.InRange(y, 0, 1000);
        }
    }

    [Fact]
    public void Fit_UsesAStreetLevelZoom_ForACity_AndAWorldZoom_ForTheGlobe()
    {
        var city = MapViewport.Fit(Berlin);
        var globe = MapViewport.Fit([new(-170, -60), new(170, 60)]);

        Assert.InRange(city.Zoom, 11, 15);
        Assert.True(globe.Zoom <= 2);
    }

    [Fact]
    public void Fit_CentersThePoints()
    {
        var viewport = MapViewport.Fit(Berlin, 1000, 1000);

        var (x1, y1) = viewport.ToPixel(Berlin[0]);
        var (x2, y2) = viewport.ToPixel(Berlin[1]);

        Assert.Equal(500, (x1 + x2) / 2, 1);
        Assert.Equal(500, (y1 + y2) / 2, 1);
    }

    [Fact]
    public void Fit_ForOnePlace_StillWorks()
    {
        var viewport = MapViewport.Fit([new(13.4, 52.5), new(13.4, 52.5)]);

        Assert.Equal(18, viewport.Zoom);
    }

    [Fact]
    public void Fit_WithoutPoints_Throws() =>
        Assert.Throws<ArgumentException>(() => MapViewport.Fit([]));

    [Fact]
    public void Tiles_CoverThePicture_AndStayInsideTheWorld()
    {
        var viewport = MapViewport.Fit(Berlin, 1000, 1000);
        var tiles = viewport.Tiles().ToList();
        var count = 1 << viewport.Zoom;

        Assert.InRange(tiles.Count, 4, 25);
        Assert.All(tiles, tile =>
        {
            Assert.Equal(viewport.Zoom, tile.Zoom);
            Assert.InRange(tile.X, 0, count - 1);
            Assert.InRange(tile.Y, 0, count - 1);
        });

        // Together the tiles reach every edge of the picture.
        var origins = tiles.Select(viewport.TileOrigin).ToList();

        Assert.True(origins.Min(o => o.X) <= 0);
        Assert.True(origins.Min(o => o.Y) <= 0);
        Assert.True(origins.Max(o => o.X) + MapViewport.TileSize >= 1000);
        Assert.True(origins.Max(o => o.Y) + MapViewport.TileSize >= 1000);
    }

    [Fact]
    public void ANorthernPoint_IsHigherInThePicture()
    {
        var viewport = MapViewport.Fit(Berlin);

        Assert.True(viewport.ToPixel(Berlin[1]).Y < viewport.ToPixel(Berlin[0]).Y);
        Assert.True(viewport.ToPixel(Berlin[1]).X > viewport.ToPixel(Berlin[0]).X);
    }
}
