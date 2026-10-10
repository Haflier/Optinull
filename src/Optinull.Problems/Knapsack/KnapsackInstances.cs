namespace Optinull.Problems.Knapsack;

public static class KnapsackInstances
{
    /// <summary>
    /// Random items (weight 1-50, value 10-100). The capacity is 40% of the total
    /// weight, but never less than the heaviest item, so every item fits alone.
    /// </summary>
    public static KnapsackProblem Random(int itemCount, int seed)
    {
        if (itemCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(itemCount));

        var random = new Random(seed);

        var items = Enumerable
            .Range(0, itemCount)
            .Select(_ =>
            {
                var weight = random.Next(1, 51);
                var value = random.Next(10, 101);

                return new KnapsackItem(value, weight);
            })
            .ToList();

        var capacity = Math.Max(
            (int)(items.Sum(item => (long)item.Weight) * 2 / 5),
            items.Max(item => item.Weight));

        return new KnapsackProblem(items, capacity);
    }
}
