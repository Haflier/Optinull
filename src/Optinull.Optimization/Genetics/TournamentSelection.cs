using Optinull.Domain.Evaluation;
using Optinull.Domain.Solutions;
using Optinull.Optimization.Comparison;
using Optinull.Optimization.Randomness;

namespace Optinull.Optimization.Genetics;

public sealed class TournamentSelection : ISelectionStrategy
{
    private readonly ISolutionComparer _comparer;
    private readonly IRandomSource _random;
    private readonly int _tournamentSize;

    public TournamentSelection(
        ISolutionComparer comparer,
        IRandomSource random,
        int tournamentSize = 3)
    {
        ArgumentNullException.ThrowIfNull(comparer);
        ArgumentNullException.ThrowIfNull(random);

        if (tournamentSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tournamentSize),
                "Tournament size must be greater than zero.");
        }

        _comparer = comparer;
        _random = random;
        _tournamentSize = tournamentSize;
    }

    public Solution Select(
        Population population,
        IReadOnlyDictionary<Solution, EvaluationResult> evaluations)
    {
        ArgumentNullException.ThrowIfNull(population);
        ArgumentNullException.ThrowIfNull(evaluations);

        if (population.Count == 0)
        {
            throw new ArgumentException(
                "Population cannot be empty.",
                nameof(population));
        }

        Solution? winner = null;
        EvaluationResult? winnerEvaluation = null;

        for (var i = 0; i < _tournamentSize; i++)
        {
            var candidate = population.Solutions[
                _random.Next(0, population.Count)];

            var candidateEvaluation = evaluations[candidate];

            if (winner is null ||
                _comparer.IsBetter(
                    candidateEvaluation,
                    winnerEvaluation!))
            {
                winner = candidate;
                winnerEvaluation = candidateEvaluation;
            }
        }

        return winner;
    }
}
