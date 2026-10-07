namespace Optinull.Problems.JobShop;

public sealed class JobShopSequenceCrossover
{
    public JobSequence Crossover(
        JobSequence firstParent,
        JobSequence secondParent,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(firstParent);
        ArgumentNullException.ThrowIfNull(secondParent);
        ArgumentNullException.ThrowIfNull(random);

        if (firstParent.Count != secondParent.Count)
        {
            throw new ArgumentException(
                "Parents must have the same sequence length.");
        }

        if (firstParent.Count < 2)
        {
            return new JobSequence(firstParent.JobIds);
        }

        var crossoverPoint =
            random.Next(
                1,
                firstParent.Count);

        var child =
            firstParent.JobIds
                .Take(crossoverPoint)
                .ToList();

        var requiredCounts =
            firstParent.JobIds
                .GroupBy(id => id)
                .ToDictionary(
                    group => group.Key,
                    group => group.Count());

        var childCounts =
            child
                .GroupBy(id => id)
                .ToDictionary(
                    group => group.Key,
                    group => group.Count());

        foreach (var jobId in secondParent.JobIds)
        {
            childCounts.TryGetValue(
                jobId,
                out var currentCount);

            requiredCounts.TryGetValue(
                jobId,
                out var requiredCount);

            if (currentCount >= requiredCount)
            {
                continue;
            }

            child.Add(jobId);
            childCounts[jobId] =
                currentCount + 1;

            if (child.Count == firstParent.Count)
            {
                break;
            }
        }

        return new JobSequence(child);
    }
}
