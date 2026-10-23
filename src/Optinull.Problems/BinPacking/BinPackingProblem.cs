namespace Optinull.Problems.BinPacking;

/// <summary>
/// Pack items into as few identical bins as possible without any bin going over its capacity.
/// Real uses: cutting stock bars, loading trucks or containers by weight, placing virtual
/// machines on servers, filling backup discs. Every item must fit in an empty bin.
/// </summary>
public sealed class BinPackingProblem
{
    private readonly int[] _sizes;

    public BinPackingProblem(IEnumerable<int> sizes, int capacity)
    {
        ArgumentNullException.ThrowIfNull(sizes);

        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                "Capacity must be greater than zero.");
        }

        _sizes = sizes.ToArray();

        if (_sizes.Length == 0)
            throw new ArgumentException("Bin packing needs at least one item.", nameof(sizes));

        if (_sizes.Any(size => size <= 0 || size > capacity))
        {
            throw new ArgumentException(
                "Item sizes must be greater than zero and no larger than the bin capacity.",
                nameof(sizes));
        }

        Capacity = capacity;
    }

    public IReadOnlyList<int> Sizes => _sizes;

    public int ItemCount => _sizes.Length;

    public int Capacity { get; }

    public long TotalSize => _sizes.Sum(size => (long)size);

    /// <summary>The fewest bins that could possibly hold everything: total size over capacity, rounded up.</summary>
    public int LowerBound => (int)((TotalSize + Capacity - 1) / Capacity);
}

/// <summary>Which bin every item is in. Bins are numbered by first appearance, so equal packings look equal.</summary>
public sealed class BinPackingPlan
{
    private readonly int[] _binOf;

    public BinPackingPlan(IEnumerable<int> binOfItem)
    {
        ArgumentNullException.ThrowIfNull(binOfItem);

        var input = binOfItem.ToArray();

        if (input.Length == 0)
            throw new ArgumentException("A plan needs at least one item.", nameof(binOfItem));

        if (input.Any(bin => bin < 0))
            throw new ArgumentException("Bin numbers cannot be negative.", nameof(binOfItem));

        var ids = new Dictionary<int, int>();

        _binOf = new int[input.Length];

        for (var item = 0; item < input.Length; item++)
        {
            if (!ids.TryGetValue(input[item], out var id))
            {
                id = ids.Count;
                ids[input[item]] = id;
            }

            _binOf[item] = id;
        }

        BinCount = ids.Count;
    }

    /// <summary>Number of items.</summary>
    public int Count => _binOf.Length;

    /// <summary>Number of bins that hold something.</summary>
    public int BinCount { get; }

    public int this[int item] => _binOf[item];

    public IReadOnlyList<int> Assignment => _binOf;

    /// <summary>The item numbers in each bin.</summary>
    public IReadOnlyList<IReadOnlyList<int>> Bins()
    {
        var bins = Enumerable.Range(0, BinCount).Select(_ => new List<int>()).ToList();

        for (var item = 0; item < _binOf.Length; item++)
            bins[_binOf[item]].Add(item);

        return bins;
    }

    public static BinPackingPlan FromBins(IEnumerable<IEnumerable<int>> bins, int itemCount)
    {
        ArgumentNullException.ThrowIfNull(bins);

        var assignment = new int[itemCount];
        Array.Fill(assignment, -1);

        var next = 0;

        foreach (var bin in bins)
        {
            foreach (var item in bin)
            {
                if (item < 0 || item >= itemCount || assignment[item] >= 0)
                    throw new ArgumentException($"Item {item} is out of range or packed twice.", nameof(bins));

                assignment[item] = next;
            }

            next++;
        }

        if (assignment.Any(bin => bin < 0))
            throw new ArgumentException("Every item must be in a bin.", nameof(bins));

        return new BinPackingPlan(assignment);
    }
}
