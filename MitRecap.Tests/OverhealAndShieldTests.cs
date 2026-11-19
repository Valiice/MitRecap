using MitRecap.Core;
using Xunit;

namespace MitRecap.Tests;

public class OverhealAndShieldTests
{
    [Fact]
    public void OverhealIsWhatDoesNotFitUnderMaxHp()
    {
        var e = new RecapEvent(RecapKind.Action, TimeSpan.Zero, 0, false, 1, 1, 1300,
        [
            new TargetHit(1, "A", true, 900, 1000, 0, 500), // 400 over
            new TargetHit(2, "B", true, 200, 1000, 0, 500), // fits
            new TargetHit(3, "C", true, 1000, 1000, 0, 300), // all over
            new TargetHit(4, "D", true, 0, 0, 0, 100), // HP unknown: not counted
        ], []);
        Assert.Equal(700u, e.Overheal);
    }

    [Fact]
    public void AHitInsideTheShieldGoesToTheFirstShield()
    {
        Assert.Equal([300d, 0d], Mitigation.SplitAbsorbed(300, 1000, 2));
    }

    [Fact]
    public void AHitThroughEveryShieldIsSharedAndCappedAtTheShield()
    {
        Assert.Equal([500d, 500d], Mitigation.SplitAbsorbed(5000, 1000, 2));
        Assert.Empty(Mitigation.SplitAbsorbed(5000, 1000, 0));
        Assert.Equal([0d], Mitigation.SplitAbsorbed(5000, 0, 1));
    }
}
