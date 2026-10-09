namespace Optinull.Reporting.Charts;

/// <summary>Draws best-so-far convergence of one or more solvers as PNG bytes.</summary>
public sealed class ConvergenceChartRenderer
{
    public byte[] Render(
        IReadOnlyList<ConvergenceSeries> series,
        string title,
        int width = 1200,
        int height = 700)
    {
        ArgumentNullException.ThrowIfNull(series);

        if (series.Count == 0)
            throw new ArgumentException("At least one series is required.", nameof(series));

        if (series.Any(s => s.Points.Count == 0))
            throw new ArgumentException("Every series needs at least one point.", nameof(series));

        var plot = new ScottPlot.Plot();

        for (var index = 0; index < series.Count; index++)
        {
            var current = series[index];

            var xs = current.Points.Select(point => (double)point.Evaluations).ToList();
            var ys = current.Points.Select(point => point.Objective).ToList();

            // Points exist only where the best value improved, so extend the
            // last level to the end of the run.
            if (current.FinalEvaluations > xs[^1])
            {
                xs.Add(current.FinalEvaluations);
                ys.Add(ys[^1]);
            }

            var scatter = plot.Add.Scatter(xs.ToArray(), ys.ToArray());
            scatter.ConnectStyle = ScottPlot.ConnectStyle.StepHorizontal;
            scatter.MarkerSize = 0;
            scatter.LineWidth = 2;
            scatter.Color = ChartPalette.Get(index);
            scatter.LegendText = current.Name;
        }

        plot.ShowLegend(ScottPlot.Alignment.UpperRight);
        plot.Title(title);
        plot.XLabel("Evaluations");
        plot.YLabel("Best objective");

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
