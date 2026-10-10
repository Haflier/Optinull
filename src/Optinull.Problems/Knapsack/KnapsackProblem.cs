namespace Optinull.Problems.Knapsack;

public readonly record struct KnapsackItem(int Value, int Weight);

/// <summary>
/// Pick items to maximize total value without the total weight going over the
/// capacity. An item heavier than the capacity can never be picked.
/// </summary>
public sealed class KnapsackProblem
{
    private readonly List<KnapsackItem> _items = [];

    public KnapsackProblem(IEnumerable<KnapsackItem> items, int capacity)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                "Capacity must be greater than zero.");
        }

        _items.AddRange(items);

        if (_items.Count == 0)
            throw new ArgumentException("A knapsack needs at least one item.", nameof(items));

        if (_items.Any(item => item.Value <= 0 || item.Weight <= 0))
        {
            throw new ArgumentException(
                "Item values and weights must be greater than zero.",
                nameof(items));
        }

        Capacity = capacity;
    }

    public IReadOnlyList<KnapsackItem> Items => _items;

    public int ItemCount => _items.Count;

    public int Capacity { get; }
}

/// <summary>Which items are in the knapsack.</summary>
public sealed class KnapsackSelection
{
    private readonly bool[] _selected;

    public KnapsackSelection(IEnumerable<bool> selected)
    {
        ArgumentNullException.ThrowIfNull(selected);

        _selected = selected.ToArray();

        if (_selected.Length == 0)
            throw new ArgumentException("A selection needs at least one item.", nameof(selected));
    }

    public int Count => _selected.Length;

    public bool this[int index] => _selected[index];

    public IReadOnlyList<bool> Flags => _selected;

    public int SelectedCount => _selected.Count(flag => flag);
}
