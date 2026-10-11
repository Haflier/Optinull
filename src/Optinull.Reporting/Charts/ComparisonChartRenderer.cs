namespace Optinull.Reporting.Charts;

public readonly record struct ComparisonBar(string Label, double Value, bool IsBest);

/// <summary>Draws one bar per solver, with the best one highlighted, as PNG bytes.</summary>
public sealed class ComparisonChartRenderer
{
    public byte[] Render(
        IReadOnlyList<ComparisonBar> bars,
        string title,
        string valueLabel,
        int width = 1000,
        int height = 700)
    {
        ArgumentNullException.ThrowIfNull(bars);

        if (bars.Count == 0)
            throw new ArgumentException("At least one bar is required.", nameof(bars));

        var plot = new ScottPlot.Plot();

        var barPlot = plot.Add.Bars(bars.Select(bar => bar.Value).ToArray());

        for (var i = 0; i < bars.Count; i++)
        {
            barPlot.Bars[i].FillColor = bars[i].IsBest
                ? ChartPalette.Get(2)
                : ScottPlot.Color.FromHex("#4d4b46");
        }

        // The value goes under the name, so every bar can be read exactly.
        plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(
            bars
                .Select((bar, index) => new ScottPlot.Tick(index, $"{bar.Label}\n{bar.Value:0.##}"))
                .ToArray());

        var top = Math.Max(bars.Max(bar => bar.Value), 1e-9);

        plot.Axes.SetLimitsY(0, top * 1.15);

        plot.Title(title);
        plot.YLabel(valueLabel);

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
