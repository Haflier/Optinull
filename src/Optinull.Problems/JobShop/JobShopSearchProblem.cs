using Optinull.Domain.Evaluation;
using Optinull.Domain.Objectives;
using Optinull.Optimization.Randomness;
using Optinull.Optimization.Search;

namespace Optinull.Problems.JobShop;

/// <summary>Adapts a job shop to the generic search solvers over job-repetition sequences.</summary>
public sealed class JobShopSearchProblem :
    IRecombinableProblem<JobSequence>,
    IConstructiveProblem<JobSequence>
{
    private readonly JobShopProblem _problem;
    private readonly IJobShopEvaluator _evaluator;
    private readonly JobShopNeighborhoodGenerator _neighborhood = new();
    private readonly JobShopCrossover _crossover;
    private readonly JobShopMutation _mutation;

    public JobShopSearchProblem(
        JobShopProblem problem,
        IJobShopEvaluator? evaluator = null,
        JobShopCrossover crossover = JobShopCrossover.OnePoint,
        JobShopMutation mutation = JobShopMutation.Swap)
    {
        ArgumentNullException.ThrowIfNull(problem);

        _problem = problem;
        _evaluator = evaluator ?? new JobShopEvaluator();
        _crossover = crossover;
        _mutation = mutation;
    }

    public ObjectiveType ObjectiveType => ObjectiveType.Minimize;

    public JobSequence CreateRandom(IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);

        var jobIds = new List<int>();

        foreach (var job in _problem.Jobs)
        {
            for (var i = 0; i < job.Operations.Count; i++)
                jobIds.Add(job.Id);
        }

        // Fisher-Yates shuffle.
        for (var i = jobIds.Count - 1; i > 0; i--)
        {
            var j = random.Next(0, i + 1);
            (jobIds[i], jobIds[j]) = (jobIds[j], jobIds[i]);
        }

        return new JobSequence(jobIds);
    }

    public EvaluationResult Evaluate(JobSequence solution) =>
        _evaluator.Evaluate(_problem, solution);

    public IEnumerable<JobSequence> Neighbors(JobSequence solution) =>
        _neighborhood.Generate(solution);

    public JobSequence RandomNeighbor(
        JobSequence solution,
        IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(solution);
        ArgumentNullException.ThrowIfNull(random);

        var ids = solution.JobIds.ToArray();

        // Nothing to swap if every entry is the same job.
        if (ids.All(id => id == ids[0]))
            return solution;

        while (true)
        {
            var first = random.Next(0, ids.Length);
            var second = random.Next(0, ids.Length - 1);

            if (second >= first)
                second++;

            // Swapping two entries of the same job changes nothing.
            if (ids[first] == ids[second])
                continue;

            (ids[first], ids[second]) = (ids[second], ids[first]);

            return new JobSequence(ids);
        }
    }

    public JobSequence Crossover(
        JobSequence first,
        JobSequence second,
        IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        ArgumentNullException.ThrowIfNull(random);

        if (first.Count != second.Count)
        {
            throw new ArgumentException(
                "Parents must have the same sequence length.");
        }

        if (first.Count < 2)
            return first;

        return _crossover switch
        {
            JobShopCrossover.OnePoint =>
                OnePointCrossover(first, second, random),

            JobShopCrossover.PrecedencePreserving =>
                PoxCrossover(first, second, random),

            _ => throw new InvalidOperationException(
                $"Unsupported crossover: {_crossover}")
        };
    }

    public JobSequence Mutate(JobSequence solution, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(solution);
        ArgumentNullException.ThrowIfNull(random);

        return _mutation switch
        {
            JobShopMutation.Swap =>
                RandomNeighbor(solution, random),

            JobShopMutation.Insertion =>
                InsertionMutation(solution, random),

            _ => throw new InvalidOperationException(
                $"Unsupported mutation: {_mutation}")
        };
    }

    private static JobSequence OnePointCrossover(
        JobSequence first,
        JobSequence second,
        IRandomSource random)
    {
        // Keep a prefix of the first parent, then fill the rest in the
        // second parent's order, never exceeding each job's operation count.
        var point = random.Next(1, first.Count);

        var child = first.JobIds.Take(point).ToList();

        var remaining = first.JobIds
            .GroupBy(id => id)
            .ToDictionary(group => group.Key, group => group.Count());

        foreach (var id in child)
            remaining[id]--;

        foreach (var id in second.JobIds)
        {
            if (remaining[id] > 0)
            {
                child.Add(id);
                remaining[id]--;
            }
        }

        return new JobSequence(child);
    }

    private JobSequence PoxCrossover(
        JobSequence first,
        JobSequence second,
        IRandomSource random)
    {
        var jobIds = _problem.Jobs.Select(job => job.Id).ToList();

        // Random non-empty, proper subset of jobs that keeps its positions.
        var kept = jobIds
            .Where(_ => random.Next(0, 2) == 0)
            .ToHashSet();

        if (kept.Count == 0)
            kept.Add(jobIds[random.Next(0, jobIds.Count)]);

        if (kept.Count == jobIds.Count && jobIds.Count > 1)
            kept.Remove(jobIds[random.Next(0, jobIds.Count)]);

        // Fill the other slots with the remaining jobs in parent B's order.
        // Both parents contain the same number of entries for every job, so
        // the filler has exactly one entry per open slot.
        var filler = second.JobIds
            .Where(id => !kept.Contains(id))
            .GetEnumerator();

        var child = new int[first.Count];

        for (var i = 0; i < child.Length; i++)
        {
            if (kept.Contains(first.JobIds[i]))
            {
                child[i] = first.JobIds[i];
            }
            else
            {
                filler.MoveNext();
                child[i] = filler.Current;
            }
        }

        return new JobSequence(child);
    }

    private static JobSequence InsertionMutation(
        JobSequence solution,
        IRandomSource random)
    {
        var ids = solution.JobIds.ToList();

        if (ids.Count < 2)
            return solution;

        // Moving an entry across identical neighbors changes nothing,
        // so retry a few times before giving up.
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var from = random.Next(0, ids.Count);
            var to = random.Next(0, ids.Count - 1);

            if (to >= from)
                to++;

            var moved = ids.ToList();
            var id = moved[from];
            moved.RemoveAt(from);
            moved.Insert(to, id);

            if (!moved.SequenceEqual(ids))
                return new JobSequence(moved);
        }

        return solution;
    }

    public JobSequence CreateGreedy(Func<JobSequence, EvaluationResult> evaluate)
    {
        // Dispatching rule: repeatedly schedule the operation that can
        // start earliest; break ties by shortest processing time.
        var jobs = _problem.Jobs;
        var nextOperation = new int[jobs.Count];
        var jobReady = new double[jobs.Count];
        var machineReady = new double[_problem.MachineCount];

        var remaining = jobs.Sum(job => job.Operations.Count);
        var sequence = new List<int>(remaining);

        while (remaining > 0)
        {
            var chosen = -1;
            var chosenStart = double.MaxValue;
            var chosenDuration = double.MaxValue;

            for (var j = 0; j < jobs.Count; j++)
            {
                if (nextOperation[j] >= jobs[j].Operations.Count)
                    continue;

                var operation = jobs[j].Operations[nextOperation[j]];

                var start = Math.Max(
                    jobReady[j],
                    machineReady[operation.MachineId]);

                if (start < chosenStart ||
                    (start == chosenStart &&
                     operation.ProcessingTime < chosenDuration))
                {
                    chosen = j;
                    chosenStart = start;
                    chosenDuration = operation.ProcessingTime;
                }
            }

            var selected = jobs[chosen].Operations[nextOperation[chosen]];
            var end = chosenStart + selected.ProcessingTime;

            jobReady[chosen] = end;
            machineReady[selected.MachineId] = end;
            nextOperation[chosen]++;

            sequence.Add(jobs[chosen].Id);
            remaining--;
        }

        return new JobSequence(sequence);
    }
}
