using Optinull.Problems.JobShop;
using Optinull.Problems.JobShop.Benchmarks;

namespace Optinull.Problems.Tests.JobShop;

public sealed class JobShopFt06KnownOptimalSequenceTests
{
    [Fact]
    public void KnownOptimalSequence_ProducesMakespan55()
    {
        var problem =
            JobShopBenchmarkInstances.Ft06();

        var sequence =
              new JobSequence(
              [
                  2, 1, 2, 3, 0, 1, 4, 5, 5, 0, 3, 2,
                  2, 5, 0, 3, 3, 0, 4, 1, 4, 1, 3, 0,
                  5, 5, 5, 1, 2, 4, 0, 4, 2, 3, 1, 4
              ]);

        var decoder =
            new JobShopScheduleDecoder();

        var schedule =
            decoder.Decode(
                problem,
                sequence);

        Console.WriteLine(
            $"Known optimal makespan: 55");

        Console.WriteLine(
            $"Schedule makespan: {schedule.Makespan}");

        foreach (var machineGroup in
                 schedule.Operations
                     .GroupBy(operation =>
                         operation.MachineId)
                     .OrderBy(group =>
                         group.Key))
        {
            Console.WriteLine();
            Console.WriteLine(
                $"Machine {machineGroup.Key}:");

            foreach (var operation in
                     machineGroup
                         .OrderBy(operation =>
                             operation.StartTime))
            {
                Console.WriteLine(
                    $"  J{operation.JobId}" +
                    $"O{operation.OperationIndex} " +
                    $"[{operation.StartTime} -> " +
                    $"{operation.EndTime}]");
            }
        }

        Assert.Equal(
            55,
            schedule.Makespan);
    }
}
