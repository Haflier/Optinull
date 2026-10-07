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
                        new JobOperation(0, 1),
                        new JobOperation(1, 3),
                        new JobOperation(2, 6),
                        new JobOperation(3, 7),
                        new JobOperation(4, 3),
                        new JobOperation(5, 6)
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
                        new JobOperation(2, 5),
                        new JobOperation(0, 5),
                        new JobOperation(3, 3),
                        new JobOperation(4, 8),
                        new JobOperation(5, 9)
                    ]),

                new Job(
                    4,
                    [
                        new JobOperation(2, 9),
                        new JobOperation(3, 3),
                        new JobOperation(5, 5),
                        new JobOperation(0, 4),
                        new JobOperation(1, 3),
                        new JobOperation(4, 1)
                    ]),

                new Job(
                    5,
                    [
                        new JobOperation(1, 3),
                        new JobOperation(0, 3),
                        new JobOperation(3, 9),
                        new JobOperation(4, 10),
                        new JobOperation(5, 4),
                        new JobOperation(2, 1)
                    ])
            ]);
    }
}
