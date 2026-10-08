using Optinull.Optimization.Randomness;

namespace Optinull.Optimization.Search;

/// <summary>A search problem that also knows how to combine and mutate solutions.</summary>
public interface IRecombinableProblem<TSolution> : ISearchProblem<TSolution>
{
    TSolution Crossover(
        TSolution first,
        TSolution second,
        IRandomSource random);

    TSolution Mutate(
        TSolution solution,
        IRandomSource random);
}
