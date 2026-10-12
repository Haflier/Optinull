namespace Optinull.Reporting.Charts;

internal static class ChartPalette
{
    private static readonly string[] Hex =
    [
        "#000000", "#e00f00", "#0bd904", "#630263", "#0802ba",
        "#d95604", "#404202", "#018068", "#474d4b", "#ba0fa6",
        "#5984c9", "#0c6900", "#6b3b0b", "#8b13d6", "#ed5858",
        "#84857a", "#cfb404", "#023816", "#4f0302", "#030236",
    ];

    public static ScottPlot.Color Get(int index) =>
        ScottPlot.Color.FromHex(Hex[((index % Hex.Length) + Hex.Length) % Hex.Length]);
}
