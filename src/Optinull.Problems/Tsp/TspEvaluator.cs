using Optinull.Domain.Evaluation;

namespace Optinull.Problems.Tsp;

public static class TspEvaluator
{
    public static EvaluationResult Evaluate(TspProblem problem, TspTour tour)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(tour);

        var count = problem.CityCount;

        if (tour.Count != count)
        {
            throw new ArgumentException(
                $"The tour visits {tour.Count} cities but the problem has {count}.",
                nameof(tour));
        }

        var seen = new bool[count];

        foreach (var city in tour.Cities)
        {
            if (city < 0 || city >= count || seen[city])
            {
                throw new ArgumentException(
                    "A tour must visit every city exactly once.",
                    nameof(tour));
            }

            seen[city] = true;
        }

        var length = 0.0;

        for (var i = 0; i < count; i++)
        {
            length += problem.Distance(
                tour.Cities[i],
                tour.Cities[(i + 1) % count]);
        }

        return new EvaluationResult(
            objectiveValue: length,
            isFeasible: true,
            penalty: 0.0);
    }
}
