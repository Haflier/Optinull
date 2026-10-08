namespace Optinull.Problems.JobShop;

public sealed class JobShopCriticalBlockNeighborhoodGenerator
{
    private readonly JobShopScheduleDecoder _decoder;

    public JobShopCriticalBlockNeighborhoodGenerator()
    {
        _decoder =
            new JobShopScheduleDecoder();
    }

    public IEnumerable<JobSequence> Generate(
        JobShopProblem problem,
        JobSequence sequence)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(sequence);

        var schedule =
            _decoder.Decode(
                problem,
                sequence);

        var criticalPath =
            FindCriticalPath(
                schedule);

        var criticalBlocks =
            FindCriticalBlocks(
                criticalPath);

        var operationPositions =
            GetOperationPositions(
                sequence);

        foreach (var block in criticalBlocks)
        {
            for (var i = 0;
                 i < block.Count - 1;
                 i++)
            {
                var first =
                    block[i];

                var second =
                    block[i + 1];

                if (!operationPositions.TryGetValue(
                        GetKey(first),
                        out var firstPosition))
                {
                    continue;
                }

                if (!operationPositions.TryGetValue(
                        GetKey(second),
                        out var secondPosition))
                {
                    continue;
                }

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

                yield return
                    new JobSequence(jobIds);
            }
        }
    }

    private static List<ScheduledOperation>
        FindCriticalPath(
            JobShopSchedule schedule)
    {
        var operations =
            schedule.Operations
                .ToDictionary(
                    GetKey);

        var machinePredecessors =
            schedule.Operations
                .GroupBy(
                    operation =>
                        operation.MachineId)
                .SelectMany(
                    machine =>
                    {
                        var ordered =
                            machine
                                .OrderBy(
                                    operation =>
                                        operation.StartTime)
                                .ToList();

                        return ordered
                            .Skip(1)
                            .Select(
                                (current, index) =>
                                    new
                                    {
                                        Current =
                                            GetKey(current),

                                        Previous =
                                            GetKey(
                                                ordered[index])
                                    });
                    })
                .ToDictionary(
                    x => x.Current,
                    x => x.Previous);

        var endOperation =
            schedule.Operations
                .OrderByDescending(
                    operation =>
                        operation.EndTime)
                .First();

        var path =
            new List<ScheduledOperation>();

        var current =
            GetKey(endOperation);

        while (true)
        {
            var operation =
                operations[current];

            path.Add(operation);

            ScheduledOperation? jobPredecessor =
                null;

            if (operation.OperationIndex > 0)
            {
                jobPredecessor =
                    operations[
                        (
                            operation.JobId,
                            operation.OperationIndex - 1
                        )];
            }

            ScheduledOperation? machinePredecessor =
                null;

            if (machinePredecessors.TryGetValue(
                    current,
                    out var machineKey))
            {
                machinePredecessor =
                    operations[machineKey];
            }

            var predecessor =
                SelectPredecessor(
                    jobPredecessor,
                    machinePredecessor,
                    operation.StartTime);

            if (predecessor is null)
                break;

            current =
                GetKey(predecessor);
        }

        path.Reverse();

        return path;
    }

    private static ScheduledOperation?
        SelectPredecessor(
            ScheduledOperation? jobPredecessor,
            ScheduledOperation? machinePredecessor,
            double startTime)
    {
        if (jobPredecessor is not null &&
            NearlyEqual(
                jobPredecessor.EndTime,
                startTime))
        {
            return jobPredecessor;
        }

        if (machinePredecessor is not null &&
            NearlyEqual(
                machinePredecessor.EndTime,
                startTime))
        {
            return machinePredecessor;
        }

        return null;
    }

    private static List<
        List<ScheduledOperation>>
        FindCriticalBlocks(
            IReadOnlyList<ScheduledOperation>
                criticalPath)
    {
        var blocks =
            new List<
                List<ScheduledOperation>>();

        var currentBlock =
            new List<ScheduledOperation>();

        foreach (var operation in criticalPath)
        {
            if (currentBlock.Count == 0)
            {
                currentBlock.Add(operation);
                continue;
            }

            var previous =
                currentBlock[^1];

            if (previous.MachineId ==
                    operation.MachineId &&
                NearlyEqual(
                    previous.EndTime,
                    operation.StartTime))
            {
                currentBlock.Add(operation);
            }
            else
            {
                if (currentBlock.Count >= 2)
                {
                    blocks.Add(
                        currentBlock);
                }

                currentBlock =
                    new List<ScheduledOperation>
                    {
                        operation
                    };
            }
        }

        if (currentBlock.Count >= 2)
        {
            blocks.Add(currentBlock);
        }

        return blocks;
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

    private static (
        int JobId,
        int OperationIndex)
        GetKey(
            ScheduledOperation operation)
    {
        return
            (
                operation.JobId,
                operation.OperationIndex
            );
    }

    private static bool NearlyEqual(
        double first,
        double second)
    {
        return Math.Abs(first - second) <
               0.000001;
    }
}
