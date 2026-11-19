using MitRecap.Core;
using Xunit;

namespace MitRecap.Tests;

public class ActionSummaryTests
{
    private static ActionSummary Summarize(params (byte Type, uint Value)[] effects)
    {
        var s = new ActionSummary();
        foreach (var (type, value) in effects)
        {
            s.AddEffect((EffectType)type, value);
        }

        return s;
    }

    [Fact]
    public void SelfBuffIsKept() => Assert.True(Summarize((14, 1191)).IsMitOrHeal);

    [Fact]
    public void PureHealIsKept() => Assert.True(Summarize((4, 20000)).IsMitOrHeal);

    [Fact]
    public void DebuffOnEnemyWithoutDamageIsKept() => Assert.True(Summarize((14, 1193)).IsMitOrHeal);

    [Fact]
    public void DamageIsDropped() => Assert.False(Summarize((3, 5000)).IsMitOrHeal);

    [Fact]
    public void DamageWithLifestealOrDotIsDropped()
    {
        Assert.False(Summarize((3, 5000), (4, 900)).IsMitOrHeal);
        Assert.False(Summarize((6, 5000), (14, 1871)).IsMitOrHeal);
        Assert.False(Summarize((1, 0), (15, 50)).IsMitOrHeal);
    }

    [Fact]
    public void ActionWithNoHealOrStatusIsDropped() => Assert.False(Summarize((24, 1), (27, 0)).IsMitOrHeal);

    [Fact]
    public void StatusIdsAreDeduplicatedAndHealAmountReturned()
    {
        var s = new ActionSummary();
        Assert.Equal(0u, s.AddEffect(EffectType.ApplyStatusTarget, 297));
        Assert.Equal(0u, s.AddEffect(EffectType.ApplyStatusTarget, 297));
        Assert.Equal(1234u, s.AddEffect(EffectType.Heal, 1234));
        Assert.Equal([297u], s.StatusIds);
    }
}
