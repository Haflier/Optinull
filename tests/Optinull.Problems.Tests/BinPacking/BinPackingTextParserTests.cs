using Optinull.Problems.BinPacking;

namespace Optinull.Problems.Tests.BinPacking;

public sealed class BinPackingTextParserTests
{
    private static BinPackingInputLimits Limits => BinPackingInputLimits.Default;

    [Fact]
    public void Parses_CapacityThenSizes_WithOptionalQuantities()
    {
        Assert.True(BinPackingTextParser.TryParse("6000\n2400 3\n1800\n1200 2", Limits, out var problem, out var error));
        Assert.Null(error);

        Assert.Equal(6000, problem!.Capacity);
        Assert.Equal(new[] { 2400, 2400, 2400, 1800, 1200, 1200 }, problem.Sizes);
    }

    [Fact]
    public void Ignores_Comments_BlankLines_WindowsLineEndings_AndCommas()
    {
        var text = "# bar length\r\n100\r\n\r\n40,2\r\n# next\r\n30;1\r\n";

        Assert.True(BinPackingTextParser.TryParse(text, Limits, out var problem, out _));
        Assert.Equal(3, problem!.ItemCount);
    }

    [Theory]
    [InlineData("", "empty")]
    [InlineData("# nothing", "empty")]
    [InlineData("100", "at least one item")]
    [InlineData("abc\n5", "Line 1")]
    [InlineData("0\n5", "Line 1")]
    [InlineData("100 5\n5", "Line 1")]
    [InlineData("100\n5 2 1", "Line 2")]
    [InlineData("100\n0", "Line 2")]
    [InlineData("100\n-5", "Line 2")]
    [InlineData("100\n1.5", "Line 2")]
    [InlineData("100\n5 0", "Line 2")]
    [InlineData("50\n60", "cannot fit")]
    public void Rejects_BadInput_WithTheReason(string text, string reason)
    {
        Assert.False(BinPackingTextParser.TryParse(text, Limits, out var problem, out var error));
        Assert.Null(problem);
        Assert.Contains(reason, error);
    }

    [Fact]
    public void Rejects_TooManyItems_CountingQuantities()
    {
        var text = $"100\n10 {Limits.MaxItems}\n10";

        Assert.False(BinPackingTextParser.TryParse(text, Limits, out _, out var error));
        Assert.Contains("Too many items", error);
    }

    [Fact]
    public void Rejects_TextThatIsTooLong()
    {
        var text = "100\n" + string.Concat(Enumerable.Repeat("# padding\n", 3000));

        Assert.False(BinPackingTextParser.TryParse(text, Limits, out _, out var error));
        Assert.Contains("too long", error);
    }
}
