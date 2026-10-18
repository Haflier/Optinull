using Optinull.Optimization.Randomness;
using Optinull.Optimization.Search;
using Optinull.Problems.Vrp;

namespace Optinull.Problems.Tests.Vrp;

public sealed class VrpDirectedCostTests
{
    private static double[,] Asymmetric(int customers, int seed)
    {
        var random = new Random(seed);
        var size = customers + 1;
        var costs = new double[size, size];

        for (var a = 0; a < size; a++)
        {
            for (var b = 0; b < size; b++)
                costs[a, b] = a == b ? 0 : random.Next(1, 51);
        }

        return costs;
    }

    private static VrpProblem Directed(int customers, int vehicles, int capacity, int seed)
    {
        var list = Enumerable
            .Range(0, customers)
            .Select(i => new VrpCustomer(i + 1, 0, i % 3 + 1))
            .ToList();

        return new VrpProblem(0, 0, list, vehicles, capacity, new VrpCostMatrix(Asymmetric(customers, seed)));
    }

    private static double Optimum(VrpProblem problem)
    {
        var best = double.PositiveInfinity;
        var order = Enumerable.Range(0, problem.CustomerCount).ToArray();

        void Permute(int k)
        {
            if (k == order.Length)
            {
                var plan = VrpSplitter.Split(problem, order);

                if (plan is not null)
                    best = Math.Min(best, VrpEvaluator.TotalDistance(problem, plan));

                return;
            }

            for (var i = k; i < order.Length; i++)
            {
                (order[k], order[i]) = (order[i], order[k]);
                Permute(k + 1);
                (order[k], order[i]) = (order[i], order[k]);
            }
        }

        Permute(0);

        return best;
    }

    [Fact]
    public void Matrix_RejectsBadInput()
    {
        Assert.Throws<ArgumentNullException>(() => new VrpCostMatrix(null!));
        Assert.Throws<ArgumentException>(() => new VrpCostMatrix(new double[1, 1]));
        Assert.Throws<ArgumentException>(() => new VrpCostMatrix(new double[2, 3]));

        var nan = Asymmetric(2, 1);
        nan[1, 2] = double.NaN;
        Assert.Throws<ArgumentException>(() => new VrpCostMatrix(nan));

        var negative = Asymmetric(2, 1);
        negative[0, 1] = -1;
        Assert.Throws<ArgumentException>(() => new VrpCostMatrix(negative));

        var unreachable = Asymmetric(2, 1);
        unreachable[2, 0] = double.PositiveInfinity;
        var error = Assert.Throws<ArgumentException>(() => new VrpCostMatrix(unreachable));
        Assert.Contains("unreachable", error.Message);
    }

    [Fact]
    public void Problem_RejectsAMatrixForTheWrongNumberOfCustomers()
    {
        var matrix = new VrpCostMatrix(Asymmetric(3, 1));

        Assert.Throws<ArgumentException>(() => new VrpProblem(0, 0, [new(1, 0, 1), new(2, 0, 1)], 1, 10, matrix));
    }

    [Fact]
    public void EuclideanMatrix_GivesTheSameCostsAsTheDefaultProblem()
    {
        var problem = VrpInstances.Random(8, 3, seed: 5);

        var same = new VrpProblem(
            problem.DepotX,
            problem.DepotY,
            problem.Customers,
            problem.Vehicles,
            problem.Capacity,
            VrpCostMatrix.Euclidean(problem.DepotX, problem.DepotY, problem.Customers));

        for (var a = 0; a < problem.CustomerCount; a++)
        {
            Assert.Equal(problem.FromDepot(a), same.FromDepot(a));
            Assert.Equal(problem.FromDepot(a), same.ToDepot(a));

            for (var b = 0; b < problem.CustomerCount; b++)
                Assert.Equal(problem.Distance(a, b), same.Distance(a, b));
        }
    }

    [Fact]
    public void RouteCosts_FollowTheDirectionOfTravel()
    {
        // Index 0 is the depot, then customers 0 and 1.
        var costs = new double[,] { { 0, 1, 10 }, { 2, 0, 3 }, { 20, 4, 0 } };
        var problem = new VrpProblem(0, 0, [new(1, 0, 1), new(2, 0, 1)], 1, 10, new VrpCostMatrix(costs));

        Assert.Equal(1, problem.FromDepot(0));
        Assert.Equal(2, problem.ToDepot(0));
        Assert.Equal(3, problem.Distance(0, 1));
        Assert.Equal(4, problem.Distance(1, 0));

        Assert.Equal(1 + 3 + 20, VrpEvaluator.RouteDistance(problem, [0, 1]));
        Assert.Equal(10 + 4 + 2, VrpEvaluator.RouteDistance(problem, [1, 0]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Split_FindsTheCheapestCuts_UnderAsymmetricCosts(int seed)
    {
        var problem = Directed(6, vehicles: 3, capacity: 6, seed);
        var random = new Random(seed);
        var order = Enumerable.Range(0, 6).OrderBy(_ => random.Next()).ToArray();

        var best = double.PositiveInfinity;

        for (var mask = 0; mask < 1 << 5; mask++)
        {
            var runs = new List<List<int>> { new() };

            for (var i = 0; i < 6; i++)
            {
                runs[^1].Add(order[i]);

                if (i < 5 && (mask & (1 << i)) != 0)
                    runs.Add([]);
            }

            if (runs.Count > problem.Vehicles ||
                runs.Any(run => VrpEvaluator.RouteLoad(problem, run) > problem.Capacity))
            {
                continue;
            }

            best = Math.Min(best, runs.Sum(run => VrpEvaluator.RouteDistance(problem, run)));
        }

        var plan = VrpSplitter.Split(problem, order);

        if (double.IsPositiveInfinity(best))
        {
            Assert.Null(plan);
            return;
        }

        Assert.NotNull(plan);
        Assert.Equal(best, VrpEvaluator.TotalDistance(problem, plan!), 9);
    }

    [Fact]
    public void EverySolver_IsMeasuredAgainstTheSameDirectedMatrix()
    {
        var problem = Directed(6, vehicles: 3, capacity: 6, seed: 11);
        var search = new VrpSearchProblem(problem);
        var optimum = Optimum(problem);

        ISearchSolver<VrpPlan>[] solvers =
        [
            new GreedyBaseline<VrpPlan>(),
            new HillClimbing<VrpPlan>(new RandomSource(1)),
            new SimulatedAnnealing<VrpPlan>(random: new RandomSource(1)),
            new IteratedLocalSearch<VrpPlan>(random: new RandomSource(1)),
            new GeneticAlgorithm<VrpPlan>(random: new RandomSource(1)),
        ];

        var bestSeen = double.PositiveInfinity;

        foreach (var solver in solvers)
        {
            var result = solver.Solve(search);

            Assert.True(result.Evaluation.IsFeasible);
            Assert.Equal(VrpEvaluator.TotalDistance(problem, result.Best), result.Evaluation.ObjectiveValue, 9);
            Assert.True(result.Evaluation.ObjectiveValue >= optimum - 1e-9, "a solver beat the true optimum");

            bestSeen = Math.Min(bestSeen, result.Evaluation.ObjectiveValue);
        }

        Assert.Equal(optimum, bestSeen, 9);
    }
}
