using Optinull.Application.Comparison;
using Optinull.Application.Optimization;
using Optinull.Application.Problems;
using Optinull.Reporting.Charts;

namespace Optinull.Application.Tests.Wizard;

public sealed class ComparisonTieTests
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47];

    private static SolverOutcome Outcome(
        SolverKind kind,
        string name,
        double score,
        double milliseconds = 0) =>
        new(
            kind,
            name,
            score,
            TimeSpan.FromMilliseconds(milliseconds),
            1,
            new ConvergenceSeries(name, [new ConvergencePointData(1, score)], 1),
            () => []);

    [Fact]
    public void ScoresThatDifferOnlyByRounding_AreATie_AndTheEarlierSolverWins()
    {
        SolverOutcome[] outcomes =
        [
            Outcome(SolverKind.Greedy, "Greedy", 10.557884025800001),
            Outcome(SolverKind.HillClimbing, "Hill Climbing", 10.5578840258),
            Outcome(SolverKind.SimulatedAnnealing, "Simulated Annealing", 10.557884025799998)
        ];

        Assert.Equal("Greedy", SolverComparisonService.PickBest(outcomes, higherIsBetter: false).SolverName);
        Assert.Equal("Greedy", SolverComparisonService.PickBest(outcomes, higherIsBetter: true).SolverName);
    }

    [Fact]
    public void ARealDifference_StillWins()
    {
        SolverOutcome[] outcomes =
        [
            Outcome(SolverKind.Greedy, "Greedy", 10.56),
            Outcome(SolverKind.HillClimbing, "Hill Climbing", 10.55)
        ];

        Assert.Equal(
            "Hill Climbing",
            SolverComparisonService.PickBest(outcomes, higherIsBetter: false).SolverName);
    }

    [Fact]
    public void ThreeCities_EverySolverTies_SoGreedyIsBest_AndBothChartsRender()
    {
        var module = ProblemRegistry.CreateDefault().FindModule("tsp")!;

        Assert.True(module.TryParseCustom("1 4\n3 2\n0 0", out var instance, out _));

        var report = new SolverComparisonService().Compare(
            instance!,
            new SolveOptions(Seed: 42, Iterations: 300));

        var table = report.Images[0].Caption;

        // Greedy and the others tie on score; whether one is also faster depends on the
        // clock, so only the message about the tie is certain.
        Assert.Contains("Best:", table);
        Assert.Contains("Improvement over greedy: 0.0%", table);
        Assert.Contains("same result", table);

        Assert.All(report.Images, image => Assert.Equal(PngSignature, image.Png.Take(4).ToArray()));
    }

    [Fact]
    public void ARealProblem_DoesNotGetTheSameResultNote()
    {
        Assert.True(ProblemRegistry.CreateDefault().TryGetBuiltIn("knap100", out var instance));

        var report = new SolverComparisonService().Compare(
            instance!,
            new SolveOptions(Seed: 1, Iterations: 300));

        Assert.DoesNotContain("same result", report.Images[0].Caption);
    }

    private static readonly FakeProblemInstance Fake = new();

    [Fact]
    public void WhenScoresTie_TheFasterSolverWins()
    {
        SolverOutcome[] outcomes =
        [
            Outcome(SolverKind.Greedy, "Greedy", 5, milliseconds: 40),
            Outcome(SolverKind.HillClimbing, "Hill Climbing", 5, milliseconds: 10),
            Outcome(SolverKind.SimulatedAnnealing, "Simulated Annealing", 7, milliseconds: 1)
        ];

        var winners = SolverComparisonService.PickWinners(outcomes, higherIsBetter: false);

        Assert.Equal("Hill Climbing", Assert.Single(winners).SolverName);

        var table = SolverComparisonService.Table(Fake, outcomes, winners);

        Assert.Contains("Best: Hill Climbing", table);
        Assert.DoesNotContain("(tie)", table);
        Assert.DoesNotContain("no clear winner", table);
    }

    [Fact]
    public void WhenTheScoreIsBetter_ItBeatsAFasterWorseSolver()
    {
        SolverOutcome[] outcomes =
        [
            Outcome(SolverKind.Greedy, "Greedy", 9, milliseconds: 0),
            Outcome(SolverKind.GeneticAlgorithm, "Genetic Algorithm", 5, milliseconds: 500)
        ];

        Assert.Equal(
            "Genetic Algorithm",
            SolverComparisonService.PickBest(outcomes, higherIsBetter: false).SolverName);
    }

    [Fact]
    public void SameScoreAndSameMilliseconds_IsATie_AndTheTableSaysSo()
    {
        SolverOutcome[] outcomes =
        [
            Outcome(SolverKind.Greedy, "Greedy", 5, milliseconds: 0.2),
            Outcome(SolverKind.HillClimbing, "Hill Climbing", 5, milliseconds: 0.4),
            Outcome(SolverKind.SimulatedAnnealing, "Simulated Annealing", 5, milliseconds: 51),
            Outcome(SolverKind.GeneticAlgorithm, "Genetic Algorithm", 7, milliseconds: 60)
        ];

        var winners = SolverComparisonService.PickWinners(outcomes, higherIsBetter: false);

        // 0.2 ms and 0.4 ms both show as 0 ms.
        Assert.Equal(["Greedy", "Hill Climbing"], winners.Select(winner => winner.SolverName).ToArray());

        var table = SolverComparisonService.Table(Fake, outcomes, winners);

        Assert.Contains("Best: Greedy, Hill Climbing (tie)", table);
        Assert.Contains("found the same best result in the same time, so there is no clear winner", table);
    }

    [Fact]
    public void EverySolverTyingOnScoreAndTime_SaysTheProblemIsTooEasy()
    {
        SolverOutcome[] outcomes =
        [
            Outcome(SolverKind.Greedy, "Greedy", 5),
            Outcome(SolverKind.HillClimbing, "Hill Climbing", 5)
        ];

        var table = SolverComparisonService.Table(
            Fake,
            outcomes,
            SolverComparisonService.PickWinners(outcomes, higherIsBetter: false));

        Assert.Contains("(tie)", table);
        Assert.Contains("Every solver found the same result in the same time", table);
    }

    [Fact]
    public void EverySolverTyingOnScoreButNotTime_NamesTheFastestAndSaysTheProblemIsTooEasy()
    {
        SolverOutcome[] outcomes =
        [
            Outcome(SolverKind.Greedy, "Greedy", 5, milliseconds: 0),
            Outcome(SolverKind.SimulatedAnnealing, "Simulated Annealing", 5, milliseconds: 51)
        ];

        var table = SolverComparisonService.Table(
            Fake,
            outcomes,
            SolverComparisonService.PickWinners(outcomes, higherIsBetter: false));

        Assert.Contains("Best: Greedy", table);
        Assert.DoesNotContain("(tie)", table);
        Assert.Contains("Every solver found the same result, so this problem is too small", table);
    }

    [Fact]
    public void ForAMaximisingProblem_TheHighestScoreWins_AndTiesGoToTheFaster()
    {
        SolverOutcome[] outcomes =
        [
            Outcome(SolverKind.Greedy, "Greedy", 90, milliseconds: 1),
            Outcome(SolverKind.SimulatedAnnealing, "Simulated Annealing", 100, milliseconds: 30),
            Outcome(SolverKind.GeneticAlgorithm, "Genetic Algorithm", 100, milliseconds: 20)
        ];

        Assert.Equal(
            "Genetic Algorithm",
            SolverComparisonService.PickBest(outcomes, higherIsBetter: true).SolverName);
    }
}
