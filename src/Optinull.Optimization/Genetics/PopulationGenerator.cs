using Optinull.Domain.Problems;
using Optinull.Optimization.Generation;
using Optinull.Optimization.Randomness;

namespace Optinull.Optimization.Genetics;

public sealed class PopulationGenerator
{
    private readonly ISolutionGenerator _solutionGenerator;
    private readonly IRandomSource _random;

    public PopulationGenerator(
        ISolutionGenerator solutionGenerator,
        IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(solutionGenerator);
        ArgumentNullException.ThrowIfNull(random);

        _solutionGenerator = solutionGenerator;
        _random = random;
    }

    public Population Generate(
        OptimizationProblem problem,
        int size)
    {
        ArgumentNullException.ThrowIfNull(problem);

        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(size),
                "Population size must be greater than zero.");
        }

        var solutions = Enumerable
            .Range(0, size)
            .Select(_ =>
                _solutionGenerator.Generate(
                    problem,
                    _random));

        return new Population(solutions);
    }
}
