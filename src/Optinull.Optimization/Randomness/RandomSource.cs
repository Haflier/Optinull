namespace Optinull.Optimization.Randomness;

public sealed class RandomSource : IRandomSource
{
    private readonly Random _random;

    public RandomSource()
        : this(Random.Shared)
    {
    }

    public RandomSource(int seed)
        : this(new Random(seed))
    {
    }

    private RandomSource(Random random)
    {
        _random = random;
    }

    public double NextDouble()
    {
        return _random.NextDouble();
    }

    public int Next(int minValue, int maxValue)
    {
        return _random.Next(minValue, maxValue);
    }
}
