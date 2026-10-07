using Optinull.Domain.Solutions;

namespace Optinull.Optimization.Genetics;

public sealed class Population
{
    private readonly List<Solution> _solutions = [];

    public IReadOnlyList<Solution> Solutions => _solutions;

    public int Count => _solutions.Count;

    public Population(IEnumerable<Solution> solutions)
    {
        ArgumentNullException.ThrowIfNull(solutions);

        _solutions.AddRange(solutions);

        if (_solutions.Count == 0)
        {
            throw new ArgumentException(
                "Population cannot be empty.",
                nameof(solutions));
        }
    }

    public void Add(Solution solution)
    {
        ArgumentNullException.ThrowIfNull(solution);

        _solutions.Add(solution);
    }
}
