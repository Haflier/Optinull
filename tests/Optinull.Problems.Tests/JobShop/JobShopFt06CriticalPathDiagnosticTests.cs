using Optinull.Problems.JobShop;
using Optinull.Problems.JobShop.Benchmarks;

namespace Optinull.Problems.Tests.JobShop;

public sealed class JobShopFt06CriticalPathDiagnosticTests
{
    [Fact]
    public void InspectCriticalPath()
    {
        var problem =
            JobShopBenchmarkInstances.Ft06();

        var initialSequence =
            new JobSequence(
            [
                0, 0, 0, 0, 0, 0,
                1, 1, 1, 1, 1, 1,
                2, 2, 2, 2, 2, 2,
                3, 3, 3, 3, 3, 3,
                4, 4, 4, 4, 4, 4,
                5, 5, 5, 5, 5, 5
            ]);

        var solver =
            new JobShopIteratedLocalSearchSolver(
                perturbationSwapCount: 3,
                iterationCount: 100,
                random: new Random(42));

        var sequence =
            solver.SolveSequence(
                problem,
                initialSequence);

        var decoder =
            new JobShopScheduleDecoder();

        var schedule =
            decoder.Decode(
                problem,
                sequence);

        Console.WriteLine(
            $"Makespan: {schedule.Makespan}");

        var operations =
            schedule.Operations
                .ToDictionary(
                    operation =>
                        (operation.JobId,
                         operation.OperationIndex));

        var machinePredecessors =
            schedule.Operations
                .GroupBy(operation => operation.MachineId)
                .SelectMany(
                    machine =>
                        machine
                            .OrderBy(operation => operation.StartTime)
                            .Zip(
                                machine
                                    .OrderBy(operation => operation.StartTime)
                                    .Skip(1),
                                (previous, current) =>
                                    new
                                    {
                                        Current =
                                            (current.JobId,
                                             current.OperationIndex),

                                        Previous =
                                            (previous.JobId,
                                             previous.OperationIndex)
                                    }))
                .ToDictionary(
                    x => x.Current,
                    x => x.Previous);

        var endOperation =
            schedule.Operations
                .OrderByDescending(
                    operation => operation.EndTime)
                .First();

        var criticalPath =
            new List<ScheduledOperation>();

        var current =
            (endOperation.JobId,
             endOperation.OperationIndex);

        while (true)
        {
            var operation =
                operations[current];

            criticalPath.Add(operation);

            ScheduledOperation? jobPredecessor = null;

            if (operation.OperationIndex > 0)
            {
                jobPredecessor =
                    operations[
                        (
                            operation.JobId,
                            operation.OperationIndex - 1
                        )];
            }

            ScheduledOperation? machinePredecessor = null;

            if (machinePredecessors.TryGetValue(
                    current,
                    out var machinePredecessorKey))
            {
                machinePredecessor =
                    operations[machinePredecessorKey];
            }

            var predecessor =
                SelectPredecessor(
                    jobPredecessor,
                    machinePredecessor,
                    operation.StartTime);

            if (predecessor is null)
                break;

            current =
                (
                    predecessor.JobId,
                    predecessor.OperationIndex
                );
        }

        criticalPath.Reverse();

        Console.WriteLine();
        Console.WriteLine("Critical path:");

        foreach (var operation in criticalPath)
        {
            Console.WriteLine(
                $"Job {operation.JobId}, " +
                $"Op {operation.OperationIndex}, " +
                $"Machine {operation.MachineId}, " +
                $"{operation.StartTime} -> " +
                $"{operation.EndTime}");
        }

        Console.WriteLine();
        Console.WriteLine("Critical blocks:");

        var blocks =
            FindCriticalBlocks(
                criticalPath);

        foreach (var block in blocks)
        {
            Console.WriteLine(
                $"Machine {block.MachineId}: " +
                string.Join(
                    " -> ",
                    block.Operations.Select(
                        operation =>
                            $"J{operation.JobId}O{operation.OperationIndex}")));
        }
    }

    private static ScheduledOperation? SelectPredecessor(
        ScheduledOperation? jobPredecessor,
        ScheduledOperation? machinePredecessor,
        double startTime)
    {
        var candidates =
            new[]
            {
                jobPredecessor,
                machinePredecessor
            }
            .Where(operation =>
                operation is not null &&
                Math.Abs(
                    operation!.EndTime - startTime) <
                    0.000001)
            .Cast<ScheduledOperation>()
            .ToList();

        return candidates.FirstOrDefault();
    }

    private static List<CriticalBlock> FindCriticalBlocks(
        IReadOnlyList<ScheduledOperation> criticalPath)
    {
        var blocks =
            new List<CriticalBlock>();

        var currentBlock =
            new List<ScheduledOperation>();

        for (var i = 0; i < criticalPath.Count; i++)
        {
            var operation =
                criticalPath[i];

            if (currentBlock.Count == 0)
            {
                currentBlock.Add(operation);
                continue;
            }

            var previous =
                currentBlock[^1];

            var isSameMachine =
                previous.MachineId ==
                operation.MachineId;

            var isConsecutive =
                Math.Abs(
                    previous.EndTime -
                    operation.StartTime) <
                0.000001;

            if (isSameMachine && isConsecutive)
            {
                currentBlock.Add(operation);
            }
            else
            {
                if (currentBlock.Count >= 2)
                {
                    blocks.Add(
                        new CriticalBlock(
                            currentBlock[0].MachineId,
                            currentBlock.ToList()));
                }

                currentBlock.Clear();
                currentBlock.Add(operation);
            }
        }

        if (currentBlock.Count >= 2)
        {
            blocks.Add(
                new CriticalBlock(
                    currentBlock[0].MachineId,
                    currentBlock.ToList()));
        }

        return blocks;
    }

    private sealed record CriticalBlock(
        int MachineId,
        IReadOnlyList<ScheduledOperation> Operations);
}
