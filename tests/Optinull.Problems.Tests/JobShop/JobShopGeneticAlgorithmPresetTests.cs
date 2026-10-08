using Optinull.Optimization.Randomness;
using Optinull.Problems.JobShop;
using Optinull.Problems.JobShop.Benchmarks;

namespace Optinull.Problems.Tests.JobShop;

public sealed class JobShopGeneticAlgorithmPresetTests
{
    [Theory]
    [InlineData(42)]
    [InlineData(43)]
    [InlineData(44)]
    public void Preset_SolvesFt06ToOptimum_WithinBudget(int seed)
    {
        var problem = JobShopGeneticAlgorithmPreset.CreateProblem(
            JobShopBenchmarkInstances.Ft06());

        var result = JobShopGeneticAlgorithmPreset
            .CreateSolver(random: new RandomSource(seed))
            .Solve(problem);

        Assert.Equal(55, result.Evaluation.ObjectiveValue);
        Assert.True(result.EvaluationCount <= 20_000);
    }
}
