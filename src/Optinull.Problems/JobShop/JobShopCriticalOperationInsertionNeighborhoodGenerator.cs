namespace Optinull.Problems.JobShop;

public sealed class JobShopCriticalOperationInsertionNeighborhoodGenerator
{
    private readonly JobShopScheduleDecoder _decoder;

    public JobShopCriticalOperationInsertionNeighborhoodGenerator()
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
            FindCriticalPath(schedule);

        var operationPositions =
            GetOperationPositions(sequence);

        // Only move operations that belong to the
        // current critical path.
        foreach (var operation in criticalPath)
        {
            if (!operationPositions.TryGetValue(
                    GetKey(operation),
                    out var originalPosition))
            {
                continue;
            }

            var jobIds =
                sequence.JobIds.ToList();

            // Remove the critical operation.
            var movedJobId =
                jobIds[originalPosition];

            jobIds.RemoveAt(originalPosition);

            // Try inserting it at every other position.
            for (var targetPosition = 0;
                 targetPosition <= jobIds.Count;
                 targetPosition++)
            {
                if (targetPosition == originalPosition)
                    continue;

                var candidate =
                    jobIds.ToList();

                candidate.Insert(
                    targetPosition,
                    movedJobId);

                yield return
                    new JobSequence(candidate);
            }
        }
    }

    private static List<ScheduledOperation>
        FindCriticalPath(
            JobShopSchedule schedule)
    {
        var operations =
            schedule.Operations
                .ToDictionary(GetKey);

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
