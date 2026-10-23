namespace Optinull.Problems.BinPacking;

public static class BinPackingInstances
{
    /// <summary>Random item sizes between minSize and maxSize (inclusive), all fitting an empty bin.</summary>
    public static BinPackingProblem Random(int itemCount, int capacity, int minSize, int maxSize, int seed)
    {
        if (itemCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(itemCount));

        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        if (minSize < 1 || minSize > maxSize || maxSize > capacity)
            throw new ArgumentException("Sizes must satisfy 1 <= minSize <= maxSize <= capacity.");

        var random = new Random(seed);

        var sizes = Enumerable
            .Range(0, itemCount)
            .Select(_ => random.Next(minSize, maxSize + 1))
            .ToList();

        return new BinPackingProblem(sizes, capacity);
    }
}
