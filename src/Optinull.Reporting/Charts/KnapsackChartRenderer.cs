namespace Optinull.Reporting.Charts;

public readonly record struct KnapsackPoint(double Weight, double Value, bool Selected);

/// <summary>Draws every item as weight against value, with the packed ones highlighted, as PNG bytes.</summary>
public sealed class KnapsackChartRenderer
{
    public byte[] Render(
        IReadOnlyList<KnapsackPoint> points,
        string title,
        int width = 1000,
        int height = 700)
    {
        ArgumentNullException.ThrowIfNull(points);

        if (points.Count == 0)
            throw new ArgumentException("At least one item is required.", nameof(points));

        var plot = new ScottPlot.Plot();

        Add(plot, points.Where(point => !point.Selected).ToList(),
            ScottPlot.Color.FromHex("#4d4b46"), "Left out");

        Add(plot, points.Where(point => point.Selected).ToList(),
            ChartPalette.Get(1), "Packed");

        plot.ShowLegend(ScottPlot.Alignment.UpperRight);
        plot.Title(title);
        plot.XLabel("Weight");
        plot.YLabel("Value");

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

    private static void Add(
        ScottPlot.Plot plot,
        IReadOnlyList<KnapsackPoint> points,
        ScottPlot.Color color,
        string label)
    {
        if (points.Count == 0)
            return;

        var scatter = plot.Add.Scatter(
            points.Select(point => point.Weight).ToArray(),
            points.Select(point => point.Value).ToArray());

        scatter.LineWidth = 0;
        scatter.MarkerSize = 10;
        scatter.Color = color;
        scatter.LegendText = label;
    }
}
