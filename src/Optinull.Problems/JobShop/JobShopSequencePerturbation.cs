namespace Optinull.Problems.JobShop;

public sealed class JobShopSequencePerturbation
{
    public JobSequence Perturb(
        JobSequence sequence,
        int swapCount,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        ArgumentNullException.ThrowIfNull(random);

        if (swapCount <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(swapCount),
                "Swap count must be greater than zero.");

        var jobIds = sequence.JobIds.ToArray();

        for (var i = 0; i < swapCount; i++)
        {
            var firstIndex = random.Next(jobIds.Length);
            var secondIndex = random.Next(jobIds.Length);

            if (firstIndex == secondIndex)
            {
                i--;
                continue;
            }

            if (jobIds[firstIndex] == jobIds[secondIndex])
            {
                i--;
                continue;
            }

            (jobIds[firstIndex], jobIds[secondIndex]) =
                (jobIds[secondIndex], jobIds[firstIndex]);
        }

        return new JobSequence(jobIds);
    }
}
