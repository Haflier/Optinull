namespace Optinull.Problems.JobShop;

public sealed class JobShopProblem
{
    private readonly List<Job> _jobs = [];

    public int MachineCount { get; }

    public IReadOnlyList<Job> Jobs =>
        _jobs;

    public JobShopProblem(
        int machineCount,
        IEnumerable<Job> jobs)
    {
        if (machineCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(machineCount),
                "Machine count must be greater than zero.");
        }

        ArgumentNullException.ThrowIfNull(jobs);

        _jobs.AddRange(jobs);

        if (_jobs.Count == 0)
        {
            throw new ArgumentException(
                "A job-shop problem must contain at least one job.",
                nameof(jobs));
        }

        MachineCount = machineCount;

        Validate();
    }

    private void Validate()
    {
        var jobIds = new HashSet<int>();

        foreach (var job in _jobs)
        {
            if (!jobIds.Add(job.Id))
            {
                throw new ArgumentException(
                    $"Duplicate job ID: {job.Id}.");
            }

            foreach (var operation in job.Operations)
            {
                if (operation.MachineId >= MachineCount)
                {
                    throw new ArgumentException(
                        $"Operation references machine " +
                        $"{operation.MachineId}, but the problem " +
                        $"only has {MachineCount} machines.");
                }
            }
        }
    }
}
