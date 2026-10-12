using Optinull.Problems.Knapsack;

namespace Optinull.Application.Problems;

/// <summary>Asks for the capacity, then how many items, then each item's value and weight.</summary>
internal static class KnapsackDialog
{
    public static IProblemDialog Create(IProblemModule module)
    {
        var limits = KnapsackInputLimits.Default;

        return new GuidedDialog(
            module,
            [
                new("capacity",
                    "Let's describe your knapsack.\n" +
                    "What is its capacity, the largest total weight it can carry? " +
                    $"(1-{limits.MaxCapacity})",
                    1, limits.MaxCapacity),
                new("items",
                    $"How many items could go in it? (1-{limits.MaxItems})",
                    1, limits.MaxItems)
            ],
            new DialogEntries(
                "items",
                Prompt,
                (line, answers, lines) => ParseItem(line, answers["capacity"], lines, limits),
                (answers, lines) => $"{answers["capacity"]}\n{string.Join("\n", lines)}"));
    }

    private static string Prompt(IReadOnlyDictionary<string, int> answers, int done, int total)
    {
        if (done == 0)
        {
            return
                $"Item 1 of {total}. Send each item as \"value weight\". Example:\n60 10\n" +
                "(worth 60, weighs 10)\n\n" +
                "You can send several items at once, one per line. Send \"undo\" to remove the last item.";
        }

        return $"Item {done + 1} of {total}: \"value weight\", for example 60 10. " +
               "Several items at once: one per line. \"undo\" removes the last item.";
    }

    private static LineResult ParseItem(
        string line,
        int capacity,
        IReadOnlyList<string> lines,
        KnapsackInputLimits limits)
    {
        var tokens = DialogNumbers.Tokens(line, ',', ';');

        if (tokens.Length != 2)
            return new LineResult(null, null, $"I need \"value weight\", but found {tokens.Length} values.");

        if (!DialogNumbers.TryWhole(tokens[0], out var value) || value < 1 || value > limits.MaxItemValue)
            return new LineResult(null, null, $"the value must be a whole number from 1 to {limits.MaxItemValue}.");

        if (!DialogNumbers.TryWhole(tokens[1], out var weight) || weight < 1 || weight > limits.MaxCapacity)
            return new LineResult(null, null, $"the weight must be a whole number from 1 to {limits.MaxCapacity}.");

        var echo = $"Item {lines.Count + 1}: value {value}, weight {weight}" +
                   (weight > capacity ? " (heavier than the knapsack, so it can never be packed)" : string.Empty);

        return new LineResult($"{value} {weight}", echo, null);
    }
}
