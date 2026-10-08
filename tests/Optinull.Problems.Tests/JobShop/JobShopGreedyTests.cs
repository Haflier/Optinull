using Optinull.Optimization.Search;
using Optinull.Problems.JobShop;
using Optinull.Problems.JobShop.Benchmarks;

namespace Optinull.Problems.Tests.JobShop;

public sealed class JobShopGreedyTests
{
    private static JobShopSearchProblem Ft06() =>
        new(JobShopBenchmarkInstances.Ft06());

    [Fact]
    public void Greedy_ProducesValidSequenceForFt06()
    {
        var result = new GreedyBaseline<JobSequence>().Solve(Ft06());

        Assert.Equal(36, result.Best.Count);
        Assert.All(
            result.Best.JobIds.GroupBy(id => id),
            group => Assert.Equal(6, group.Count()));

        // 55 is the proven optimum; nothing can beat it.
        Assert.True(result.Evaluation.ObjectiveValue >= 55);
    }

    [Fact]
    public void Greedy_IsDeterministic()
    {
        var first = new GreedyBaseline<JobSequence>().Solve(Ft06());
        var second = new GreedyBaseline<JobSequence>().Solve(Ft06());

        Assert.Equal(first.Best.JobIds, second.Best.JobIds);
    }

    [Fact]
    public void LocalSearch_FromGreedyStart_IsNeverWorse()
    {
        var problem = Ft06();

        var greedy = new GreedyBaseline<JobSequence>().Solve(problem);

        var climbed = new HillClimbing<JobSequence>()
            .Solve(problem, greedy.Best);

        Assert.True(
            climbed.Evaluation.ObjectiveValue <=
            greedy.Evaluation.ObjectiveValue);
    }
}
