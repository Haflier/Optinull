namespace Optinull.Problems.JobShop;

public sealed class JobShopScheduleDecoder
{
    public JobShopSchedule Decode(
        JobShopProblem problem,
        IReadOnlyList<int> jobSequence)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(jobSequence);

        ValidateSequence(
            problem,
            jobSequence);

        var nextOperationIndex = problem.Jobs
            .ToDictionary(
                job => job.Id,
                _ => 0);

        var jobReadyTime = problem.Jobs
            .ToDictionary(
                job => job.Id,
                _ => 0.0);

        var machineReadyTime = Enumerable
            .Range(0, problem.MachineCount)
            .ToDictionary(
                machineId => machineId,
                _ => 0.0);

        var scheduledOperations =
            new List<ScheduledOperation>(
                jobSequence.Count);

        foreach (var jobId in jobSequence)
        {
            var job = problem.Jobs
                .First(job => job.Id == jobId);

            var operationIndex =
                nextOperationIndex[jobId];

            var operation =
                job.Operations[operationIndex];

            var startTime = Math.Max(
                jobReadyTime[jobId],
                machineReadyTime[operation.MachineId]);

            var scheduledOperation =
                new ScheduledOperation(
                    jobId,
                    operationIndex,
                    operation.MachineId,
                    operation.ProcessingTime,
                    startTime);

            scheduledOperations.Add(
                scheduledOperation);

            jobReadyTime[jobId] =
                scheduledOperation.EndTime;

            machineReadyTime[operation.MachineId] =
                scheduledOperation.EndTime;

            nextOperationIndex[jobId]++;
        }

        return new JobShopSchedule(
            scheduledOperations);
    }

    private static void ValidateSequence(
        JobShopProblem problem,
        IReadOnlyList<int> jobSequence)
    {
        var expectedOperationCount =
            problem.Jobs.Sum(
                job => job.Operations.Count);

        if (jobSequence.Count != expectedOperationCount)
        {
            throw new ArgumentException(
                "Job sequence must contain exactly one " +
                "entry for every operation.",
                nameof(jobSequence));
        }

        var jobsById = problem.Jobs
            .ToDictionary(job => job.Id);

        var occurrenceCounts = problem.Jobs
            .ToDictionary(
                job => job.Id,
                _ => 0);

        foreach (var jobId in jobSequence)
        {
            if (!jobsById.ContainsKey(jobId))
            {
                throw new ArgumentException(
                    $"Unknown job ID: {jobId}.",
                    nameof(jobSequence));
            }

            occurrenceCounts[jobId]++;
        }

        foreach (var job in problem.Jobs)
        {
            var actualCount =
                occurrenceCounts[job.Id];

            var expectedCount =
                job.Operations.Count;

            if (actualCount != expectedCount)
            {
                throw new ArgumentException(
                    $"Job {job.Id} appears {actualCount} times, " +
                    $"but it has {expectedCount} operations.",
                    nameof(jobSequence));
            }
        }
    }
}
