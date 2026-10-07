namespace Optinull.Problems.JobShop;

public sealed class JobOperation
{
    public int MachineId { get; }
    public double ProcessingTime { get; }

    public JobOperation(
        int machineId,
        double processingTime)
    {
        if (machineId < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(machineId),
                "Machine ID cannot be negative.");
        }

        if (processingTime <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(processingTime),
                "Processing time must be greater than zero.");
        }

        MachineId = machineId;
        ProcessingTime = processingTime;
    }
}
