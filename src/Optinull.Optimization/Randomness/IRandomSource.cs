namespace Optinull.Optimization.Randomness;

public interface IRandomSource
{
    double NextDouble();

    int Next(int minValue, int maxValue);
}
