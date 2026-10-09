namespace Optinull.Problems.Tsp;

public readonly record struct City(double X, double Y);

/// <summary>A set of cities; the goal is the shortest closed tour through all of them.</summary>
public sealed class TspProblem
{
    private readonly double[,] _distances;
    private readonly List<City> _cities = [];

    public TspProblem(IEnumerable<City> cities)
    {
        ArgumentNullException.ThrowIfNull(cities);

        _cities.AddRange(cities);

        if (_cities.Count < 3)
        {
            throw new ArgumentException(
                "A tour needs at least three cities.",
                nameof(cities));
        }

        if (_cities.Any(city => !double.IsFinite(city.X) || !double.IsFinite(city.Y)))
        {
            throw new ArgumentException(
                "City coordinates must be finite numbers.",
                nameof(cities));
        }

        _distances = new double[_cities.Count, _cities.Count];

        for (var a = 0; a < _cities.Count; a++)
        {
            for (var b = 0; b < _cities.Count; b++)
            {
                _distances[a, b] = Math.Sqrt(
                    Math.Pow(_cities[a].X - _cities[b].X, 2) +
                    Math.Pow(_cities[a].Y - _cities[b].Y, 2));
            }
        }
    }

    public IReadOnlyList<City> Cities => _cities;

    public int CityCount => _cities.Count;

    public double Distance(int from, int to) => _distances[from, to];
}
