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

    public GeneticAlgorithm(
        int populationSize = 50,
        int generations = 100,
        int eliteCount = 1,
        double mutationRate = 0.1,
        int tournamentSize = 3,
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

        _populationSize = populationSize;
        _generations = generations;
        _eliteCount = eliteCount;
        _mutationRate = mutationRate;
        _tournamentSize = tournamentSize;
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

        var population = Rank(
            run,
            Enumerable
                .Range(0, _populationSize)
                .Select(_ => Evaluate(run, problem.CreateRandom(_random)))
                .ToList());

        for (var generation = 0; generation < _generations; generation++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Elites keep their evaluation; only new children are scored.
            var next = population.Take(_eliteCount).ToList();

            while (next.Count < _populationSize)
            {
                var parent1 = Select(run, population);
                var parent2 = Select(run, population);

                var child = problem.Crossover(
                    parent1.Solution,
                    parent2.Solution,
                    _random);

                if (_random.NextDouble() < _mutationRate)
                    child = problem.Mutate(child, _random);

                next.Add(Evaluate(run, child));
            }

            population = Rank(run, next);
        }

        // SearchRun has tracked the best solution ever evaluated.
        return run.Complete();
    }

    private static Individual Evaluate(
        SearchRun<TSolution> run,
        TSolution solution) =>
        new(solution, run.Evaluate(solution));

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
