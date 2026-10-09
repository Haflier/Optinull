namespace Optinull.Reporting.Charts;

internal static class ChartPalette
{
    private static readonly string[] Hex =
    [
        "#1f77b4", "#ff7f0e", "#2ca02c", "#d62728", "#9467bd",
        "#8c564b", "#e377c2", "#7f7f7f", "#bcbd22", "#17becf"
    ];

    public static ScottPlot.Color Get(int index) =>
        ScottPlot.Color.FromHex(Hex[((index % Hex.Length) + Hex.Length) % Hex.Length]);
}
