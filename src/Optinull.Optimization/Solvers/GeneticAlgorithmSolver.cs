using Optinull.Domain.Evaluation;
using Optinull.Domain.Problems;
using Optinull.Domain.Solutions;
using Optinull.Optimization.Comparison;
using Optinull.Optimization.Genetics;
using Optinull.Optimization.Generation;
using Optinull.Optimization.Randomness;

namespace Optinull.Optimization.Solvers;

public sealed class GeneticAlgorithmSolver : IOptimizationSolver
{
    private readonly ProblemEvaluator _evaluator;
    private readonly PopulationGenerator _populationGenerator;
    private readonly ICrossoverStrategy _crossover;
    private readonly IMutationStrategy _mutation;
    private readonly IRandomSource _random;

    private readonly int _populationSize;
    private readonly int _generations;
    private readonly int _eliteCount;

    public GeneticAlgorithmSolver(
        int populationSize = 50,
        int generations = 100,
        int eliteCount = 1,
        double mutationRate = 0.1,
        IRandomSource? random = null)
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

        _random = random ?? new RandomSource();

        _populationSize = populationSize;
        _generations = generations;
        _eliteCount = eliteCount;

        _evaluator = new ProblemEvaluator();

        var solutionGenerator = new RandomSolutionGenerator();

        _populationGenerator = new PopulationGenerator(
            solutionGenerator,
            _random);

        _crossover = new OnePointCrossover(_random);

        _mutation = new BasicMutation(
            _random,
            mutationRate);
    }

    public OptimizationResult Solve(
        OptimizationProblem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);

        if (problem.Objective is null)
        {
            throw new InvalidOperationException(
                "Cannot solve a problem without an objective.");
        }

        if (problem.Variables.Count < 2)
        {
            throw new InvalidOperationException(
                "Genetic algorithm requires at least two variables.");
        }

        var comparer = new SolutionComparer(
            problem.Objective.Type);

        var population = GenerateFeasiblePopulation(problem);

        var best = FindBest(
            population,
            problem,
            comparer);

        for (var generation = 0;
             generation < _generations;
             generation++)
        {
            population = CreateNextGeneration(
                population,
                problem,
                comparer);

            var generationBest = FindBest(
                population,
                problem,
                comparer);

            if (comparer.IsBetter(
                    generationBest.Evaluation,
                    best.Evaluation))
            {
                best = generationBest;
            }
        }

        return best;
    }

    private Population GenerateFeasiblePopulation(
        OptimizationProblem problem)
    {
        var solutions = new List<Solution>();

        var maximumAttempts = _populationSize * 1000;
        var attempts = 0;

        while (solutions.Count < _populationSize)
        {
            if (attempts++ >= maximumAttempts)
            {
                throw new InvalidOperationException(
                    "Unable to generate a feasible initial population.");
            }

            var solution = _populationGenerator
                .Generate(problem, 1)
                .Solutions[0];

            var evaluation = _evaluator.Evaluate(
                problem,
                solution);

            if (evaluation.IsFeasible)
            {
                solutions.Add(solution);
            }
        }

        return new Population(solutions);
    }

    private Population CreateNextGeneration(
        Population population,
        OptimizationProblem problem,
        ISolutionComparer comparer)
    {
        var evaluations = EvaluatePopulation(
            problem,
            population);

        var eliteSolutions = GetEliteSolutions(
            population,
            evaluations,
            comparer);

        var nextGeneration = new Population(
            eliteSolutions);

        var maximumAttempts = _populationSize * 1000;
        var attempts = 0;

        while (nextGeneration.Count < _populationSize)
        {
            if (attempts++ >= maximumAttempts)
            {
                throw new InvalidOperationException(
                    "Unable to generate enough feasible offspring.");
            }

            var parent1 = Select(
                population,
                evaluations,
                comparer);

            var parent2 = Select(
                population,
                evaluations,
                comparer);

            var child = _crossover.Crossover(
                problem,
                parent1,
                parent2);

            child = _mutation.Mutate(
                problem,
                child);

            var childEvaluation = _evaluator.Evaluate(
                problem,
                child);

            // Hard-constraint violations prevent the child
            // from entering the next generation.
            if (!childEvaluation.IsFeasible)
            {
                continue;
            }

            nextGeneration.Add(child);
        }

        return nextGeneration;
    }

    private Solution Select(
        Population population,
        IReadOnlyDictionary<Solution, EvaluationResult> evaluations,
        ISolutionComparer comparer)
    {
        var selection = new TournamentSelection(
            comparer,
            _random,
            tournamentSize: 3);

        return selection.Select(
            population,
            evaluations);
    }

    private Dictionary<Solution, EvaluationResult> EvaluatePopulation(
        OptimizationProblem problem,
        Population population)
    {
        return population.Solutions.ToDictionary(
            solution => solution,
            solution => _evaluator.Evaluate(
                problem,
                solution));
    }

    private OptimizationResult FindBest(
        Population population,
        OptimizationProblem problem,
        ISolutionComparer comparer)
    {
        var evaluations = EvaluatePopulation(
            problem,
            population);

        Solution? bestSolution = null;
        EvaluationResult? bestEvaluation = null;

        foreach (var solution in population.Solutions)
        {
            var evaluation = evaluations[solution];

            if (bestSolution is null ||
                comparer.IsBetter(
                    evaluation,
                    bestEvaluation!))
            {
                bestSolution = solution;
                bestEvaluation = evaluation;
            }
        }

        return new OptimizationResult(
            bestSolution!,
            bestEvaluation!);
    }

    private IEnumerable<Solution> GetEliteSolutions(
        Population population,
        IReadOnlyDictionary<Solution, EvaluationResult> evaluations,
        ISolutionComparer comparer)
    {
        return population.Solutions
            .OrderByDescending(
                solution => evaluations[solution],
                new EvaluationComparer(comparer))
            .Take(_eliteCount);
    }

    private sealed class EvaluationComparer
        : IComparer<EvaluationResult>
    {
        private readonly ISolutionComparer _solutionComparer;

        public EvaluationComparer(
            ISolutionComparer solutionComparer)
        {
            _solutionComparer = solutionComparer;
        }

        public int Compare(
            EvaluationResult? x,
            EvaluationResult? y)
        {
            if (ReferenceEquals(x, y))
                return 0;

            if (x is null)
                return -1;

            if (y is null)
                return 1;

            if (_solutionComparer.IsBetter(x, y))
                return 1;

            if (_solutionComparer.IsBetter(y, x))
                return -1;

            return 0;
        }
    }
}
