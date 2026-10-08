using Optinull.Domain.Evaluation;
using Optinull.Domain.Objectives;
using Optinull.Optimization.Comparison;

namespace Optinull.Optimization.Tests;

public class SolutionComparerTests
{
    [Fact]
    public void Maximize_LargerValue_IsBetter()
    {
        var comparer = new SolutionComparer(
            ObjectiveType.Maximize);

        var current = new EvaluationResult(
            objectiveValue: 5,
            isFeasible: true,
            penalty: 0);

        var candidate = new EvaluationResult(
            objectiveValue: 8,
            isFeasible: true,
            penalty: 0);

        Assert.True(
            comparer.IsBetter(candidate, current));
    }

    [Fact]
    public void Minimize_SmallerValue_IsBetter()
    {
        var comparer = new SolutionComparer(
            ObjectiveType.Minimize);

        var current = new EvaluationResult(
            objectiveValue: 8,
            isFeasible: true,
            penalty: 0);

        var candidate = new EvaluationResult(
            objectiveValue: 5,
            isFeasible: true,
            penalty: 0);

        Assert.True(
            comparer.IsBetter(candidate, current));
    }

    [Fact]
    public void InfeasibleCandidate_IsNeverBetter()
    {
        var comparer = new SolutionComparer(
            ObjectiveType.Maximize);

        var current = new EvaluationResult(
            objectiveValue: 5,
            isFeasible: true,
            penalty: 0);

        var candidate = new EvaluationResult(
            objectiveValue: 100,
            isFeasible: false,
            penalty: 10);

        Assert.False(
            comparer.IsBetter(candidate, current));
    }

    [Fact]
    public void FeasibleCandidate_IsBetterThanInfeasibleCurrent()
    {
        var comparer = new SolutionComparer(
            ObjectiveType.Maximize);

        var current = new EvaluationResult(
            objectiveValue: 100,
            isFeasible: false,
            penalty: 10);

        var candidate = new EvaluationResult(
            objectiveValue: 5,
            isFeasible: true,
            penalty: 0);

        Assert.True(
            comparer.IsBetter(candidate, current));
    }

    [Fact]
    public void Maximize_Penalty_ReducesValue()
    {
        var comparer = new SolutionComparer(ObjectiveType.Maximize);

        var penalized = new EvaluationResult(10, isFeasible: true, penalty: 5);
        var clean = new EvaluationResult(8, isFeasible: true, penalty: 0);

        Assert.False(comparer.IsBetter(penalized, clean));
        Assert.True(comparer.IsBetter(clean, penalized));
    }

    [Fact]
    public void Minimize_Penalty_IncreasesValue()
    {
        var comparer = new SolutionComparer(ObjectiveType.Minimize);

        var penalized = new EvaluationResult(5, isFeasible: true, penalty: 10);
        var clean = new EvaluationResult(8, isFeasible: true, penalty: 0);

        Assert.False(comparer.IsBetter(penalized, clean));
        Assert.True(comparer.IsBetter(clean, penalized));
    }

    [Fact]
    public void EqualPenalizedValues_AreNotBetterThanEachOther()
    {
        var comparer = new SolutionComparer(ObjectiveType.Maximize);

        var a = new EvaluationResult(10, isFeasible: true, penalty: 2);
        var b = new EvaluationResult(8, isFeasible: true, penalty: 0);

        Assert.False(comparer.IsBetter(a, b));
        Assert.False(comparer.IsBetter(b, a));
    }
}
