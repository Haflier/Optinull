using System.Globalization;

namespace Optinull.Problems.Tsp;

/// <summary>Size limits for user-supplied TSP instances.</summary>
public sealed record TspInputLimits(
    int MaxCities = 200,
    int MaxCharacters = 20_000,
    double MaxCoordinate = 1_000_000)
{
    public static TspInputLimits Default { get; } = new();
}

/// <summary>
/// Reads cities from text: one city per line as "x y" (or "id x y", where the id
/// is ignored). Blank lines and lines starting with # are ignored. Decimals use a dot.
/// </summary>
public static class TspTextParser
{
    private static readonly char[] Separators = [' ', '\t', ',', ';'];

    public static bool TryParse(
        string? text,
        TspInputLimits limits,
        out TspProblem? problem,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(limits);

        error = Parse(text, limits, out problem);

        return error is null;
    }

    private static string? Parse(
        string? text,
        TspInputLimits limits,
        out TspProblem? problem)
    {
        problem = null;

        if (string.IsNullOrWhiteSpace(text))
            return "The instance is empty.";

        if (text.Length > limits.MaxCharacters)
            return $"The instance is too long (limit {limits.MaxCharacters} characters).";

        var lines = text
            .Replace("\r", string.Empty)
            .Split('\n')
            .Select((content, index) => (Number: index + 1, Content: content.Trim()))
            .Where(line => line.Content.Length > 0 && !line.Content.StartsWith('#'));

        var cities = new List<City>();
        var columns = 0;

        foreach (var line in lines)
        {
            var tokens = line.Content.Split(Separators, StringSplitOptions.RemoveEmptyEntries);

            if (tokens.Length is not (2 or 3))
            {
                return $"Line {line.Number}: expected 'x y' (or 'id x y'), " +
                       $"but found {tokens.Length} values.";
            }

            if (columns == 0)
                columns = tokens.Length;
            else if (tokens.Length != columns)
                return $"Line {line.Number}: every line must have the same number of values.";

            if (!TryRead(tokens[^2], limits, out var x) ||
                !TryRead(tokens[^1], limits, out var y))
            {
                return $"Line {line.Number}: coordinates must be numbers " +
                       $"between -{limits.MaxCoordinate:0} and {limits.MaxCoordinate:0}.";
            }

            cities.Add(new City(x, y));

            if (cities.Count > limits.MaxCities)
                return $"Too many cities; the limit is {limits.MaxCities}.";
        }

        if (cities.Count == 0)
            return "The instance is empty.";

        if (cities.Count < 3)
            return $"A tour needs at least 3 cities, found {cities.Count}.";

        problem = new TspProblem(cities);

        return null;
    }

    private static bool TryRead(string token, TspInputLimits limits, out double value) =>
        double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
        double.IsFinite(value) &&
        Math.Abs(value) <= limits.MaxCoordinate;
}
