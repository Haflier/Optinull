using Optinull.Application.Optimization;
using Optinull.Application.Reporting;
using Optinull.Problems.JobShop.Benchmarks;

namespace Optinull.Application.Tests;

public sealed class JobShopSolveServiceTests
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47];

    [Theory]
    [InlineData(SolverKind.SimulatedAnnealing)]
    [InlineData(SolverKind.GeneticAlgorithm)]
    public void Solve_Ft06_ReturnsConsistentSchedule(SolverKind kind)
    {
        var problem = JobShopBenchmarkInstances.Ft06();

        var result = new JobShopSolveService().Solve(problem, kind, seed: 42);

        Assert.True(result.Makespan >= 55);
        Assert.Equal(result.Makespan, result.Schedule.Makespan);
        Assert.Equal(36, result.Schedule.Operations.Count);
    }

    [Fact]
    public void Solve_SameSeed_IsReproducible()
    {
        var problem = JobShopBenchmarkInstances.Ft06();
        var service = new JobShopSolveService();

        Assert.Equal(
            service.Solve(problem, seed: 7).Makespan,
            service.Solve(problem, seed: 7).Makespan);
    }

    [Fact]
    public void Render_ReturnsTwoPngs()
    {
        var problem = JobShopBenchmarkInstances.Ft06();
        var result = new JobShopSolveService().Solve(problem);

        var report = new JobShopReportService().Render(problem, result);

        Assert.Equal(PngSignature, report.GanttPng.Take(4).ToArray());
        Assert.Equal(PngSignature, report.ConvergencePng.Take(4).ToArray());
    }
}
