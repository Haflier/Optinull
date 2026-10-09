using Optinull.Domain.Evaluation;
using Optinull.Domain.Objectives;
using Optinull.Optimization.Randomness;
using Optinull.Optimization.Search;

namespace Optinull.Problems.Tsp;

/// <summary>
/// Adapts a TSP to the generic solvers. Moves are 2-opt (reverse a stretch of
/// the tour); the genetic algorithm recombines tours with order crossover.
/// </summary>
public sealed class TspSearchProblem : IRecombinableProblem<TspTour>
{
    private readonly TspProblem _problem;

    public TspSearchProblem(TspProblem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);

        _problem = problem;
    }

    public ObjectiveType ObjectiveType => ObjectiveType.Minimize;

    public TspTour CreateRandom(IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);

        var cities = Enumerable.Range(0, _problem.CityCount).ToArray();

        // Fisher-Yates shuffle.
        for (var i = cities.Length - 1; i > 0; i--)
        {
            var j = random.Next(0, i + 1);
            (cities[i], cities[j]) = (cities[j], cities[i]);
        }

        return new TspTour(cities);
    }

    public EvaluationResult Evaluate(TspTour solution) =>
        TspEvaluator.Evaluate(_problem, solution);

    public IEnumerable<TspTour> Neighbors(TspTour solution)
    {
        ArgumentNullException.ThrowIfNull(solution);

        var count = solution.Count;

        for (var i = 0; i < count - 1; i++)
        {
            for (var j = i + 1; j < count; j++)
            {
                // Reversing the whole tour gives the same cycle.
                if (i == 0 && j == count - 1)
                    continue;

                yield return Reverse(solution, i, j);
            }
        }
    }

    public TspTour RandomNeighbor(TspTour solution, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(solution);
        ArgumentNullException.ThrowIfNull(random);

        var count = solution.Count;

        while (true)
        {
            var a = random.Next(0, count);
            var b = random.Next(0, count - 1);

            if (b >= a)
                b++;

            var i = Math.Min(a, b);
            var j = Math.Max(a, b);

            if (i == 0 && j == count - 1)
                continue;

            return Reverse(solution, i, j);
        }
    }

    public TspTour Crossover(
        TspTour first,
        TspTour second,
        IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        ArgumentNullException.ThrowIfNull(random);

        if (first.Count != second.Count)
        {
            throw new ArgumentException("Parents must have the same length.");
        }

        // Order crossover: keep a stretch of the first parent, then fill the
        // remaining places with the other cities in the second parent's order.
        var count = first.Count;

        var a = random.Next(0, count);
        var b = random.Next(0, count);

        if (a > b)
            (a, b) = (b, a);

        var child = new int[count];
        var used = new bool[count];

        for (var i = a; i <= b; i++)
        {
            child[i] = first.Cities[i];
            used[first.Cities[i]] = true;
        }

        var position = (b + 1) % count;

        for (var k = 0; k < count; k++)
        {
            var city = second.Cities[(b + 1 + k) % count];

            if (used[city])
                continue;

            child[position] = city;
            used[city] = true;
            position = (position + 1) % count;
        }

        return new TspTour(child);
    }

    public TspTour Mutate(TspTour solution, IRandomSource random) =>
        RandomNeighbor(solution, random);

    private static TspTour Reverse(TspTour tour, int from, int to)
    {
        var cities = tour.Cities.ToArray();

        Array.Reverse(cities, from, to - from + 1);

        return new TspTour(cities);
    }
}
