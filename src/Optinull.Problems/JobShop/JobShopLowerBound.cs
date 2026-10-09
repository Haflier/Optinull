namespace Optinull.Problems.JobShop;

public static class JobShopLowerBound
{
    /// <summary>
    /// No schedule can finish before the longest job, or before the busiest
    /// machine has processed all of its work.
    /// </summary>
    public static double Compute(JobShopProblem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);

        var longestJob = problem.Jobs.Max(
            job => job.Operations.Sum(operation => operation.ProcessingTime));

        var load = new double[problem.MachineCount];

        foreach (var job in problem.Jobs)
        {
            foreach (var operation in job.Operations)
                load[operation.MachineId] += operation.ProcessingTime;
        }

        return Math.Max(longestJob, load.Max());
    }
}
