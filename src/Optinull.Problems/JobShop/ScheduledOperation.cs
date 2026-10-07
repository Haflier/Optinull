namespace Optinull.Problems.JobShop;

public sealed class ScheduledOperation
{
    public int JobId { get; }
    public int OperationIndex { get; }
    public int MachineId { get; }
    public double ProcessingTime { get; }
    public double StartTime { get; }
    public double EndTime { get; }

    public ScheduledOperation(
        int jobId,
        int operationIndex,
        int machineId,
        double processingTime,
        double startTime)
    {
        if (jobId < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(jobId));
        }

        if (operationIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(operationIndex));
        }

        if (machineId < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(machineId));
        }

        if (processingTime <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(processingTime));
        }

        if (startTime < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(startTime));
        }

        JobId = jobId;
        OperationIndex = operationIndex;
        MachineId = machineId;
        ProcessingTime = processingTime;
        StartTime = startTime;
        EndTime = startTime + processingTime;
    }
}
