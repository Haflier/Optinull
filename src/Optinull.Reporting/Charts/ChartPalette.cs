namespace Optinull.Reporting.Charts;

internal static class ChartPalette
{
    private static readonly string[] Hex =
    [
        "#000000", "#e00f00", "#0c6900", "#0802ba", "#630263",
        "#d95604", "#404202", "#018068", "#474d4b", "#ba0fa6",
        "#5984c9", "#0bd904", "#6b3b0b", "#8b13d6", "#ed5858",
        "#cfb404", "#84857a", "#023816", "#4f0302", "#030236",
    ];

    public static ScottPlot.Color Get(int index) =>
        ScottPlot.Color.FromHex(Hex[((index % Hex.Length) + Hex.Length) % Hex.Length]);
}
