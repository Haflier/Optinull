using Optinull.Reporting.Charts;

namespace Optinull.Reporting.Tests;

public sealed class VrpChartRendererTests
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47];

    private static readonly RoutePoint Depot = new(50, 50);

    private static readonly RoutePoint[] Customers =
        [new(10, 10), new(20, 80), new(90, 20), new(70, 70), new(30, 40)];

    [Fact]
    public void Render_ReturnsPng()
    {
        var bytes = new VrpChartRenderer().Render(
            Depot,
            Customers,
            [new([0, 4], "Route 1: load 9/10"), new([1, 3], "Route 2: load 7/10"), new([2], "Route 3: load 5/10")],
            "Routes");

        Assert.True(bytes.Length > 1000);
        Assert.Equal(PngSignature, bytes.Take(4).ToArray());
    }

    [Fact]
    public void Render_WithManyRoutes_ReusesThePalette()
    {
        var routes = Enumerable.Range(0, 25)
            .Select(index => new VrpChartRoute([index % Customers.Length], $"Route {index + 1}"))
            .ToList();

        var bytes = new VrpChartRenderer().Render(Depot, Customers, routes, "Many routes");

        Assert.Equal(PngSignature, bytes.Take(4).ToArray());
    }

    [Fact]
    public void Render_WithOneCustomerOnOneRoute_StillWorks()
    {
        var bytes = new VrpChartRenderer().Render(
            Depot, [new(10, 10)], [new([0], "Route 1")], "One");

        Assert.Equal(PngSignature, bytes.Take(4).ToArray());
    }

    [Fact]
    public void Render_RejectsMissingCustomersRoutesAndUnknownStops()
    {
        var renderer = new VrpChartRenderer();

        Assert.Throws<ArgumentException>(() => renderer.Render(Depot, [], [new([0], "r")], "t"));
        Assert.Throws<ArgumentException>(() => renderer.Render(Depot, Customers, [], "t"));
        Assert.Throws<ArgumentException>(() => renderer.Render(Depot, Customers, [new([9], "r")], "t"));
        Assert.Throws<ArgumentException>(() => renderer.Render(Depot, Customers, [new([-1], "r")], "t"));
    }
}
