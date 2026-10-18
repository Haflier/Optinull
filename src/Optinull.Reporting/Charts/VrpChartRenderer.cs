namespace Optinull.Reporting.Charts;

/// <summary>One vehicle's route: the customers it visits in order, and its legend text.</summary>
public sealed record VrpChartRoute(IReadOnlyList<int> Stops, string Label);

/// <summary>
/// Draws a routing plan as PNG bytes: the depot (large, red) and one colour per route, each
/// starting and ending at the depot.
/// </summary>
public sealed class VrpChartRenderer
{
    public byte[] Render(
        RoutePoint depot,
        IReadOnlyList<RoutePoint> customers,
        IReadOnlyList<VrpChartRoute> routes,
        string title,
        int width = 1000,
        int height = 1000)
    {
        ArgumentNullException.ThrowIfNull(customers);
        ArgumentNullException.ThrowIfNull(routes);

        if (customers.Count == 0)
            throw new ArgumentException("At least one customer is required.", nameof(customers));

        if (routes.Count == 0)
            throw new ArgumentException("At least one route is required.", nameof(routes));

        if (routes.Any(route => route.Stops.Any(stop => stop < 0 || stop >= customers.Count)))
            throw new ArgumentException("A route visits a customer that does not exist.", nameof(routes));

        var plot = new ScottPlot.Plot();

        for (var r = 0; r < routes.Count; r++)
        {
            var stops = routes[r].Stops;

            var xs = new double[stops.Count + 2];
            var ys = new double[stops.Count + 2];

            xs[0] = depot.X;
            ys[0] = depot.Y;

            for (var i = 0; i < stops.Count; i++)
            {
                xs[i + 1] = customers[stops[i]].X;
                ys[i + 1] = customers[stops[i]].Y;
            }

            xs[^1] = depot.X;
            ys[^1] = depot.Y;

            var line = plot.Add.Scatter(xs, ys);

            line.Color = ChartPalette.Get(r);
            line.LineWidth = 2;
            line.MarkerSize = 7;
            line.LegendText = routes[r].Label;
        }

        var depotMark = plot.Add.Scatter(new[] { depot.X }, new[] { depot.Y });

        depotMark.Color = ScottPlot.Colors.Red;
        depotMark.MarkerSize = 16;
        depotMark.LineWidth = 0;
        depotMark.LegendText = "Depot";

        plot.ShowLegend(ScottPlot.Alignment.UpperRight);
        plot.Axes.SquareUnits();
        plot.Title(title);

        plot.FigureBackground.Color = ScottPlot.Color.FromHex("#0747af");
        plot.DataBackground.Color = ScottPlot.Color.FromHex("#c0c08a");

        plot.Axes.Color(
            ScottPlot.Color.FromHex("#ffffff"));

        plot.Axes.Left.TickLabelStyle.ForeColor =
            ScottPlot.Color.FromHex("#ffffff");
        plot.Axes.Bottom.TickLabelStyle.ForeColor =
            ScottPlot.Color.FromHex("#ffffff");

        plot.Grid.MajorLineColor =
            ScottPlot.Color.FromHex("#1f74b5");

        plot.Grid.MinorLineColor =
            ScottPlot.Color.FromHex("#1f74b5");

        return plot.GetImageBytes(width, height, ScottPlot.ImageFormat.Png);
    }
}
