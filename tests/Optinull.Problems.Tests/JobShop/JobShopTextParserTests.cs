using Optinull.Problems.JobShop;
using Optinull.Problems.JobShop.Benchmarks;
using Optinull.Problems.JobShop.Parsing;

namespace Optinull.Problems.Tests.JobShop;

public sealed class JobShopTextParserTests
{
    private const string Small = "2 2\n0 3 1 2\n1 4 0 2";

    private static JobShopInputLimits Limits => JobShopInputLimits.Default;

    [Fact]
    public void Parses_Header_Jobs_AndOperations()
    {
        Assert.True(JobShopTextParser.TryParse(Small, Limits, out var problem, out var error));
        Assert.Null(error);

        Assert.Equal(2, problem!.MachineCount);
        Assert.Equal(2, problem.Jobs.Count);

        var first = problem.Jobs[0].Operations;
        Assert.Equal(2, first.Count);
        Assert.Equal((0, 3.0), (first[0].MachineId, first[0].ProcessingTime));
        Assert.Equal((1, 2.0), (first[1].MachineId, first[1].ProcessingTime));
    }

    [Fact]
    public void Ignores_Comments_BlankLines_AndWindowsLineEndings()
    {
        var text = "# my instance\r\n\r\n2 2\r\n0 3 1 2\r\n\r\n# job 2\r\n1 4 0 2\r\n";

        Assert.True(JobShopTextParser.TryParse(text, Limits, out var problem, out _));
        Assert.Equal(2, problem!.Jobs.Count);
    }

    [Fact]
    public void Accepts_CommasAndTabsAsSeparators()
    {
        var text = "2,2\n0,3\t1,2\n1 4 0 2";

        Assert.True(JobShopTextParser.TryParse(text, Limits, out var problem, out _));
        Assert.Equal(2, problem!.Jobs.Count);
    }

    [Theory]
    [InlineData("", "empty")]
    [InlineData("   \n  ", "empty")]
    [InlineData("# only a comment", "empty")]
    [InlineData("3", "first line")]
    [InlineData("a b", "first line")]
    [InlineData("0 2", "positive")]
    [InlineData("2 2\n0 3 1 2", "Expected 2 job lines")]
    [InlineData("2 2\n0 3 1\n1 4 0 2", "pairs")]
    [InlineData("2 2\n0 3 1 x\n1 4 0 2", "whole numbers")]
    [InlineData("2 2\n0 3 5 2\n1 4 0 2", "machine 5")]
    [InlineData("2 2\n0 0 1 2\n1 4 0 2", "processing time")]
    [InlineData("2 2\n0 3 1 2\n1 4 9 2", "Line 3")]
    public void InvalidInput_IsRejectedWithAUsefulMessage(string text, string expected)
    {
        Assert.False(JobShopTextParser.TryParse(text, Limits, out var problem, out var error));
        Assert.Null(problem);
        Assert.Contains(expected, error);
    }

    [Fact]
    public void TooManyJobs_IsRejected()
    {
        var limits = new JobShopInputLimits(MaxJobs: 1);

        Assert.False(JobShopTextParser.TryParse(Small, limits, out _, out var error));
        Assert.Contains("Too many jobs", error);
    }

    [Fact]
    public void TooManyMachines_IsRejected()
    {
        var limits = new JobShopInputLimits(MaxMachines: 1);

        Assert.False(JobShopTextParser.TryParse(Small, limits, out _, out var error));
        Assert.Contains("Too many machines", error);
    }

    [Fact]
    public void TooManyOperations_IsRejected()
    {
        var limits = new JobShopInputLimits(MaxOperations: 3);

        Assert.False(JobShopTextParser.TryParse(Small, limits, out _, out var error));
        Assert.Contains("Too many operations", error);
    }

    [Fact]
    public void TooLongText_IsRejected()
    {
        var limits = new JobShopInputLimits(MaxCharacters: 5);

        Assert.False(JobShopTextParser.TryParse(Small, limits, out _, out var error));
        Assert.Contains("too long", error);
    }

    [Fact]
    public void LowerBound_IsLongestJobOrBusiestMachine()
    {
        JobShopTextParser.TryParse(Small, Limits, out var problem, out _);

        // Jobs take 5 and 6; machine 0 works 5, machine 1 works 6.
        Assert.Equal(6, JobShopLowerBound.Compute(problem!));
    }

    [Fact]
    public void LowerBound_Ft06_IsBelowKnownOptimum()
    {
        var bound = JobShopLowerBound.Compute(JobShopBenchmarkInstances.Ft06());

        Assert.InRange(bound, 1, 55);
    }
}
