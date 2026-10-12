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

        // Where solvers end on the same value their lines lie on top of each other, so
        // those are drawn as nested stripes: the earliest is the thickest and underneath,
        // and every later one is thinner on top of it, like a background.
        var finalLevels = series.Select(item => item.Points[^1].Objective).ToList();

        static bool SameLevel(double first, double second) =>
            Math.Abs(first - second) <= Math.Max(Math.Abs(second) * 1e-9, 1e-12);

        var allXs = new List<double>();
        var allYs = new List<double>();

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

            allXs.AddRange(xs);
            allYs.AddRange(ys);

            var scatter = plot.Add.Scatter(xs.ToArray(), ys.ToArray());
            scatter.ConnectStyle = ScottPlot.ConnectStyle.StepHorizontal;

            var sharing = finalLevels.Count(level => SameLevel(level, finalLevels[index]));
            var laterSharing = finalLevels
                .Skip(index + 1)
                .Count(level => SameLevel(level, finalLevels[index]));

            var lineWidth = sharing > 1 ? 2f + laterSharing * 4f : 2f;

            // A run that ended after one evaluation is a single point, which a line cannot
            // show; the marker is a little wider than the stripe so it stays visible.
            scatter.MarkerSize = xs.Count == 1 ? Math.Max(8f, lineWidth + 4f) : 0;
            scatter.LineWidth = lineWidth;
            scatter.Color = ChartPalette.Get(index);
            scatter.LegendText = current.Name;
        }

        // When every solver reaches the same value (a trivial problem) the data has no
        // height or width, and the chart would be an empty frame. Give it some room.
        var lowest = allYs.Min();
        var highest = allYs.Max();

        if (highest - lowest <= Math.Abs(highest) * 1e-9)
        {
            var margin = Math.Max(Math.Abs(highest) * 0.05, 1.0);

            plot.Axes.SetLimitsY(lowest - margin, highest + margin);
        }

        if (allXs.Max() - allXs.Min() < 1e-9)
            plot.Axes.SetLimitsX(allXs.Min() - 1, allXs.Max() + 1);

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
