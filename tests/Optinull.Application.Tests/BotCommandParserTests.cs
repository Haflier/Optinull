using Optinull.Application.Commands;
using Optinull.Application.Optimization;

namespace Optinull.Application.Tests;

public sealed class BotCommandParserTests
{
    [Theory]
    [InlineData("/start")]
    [InlineData("/help")]
    [InlineData("/HELP")]
    [InlineData("/help@OptinullBot")]
    public void HelpAndStart_ParseAsHelp(string text) =>
        Assert.IsType<HelpCommand>(BotCommandParser.Parse(text));

    [Theory]
    [InlineData("/solve ft06", "ft06", JobShopSolverKind.SimulatedAnnealing)]
    [InlineData("/solve FT10 ga", "ft10", JobShopSolverKind.GeneticAlgorithm)]
    [InlineData("/solve ft06 SA", "ft06", JobShopSolverKind.SimulatedAnnealing)]
    [InlineData("/solve@OptinullBot ft06 ga", "ft06", JobShopSolverKind.GeneticAlgorithm)]
    [InlineData("  /solve   ft06   ga  ", "ft06", JobShopSolverKind.GeneticAlgorithm)]
    public void Solve_ParsesBenchmarkAndSolver(
        string text,
        string benchmark,
        JobShopSolverKind solver)
    {
        var command = Assert.IsType<SolveCommand>(BotCommandParser.Parse(text));

        Assert.Equal(benchmark, command.Benchmark);
        Assert.Equal(solver, command.Solver);
    }

    [Theory]
    [InlineData("/solve")]
    [InlineData("/solve ft06 sa extra")]
    [InlineData("/solve ft06 tabu")]
    [InlineData("/unknown")]
    [InlineData("hello")]
    [InlineData("")]
    [InlineData(null)]
    public void BadInput_ParsesAsInvalid(string? text) =>
        Assert.IsType<InvalidCommand>(BotCommandParser.Parse(text));
}
