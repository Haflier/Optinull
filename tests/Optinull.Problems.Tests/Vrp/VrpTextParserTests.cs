using Optinull.Problems.Vrp;

namespace Optinull.Problems.Tests.Vrp;

public sealed class VrpTextParserTests
{
    private static VrpInputLimits Limits => VrpInputLimits.Default;

    private const string Valid = "3 30\n50 50\n20 30 7\n80 65 12\n60 20 9";

    [Fact]
    public void Parses_VehiclesCapacityDepotAndCustomers()
    {
        Assert.True(VrpTextParser.TryParse(Valid, Limits, out var problem, out var error));
        Assert.Null(error);

        Assert.Equal(3, problem!.Vehicles);
        Assert.Equal(30, problem.Capacity);
        Assert.Equal(50, problem.DepotX);
        Assert.Equal(3, problem.CustomerCount);
        Assert.Equal(new VrpCustomer(80, 65, 12), problem.Customers[1]);
    }

    [Fact]
    public void Ignores_Comments_BlankLines_WindowsLineEndings_AndSeparators()
    {
        var text = "# fleet\r\n3, 30\r\n\r\n50;50\r\n20 30 7\r\n# last\r\n80,65,12\r\n";

        Assert.True(VrpTextParser.TryParse(text, Limits, out var problem, out _));
        Assert.Equal(2, problem!.CustomerCount);
    }

    [Fact]
    public void Coordinates_MayBeDecimalOrNegative()
    {
        Assert.True(VrpTextParser.TryParse("2 10\n-1.5 2.25\n3.75 -4 5", Limits, out var problem, out _));

        Assert.Equal(-1.5, problem!.DepotX);
        Assert.Equal(-4, problem.Customers[0].Y);
    }

    [Theory]
    [InlineData("", "empty")]
    [InlineData("# nothing", "empty")]
    [InlineData("3", "Line 1")]
    [InlineData("x 30\n0 0\n1 1 1", "Line 1")]
    [InlineData("0 30\n0 0\n1 1 1", "Line 1")]
    [InlineData("21 30\n0 0\n1 1 1", "Line 1")]
    [InlineData("3 0\n0 0\n1 1 1", "Line 1")]
    [InlineData("3 30 5\n0 0\n1 1 1", "Line 1")]
    [InlineData("3 30", "Add the depot")]
    [InlineData("3 30\n0", "Line 2")]
    [InlineData("3 30\na b\n1 1 1", "Line 2")]
    [InlineData("3 30\n0 0", "Add at least one customer")]
    [InlineData("3 30\n0 0\n1 1", "Line 3")]
    [InlineData("3 30\n0 0\n1 1 1 1", "Line 3")]
    [InlineData("3 30\n0 0\nx 1 5", "Line 3")]
    [InlineData("3 30\n0 0\n1 1 0", "Line 3")]
    [InlineData("3 30\n0 0\n1 1 31", "Line 3")]
    [InlineData("3 30\n0 0\n1 1 2.5", "Line 3")]
    [InlineData("3 30\n0 0\n1 1 -4", "Line 3")]
    [InlineData("3 30\n0 0\n2000000 1 5", "Line 3")]
    public void Rejects_BadInput_WithTheReason(string text, string reason)
    {
        Assert.False(VrpTextParser.TryParse(text, Limits, out var problem, out var error));
        Assert.Null(problem);
        Assert.Contains(reason, error);
    }

    [Fact]
    public void Rejects_AFleetTooSmallForTheLoads_NamingTheNumberNeeded()
    {
        // Two loads of 8 cannot share a vehicle of 10.
        Assert.False(VrpTextParser.TryParse("1 10\n0 0\n1 1 8\n2 2 8", Limits, out _, out var error));

        Assert.Contains("at least 2 vehicles", error);
        Assert.Contains("gives 1", error);
    }

    [Fact]
    public void Rejects_TooManyCustomers()
    {
        var customers = string.Join("\n", Enumerable.Repeat("1 1 1", Limits.MaxCustomers + 1));

        Assert.False(VrpTextParser.TryParse("5 1000\n0 0\n" + customers, Limits, out _, out var error));
        Assert.Contains("Too many customers", error);
    }

    [Fact]
    public void Rejects_TooLongAnInstance()
    {
        var text = "3 30\n0 0\n" + new string('#', Limits.MaxCharacters);

        Assert.False(VrpTextParser.TryParse(text, Limits, out _, out var error));
        Assert.Contains("too long", error);
    }
}
