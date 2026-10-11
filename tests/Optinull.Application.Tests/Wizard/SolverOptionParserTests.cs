using Optinull.Application.Commands;
using Optinull.Application.Optimization;

namespace Optinull.Application.Tests.Wizard;

public sealed class SolverOptionParserTests
{
    [Fact]
    public void Optimize_ParsesToTheWizardCommand() =>
        Assert.IsType<OptimizeCommand>(BotCommandParser.Parse("/optimize"));

    [Theory]
    [InlineData("/solve ft06 greedy", SolverKind.Greedy)]
    [InlineData("/solve ft06 HC", SolverKind.HillClimbing)]
    [InlineData("/solve ft06 sa", SolverKind.SimulatedAnnealing)]
    [InlineData("/solve ft06 ga", SolverKind.GeneticAlgorithm)]
    [InlineData("/solve ft06", SolverKind.SimulatedAnnealing)]
    public void Solve_ParsesEverySolver(string text, SolverKind expected)
    {
        var command = Assert.IsType<SolveCommand>(BotCommandParser.Parse(text));

        Assert.Equal(expected, command.Solver);
        Assert.False(command.CompareAll);
    }

    [Theory]
    [InlineData("/solve ft06 all")]
    [InlineData("/solve ft06 compare")]
    [InlineData("/solve ft06 iterations=500 ALL")]
    [InlineData("/compare ft06")]
    [InlineData("/compare ft06 iterations=500 seed=3")]
    [InlineData("/compare@Optinull_bot ft06")]
    public void CompareForms_ParseAsCompareAll(string text)
    {
        var command = Assert.IsType<SolveCommand>(BotCommandParser.Parse(text));

        Assert.True(command.CompareAll);
        Assert.Equal("ft06", command.Target);
    }

    [Fact]
    public void Compare_KeepsIterationsSeedAndSettings()
    {
        var command = Assert.IsType<SolveCommand>(
            BotCommandParser.Parse("/compare tsp cities=30 iterations=500 seed=3"));

        Assert.Equal(500, command.Iterations);
        Assert.Equal(3, command.Seed);
        Assert.Equal("30", command.ParameterValues["cities"]);
    }

    [Theory]
    [InlineData("/compare")]
    [InlineData("/compare ft06 ga")]
    [InlineData("/solve ft06 all ga")]
    [InlineData("/solve ft06 ga all")]
    [InlineData("/solve ft06 hc sa")]
    public void ConflictingSolvers_AreInvalid(string text) =>
        Assert.IsType<InvalidCommand>(BotCommandParser.Parse(text));
}
