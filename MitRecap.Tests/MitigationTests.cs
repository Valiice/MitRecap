using MitRecap.Core;
using Xunit;

namespace MitRecap.Tests;

public class MitigationTests
{
    [Fact]
    public void SingleMitigationGetsEverythingItSaved()
    {
        // 800 taken through 20% means 1000 was coming.
        var saved = Mitigation.Split(800, [0.2]);
        Assert.Equal(200, saved[0], 6);
    }

    [Fact]
    public void StackedMitigationsMultiplyAndTheSplitAddsUp()
    {
        // 20% and 10% leave 72%: 720 taken means 1000 was coming, so 280 was saved in total.
        var saved = Mitigation.Split(720, [0.2, 0.1]);
        Assert.Equal(280, saved.Sum(), 6);
        Assert.True(saved[0] > saved[1] * 2);
    }

    [Fact]
    public void EqualMitigationsShareEqually()
    {
        var saved = Mitigation.Split(722.5, [0.15, 0.15]);
        Assert.Equal(saved[0], saved[1], 6);
        Assert.Equal(1000 - 722.5, saved.Sum(), 6);
    }

    [Fact]
    public void NothingIsSavedWithoutMitigationOrDamage()
    {
        Assert.Equal([0d, 0d], Mitigation.Split(500, [0, 0]));
        Assert.Equal([0d], Mitigation.Split(0, [0.2]));
        Assert.Empty(Mitigation.Split(500, []));
    }
}
