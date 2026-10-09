namespace Optinull.Problems.Tsp;

/// <summary>Generated instances whose optimal tour length is known exactly.</summary>
public static class TspBenchmarkInstances
{
    /// <summary>Cities evenly spaced on a circle; the optimum is the polygon around it.</summary>
    public static TspProblem Circle(int cityCount, double radius = 100)
    {
        if (cityCount < 3)
            throw new ArgumentOutOfRangeException(nameof(cityCount));

        return new TspProblem(
            Enumerable.Range(0, cityCount).Select(i =>
            {
                var angle = 2 * Math.PI * i / cityCount;

                return new City(radius * Math.Cos(angle), radius * Math.Sin(angle));
            }));
    }

    public static double CircleOptimum(int cityCount, double radius = 100) =>
        2 * cityCount * radius * Math.Sin(Math.PI / cityCount);

    /// <summary>A square grid of cities. An even side has a tour using only grid edges.</summary>
    public static TspProblem Grid(int side, double spacing = 10)
    {
        if (side < 2 || side % 2 != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(side),
                "The grid side must be an even number of at least 2.");
        }

        return new TspProblem(
            Enumerable.Range(0, side * side).Select(i =>
                new City(i % side * spacing, i / side * spacing)));
    }

    public static double GridOptimum(int side, double spacing = 10) =>
        side * side * spacing;

    /// <summary>Pseudo-random cities in a square. The optimum is unknown.</summary>
    public static TspProblem RandomCities(int cityCount, int seed, double size = 1000)
    {
        if (cityCount < 3)
            throw new ArgumentOutOfRangeException(nameof(cityCount));

        var random = new Random(seed);

        return new TspProblem(
            Enumerable
                .Range(0, cityCount)
                .Select(_ => new City(random.NextDouble() * size, random.NextDouble() * size))
                .ToList());
    }
}
