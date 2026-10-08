using Optinull.Domain.Evaluation;
using Optinull.Optimization.Randomness;

namespace Optinull.Optimization.Search;

public sealed class GeneticAlgorithm<TSolution> : ISearchSolver<TSolution>
{
    private readonly IRandomSource _random;
    private readonly int _populationSize;
    private readonly int _generations;
    private readonly int _eliteCount;
    private readonly double _mutationRate;
    private readonly int _tournamentSize;
    private readonly int _localSearchTries;
    private readonly int? _maxEvaluations;

    /// <param name="localSearchTries">
    /// Random neighbors tried on every new individual; improvements are kept
    /// (a "memetic" GA). Zero disables it.
    /// </param>
    /// <param name="maxEvaluations">
    /// Optional evaluation budget; the search stops once it is reached.
    /// </param>
    public GeneticAlgorithm(
        int populationSize = 50,
        int generations = 100,
        int eliteCount = 1,
        double mutationRate = 0.1,
        int tournamentSize = 3,
        int localSearchTries = 0,
        int? maxEvaluations = null,
        IRandomSource? random = null)
    {
        if (populationSize <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(populationSize),
                "Population size must be greater than zero.");

        if (generations <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(generations),
                "Number of generations must be greater than zero.");

        if (eliteCount < 0 || eliteCount > populationSize)
            throw new ArgumentOutOfRangeException(
                nameof(eliteCount),
                "Elite count must be between zero and population size.");

        if (mutationRate < 0 || mutationRate > 1)
            throw new ArgumentOutOfRangeException(
                nameof(mutationRate),
                "Mutation rate must be between zero and one.");

        if (tournamentSize <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(tournamentSize),
                "Tournament size must be greater than zero.");

        if (localSearchTries < 0)
            throw new ArgumentOutOfRangeException(
                nameof(localSearchTries),
                "Local search tries cannot be negative.");

        if (maxEvaluations is <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(maxEvaluations),
                "Evaluation budget must be greater than zero.");

        _populationSize = populationSize;
        _generations = generations;
        _eliteCount = eliteCount;
        _mutationRate = mutationRate;
        _tournamentSize = tournamentSize;
        _localSearchTries = localSearchTries;
        _maxEvaluations = maxEvaluations;
        _random = random ?? new RandomSource();
    }

    SearchResult<TSolution> ISearchSolver<TSolution>.Solve(
        ISearchProblem<TSolution> problem,
        CancellationToken cancellationToken)
    {
        if (problem is IRecombinableProblem<TSolution> recombinable)
            return Solve(recombinable, cancellationToken);

        throw new InvalidOperationException(
            "A genetic algorithm needs a problem that implements " +
            "IRecombinableProblem (crossover and mutation).");
    }

    public SearchResult<TSolution> Solve(
        IRecombinableProblem<TSolution> problem,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(problem);

        var run = new SearchRun<TSolution>(problem);

        var initial = new List<Individual>(_populationSize);

        for (var i = 0; i < _populationSize; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var individual = Evaluate(run, problem.CreateRandom(_random));
            initial.Add(Improve(run, problem, individual));
        }

        var population = Rank(run, initial);

        for (var generation = 0; generation < _generations; generation++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (BudgetReached(run))
                break;

            // Elites keep their evaluation; only new children are scored.
            var next = population.Take(_eliteCount).ToList();
            var exhausted = false;

            while (next.Count < _populationSize)
            {
                if (BudgetReached(run))
                {
                    exhausted = true;
                    break;
                }

                var parent1 = Select(run, population);
                var parent2 = Select(run, population);

                var child = problem.Crossover(
                    parent1.Solution,
                    parent2.Solution,
                    _random);

                if (_random.NextDouble() < _mutationRate)
                    child = problem.Mutate(child, _random);

                var individual = Evaluate(run, child);

                next.Add(Improve(run, problem, individual));
            }

            if (exhausted)
                break;

            population = Rank(run, next);
        }

        // SearchRun has tracked the best solution ever evaluated.
        return run.Complete();
    }

    private bool BudgetReached(SearchRun<TSolution> run) =>
        _maxEvaluations is { } max && run.EvaluationCount >= max;

    private static Individual Evaluate(
        SearchRun<TSolution> run,
        TSolution solution) =>
        new(solution, run.Evaluate(solution));

    // First-improvement random descent on a new individual.
    private Individual Improve(
        SearchRun<TSolution> run,
        IRecombinableProblem<TSolution> problem,
        Individual individual)
    {
        for (var attempt = 0;
             attempt < _localSearchTries && !BudgetReached(run);
             attempt++)
        {
            var neighbor = problem.RandomNeighbor(
                individual.Solution,
                _random);

            var evaluation = run.Evaluate(neighbor);

            if (run.IsBetter(evaluation, individual.Evaluation))
                individual = new Individual(neighbor, evaluation);
        }

        return individual;
    }

    private Individual Select(
        SearchRun<TSolution> run,
        IReadOnlyList<Individual> population)
    {
        var winner = population[_random.Next(0, population.Count)];

        for (var i = 1; i < _tournamentSize; i++)
        {
            var candidate = population[_random.Next(0, population.Count)];

            if (run.IsBetter(candidate.Evaluation, winner.Evaluation))
                winner = candidate;
        }

        return winner;
    }

    // Best first. Feasible beats infeasible; infeasible solutions tie.
    private static List<Individual> Rank(
        SearchRun<TSolution> run,
        IEnumerable<Individual> individuals) =>
        individuals
            .OrderBy(
                individual => individual.Evaluation,
                Comparer<EvaluationResult>.Create((a, b) =>
                    run.IsBetter(a, b) ? -1 :
                    run.IsBetter(b, a) ? 1 : 0))
            .ToList();

    private sealed record Individual(
        TSolution Solution,
        EvaluationResult Evaluation);
}
