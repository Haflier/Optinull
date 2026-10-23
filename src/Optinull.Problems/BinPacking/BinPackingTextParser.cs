using System.Globalization;

namespace Optinull.Problems.BinPacking;

/// <summary>Size limits for user-supplied bin packing problems.</summary>
public sealed record BinPackingInputLimits(
    int MaxItems = 200,
    int MaxCapacity = 100_000,
    int MaxCharacters = 20_000)
{
    public static BinPackingInputLimits Default { get; } = new();
}

/// <summary>
/// Reads a bin packing problem from text: the first line is the capacity of one bin, then one
/// line per kind of item as "size" or "size quantity". Blank lines and lines starting with #
/// are ignored.
/// </summary>
public static class BinPackingTextParser
{
    private static readonly char[] Separators = [' ', '\t', ',', ';'];

    public static bool TryParse(
        string? text,
        BinPackingInputLimits limits,
        out BinPackingProblem? problem,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(limits);

        error = Parse(text, limits, out problem);

        return error is null;
    }

    private static string? Parse(
        string? text,
        BinPackingInputLimits limits,
        out BinPackingProblem? problem)
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
            return $"Line {lines[0].Number}: the first line must be the capacity of one bin, " +
                   $"a whole number from 1 to {limits.MaxCapacity}.";
        }

        var sizes = new List<int>();

        foreach (var line in lines.Skip(1))
        {
            var tokens = line.Content.Split(Separators, StringSplitOptions.RemoveEmptyEntries);

            if (tokens.Length is < 1 or > 2)
            {
                return $"Line {line.Number}: expected 'size' or 'size quantity', " +
                       $"but found {tokens.Length} values.";
            }

            if (!TryRead(tokens[0], limits.MaxCapacity, out var size))
            {
                return $"Line {line.Number}: the size must be a whole number " +
                       $"from 1 to {limits.MaxCapacity}.";
            }

            var quantity = 1;

            if (tokens.Length == 2 && !TryRead(tokens[1], limits.MaxItems, out quantity))
            {
                return $"Line {line.Number}: the quantity must be a whole number " +
                       $"from 1 to {limits.MaxItems}.";
            }

            if (size > capacity)
            {
                return $"Line {line.Number}: an item of size {size} cannot fit " +
                       $"in a bin of capacity {capacity}.";
            }

            if (sizes.Count + quantity > limits.MaxItems)
                return $"Too many items; the limit is {limits.MaxItems} (quantities count).";

            sizes.AddRange(Enumerable.Repeat(size, quantity));
        }

        if (sizes.Count == 0)
            return "Add at least one item (a 'size' or 'size quantity' line) after the capacity.";

        problem = new BinPackingProblem(sizes, capacity);

        return null;
    }

    private static bool TryRead(string token, int max, out int value) =>
        int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out value) &&
        value >= 1 &&
        value <= max;
}
