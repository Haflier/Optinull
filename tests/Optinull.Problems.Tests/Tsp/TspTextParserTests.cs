using Optinull.Problems.Tsp;

namespace Optinull.Problems.Tests.Tsp;

public sealed class TspTextParserTests
{
    private static TspInputLimits Limits => TspInputLimits.Default;

    [Fact]
    public void Parses_XY_Lines()
    {
        Assert.True(TspTextParser.TryParse("0 0\n10 0\n10.5 -3", Limits, out var problem, out var error));
        Assert.Null(error);

        Assert.Equal(3, problem!.CityCount);
        Assert.Equal(new City(10.5, -3), problem.Cities[2]);
    }

    [Fact]
    public void Parses_IdXY_Lines_IgnoringTheId()
    {
        Assert.True(TspTextParser.TryParse("7 0 0\n8 10 0\n9 10 10", Limits, out var problem, out _));
        Assert.Equal(new City(10, 10), problem!.Cities[2]);
    }

    [Fact]
    public void Ignores_Comments_BlankLines_AndWindowsLineEndings()
    {
        var text = "# cities\r\n\r\n0 0\r\n10 0\r\n\r\n# last\r\n10 10\r\n";

        Assert.True(TspTextParser.TryParse(text, Limits, out var problem, out _));
        Assert.Equal(3, problem!.CityCount);
    }

    [Theory]
    [InlineData("", "empty")]
    [InlineData("# nothing", "empty")]
    [InlineData("0 0\n1 1", "at least 3 cities")]
    [InlineData("0 0\n1 1\n5", "Line 3")]
    [InlineData("0 0\n1 1\n2 2 2 2", "Line 3")]
    [InlineData("0 0\n1 1\n7 2 2", "same number")]
    [InlineData("0 0\n1 1\na b", "Line 3")]
    [InlineData("0 0\n1 1\nNaN 2", "Line 3")]
    [InlineData("0 0\n1 1\n2000000 2", "Line 3")]
    public void InvalidInput_IsRejectedWithAUsefulMessage(string text, string expected)
    {
        Assert.False(TspTextParser.TryParse(text, Limits, out var problem, out var error));
        Assert.Null(problem);
        Assert.Contains(expected, error);
    }

    [Fact]
    public void TooManyCities_IsRejected()
    {
        var limits = new TspInputLimits(MaxCities: 3);

        Assert.False(TspTextParser.TryParse("0 0\n1 1\n2 2\n3 3", limits, out _, out var error));
        Assert.Contains("Too many cities", error);
    }

    [Fact]
    public void TooLongText_IsRejected()
    {
        var limits = new TspInputLimits(MaxCharacters: 5);

        Assert.False(TspTextParser.TryParse("0 0\n1 1\n2 2", limits, out _, out var error));
        Assert.Contains("too long", error);
    }
}
