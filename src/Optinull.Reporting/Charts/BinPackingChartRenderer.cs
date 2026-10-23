namespace Optinull.Reporting.Charts;

/// <summary>Draws every bin as a column of stacked items against its capacity, as PNG bytes.</summary>
public sealed class BinPackingChartRenderer
{
    public byte[] Render(
        IReadOnlyList<IReadOnlyList<int>> bins,
        int capacity,
        string title,
        int width = 1400,
        int height = 700)
    {
        ArgumentNullException.ThrowIfNull(bins);

        if (bins.Count == 0)
            throw new ArgumentException("At least one bin is required.", nameof(bins));

        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");

        if (bins.Any(bin => bin.Count == 0 || bin.Any(size => size <= 0)))
            throw new ArgumentException("Every bin needs items with a positive size.", nameof(bins));

        var plot = new ScottPlot.Plot();
        var showLabels = bins.Count <= 30;
        var top = (double)capacity;

        for (var index = 0; index < bins.Count; index++)
        {
            var x = index + 1;

            // The whole bin first, so the room that is left over shows.
            var shell = plot.Add.Rectangle(x - 0.4, x + 0.4, 0, capacity);
            shell.FillColor = ScottPlot.Color.FromHex("#f4f4dc");
            shell.LineColor = ScottPlot.Colors.White;
            shell.LineWidth = 1;

            double bottom = 0;

            for (var k = 0; k < bins[index].Count; k++)
            {
                var size = bins[index][k];

                var piece = plot.Add.Rectangle(x - 0.4, x + 0.4, bottom, bottom + size);
                piece.FillColor = ChartPalette.Get(k + 1);
                piece.LineColor = ScottPlot.Colors.White;
                piece.LineWidth = 1;

                if (showLabels && size >= capacity * 0.04)
                {
                    var text = plot.Add.Text(size.ToString(), x, bottom + size / 2.0);
                    text.LabelStyle.Alignment = ScottPlot.Alignment.MiddleCenter;
                    text.LabelStyle.ForeColor = ScottPlot.Colors.White;
                    text.LabelStyle.FontSize = 11;
                }

                bottom += size;
            }

            top = Math.Max(top, bottom);
        }

        var step = Math.Max(1, bins.Count / 20);

        plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(
            Enumerable
                .Range(0, bins.Count)
                .Where(index => index % step == 0)
                .Select(index => new ScottPlot.Tick(index + 1, (index + 1).ToString()))
                .ToArray());

        plot.Axes.SetLimits(0.4, bins.Count + 0.6, 0, top * 1.05);

        plot.Title(title);
        plot.XLabel("Bin");
        plot.YLabel("Load");

        ChartTheme.Apply(plot);

        return plot.GetImageBytes(width, height, ScottPlot.ImageFormat.Png);
    }
}
