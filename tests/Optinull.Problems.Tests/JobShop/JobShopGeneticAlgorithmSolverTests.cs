using Optinull.Problems.JobShop;

namespace Optinull.Problems.Tests.JobShop;

public sealed class JobShopGeneticAlgorithmSolverTests
{
    [Fact]
    public void Solve_ReturnsValidSchedule()
    {
        var problem = CreateProblem();

        var solver =
            new JobShopGeneticAlgorithmSolver(
                populationSize: 20,
                generations: 20,
                eliteCount: 2,
                mutationRate: 0.1,
                random: new Random(42));

        var result =
            solver.Solve(problem);

        Assert.Equal(
            4,
            result.Operations.Count);

        Assert.True(
            result.Makespan > 0);
    }

    [Fact]
    public void Solve_DoesNotReturnWorseThanBestInitialPopulation()
    {
        var problem = CreateProblem();

        var initialRandom =
            new Random(42);

        var generator =
            new JobShopSequenceGenerator();

        var evaluator =
            new JobShopEvaluator();

        var initialPopulation =
            Enumerable
                .Range(0, 20)
                .Select(_ =>
                    generator.Generate(
                        problem,
                        initialRandom))
                .ToList();

        var bestInitialMakespan =
            initialPopulation
                .Select(sequence =>
                    evaluator.Evaluate(
                        problem,
                        sequence)
                        .ObjectiveValue)
                .Min();

        var solver =
            new JobShopGeneticAlgorithmSolver(
                populationSize: 20,
                generations: 20,
                eliteCount: 2,
                mutationRate: 0.1,
                random: new Random(42));

        var result =
            solver.Solve(problem);

        Assert.True(
            result.Makespan <=
            bestInitialMakespan);
    }

    [Fact]
    public void Solve_IsReproducibleWithSameSeed()
    {
        var firstSolver =
            new JobShopGeneticAlgorithmSolver(
                populationSize: 20,
                generations: 20,
                eliteCount: 2,
                mutationRate: 0.1,
                random: new Random(42));

        var secondSolver =
            new JobShopGeneticAlgorithmSolver(
                populationSize: 20,
                generations: 20,
                eliteCount: 2,
                mutationRate: 0.1,
                random: new Random(42));

        var firstResult =
            firstSolver.Solve(
                CreateProblem());

        var secondResult =
            secondSolver.Solve(
                CreateProblem());

        Assert.Equal(
            firstResult.Makespan,
            secondResult.Makespan);

        Assert.Equal(
            firstResult.Operations.Select(
                operation => operation.JobId),
            secondResult.Operations.Select(
                operation => operation.JobId));

        Assert.Equal(
            firstResult.Operations.Select(
                operation => operation.StartTime),
            secondResult.Operations.Select(
                operation => operation.StartTime));
    }

    [Fact]
    public void Solve_FindsKnownOptimalMakespan_OnThreeByThreeBenchmark()
    {
        var problem =
            CreateThreeByThreeProblem();

        var solver =
            new JobShopGeneticAlgorithmSolver(
                populationSize: 100,
                generations: 100,
                eliteCount: 2,
                mutationRate: 0.1,
                random: new Random(42));

        var result =
            solver.Solve(problem);

        Console.WriteLine(
            $"3x3 GA makespan: {result.Makespan}");

        Console.WriteLine(
            $"3x3 GA sequence: [{string.Join(", ", result.Operations.Select(operation => operation.JobId))}]");

        Assert.Equal(
            11.0,
            result.Makespan);
    }

    [Fact]
    public void Constructor_RejectsInvalidPopulationSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new JobShopGeneticAlgorithmSolver(
                populationSize: 0));
    }

    [Fact]
    public void Constructor_RejectsInvalidGenerations()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new JobShopGeneticAlgorithmSolver(
                generations: 0));
    }

    [Fact]
    public void Constructor_RejectsInvalidEliteCount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new JobShopGeneticAlgorithmSolver(
                populationSize: 10,
                eliteCount: 11));
    }

    [Fact]
    public void Constructor_RejectsInvalidMutationRate()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new JobShopGeneticAlgorithmSolver(
                mutationRate: -0.1));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new JobShopGeneticAlgorithmSolver(
                mutationRate: 1.1));
    }

    private static JobShopProblem CreateProblem()
    {
        var job0 =
            new Job(
                id: 0,
                operations:
                [
                    new JobOperation(0, 3),
                    new JobOperation(1, 2)
                ]);

        var job1 =
            new Job(
                id: 1,
                operations:
                [
                    new JobOperation(1, 4),
                    new JobOperation(0, 1)
                ]);

        return new JobShopProblem(
            machineCount: 2,
            jobs:
            [
                job0,
                job1
            ]);
    }

    private static JobShopProblem CreateThreeByThreeProblem()
    {
        var job0 =
            new Job(
                0,
                [
                    new JobOperation(0, 3),
                    new JobOperation(1, 2),
                    new JobOperation(2, 2)
                ]);

        var job1 =
            new Job(
                1,
                [
                    new JobOperation(0, 2),
                    new JobOperation(2, 1),
                    new JobOperation(1, 4)
                ]);

        var job2 =
            new Job(
                2,
                [
                    new JobOperation(1, 4),
                    new JobOperation(2, 3),
                    new JobOperation(0, 2)
                ]);

        return new JobShopProblem(
            machineCount: 3,
            jobs:
            [
                job0,
                job1,
                job2
            ]);
    }
}
