using Optinull.Problems.BinPacking;

namespace Optinull.Application.Problems;

/// <summary>Asks for the bin capacity, then how many kinds of item, then each kind's size and quantity.</summary>
internal static class BinPackingDialog
{
    public static IProblemDialog Create(IProblemModule module)
    {
        var limits = BinPackingInputLimits.Default;

        return new GuidedDialog(
            module,
            [
                new("capacity",
                    "Let's describe what you need to pack.\n" +
                    "How much can one bin hold (a box, truck, bar or disc)? " +
                    $"(1-{limits.MaxCapacity})",
                    1, limits.MaxCapacity),
                new("types",
                    $"How many different item sizes are there? (1-{limits.MaxItems})",
                    1, limits.MaxItems)
            ],
            new DialogEntries(
                "types",
                Prompt,
                (line, answers, lines) => ParseLine(line, answers["capacity"], lines, limits),
                (answers, lines) => $"{answers["capacity"]}\n{string.Join("\n", lines)}"));
    }

    private static string Prompt(IReadOnlyDictionary<string, int> answers, int done, int total)
    {
        if (done == 0)
        {
            return
                $"Item size 1 of {total}. Send each as \"size quantity\". Example:\n2400 3\n" +
                "(three items of size 2400; the quantity is optional and defaults to 1)\n\n" +
                "You can send several lines at once, one per line. Send \"undo\" to remove the last line.";
        }

        return $"Item size {done + 1} of {total}: \"size quantity\", for example 2400 3. " +
               "Several lines at once: one per line. \"undo\" removes the last line.";
    }

    private static LineResult ParseLine(
        string line,
        int capacity,
        IReadOnlyList<string> lines,
        BinPackingInputLimits limits)
    {
        var tokens = DialogNumbers.Tokens(line, ',', ';');

        if (tokens.Length is < 1 or > 2)
        {
            return new LineResult(
                null,
                null,
                $"I need \"size\" or \"size quantity\", but found {tokens.Length} values.");
        }

        if (!DialogNumbers.TryWhole(tokens[0], out var size) || size < 1 || size > capacity)
        {
            return new LineResult(
                null,
                null,
                $"the size must be a whole number from 1 to the bin capacity {capacity}.");
        }

        var quantity = 1;

        if (tokens.Length == 2 &&
            (!DialogNumbers.TryWhole(tokens[1], out quantity) || quantity < 1 || quantity > limits.MaxItems))
        {
            return new LineResult(
                null,
                null,
                $"the quantity must be a whole number from 1 to {limits.MaxItems}.");
        }

        // Lines are stored as "size quantity".
        var total = lines.Sum(existing => int.Parse(existing.Split(' ')[1])) + quantity;

        if (total > limits.MaxItems)
        {
            return new LineResult(
                null,
                null,
                $"that would make {total} items in all; the limit is {limits.MaxItems}.");
        }

        return new LineResult(
            $"{size} {quantity}",
            $"Item size {lines.Count + 1}: size {size}, quantity {quantity}",
            null);
    }
}
