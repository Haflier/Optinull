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
    [InlineData("/solve ft06", "ft06", SolverKind.SimulatedAnnealing)]
    [InlineData("/solve FT10 ga", "ft10", SolverKind.GeneticAlgorithm)]
    [InlineData("/solve ft06 SA", "ft06", SolverKind.SimulatedAnnealing)]
    [InlineData("/solve@OptinullBot circle20 ga", "circle20", SolverKind.GeneticAlgorithm)]
    [InlineData("  /solve   ft06   ga  ", "ft06", SolverKind.GeneticAlgorithm)]
    [InlineData("/solve tsp ga\n0 0\n1 1\n2 0", "tsp", SolverKind.GeneticAlgorithm)]
    [InlineData("/solve jobshop\r\n2 2\r\n0 3 1 2\r\n1 4 0 2", "jobshop", SolverKind.SimulatedAnnealing)]
    public void Solve_ParsesTargetAndSolver(string text, string target, SolverKind solver)
    {
        var command = Assert.IsType<SolveCommand>(BotCommandParser.Parse(text));

        Assert.Equal(target, command.Target);
        Assert.Equal(solver, command.Solver);
    }

    [Fact]
    public void Solve_KeepsTheLinesAfterTheCommandAsInstanceText()
    {
        var command = Assert.IsType<SolveCommand>(
            BotCommandParser.Parse("/solve tsp\n0 0\n10 0\n10 10"));

        Assert.Contains("10 10", command.InstanceText);
    }

    [Theory]
    [InlineData("/solve tsp")]
    [InlineData("/solve tsp ga\n   \n")]
    public void Solve_WithoutLines_HasNoInstanceText(string text)
    {
        var command = Assert.IsType<SolveCommand>(BotCommandParser.Parse(text));

        Assert.Null(command.InstanceText);
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

    [Theory]
    [InlineData("/cancel")]
    [InlineData("/CANCEL")]
    [InlineData("/cancel@OptinullBot")]
    public void Cancel_ParsesAsCancel(string text) =>
        Assert.IsType<CancelCommand>(BotCommandParser.Parse(text));
}
