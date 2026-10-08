using Optinull.Optimization.Randomness;

namespace Optinull.Optimization.Search;

public sealed class IteratedLocalSearch<TSolution> : ISearchSolver<TSolution>
{
    private readonly IRandomSource _random;
    private readonly int _perturbationSize;
    private readonly int _iterationCount;

    public IteratedLocalSearch(
        int perturbationSize = 3,
        int iterationCount = 100,
        IRandomSource? random = null)
    {
        if (perturbationSize <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(perturbationSize),
                "Perturbation size must be greater than zero.");

        if (iterationCount <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(iterationCount),
                "Iteration count must be greater than zero.");

        _perturbationSize = perturbationSize;
        _iterationCount = iterationCount;
        _random = random ?? new RandomSource();
    }

    public SearchResult<TSolution> Solve(
        ISearchProblem<TSolution> problem,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(problem);

        return Solve(
            problem,
            problem.CreateRandom(_random),
            cancellationToken);
    }

    public SearchResult<TSolution> Solve(
        ISearchProblem<TSolution> problem,
        TSolution initialSolution,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(problem);

        var run = new SearchRun<TSolution>(problem);

        var (current, _) = HillClimbing<TSolution>.Climb(
            run,
            problem,
            initialSolution,
            cancellationToken);

        for (var iteration = 0; iteration < _iterationCount; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Perturbation = several random moves in a row.
            var perturbed = current;

            for (var move = 0; move < _perturbationSize; move++)
            {
                perturbed = problem.RandomNeighbor(perturbed, _random);
            }

            (current, _) = HillClimbing<TSolution>.Climb(
                run,
                problem,
                perturbed,
                cancellationToken);
        }

        return run.Complete();
    }
}
