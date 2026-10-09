namespace Optinull.Problems.JobShop.Benchmarks;

public static class JobShopBenchmarkInstances
{
    public static JobShopProblem Ft06()
    {
        return new JobShopProblem(
            machineCount: 6,
            jobs:
            [
                new Job(
                    0,
                    [
                        new JobOperation(2, 1),
                        new JobOperation(0, 3),
                        new JobOperation(1, 6),
                        new JobOperation(3, 7),
                        new JobOperation(5, 3),
                        new JobOperation(4, 6)
                    ]),

                new Job(
                    1,
                    [
                        new JobOperation(1, 8),
                        new JobOperation(2, 5),
                        new JobOperation(4, 10),
                        new JobOperation(5, 10),
                        new JobOperation(0, 10),
                        new JobOperation(3, 4)
                    ]),

                new Job(
                    2,
                    [
                        new JobOperation(2, 5),
                        new JobOperation(3, 4),
                        new JobOperation(5, 8),
                        new JobOperation(0, 9),
                        new JobOperation(1, 1),
                        new JobOperation(4, 7)
                    ]),

                new Job(
                    3,
                    [
                        new JobOperation(1, 5),
                        new JobOperation(0, 5),
                        new JobOperation(2, 5),
                        new JobOperation(3, 3),
                        new JobOperation(4, 8),
                        new JobOperation(5, 9)
                    ]),

                new Job(
                    4,
                    [
                        new JobOperation(2, 9),
                        new JobOperation(1, 3),
                        new JobOperation(4, 5),
                        new JobOperation(5, 4),
                        new JobOperation(0, 3),
                        new JobOperation(3, 1)
                    ]),

                new Job(
                    5,
                    [
                        new JobOperation(1, 3),
                        new JobOperation(3, 3),
                        new JobOperation(5, 9),
                        new JobOperation(0, 10),
                        new JobOperation(4, 4),
                        new JobOperation(2, 1)
                    ])
            ]);
    }

    /// <summary>
    /// Fisher and Thompson 10x10 (mt10). Known optimal makespan: 930.
    /// Each row is one job: machine, processing time, machine, time, ...
    /// </summary>
    public static JobShopProblem Ft10() =>
        FromMachineTimePairs(
            machineCount: 10,
            rows:
            [
                [0, 29, 1, 78, 2, 9, 3, 36, 4, 49, 5, 11, 6, 62, 7, 56, 8, 44, 9, 21],
                [0, 43, 2, 90, 4, 75, 9, 11, 3, 69, 1, 28, 6, 46, 5, 46, 7, 72, 8, 30],
                [1, 91, 0, 85, 3, 39, 2, 74, 8, 90, 5, 10, 7, 12, 6, 89, 9, 45, 4, 33],
                [1, 81, 2, 95, 0, 71, 4, 99, 6, 9, 8, 52, 7, 85, 3, 98, 9, 22, 5, 43],
                [2, 14, 0, 6, 1, 22, 5, 61, 3, 26, 4, 69, 8, 21, 7, 49, 9, 72, 6, 53],
                [2, 84, 1, 2, 5, 52, 3, 95, 8, 48, 9, 72, 0, 47, 6, 65, 4, 6, 7, 25],
                [1, 46, 0, 37, 3, 61, 2, 13, 6, 32, 5, 21, 9, 32, 8, 89, 7, 30, 4, 55],
                [2, 31, 0, 86, 1, 46, 5, 74, 4, 32, 6, 88, 8, 19, 9, 48, 7, 36, 3, 79],
                [0, 76, 1, 69, 3, 76, 5, 51, 2, 85, 9, 11, 6, 40, 7, 89, 4, 26, 8, 74],
                [1, 85, 0, 13, 2, 61, 6, 7, 8, 64, 9, 76, 5, 47, 3, 52, 4, 90, 7, 45]
            ]);

    private static JobShopProblem FromMachineTimePairs(
        int machineCount,
        int[][] rows) =>
        new(
            machineCount,
            rows.Select((row, id) =>
                new Job(
                    id,
                    Enumerable
                        .Range(0, row.Length / 2)
                        .Select(i => new JobOperation(row[2 * i], row[2 * i + 1])))));
}
