namespace Optinull.Problems.Tsp;

/// <summary>The order in which the cities are visited; the tour returns to the first.</summary>
public sealed class TspTour
{
    private readonly int[] _cities;

    public TspTour(IEnumerable<int> cities)
    {
        ArgumentNullException.ThrowIfNull(cities);

        _cities = cities.ToArray();

        if (_cities.Length == 0)
        {
            throw new ArgumentException(
                "A tour must visit at least one city.",
                nameof(cities));
        }
    }

    public IReadOnlyList<int> Cities => _cities;

    public int Count => _cities.Length;
}
