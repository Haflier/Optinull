namespace Optinull.Reporting.Charts;

/// <summary>The blue figure and tan plot area every chart shares.</summary>
internal static class ChartTheme
{
    public static void Apply(ScottPlot.Plot plot)
    {
        ArgumentNullException.ThrowIfNull(plot);

        plot.FigureBackground.Color = ScottPlot.Color.FromHex("#0747af");
        plot.DataBackground.Color = ScottPlot.Color.FromHex("#c0c08a");

        plot.Axes.Color(ScottPlot.Color.FromHex("#ffffff"));

        plot.Axes.Left.TickLabelStyle.ForeColor = ScottPlot.Color.FromHex("#ffffff");
        plot.Axes.Bottom.TickLabelStyle.ForeColor = ScottPlot.Color.FromHex("#ffffff");

        plot.Grid.MajorLineColor = ScottPlot.Color.FromHex("#1f74b5");
        plot.Grid.MinorLineColor = ScottPlot.Color.FromHex("#1f74b5");
    }
}
