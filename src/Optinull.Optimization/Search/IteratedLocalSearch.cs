using Optinull.Optimization.Randomness;

namespace Optinull.Optimization.Search;

public sealed class IteratedLocalSearch<TSolution> : ISearchSolver<TSolution>
{
    private readonly IRandomSource _random;
    private readonly int _perturbationSize;
    private readonly int _iterationCount;
    private readonly int? _maxEvaluations;

    /// <param name="maxEvaluations">
    /// Optional evaluation budget. No new restart begins once it is reached; the climb
    /// in progress is allowed to finish, so the total can overshoot by one climb.
    /// </param>
    public IteratedLocalSearch(
        int perturbationSize = 3,
        int iterationCount = 100,
        IRandomSource? random = null,
        int? maxEvaluations = null)
    {
        if (perturbationSize <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(perturbationSize),
                "Perturbation size must be greater than zero.");

        if (iterationCount <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(iterationCount),
                "Iteration count must be greater than zero.");

        if (maxEvaluations is <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(maxEvaluations),
                "Evaluation budget must be greater than zero.");

        _perturbationSize = perturbationSize;
        _iterationCount = iterationCount;
        _maxEvaluations = maxEvaluations;
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

            if (_maxEvaluations is { } max && run.EvaluationCount >= max)
                break;

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
