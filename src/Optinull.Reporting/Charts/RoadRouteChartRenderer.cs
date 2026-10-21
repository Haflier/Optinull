namespace Optinull.Reporting.Charts;

/// <summary>
/// One truck on a map: the line it drives, the customers it stops at in order, and its legend
/// text. Points are longitude (X) and latitude (Y) in degrees.
/// </summary>
public sealed record RoadChartRoute(
    IReadOnlyList<RoutePoint> Line,
    IReadOnlyList<RoutePoint> Stops,
    string Label);

/// <summary>
/// Draws routes over a plain background as PNG bytes: one colour per truck, numbered stops, and
/// a red depot. Longitudes are scaled by the cosine of the middle latitude, so distances look
/// right away from the equator.
/// </summary>
public sealed class RoadRouteChartRenderer
{
    public byte[] Render(
        RoutePoint depot,
        IReadOnlyList<RoadChartRoute> routes,
        string title,
        int width = 1000,
        int height = 1000)
    {
        ArgumentNullException.ThrowIfNull(routes);

        if (routes.Count == 0)
            throw new ArgumentException("At least one route is required.", nameof(routes));

        var everything = routes.SelectMany(route => route.Line).Append(depot).ToList();
        var middleLatitude = (everything.Min(point => point.Y) + everything.Max(point => point.Y)) / 2;
        var scale = Math.Max(0.1, Math.Cos(middleLatitude * Math.PI / 180));

        double X(RoutePoint point) => point.X * scale;

        var plot = new ScottPlot.Plot();

        for (var r = 0; r < routes.Count; r++)
        {
            var route = routes[r];
            var color = ChartPalette.Get(r);

            if (route.Line.Count >= 2)
            {
                var line = plot.Add.Scatter(
                    route.Line.Select(X).ToArray(),
                    route.Line.Select(point => point.Y).ToArray());

                line.Color = color;
                line.LineWidth = 3;
                line.MarkerSize = 0;
                line.LegendText = route.Label;
            }

            for (var i = 0; i < route.Stops.Count; i++)
            {
                var stop = route.Stops[i];

                var dot = plot.Add.Scatter(new[] { X(stop) }, new[] { stop.Y });

                dot.Color = color;
                dot.MarkerSize = 18;
                dot.LineWidth = 0;

                var number = plot.Add.Text((i + 1).ToString(), X(stop), stop.Y);

                number.LabelStyle.Alignment = ScottPlot.Alignment.MiddleCenter;
                number.LabelStyle.ForeColor = ScottPlot.Colors.White;
                number.LabelStyle.FontSize = 11;
            }
        }

        var depotMark = plot.Add.Scatter(new[] { X(depot) }, new[] { depot.Y });

        depotMark.Color = ScottPlot.Colors.Red;
        depotMark.MarkerSize = 22;
        depotMark.LineWidth = 0;
        depotMark.LegendText = "Depot";

        // The axes would show scaled longitudes, which mean nothing, so they carry no numbers.
        plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(Array.Empty<ScottPlot.Tick>());
        plot.Axes.Left.TickGenerator = new ScottPlot.TickGenerators.NumericManual(Array.Empty<ScottPlot.Tick>());

        plot.ShowLegend(ScottPlot.Alignment.UpperRight);
        plot.Axes.SquareUnits();
        plot.Title(title);

        plot.FigureBackground.Color = ScottPlot.Color.FromHex("#0747af");
        plot.DataBackground.Color = ScottPlot.Color.FromHex("#c0c08a");
        plot.Axes.Color(ScottPlot.Color.FromHex("#ffffff"));
        plot.Grid.MajorLineColor = ScottPlot.Color.FromHex("#1f74b5");
        plot.Grid.MinorLineColor = ScottPlot.Color.FromHex("#1f74b5");

        return plot.GetImageBytes(width, height, ScottPlot.ImageFormat.Png);
    }
}
