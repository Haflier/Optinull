namespace Optinull.Problems.JobShop;

public sealed class JobShopSequenceGenerator
{
    public JobSequence Generate(
        JobShopProblem problem,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(random);

        var jobIds = new List<int>();

        foreach (var job in problem.Jobs)
        {
            // A job ID appears once for every operation.
            //
            // Example:
            // Job 0 has 3 operations -> [0, 0, 0]
            // Job 1 has 2 operations -> [1, 1]
            //
            // After shuffling, this could become:
            // [0, 1, 0, 1, 0]
            for (var i = 0; i < job.Operations.Count; i++)
            {
                jobIds.Add(job.Id);
            }
        }

        Shuffle(jobIds, random);

        return new JobSequence(jobIds);
    }

    private static void Shuffle(
        List<int> values,
        Random random)
    {
        // Fisher-Yates shuffle.
        //
        // Each position is swapped with a randomly
        // selected position from the remaining range.
        for (var i = values.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);

            (values[i], values[j]) =
                (values[j], values[i]);
        }
    }
}
