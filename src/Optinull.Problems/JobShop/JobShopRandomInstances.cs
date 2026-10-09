namespace Optinull.Problems.JobShop;

/// <summary>Random job shops: every job visits every machine once, in random order.</summary>
public static class JobShopRandomInstances
{
    public static JobShopProblem Generate(
        int jobCount,
        int machineCount,
        int seed,
        int minTime = 1,
        int maxTime = 20)
    {
        if (jobCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(jobCount));

        if (machineCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(machineCount));

        if (minTime <= 0 || maxTime < minTime)
            throw new ArgumentOutOfRangeException(nameof(maxTime));

        var random = new Random(seed);
        var jobs = new List<Job>(jobCount);

        for (var id = 0; id < jobCount; id++)
        {
            var machines = Enumerable.Range(0, machineCount).ToArray();

            // Fisher-Yates shuffle.
            for (var i = machines.Length - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (machines[i], machines[j]) = (machines[j], machines[i]);
            }

            var operations = machines
                .Select(machine => new JobOperation(machine, random.Next(minTime, maxTime + 1)))
                .ToList();

            jobs.Add(new Job(id, operations));
        }

        return new JobShopProblem(machineCount, jobs);
    }
}
