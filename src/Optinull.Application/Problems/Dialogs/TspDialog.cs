using System.Globalization;
using Optinull.Problems.Tsp;

namespace Optinull.Application.Problems;

/// <summary>Asks how many cities, then their coordinates.</summary>
internal static class TspDialog
{
    public static IProblemDialog Create(IProblemModule module)
    {
        var limits = TspInputLimits.Default;

        return new GuidedDialog(
            module,
            [
                new("cities",
                    "Let's describe your cities.\n" +
                    $"How many cities does the tour visit? (3-{limits.MaxCities})",
                    3, limits.MaxCities)
            ],
            new DialogEntries(
                "cities",
                Prompt,
                (line, _, lines) => ParseCity(line, lines, limits),
                (_, lines) => string.Join("\n", lines)));
    }

    private static string Prompt(IReadOnlyDictionary<string, int> answers, int done, int total)
    {
        if (done == 0)
        {
            return
                $"Send the coordinates of the {total} cities, one city per line as \"x y\". Example:\n" +
                "12.5 40\n8 17.25\n(decimals use a dot)\n\n" +
                "Send them all in one message, in several, or attach a .txt file. " +
                "Send \"undo\" to remove the last city.";
        }

        return $"{done} of {total} cities so far. Send {total - done} more, one \"x y\" per line, " +
               "or \"undo\" to remove the last one.";
    }

    private static LineResult ParseCity(string line, IReadOnlyList<string> lines, TspInputLimits limits)
    {
        var tokens = line.Split(new[] { ' ', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length != 2)
            return new LineResult(null, null, $"I need \"x y\", but found {tokens.Length} values.");

        if (!TryRead(tokens[0], limits, out var x) || !TryRead(tokens[1], limits, out var y))
        {
            return new LineResult(
                null,
                null,
                $"coordinates must be numbers between -{limits.MaxCoordinate:0} and {limits.MaxCoordinate:0}.");
        }

        var text = $"{x.ToString("R", CultureInfo.InvariantCulture)} {y.ToString("R", CultureInfo.InvariantCulture)}";

        return new LineResult(text, $"City {lines.Count + 1}: ({tokens[0]}, {tokens[1]})", null);
    }

    private static bool TryRead(string token, TspInputLimits limits, out double value) =>
        double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
        double.IsFinite(value) &&
        Math.Abs(value) <= limits.MaxCoordinate;
}
