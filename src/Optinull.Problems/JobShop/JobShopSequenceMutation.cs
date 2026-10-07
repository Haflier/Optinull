namespace Optinull.Problems.JobShop;

public sealed class JobShopSequenceMutation
{
    public JobSequence Mutate(
        JobSequence sequence,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        ArgumentNullException.ThrowIfNull(random);

        if (sequence.Count < 2)
        {
            return new JobSequence(sequence.JobIds);
        }

        var jobIds =
            sequence.JobIds.ToList();

        var firstIndex =
            random.Next(jobIds.Count);

        var secondIndex =
            random.Next(jobIds.Count - 1);

        // Make sure the two indexes are different.
        if (secondIndex >= firstIndex)
        {
            secondIndex++;
        }

        (jobIds[firstIndex], jobIds[secondIndex]) =
            (jobIds[secondIndex], jobIds[firstIndex]);

        return new JobSequence(jobIds);
    }
}
