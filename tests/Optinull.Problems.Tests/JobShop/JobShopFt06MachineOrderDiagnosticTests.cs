using Optinull.Problems.JobShop;
using Optinull.Problems.JobShop.Benchmarks;

namespace Optinull.Problems.Tests.JobShop;

public sealed class JobShopFt06MachineOrderDiagnosticTests
{
    [Fact]
    public void InspectMachineOrders()
    {
        var problem =
            JobShopBenchmarkInstances.Ft06();

        var sequence =
            new JobSequence(
            [
                2, 0, 1, 2, 1, 1, 2, 5, 5, 3, 3, 5,
                4, 0, 2, 1, 3, 3, 5, 4, 5, 4, 1, 0,
                5, 4, 0, 1, 2, 3, 3, 0, 2, 4, 0, 4
            ]);

        var decoder =
            new JobShopScheduleDecoder();

        var schedule =
            decoder.Decode(
                problem,
                sequence);

        Console.WriteLine(
            $"Makespan: {schedule.Makespan}");

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
            78,
            schedule.Makespan);
    }
}
