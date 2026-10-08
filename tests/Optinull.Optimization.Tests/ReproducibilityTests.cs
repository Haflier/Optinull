using Optinull.Optimization.Randomness;

namespace Optinull.Optimization.Tests;

public sealed class ReproducibilityTests
{
    [Fact]
    public void RandomSource_SameSeed_ProducesSameSequence()
    {
        var first = new RandomSource(123);
        var second = new RandomSource(123);

        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(first.Next(0, 1000), second.Next(0, 1000));
            Assert.Equal(first.NextDouble(), second.NextDouble());
        }
    }
}
