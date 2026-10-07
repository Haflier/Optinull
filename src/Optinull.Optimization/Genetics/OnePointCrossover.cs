using Optinull.Domain.Problems;
using Optinull.Domain.Solutions;
using Optinull.Optimization.Randomness;

namespace Optinull.Optimization.Genetics;

public sealed class OnePointCrossover : ICrossoverStrategy
{
    private readonly IRandomSource _random;

    public OnePointCrossover(IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);

        _random = random;
    }

    public Solution Crossover(
        OptimizationProblem problem,
        Solution firstParent,
        Solution secondParent)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(firstParent);
        ArgumentNullException.ThrowIfNull(secondParent);

        if (problem.Variables.Count < 2)
        {
            throw new InvalidOperationException(
                "One-point crossover requires at least two variables.");
        }

        var crossoverPoint = _random.Next(
            1,
            problem.Variables.Count);

        var child = new Solution();

        for (var i = 0; i < problem.Variables.Count; i++)
        {
            var variable = problem.Variables[i];

            // Before the crossover point, inherit from parent 1.
            // From the crossover point onward, inherit from parent 2.
            var source = i < crossoverPoint
                ? firstParent
                : secondParent;

            child.SetValue(
                variable,
                source.GetValue(variable));
        }

        return child;
    }
}
