using Optinull.Problems.JobShop;

namespace Optinull.Problems.Tests.JobShop;

public sealed class JobShopHillClimbingSolverTests
{
    [Fact]
    public void Solve_ReturnsValidSchedule()
    {
        var problem = CreateProblem();

        var initialSequence =
            new JobSequence([0, 1, 0, 1]);

        var solver =
            new JobShopHillClimbingSolver();

        var schedule =
            solver.Solve(
                problem,
                initialSequence);

        Assert.Equal(
            4,
            schedule.Operations.Count);

        Assert.True(
            schedule.Makespan > 0);
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
            new JobShopHillClimbingSolver();

        var schedule =
            solver.Solve(
                problem,
                initialSequence);

        Assert.True(
            schedule.Makespan <=
            initialEvaluation.ObjectiveValue);
    }

    [Fact]
    public void Solve_FindsKnownOptimalMakespan()
    {
        var problem = CreateProblem();

        var initialSequence =
            new JobSequence([0, 0, 1, 1]);

        var solver =
            new JobShopHillClimbingSolver();

        var schedule =
            solver.Solve(
                problem,
                initialSequence);

        // The total workload on machine 0 is:
        // J0 = 3 + J1 = 3 = 6.
        //
        // Therefore no schedule can have a makespan
        // below 6. The solver reaches that lower bound,
        // so 6 is the global optimum for this instance.
        Assert.Equal(
            6.0,
            schedule.Makespan);
    }

    [Fact]
    public void Solve_ReturnsLocalOptimum()
    {
        var problem = CreateProblem();

        var initialSequence =
            new JobSequence([0, 0, 1, 1]);

        var solver =
            new JobShopHillClimbingSolver();

        var schedule =
            solver.Solve(
                problem,
                initialSequence);

        var neighborhoodGenerator =
            new JobShopNeighborhoodGenerator();

        var evaluator =
            new JobShopEvaluator();

        var finalSequence =
            new JobSequence(
                schedule.Operations
                    .Select(operation => operation.JobId));

        var finalEvaluation =
            evaluator.Evaluate(
                problem,
                finalSequence);

        Assert.Equal(
            schedule.Makespan,
            finalEvaluation.ObjectiveValue);

        Assert.All(
            neighborhoodGenerator.Generate(finalSequence),
            neighbor =>
            {
                var evaluation =
                    evaluator.Evaluate(
                        problem,
                        neighbor);

                Assert.True(
                    evaluation.ObjectiveValue >=
                    finalEvaluation.ObjectiveValue);
            });
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
            new JobShopHillClimbingSolver();

        var schedule =
            solver.Solve(
                problem,
                initialSequence);

        Assert.Equal(
            11.0,
            schedule.Makespan);
    }

    private static JobShopProblem CreateProblem()
    {
        var job0 =
            new Job(
                0,
                [
                    new JobOperation(
                        machineId: 0,
                        processingTime: 3),

                    new JobOperation(
                        machineId: 1,
                        processingTime: 2)
                ]);

        var job1 =
            new Job(
                1,
                [
                    new JobOperation(
                        machineId: 1,
                        processingTime: 2),

                    new JobOperation(
                        machineId: 0,
                        processingTime: 3)
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
