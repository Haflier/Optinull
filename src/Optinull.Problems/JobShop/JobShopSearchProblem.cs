using Optinull.Domain.Evaluation;
using Optinull.Domain.Objectives;
using Optinull.Optimization.Randomness;
using Optinull.Optimization.Search;

namespace Optinull.Problems.JobShop;

/// <summary>Adapts a job shop to ISearchProblem over job-repetition sequences.</summary>
public sealed class JobShopSearchProblem : IRecombinableProblem<JobSequence>
{
    private readonly JobShopProblem _problem;
    private readonly IJobShopEvaluator _evaluator;
    private readonly JobShopNeighborhoodGenerator _neighborhood = new();

    public JobShopSearchProblem(
        JobShopProblem problem,
        IJobShopEvaluator? evaluator = null)
    {
        ArgumentNullException.ThrowIfNull(problem);

        _problem = problem;
        _evaluator = evaluator ?? new JobShopEvaluator();
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

    public JobSequence Mutate(JobSequence solution, IRandomSource random) =>
        RandomNeighbor(solution, random);
}
