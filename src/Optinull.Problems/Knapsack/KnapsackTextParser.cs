using System.Globalization;

namespace Optinull.Problems.Knapsack;

/// <summary>Size limits for user-supplied knapsacks.</summary>
public sealed record KnapsackInputLimits(
    int MaxItems = 100,
    int MaxCapacity = 100_000,
    int MaxItemValue = 100_000,
    int MaxCharacters = 20_000)
{
    public static KnapsackInputLimits Default { get; } = new();
}

/// <summary>
/// Reads a knapsack from text: the first line is the capacity, then one item per
/// line as "value weight". Blank lines and lines starting with # are ignored.
/// </summary>
public static class KnapsackTextParser
{
    private static readonly char[] Separators = [' ', '\t', ',', ';'];

    public static bool TryParse(
        string? text,
        KnapsackInputLimits limits,
        out KnapsackProblem? problem,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(limits);

        error = Parse(text, limits, out problem);

        return error is null;
    }

    private static string? Parse(
        string? text,
        KnapsackInputLimits limits,
        out KnapsackProblem? problem)
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
            .Where(line => line.Content.Length > 0 && !line.Content.StartsWith('#'))
            .ToList();

        if (lines.Count == 0)
            return "The instance is empty.";

        var header = lines[0].Content.Split(Separators, StringSplitOptions.RemoveEmptyEntries);

        if (header.Length != 1 ||
            !TryRead(header[0], limits.MaxCapacity, out var capacity))
        {
            return $"Line {lines[0].Number}: the first line must be the capacity, " +
                   $"a whole number from 1 to {limits.MaxCapacity}.";
        }

        var items = new List<KnapsackItem>();

        foreach (var line in lines.Skip(1))
        {
            var tokens = line.Content.Split(Separators, StringSplitOptions.RemoveEmptyEntries);

            if (tokens.Length != 2)
            {
                return $"Line {line.Number}: expected 'value weight', " +
                       $"but found {tokens.Length} values.";
            }

            if (!TryRead(tokens[0], limits.MaxItemValue, out var value) ||
                !TryRead(tokens[1], limits.MaxCapacity, out var weight))
            {
                return $"Line {line.Number}: value and weight must be whole numbers " +
                       $"greater than zero (value up to {limits.MaxItemValue}, " +
                       $"weight up to {limits.MaxCapacity}).";
            }

            items.Add(new KnapsackItem(value, weight));

            if (items.Count > limits.MaxItems)
                return $"Too many items; the limit is {limits.MaxItems}.";
        }

        if (items.Count == 0)
            return "Add at least one item (a 'value weight' line) after the capacity.";

        problem = new KnapsackProblem(items, capacity);

        return null;
    }

    private static bool TryRead(string token, int max, out int value) =>
        int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out value) &&
        value >= 1 &&
        value <= max;
}
