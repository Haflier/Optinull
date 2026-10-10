using Optinull.Problems.Knapsack;

namespace Optinull.Problems.Tests.Knapsack;

public sealed class KnapsackTextParserTests
{
    private static KnapsackInputLimits Limits => KnapsackInputLimits.Default;

    [Fact]
    public void Parses_CapacityThenItems()
    {
        Assert.True(KnapsackTextParser.TryParse("50\n60 10\n100 20\n120 30", Limits, out var problem, out var error));
        Assert.Null(error);

        Assert.Equal(50, problem!.Capacity);
        Assert.Equal(3, problem.ItemCount);
        Assert.Equal(new KnapsackItem(120, 30), problem.Items[2]);
    }

    [Fact]
    public void Ignores_Comments_BlankLines_WindowsLineEndings_AndCommas()
    {
        var text = "# capacity\r\n50\r\n\r\n60,10\r\n# next\r\n100;20\r\n";

        Assert.True(KnapsackTextParser.TryParse(text, Limits, out var problem, out _));
        Assert.Equal(2, problem!.ItemCount);
    }

    [Theory]
    [InlineData("", "empty")]
    [InlineData("# nothing", "empty")]
    [InlineData("50", "at least one item")]
    [InlineData("abc\n1 1", "Line 1")]
    [InlineData("0\n1 1", "Line 1")]
    [InlineData("50 10\n1 1", "Line 1")]
    [InlineData("50\n60", "Line 2")]
    [InlineData("50\n60 10 3", "Line 2")]
    [InlineData("50\n60 0", "Line 2")]
    [InlineData("50\n-1 5", "Line 2")]
    [InlineData("50\n1.5 5", "Line 2")]
    public void Rejects_BadInput_WithTheReason(string text, string reason)
    {
        Assert.False(KnapsackTextParser.TryParse(text, Limits, out var problem, out var error));
        Assert.Null(problem);
        Assert.Contains(reason, error);
    }

    [Fact]
    public void Rejects_TooManyItems()
    {
        var lines = string.Join("\n", Enumerable.Repeat("1 1", Limits.MaxItems + 1));

        Assert.False(KnapsackTextParser.TryParse("100\n" + lines, Limits, out _, out var error));
        Assert.Contains("Too many items", error);
    }
}
