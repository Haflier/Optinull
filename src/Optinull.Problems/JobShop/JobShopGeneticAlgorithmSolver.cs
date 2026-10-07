namespace Optinull.Problems.JobShop;

public sealed class JobShopGeneticAlgorithmSolver
{
    private readonly JobShopEvaluator _evaluator;
    private readonly JobShopScheduleDecoder _decoder;
    private readonly JobShopSequenceGenerator _sequenceGenerator;
    private readonly JobShopSequenceMutation _mutation;
    private readonly JobShopSequenceCrossover _crossover;
    private readonly Random _random;

    private readonly int _populationSize;
    private readonly int _generations;
    private readonly int _eliteCount;
    private readonly double _mutationRate;

    public JobShopGeneticAlgorithmSolver(
        int populationSize = 50,
        int generations = 100,
        int eliteCount = 1,
        double mutationRate = 0.1,
        Random? random = null)
    {
        if (populationSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(populationSize),
                "Population size must be greater than zero.");
        }

        if (generations <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(generations),
                "Number of generations must be greater than zero.");
        }

        if (eliteCount < 0 || eliteCount > populationSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(eliteCount),
                "Elite count must be between zero and population size.");
        }

        if (mutationRate < 0 || mutationRate > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(mutationRate),
                "Mutation rate must be between zero and one.");
        }

        _evaluator = new JobShopEvaluator();
        _decoder = new JobShopScheduleDecoder();
        _sequenceGenerator = new JobShopSequenceGenerator();
        _mutation = new JobShopSequenceMutation();
        _crossover = new JobShopSequenceCrossover();
        _random = random ?? Random.Shared;

        _populationSize = populationSize;
        _generations = generations;
        _eliteCount = eliteCount;
        _mutationRate = mutationRate;
    }

    public JobShopSchedule Solve(
        JobShopProblem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);

        var population =
            GenerateInitialPopulation(problem);

        var best =
            FindBest(
                problem,
                population);

        for (var generation = 0;
             generation < _generations;
             generation++)
        {
            population =
                CreateNextGeneration(
                    problem,
                    population);

            var generationBest =
                FindBest(
                    problem,
                    population);

            if (generationBest.Evaluation.ObjectiveValue <
                best.Evaluation.ObjectiveValue)
            {
                best = generationBest;
            }
        }

        return _decoder.Decode(
            problem,
            best.Sequence);
    }

    private List<JobSequence> GenerateInitialPopulation(
        JobShopProblem problem)
    {
        var population = new List<JobSequence>(
            _populationSize);

        for (var i = 0;
             i < _populationSize;
             i++)
        {
            population.Add(
                _sequenceGenerator.Generate(
                    problem,
                    _random));
        }

        return population;
    }

    private List<JobSequence> CreateNextGeneration(
        JobShopProblem problem,
        List<JobSequence> population)
    {
        var ranked =
            population
                .Select(sequence => new EvaluatedSequence(
                    sequence,
                    _evaluator.Evaluate(
                        problem,
                        sequence)))
                .OrderBy(item =>
                    item.Evaluation.ObjectiveValue)
                .ToList();

        var nextGeneration =
            ranked
                .Take(_eliteCount)
                .Select(item => item.Sequence)
                .ToList();

        while (nextGeneration.Count <
               _populationSize)
        {
            var parent1 =
                TournamentSelect(
                    ranked);

            var parent2 =
                TournamentSelect(
                    ranked);

            var child =
                _crossover.Crossover(
                    parent1,
                    parent2,
                    _random);

            if (_random.NextDouble() <
                _mutationRate)
            {
                child =
                    _mutation.Mutate(
                        child,
                        _random);
            }

            nextGeneration.Add(child);
        }

        return nextGeneration;
    }

    private JobSequence TournamentSelect(
        List<EvaluatedSequence> population)
    {
        const int tournamentSize = 3;

        EvaluatedSequence? winner = null;

        for (var i = 0;
             i < tournamentSize;
             i++)
        {
            var candidate =
                population[
                    _random.Next(
                        population.Count)];

            if (winner is null ||
                candidate.Evaluation.ObjectiveValue <
                winner.Evaluation.ObjectiveValue)
            {
                winner = candidate;
            }
        }

        return winner!.Sequence;
    }

    private EvaluatedSequence FindBest(
        JobShopProblem problem,
        IEnumerable<JobSequence> population)
    {
        return population
            .Select(sequence => new EvaluatedSequence(
                sequence,
                _evaluator.Evaluate(
                    problem,
                    sequence)))
            .OrderBy(item =>
                item.Evaluation.ObjectiveValue)
            .First();
    }

    private sealed record EvaluatedSequence(
        JobSequence Sequence,
        Optinull.Domain.Evaluation.EvaluationResult Evaluation);
}
