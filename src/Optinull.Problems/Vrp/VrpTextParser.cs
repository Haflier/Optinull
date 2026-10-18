using System.Globalization;

namespace Optinull.Problems.Vrp;

/// <summary>Size limits for user-supplied routing problems.</summary>
public sealed record VrpInputLimits(
    int MaxCustomers = 100,
    int MaxVehicles = 20,
    int MaxCapacity = 100_000,
    double MaxCoordinate = 1_000_000,
    int MaxCharacters = 20_000)
{
    public static VrpInputLimits Default { get; } = new();
}

/// <summary>
/// Reads a routing problem from text. The first line is "vehicles capacity", the second is the
/// depot as "x y", and every later line is a customer as "x y demand". Blank lines and lines
/// starting with # are ignored.
/// </summary>
public static class VrpTextParser
{
    private static readonly char[] Separators = [' ', '\t', ',', ';'];

    public static bool TryParse(
        string? text,
        VrpInputLimits limits,
        out VrpProblem? problem,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(limits);

        error = Parse(text, limits, out problem);

        return error is null;
    }

    private static string? Parse(string? text, VrpInputLimits limits, out VrpProblem? problem)
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

        if (header.Length != 2 ||
            !TryWhole(header[0], limits.MaxVehicles, out var vehicles) ||
            !TryWhole(header[1], limits.MaxCapacity, out var capacity))
        {
            return $"Line {lines[0].Number}: the first line must be \"vehicles capacity\", " +
                   $"whole numbers (vehicles 1 to {limits.MaxVehicles}, capacity 1 to {limits.MaxCapacity}).";
        }

        if (lines.Count < 2)
            return "Add the depot, as \"x y\", on the line after \"vehicles capacity\".";

        var depot = lines[1].Content.Split(Separators, StringSplitOptions.RemoveEmptyEntries);

        if (depot.Length != 2 ||
            !TryCoordinate(depot[0], limits, out var depotX) ||
            !TryCoordinate(depot[1], limits, out var depotY))
        {
            return $"Line {lines[1].Number}: the depot must be \"x y\", numbers between " +
                   $"-{limits.MaxCoordinate:0} and {limits.MaxCoordinate:0}.";
        }

        var customers = new List<VrpCustomer>();

        foreach (var line in lines.Skip(2))
        {
            var tokens = line.Content.Split(Separators, StringSplitOptions.RemoveEmptyEntries);

            if (tokens.Length != 3)
            {
                return $"Line {line.Number}: expected \"x y demand\", but found {tokens.Length} values.";
            }

            if (!TryCoordinate(tokens[0], limits, out var x) || !TryCoordinate(tokens[1], limits, out var y))
            {
                return $"Line {line.Number}: coordinates must be numbers between " +
                       $"-{limits.MaxCoordinate:0} and {limits.MaxCoordinate:0}.";
            }

            if (!TryWhole(tokens[2], capacity, out var demand))
            {
                return $"Line {line.Number}: the demand must be a whole number from 1 to the capacity {capacity}.";
            }

            customers.Add(new VrpCustomer(x, y, demand));

            if (customers.Count > limits.MaxCustomers)
                return $"Too many customers; the limit is {limits.MaxCustomers}.";
        }

        if (customers.Count == 0)
            return "Add at least one customer, as \"x y demand\", after the depot.";

        var required = VrpProblem.RequiredVehicles(customers.Select(customer => customer.Demand), capacity);

        if (vehicles < required)
        {
            return $"These loads need at least {required} vehicles of capacity {capacity}, " +
                   $"but the first line gives {vehicles}.";
        }

        problem = new VrpProblem(depotX, depotY, customers, vehicles, capacity);

        return null;
    }

    private static bool TryWhole(string token, int max, out int value) =>
        int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out value) &&
        value >= 1 &&
        value <= max;

    private static bool TryCoordinate(string token, VrpInputLimits limits, out double value) =>
        double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
        double.IsFinite(value) &&
        Math.Abs(value) <= limits.MaxCoordinate;
}
