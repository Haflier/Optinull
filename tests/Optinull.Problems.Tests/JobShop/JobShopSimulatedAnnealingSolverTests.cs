using Optinull.Problems.JobShop;

namespace Optinull.Problems.Tests.JobShop;

public sealed class JobShopSimulatedAnnealingSolverTests
{
    [Fact]
    public void Solve_ReturnsValidSchedule()
    {
        var problem = CreateProblem();

        var initialSequence =
            new JobSequence([0, 1, 0, 1]);

        var solver =
            new JobShopSimulatedAnnealingSolver(
                initialTemperature: 100,
                coolingRate: 0.95,
                iterationsPerTemperature: 10,
                random: new Random(42));

        var result =
            solver.Solve(
                problem,
                initialSequence);

        Assert.Equal(
            4,
            result.Operations.Count);

        Assert.True(
            result.Makespan > 0);
    }

    [Fact]
    public void Solve_DoesNotReturnWorseSolution()
    {
        var problem = CreateProblem();

        var initialSequence =
            new JobSequence([0, 1, 0, 1]);

        var evaluator =
            new JobShopEvaluator();

        var initialEvaluation =
            evaluator.Evaluate(
                problem,
                initialSequence);

        var solver =
            new JobShopSimulatedAnnealingSolver(
                initialTemperature: 100,
                coolingRate: 0.95,
                iterationsPerTemperature: 10,
                random: new Random(42));

        var result =
            solver.Solve(
                problem,
                initialSequence);

        Assert.True(
            result.Makespan <=
            initialEvaluation.ObjectiveValue);
    }

    [Fact]
    public void Solve_IsReproducibleWithSameSeed()
    {
        var firstProblem =
            CreateProblem();

        var secondProblem =
            CreateProblem();

        var firstSequence =
            new JobSequence([0, 1, 0, 1]);

        var secondSequence =
            new JobSequence([0, 1, 0, 1]);

        var firstSolver =
            new JobShopSimulatedAnnealingSolver(
                initialTemperature: 100,
                coolingRate: 0.95,
                iterationsPerTemperature: 10,
                random: new Random(42));

        var secondSolver =
            new JobShopSimulatedAnnealingSolver(
                initialTemperature: 100,
                coolingRate: 0.95,
                iterationsPerTemperature: 10,
                random: new Random(42));

        var firstResult =
            firstSolver.Solve(
                firstProblem,
                firstSequence);

        var secondResult =
            secondSolver.Solve(
                secondProblem,
                secondSequence);

        Assert.Equal(
            firstResult.Makespan,
            secondResult.Makespan);

        Assert.Equal(
            firstResult.Operations.Select(operation => operation.JobId),
            secondResult.Operations.Select(operation => operation.JobId));

        Assert.Equal(
            firstResult.Operations.Select(operation => operation.StartTime),
            secondResult.Operations.Select(operation => operation.StartTime));
    }

    [Fact]
    public void Solve_ReturnsInitialSolution_WhenSequenceHasOneOperation()
    {
        var problem =
            new JobShopProblem(
                machineCount: 1,
                jobs:
                [
                    new Job(
                        id: 0,
                        operations:
                        [
                            new JobOperation(
                                machineId: 0,
                                processingTime: 5)
                        ]
                    )
                ]);

        var initialSequence =
            new JobSequence([0]);

        var solver =
            new JobShopSimulatedAnnealingSolver(
                random: new Random(42));

        var result =
            solver.Solve(
                problem,
                initialSequence);

        Assert.Equal(
            5,
            result.Makespan);
    }

    [Fact]
    public void Solve_FindsKnownOptimalMakespan_OnThreeByThreeBenchmark()
    {
        var problem =
            CreateThreeByThreeProblem();

        var initialSequence =
            new JobSequence(
            [
                0, 0, 0,
                1, 1, 1,
                2, 2, 2
            ]);

        var solver =
            new JobShopSimulatedAnnealingSolver(
                initialTemperature: 100,
                coolingRate: 0.95,
                iterationsPerTemperature: 10,
                random: new Random(42));

        var result =
            solver.Solve(
                problem,
                initialSequence);

        Console.WriteLine(
            $"3x3 SA makespan: {result.Makespan}");

        Console.WriteLine(
            $"3x3 SA sequence: [{string.Join(", ", result.Operations.Select(operation => operation.JobId))}]");

        Assert.Equal(
            11.0,
            result.Makespan);
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

    [Fact]
    public void Constructor_RejectsInvalidInitialTemperature()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new JobShopSimulatedAnnealingSolver(
                initialTemperature: 0));
    }

    [Fact]
    public void Constructor_RejectsInvalidCoolingRate()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new JobShopSimulatedAnnealingSolver(
                coolingRate: 0));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new JobShopSimulatedAnnealingSolver(
                coolingRate: 1));
    }

    [Fact]
    public void Constructor_RejectsInvalidIterationsPerTemperature()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new JobShopSimulatedAnnealingSolver(
                iterationsPerTemperature: 0));
    }
}
