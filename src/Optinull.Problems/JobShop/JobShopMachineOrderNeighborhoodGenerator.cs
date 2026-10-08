namespace Optinull.Problems.JobShop;

public sealed class JobShopMachineOrderNeighborhoodGenerator
{
    private readonly JobShopScheduleDecoder _decoder;

    public JobShopMachineOrderNeighborhoodGenerator()
    {
        _decoder =
            new JobShopScheduleDecoder();
    }

    public IEnumerable<JobSequence> Generate(
        JobShopProblem problem,
        JobSequence sequence)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(sequence);

        var schedule =
            _decoder.Decode(
                problem,
                sequence);

        var operations =
            schedule.Operations.ToDictionary(
                GetKey);

        var operationKeys =
            operations.Keys.ToList();

        foreach (var machineGroup in
                 schedule.Operations
                     .GroupBy(operation =>
                         operation.MachineId))
        {
            var ordered =
                machineGroup
                    .OrderBy(operation =>
                        operation.StartTime)
                    .ToList();

            // Try reversing the ordering of every
            // adjacent pair on this machine.
            //
            // Example:
            //
            // A -> B -> C
            //
            // becomes:
            //
            // B -> A -> C
            //
            // and:
            //
            // A -> C -> B
            //
            for (var i = 0;
                 i < ordered.Count - 1;
                 i++)
            {
                var machineOrder =
                    schedule.Operations
                        .GroupBy(operation =>
                            operation.MachineId)
                        .ToDictionary(
                            group =>
                                group.Key,
                            group =>
                                group
                                    .OrderBy(
                                        operation =>
                                            operation.StartTime)
                                    .Select(
                                        GetKey)
                                    .ToList());

                var first =
                    GetKey(ordered[i]);

                var second =
                    GetKey(ordered[i + 1]);

                var machineId =
                    ordered[i].MachineId;

                var modifiedOrder =
                    machineOrder[machineId];

                (
                    modifiedOrder[i],
                    modifiedOrder[i + 1]
                ) =
                (
                    modifiedOrder[i + 1],
                    modifiedOrder[i]
                );

                var candidate =
                    TryBuildJobSequence(
                        problem,
                        machineOrder,
                        operations);

                if (candidate is not null)
                {
                    yield return candidate;
                }
            }
        }
    }

    private static JobSequence?
        TryBuildJobSequence(
            JobShopProblem problem,
            Dictionary<
                int,
                List<(int JobId, int OperationIndex)>>
                machineOrder,
            Dictionary<
                (int JobId, int OperationIndex),
                ScheduledOperation>
                operations)
    {
        var allOperations =
            operations.Keys.ToList();

        var successors =
            allOperations.ToDictionary(
                operation => operation,
                _ =>
                    new List<
                        (int JobId, int OperationIndex)>());

        var inDegree =
            allOperations.ToDictionary(
                operation => operation,
                _ => 0);

        // Job precedence:
        //
        // J0O0 -> J0O1 -> J0O2 -> ...
        foreach (var job in problem.Jobs)
        {
            for (var i = 0;
                 i < job.Operations.Count - 1;
                 i++)
            {
                var from =
                    (job.Id, i);

                var to =
                    (job.Id, i + 1);

                AddEdge(
                    from,
                    to,
                    successors,
                    inDegree);
            }
        }

        // Machine precedence:
        //
        // The modified machine order becomes
        // precedence constraints.
        foreach (var machine in machineOrder.Values)
        {
            for (var i = 0;
                 i < machine.Count - 1;
                 i++)
            {
                AddEdge(
                    machine[i],
                    machine[i + 1],
                    successors,
                    inDegree);
            }
        }

        // Topological sort the resulting disjunctive
        // graph.
        //
        // If the modified machine order creates a cycle,
        // it is not a valid schedule representation.
        var ready =
            new Queue<
                (int JobId, int OperationIndex)>(
                allOperations
                    .Where(operation =>
                        inDegree[operation] == 0));

        var orderedOperations =
            new List<
                (int JobId, int OperationIndex)>(
                allOperations.Count);

        while (ready.Count > 0)
        {
            var current =
                ready.Dequeue();

            orderedOperations.Add(current);

            foreach (var successor in
                     successors[current])
            {
                inDegree[successor]--;

                if (inDegree[successor] == 0)
                {
                    ready.Enqueue(successor);
                }
            }
        }

        if (orderedOperations.Count !=
            allOperations.Count)
        {
            // The machine-order modification created
            // a cycle with the job precedence constraints.
            return null;
        }

        var jobIds =
            orderedOperations
                .Select(operation =>
                    operation.JobId)
                .ToList();

        return new JobSequence(jobIds);
    }

    private static void AddEdge(
        (
            int JobId,
            int OperationIndex) from,
        (
            int JobId,
            int OperationIndex) to,
        Dictionary<
            (int JobId, int OperationIndex),
            List<(int JobId, int OperationIndex)>>
            successors,
        Dictionary<
            (int JobId, int OperationIndex),
            int>
            inDegree)
    {
        successors[from].Add(to);
        inDegree[to]++;
    }

    private static (
        int JobId,
        int OperationIndex)
        GetKey(
            ScheduledOperation operation)
    {
        return
            (
                operation.JobId,
                operation.OperationIndex
            );
    }
}
