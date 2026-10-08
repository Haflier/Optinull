using Optinull.Problems.JobShop;
using Optinull.Problems.JobShop.Benchmarks;

namespace Optinull.Problems.Tests.JobShop;

public sealed class JobShopFt06CriticalBlockMoveDiagnosticTests
{
    [Fact]
    public void InspectCriticalBlockMoves()
    {
        var problem =
            JobShopBenchmarkInstances.Ft06();

        var sequence =
            new JobSequence(
            [
                2, 0, 1, 2, 1, 1, 2, 5, 5, 3, 3, 5,
                4, 0, 2, 1, 3, 3, 5, 4, 5, 4, 1, 0,
                5, 4, 0, 1, 2, 3, 3, 0, 2, 4, 0, 4
            ]);

        var evaluator =
            new JobShopEvaluator();

        var decoder =
            new JobShopScheduleDecoder();

        var original =
            evaluator.Evaluate(
                problem,
                sequence);

        Console.WriteLine(
            $"Original makespan: {original.ObjectiveValue}");

        TestMove(
            problem,
            sequence,
            evaluator,
            decoder,
            machineId: 0,
            firstOperation: (2, 3),
            secondOperation: (3, 2));

        TestMove(
            problem,
            sequence,
            evaluator,
            decoder,
            machineId: 5,
            firstOperation: (3, 5),
            secondOperation: (0, 5));
    }

    private static void TestMove(
        JobShopProblem problem,
        JobSequence sequence,
        JobShopEvaluator evaluator,
        JobShopScheduleDecoder decoder,
        int machineId,
        (int JobId, int OperationIndex) firstOperation,
        (int JobId, int OperationIndex) secondOperation)
    {
        var positions =
            GetOperationPositions(sequence);

        var firstPosition =
            positions[firstOperation];

        var secondPosition =
            positions[secondOperation];

        var jobIds =
            sequence.JobIds.ToArray();

        (
            jobIds[firstPosition],
            jobIds[secondPosition]
        ) =
        (
            jobIds[secondPosition],
            jobIds[firstPosition]
        );

        var neighbor =
            new JobSequence(jobIds);

        var evaluation =
            evaluator.Evaluate(
                problem,
                neighbor);

        var schedule =
            decoder.Decode(
                problem,
                neighbor);

        Console.WriteLine();
        Console.WriteLine(
            $"Machine {machineId}: " +
            $"J{firstOperation.JobId}O{firstOperation.OperationIndex} " +
            $"<-> " +
            $"J{secondOperation.JobId}O{secondOperation.OperationIndex}");

        Console.WriteLine(
            $"Makespan: {evaluation.ObjectiveValue}");

        Console.WriteLine(
            $"Sequence: [{string.Join(", ", neighbor.JobIds)}]");

        Console.WriteLine(
            $"Schedule makespan: {schedule.Makespan}");
    }

    private static Dictionary<
        (int JobId, int OperationIndex),
        int>
        GetOperationPositions(
            JobSequence sequence)
    {
        var positions =
            new Dictionary<
                (int JobId, int OperationIndex),
                int>();

        var operationCounters =
            new Dictionary<int, int>();

        for (var position = 0;
             position < sequence.Count;
             position++)
        {
            var jobId =
                sequence.JobIds[position];

            operationCounters.TryGetValue(
                jobId,
                out var operationIndex);

            positions[
                (jobId, operationIndex)] =
                position;

            operationCounters[jobId] =
                operationIndex + 1;
        }

        return positions;
    }
}
