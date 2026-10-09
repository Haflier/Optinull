namespace Optinull.Reporting.Charts;

/// <summary>Draws a Gantt chart (one row per resource) as PNG bytes.</summary>
public sealed class GanttChartRenderer
{
    public byte[] Render(
        IReadOnlyList<GanttBar> bars,
        IReadOnlyList<string> rowLabels,
        string title,
        int width = 1400)
    {
        ArgumentNullException.ThrowIfNull(bars);
        ArgumentNullException.ThrowIfNull(rowLabels);

        if (bars.Count == 0)
            throw new ArgumentException("At least one bar is required.", nameof(bars));

        if (rowLabels.Count == 0)
            throw new ArgumentException("At least one row is required.", nameof(rowLabels));

        if (bars.Any(bar => bar.Row < 0 || bar.Row >= rowLabels.Count))
            throw new ArgumentException("A bar refers to a row that has no label.", nameof(bars));

        if (bars.Any(bar => bar.End <= bar.Start))
            throw new ArgumentException("Every bar must end after it starts.", nameof(bars));

        var plot = new ScottPlot.Plot();

        var lastRow = rowLabels.Count - 1;
        var end = bars.Max(bar => bar.End);

        foreach (var bar in bars)
        {
            // Row 0 is drawn at the top.
            var y = lastRow - bar.Row;

            var rectangle = plot.Add.Rectangle(bar.Start, bar.End, y - 0.4, y + 0.4);
            rectangle.FillColor = ChartPalette.Get(bar.ColorIndex);
            rectangle.LineColor = ScottPlot.Colors.White;
            rectangle.LineWidth = 1;

            // Skip labels on bars too narrow to hold them.
            if (bar.End - bar.Start >= end * 0.025)
            {
                var text = plot.Add.Text(bar.Label, (bar.Start + bar.End) / 2, y);
                text.LabelStyle.Alignment = ScottPlot.Alignment.MiddleCenter;
                text.LabelStyle.ForeColor = ScottPlot.Colors.White;
                text.LabelStyle.FontSize = 11;
            }
        }

        plot.Axes.Left.TickGenerator = new ScottPlot.TickGenerators.NumericManual(
            rowLabels
                .Select((label, index) => new ScottPlot.Tick(lastRow - index, label))
                .ToArray());

        plot.Axes.SetLimits(0, end * 1.02, -0.6, lastRow + 0.6);

        plot.Title(title);
        plot.XLabel("Time");

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

        var height = Math.Max(400, 140 + rowLabels.Count * 55);

        return plot.GetImageBytes(width, height, ScottPlot.ImageFormat.Png);
    }
}
