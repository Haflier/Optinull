using Optinull.Problems.JobShop;

namespace Optinull.Problems.Tests.JobShop;

public sealed class JobSequenceTests
{
    [Fact]
    public void Constructor_StoresJobIds()
    {
        var sequence =
            new JobSequence([0, 1, 0, 1]);

        Assert.Equal(
            [0, 1, 0, 1],
            sequence.JobIds);

        Assert.Equal(
            4,
            sequence.Count);
    }

    [Fact]
    public void Constructor_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => new JobSequence(null!));
    }

    [Fact]
    public void Constructor_RejectsEmptySequence()
    {
        Assert.Throws<ArgumentException>(
            () => new JobSequence(Array.Empty<int>()));
    }

    [Fact]
    public void Constructor_RejectsNegativeJobId()
    {
        Assert.Throws<ArgumentException>(
            () => new JobSequence([0, -1, 0]));
    }
}
