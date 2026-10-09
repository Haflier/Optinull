namespace Optinull.Reporting.Charts;

public readonly record struct RoutePoint(double X, double Y);

/// <summary>Draws a closed route through points (the first stop is marked red) as PNG bytes.</summary>
public sealed class RouteChartRenderer
{
    public byte[] Render(
        IReadOnlyList<RoutePoint> points,
        IReadOnlyList<int> order,
        string title,
        int width = 1000,
        int height = 1000)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(order);

        if (points.Count < 2)
            throw new ArgumentException("At least two points are required.", nameof(points));

        if (order.Count != points.Count ||
            order.Distinct().Count() != order.Count ||
            order.Any(index => index < 0 || index >= points.Count))
        {
            throw new ArgumentException(
                "The order must visit every point exactly once.",
                nameof(order));
        }

        var plot = new ScottPlot.Plot();

        // One extra step closes the loop.
        var xs = new double[order.Count + 1];
        var ys = new double[order.Count + 1];

        for (var i = 0; i <= order.Count; i++)
        {
            var point = points[order[i % order.Count]];
            xs[i] = point.X;
            ys[i] = point.Y;
        }

        var route = plot.Add.Scatter(xs, ys);
        route.Color = ChartPalette.Get(0);
        route.LineWidth = 2;
        route.MarkerSize = 7;

        var start = plot.Add.Scatter(new[] { xs[0] }, new[] { ys[0] });
        start.Color = ScottPlot.Colors.Red;
        start.MarkerSize = 14;
        start.LineWidth = 0;

        plot.Axes.SquareUnits();
        plot.Title(title);

        return plot.GetImageBytes(width, height, ScottPlot.ImageFormat.Png);
    }
}
