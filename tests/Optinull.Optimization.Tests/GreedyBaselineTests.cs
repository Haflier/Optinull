using Optinull.Domain.Problems;
using Optinull.Optimization.Search;

namespace Optinull.Optimization.Tests;

public sealed class GreedyBaselineTests
{
    private static OptimizationSearchProblem Create(bool maximize)
    {
        var problem = new OptimizationProblem("sum");
        var x = problem.AddIntegerVariable("x", 0, 10);
        var y = problem.AddIntegerVariable("y", 0, 10);

        if (maximize)
            problem.Maximize(x + y);
        else
            problem.Minimize(x + y);

        return new OptimizationSearchProblem(problem);
    }

    [Fact]
    public void Maximize_ChoosesUpperBounds()
    {
        var result = new GreedyBaseline<Optinull.Domain.Solutions.Solution>()
            .Solve(Create(maximize: true));

        Assert.Equal(20, result.Evaluation.ObjectiveValue);
    }

    [Fact]
    public void Minimize_KeepsLowerBounds()
    {
        var result = new GreedyBaseline<Optinull.Domain.Solutions.Solution>()
            .Solve(Create(maximize: false));

        Assert.Equal(0, result.Evaluation.ObjectiveValue);
    }

    [Fact]
    public void Solve_CountsEvaluations()
    {
        var result = new GreedyBaseline<Optinull.Domain.Solutions.Solution>()
            .Solve(Create(maximize: true));

        Assert.True(result.EvaluationCount > 0);
        Assert.NotEmpty(result.Convergence);
    }

    [Fact]
    public void Solve_WithoutConstructiveProblem_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new GreedyBaseline<int>().Solve(new NotConstructive()));
    }

    private sealed class NotConstructive : ISearchProblem<int>
    {
        public Optinull.Domain.Objectives.ObjectiveType ObjectiveType =>
            Optinull.Domain.Objectives.ObjectiveType.Minimize;

        public int CreateRandom(Optinull.Optimization.Randomness.IRandomSource random) => 0;

        public Optinull.Domain.Evaluation.EvaluationResult Evaluate(int solution) =>
            new(solution, true, 0);

        public int RandomNeighbor(int solution, Optinull.Optimization.Randomness.IRandomSource random) => solution;

        public IEnumerable<int> Neighbors(int solution) => [];
    }
}
