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
    public void Solve_FindsImprovement_WhenOneExists()
    {
        var problem = CreateProblem();

        var initialSequence =
            new JobSequence([0, 0, 1, 1]);

        var evaluator =
            new JobShopEvaluator();

        var initialEvaluation =
            evaluator.Evaluate(
                problem,
                initialSequence);

        Assert.Equal(
            10.0,
            initialEvaluation.ObjectiveValue);

        var solver =
            new JobShopHillClimbingSolver();

        var schedule =
            solver.Solve(
                problem,
                initialSequence);

        Assert.True(
            schedule.Makespan <
            initialEvaluation.ObjectiveValue);
    }

    private static JobShopProblem CreateProblem()
    {
        var job0 = new Job(
            0,
            [
                new JobOperation(
                    machineId: 0,
                    processingTime: 3),
                new JobOperation(
                    machineId: 1,
                    processingTime: 2)
            ]);

        var job1 = new Job(
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
            jobs: [job0, job1]);
    }
}
