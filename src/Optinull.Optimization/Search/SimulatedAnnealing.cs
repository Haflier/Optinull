using Optinull.Domain.Evaluation;
using Optinull.Optimization.Randomness;

namespace Optinull.Optimization.Search;

public sealed class SimulatedAnnealing<TSolution> : ISearchSolver<TSolution>
{
    private readonly IRandomSource _random;
    private readonly double _initialTemperature;
    private readonly double _coolingRate;
    private readonly int _iterationsPerTemperature;
    private readonly double _minimumTemperature;

    public SimulatedAnnealing(
        double initialTemperature = 100.0,
        double coolingRate = 0.95,
        int iterationsPerTemperature = 10,
        double minimumTemperature = 0.001,
        IRandomSource? random = null)
    {
        if (initialTemperature <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(initialTemperature),
                "Initial temperature must be greater than zero.");

        if (coolingRate <= 0 || coolingRate >= 1)
            throw new ArgumentOutOfRangeException(
                nameof(coolingRate),
                "Cooling rate must be greater than zero and less than one.");

        if (iterationsPerTemperature <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(iterationsPerTemperature),
                "Iterations per temperature must be greater than zero.");

        if (minimumTemperature <= 0 ||
            minimumTemperature >= initialTemperature)
            throw new ArgumentOutOfRangeException(
                nameof(minimumTemperature),
                "Minimum temperature must be positive and below the initial temperature.");

        _initialTemperature = initialTemperature;
        _coolingRate = coolingRate;
        _iterationsPerTemperature = iterationsPerTemperature;
        _minimumTemperature = minimumTemperature;
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

        var current = initialSolution;
        var currentEvaluation = run.Evaluate(current);

        var temperature = _initialTemperature;

        while (temperature > _minimumTemperature)
        {
            for (var i = 0; i < _iterationsPerTemperature; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var candidate = problem.RandomNeighbor(current, _random);
                var candidateEvaluation = run.Evaluate(candidate);

                if (ShouldAccept(
                        run,
                        currentEvaluation,
                        candidateEvaluation,
                        temperature))
                {
                    current = candidate;
                    currentEvaluation = candidateEvaluation;
                }
            }

            temperature *= _coolingRate;
        }

        // SearchRun has tracked the best solution ever evaluated.
        return run.Complete();
    }

    private bool ShouldAccept(
        SearchRun<TSolution> run,
        EvaluationResult current,
        EvaluationResult candidate,
        double temperature)
    {
        // Wander until a feasible point is found.
        if (!current.IsFeasible)
            return true;

        if (!candidate.IsFeasible)
            return false;

        if (run.IsBetter(candidate, current))
            return true;

        var difference = Math.Abs(
            candidate.ObjectiveValue - current.ObjectiveValue);

        return _random.NextDouble() < Math.Exp(-difference / temperature);
    }
}
