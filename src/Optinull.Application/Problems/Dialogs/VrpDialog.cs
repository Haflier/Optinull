using System.Globalization;
using Optinull.Problems.Vrp;

namespace Optinull.Application.Problems;

/// <summary>Asks for vehicles, capacity and customers, then the depot and each customer's place and demand.</summary>
internal static class VrpDialog
{
    public static IProblemDialog Create(IProblemModule module)
    {
        var limits = VrpInputLimits.Default;

        return new GuidedDialog(
            module,
            [
                new("vehicles",
                    "Let's describe your delivery problem.\n" +
                    $"How many vehicles are there? (1-{limits.MaxVehicles})",
                    1, limits.MaxVehicles),
                new("capacity",
                    $"How much can one vehicle carry? (1-{limits.MaxCapacity})",
                    1, limits.MaxCapacity),
                new("customers",
                    $"How many customers need a delivery? (1-{limits.MaxCustomers})",
                    1, limits.MaxCustomers)
            ],
            new DialogEntries(
                "customers",
                Prompt,
                (line, answers, lines) => ParseLine(line, answers["capacity"], lines, limits),
                (answers, lines) => $"{answers["vehicles"]} {answers["capacity"]}\n{string.Join("\n", lines)}",
                ExtraLines: 1));
    }

    // The first line is the depot; every later one is a customer.
    private static string Prompt(IReadOnlyDictionary<string, int> answers, int done, int total)
    {
        var customers = total - 1;
        var capacity = answers["capacity"];

        if (done == 0)
        {
            return
                "First the depot, where the vehicles start and end, as \"x y\". Then one customer per line " +
                $"as \"x y demand\" ({customers} of them). The demand is how much the customer needs, " +
                $"at most {capacity}. Example:\n50 50\n20 30 7\n80 65 12\n\n" +
                "Send the depot first. You can send several lines at once, or attach a .txt file. " +
                "Send \"undo\" to remove the last line.";
        }

        return $"{done - 1} of {customers} customers so far. Send {total - done} more as \"x y demand\", " +
               "or \"undo\" to remove the last line.";
    }

    private static LineResult ParseLine(
        string line,
        int capacity,
        IReadOnlyList<string> lines,
        VrpInputLimits limits)
    {
        var isDepot = lines.Count == 0;
        var expected = isDepot ? 2 : 3;
        var tokens = line.Split(new[] { ' ', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length != expected)
        {
            return new LineResult(
                null,
                null,
                isDepot
                    ? $"the depot must be \"x y\", but I found {tokens.Length} values."
                    : $"I need \"x y demand\", but found {tokens.Length} values.");
        }

        if (!TryRead(tokens[0], limits, out var x) || !TryRead(tokens[1], limits, out var y))
        {
            return new LineResult(
                null,
                null,
                $"coordinates must be numbers between -{limits.MaxCoordinate:0} and {limits.MaxCoordinate:0}.");
        }

        var text = $"{x.ToString("R", CultureInfo.InvariantCulture)} {y.ToString("R", CultureInfo.InvariantCulture)}";

        if (isDepot)
            return new LineResult(text, $"Depot: ({tokens[0]}, {tokens[1]})", null);

        if (!DialogNumbers.TryWhole(tokens[2], out var demand) || demand < 1 || demand > capacity)
            return new LineResult(null, null, $"the demand must be a whole number from 1 to the capacity {capacity}.");

        return new LineResult(
            $"{text} {demand}",
            $"Customer {lines.Count}: ({tokens[0]}, {tokens[1]}), demand {demand}",
            null);
    }

    private static bool TryRead(string token, VrpInputLimits limits, out double value) =>
        double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
        double.IsFinite(value) &&
        Math.Abs(value) <= limits.MaxCoordinate;
}
