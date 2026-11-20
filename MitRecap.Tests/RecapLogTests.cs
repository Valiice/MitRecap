using MitRecap.Core;
using Xunit;

namespace MitRecap.Tests;

public class RecapLogTests
{
    [Fact]
    public void APullAlreadyGoingIsKept()
    {
        var start = new DateTime(2026, 1, 1, 20, 0, 0);
        var log = new RecapLog();
        log.StartSession(start);
        log.CombatStarted(start.AddSeconds(5));
        log.CombatStarted(start.AddSeconds(6));

        Assert.Single(log.Pulls);
        Assert.True(log.InCombat);
    }

    [Fact]
    public void HitsLandingTogetherEachStartFromWhatTheLastOneLeft()
    {
        var start = new DateTime(2026, 1, 1, 20, 0, 0);
        var log = new RecapLog();
        log.StartSession(start);
        log.AddDamage(start.AddSeconds(10.0), 1, "Boss", 30, 2000, 5000, 5000);
        log.AddDamage(start.AddSeconds(10.3), 1, "Boss", 30, 2000, 5000, 5000);
        log.AddDamage(start.AddSeconds(10.5), 1, "Boss", 30, 2000, 5000, 5000);
        log.AddDeath(start.AddSeconds(11), 1);

        Assert.Equal([5000u, 3000u, 1000u], Assert.Single(log.Events).LastHits.Select(h => h.HpBefore));
    }

    [Fact]
    public void AChangedHpReadingBetweenHitsIsTrusted()
    {
        var start = new DateTime(2026, 1, 1, 20, 0, 0);
        var log = new RecapLog();
        log.StartSession(start);
        log.AddDamage(start.AddSeconds(10.0), 1, "Boss", 30, 500, 1400, 2300);
        log.AddDamage(start.AddSeconds(11.0), 1, "Boss", 30, 1200, 1900, 2300);
        log.AddDeath(start.AddSeconds(12), 1);

        // A heal landed in between, so the second hit did not start from 900.
        Assert.Equal([1400u, 1900u], Assert.Single(log.Events).LastHits.Select(h => h.HpBefore));
    }

    [Fact]
    public void AnInstantDeathEffectIsTheKillingBlow()
    {
        Assert.Null(EnemyHit.Amount(EffectType.Damage, 0, 5000));
        Assert.Null(EnemyHit.Amount(EffectType.ApplyStatusTarget, 12, 5000));
        Assert.Equal(700u, EnemyHit.Amount(EffectType.BlockedDamage, 700, 5000));
        Assert.Equal(5000u, EnemyHit.Amount((EffectType)0x33, 0, 5000));

        var start = new DateTime(2026, 1, 1, 20, 0, 0);
        var log = new RecapLog();
        log.StartSession(start);
        log.AddDamage(start.AddSeconds(10), 1, "Phlegethon", 50, EnemyHit.Amount(EffectType.InstantDeath, 0, 5000)!.Value, 5000, 6000);
        log.AddDeath(start.AddSeconds(11), 1);

        var death = Assert.Single(log.Events);
        Assert.Equal(50u, death.KillingBlow?.ActionId);
        Assert.Equal("Died to", RecapText.DeathVerb(death));
    }

    [Fact]
    public void AHitOnSomeoneAlreadyAtZeroIsNotTheKillingBlow()
    {
        var start = new DateTime(2026, 1, 1, 20, 0, 0);
        var log = new RecapLog();
        log.StartSession(start);
        log.AddDamage(start.AddSeconds(10.0), 1, "Goobbue", 40, 1400, 528, 6000);
        log.AddDamage(start.AddSeconds(10.7), 1, "Goobbue", 7, 1158, 0, 6000);
        log.AddDeath(start.AddSeconds(11), 1);

        var death = Assert.Single(log.Events);
        Assert.Equal(40u, death.KillingBlow?.ActionId);
        Assert.Equal("Died to", RecapText.DeathVerb(death));
    }

    [Fact]
    public void LifeStealGoesToThePressThatGrantedIt()
    {
        var start = new DateTime(2026, 1, 1, 20, 0, 0);
        var log = new RecapLog();
        log.StartSession(start);
        log.AddAction(start.AddSeconds(5), 1, 40, [new TargetHit(1, "Tank", true, 900, 1000, 0, 0)], [735]);

        log.CreditHeal(start.AddSeconds(6), 1, 735, 0, 300, 100);
        log.CreditHeal(start.AddSeconds(7), 1, 735, 0, 200, 0);
        log.CreditHeal(start.AddSeconds(8), 2, 735, 0, 999, 0);

        var press = Assert.Single(log.Events);
        Assert.Equal(500u, press.TotalHealed);
        Assert.Equal(100u, press.Overheal);
    }

    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0);

    private static DateTime At(double seconds) => T0.AddSeconds(seconds);

    private static TargetHit Hit(uint healed = 0) => new(1, "A", true, 100, 1000, 0, healed);

    [Fact]
    public void NothingIsRecordedOutsideASession()
    {
        var log = new RecapLog();
        log.CombatStarted(At(1));
        log.Touch(1, "A", "WAR");
        log.AddAction(At(2), 1, 7531, [Hit()], []);
        Assert.Empty(log.Events);
        Assert.Empty(log.Pulls);
        Assert.Empty(log.Players);
    }

    [Fact]
    public void EventsAreAssignedToPullsAndTimedFromPullStart()
    {
        var log = new RecapLog();
        log.StartSession(T0);
        log.AddAction(At(5), 1, 10, [Hit()], []);
        log.CombatStarted(At(60));
        log.AddAction(At(72), 1, 11, [Hit()], []);
        log.CombatEnded(At(100));
        log.AddAction(At(110), 1, 12, [Hit()], []);
        log.CombatStarted(At(200));
        log.AddDeath(At(230), 1);

        Assert.Equal([0, 1, 1, 2], log.Events.Select(e => e.Pull));
        Assert.Equal([false, true, false, true], log.Events.Select(e => e.InCombat));
        Assert.Equal(TimeSpan.FromSeconds(5), log.PullTime(log.Events[0]));
        Assert.Equal(TimeSpan.FromSeconds(12), log.PullTime(log.Events[1]));
        Assert.Equal(TimeSpan.FromSeconds(30), log.PullTime(log.Events[3]));
        Assert.Equal(TimeSpan.FromSeconds(100), log.Pulls[0].End);
        Assert.Null(log.Pulls[1].End);
        Assert.Equal(RecapKind.Death, log.Events[3].Kind);
    }

    [Fact]
    public void PressesShortlyBeforeAPullMoveIntoItWithNegativeTime()
    {
        var log = new RecapLog();
        log.StartSession(T0);
        log.CombatStarted(At(10));
        log.AddAction(At(20), 1, 10, [Hit()], []);
        log.CombatEnded(At(30));
        log.AddAction(At(35), 1, 11, [Hit()], []);
        log.AddAction(At(95), 1, 12, [Hit()], []);
        log.CombatStarted(At(100));

        Assert.Equal([1, 1, 2], log.Events.Select(e => e.Pull));
        Assert.Equal(TimeSpan.FromSeconds(-5), log.PullTime(log.Events[2]));
        Assert.Equal([12u], log.Query(1, 2).Select(e => e.ActionId));
    }

    [Fact]
    public void MitigatedDamageIsCreditedToTheLatestMatchingPress()
    {
        var log = new RecapLog();
        log.StartSession(T0);
        log.AddAction(At(1), 1, 7531, [Hit()], [1191]);
        log.AddAction(At(2), 2, 7531, [Hit()], [1191]);
        log.AddAction(At(3), 1, 188, [], []);
        log.AddAction(At(100), 1, 7531, [Hit()], [1191]);

        log.Credit(At(101), 1, 1191, 0, 50);
        log.Credit(At(102), 1, 1191, 0, 25);
        log.Credit(At(103), 2, 1191, 0, 10);
        log.Credit(At(104), 1, 299, 188, 7);
        log.Credit(At(105), 3, 1191, 0, 999);
        log.Credit(At(400), 2, 1191, 0, 999);

        Assert.Equal([0d, 10d, 7d, 75d], log.Events.Select(e => e.Mitigated));
    }

    [Fact]
    public void DeathCarriesTheRecentHitsOfThatPlayerOnly()
    {
        var log = new RecapLog();
        log.StartSession(T0);
        log.AddDamage(At(1), 1, "Old", 1, 10, 900, 1000);
        for (uint i = 0; i < 6; i++)
        {
            log.AddDamage(At(20 + i), 1, "Mob", 100 + i, 50, 500, 1000);
        }

        log.AddDamage(At(25), 2, "Mob", 7, 50, 500, 1000);
        log.AddDeath(At(26), 1);
        log.AddDeath(At(60), 1);
        log.AddDeath(At(60), 2);

        // Capped at five, oldest first, and the killing blow is last.
        Assert.Equal([101u, 102u, 103u, 104u, 105u], log.Events[0].LastHits.Select(h => h.ActionId));
        Assert.Empty(log.Events[1].LastHits);
        Assert.Empty(log.Events[2].LastHits);
    }

    [Fact]
    public void QueryFiltersByPlayerAndPull()
    {
        var log = new RecapLog();
        log.StartSession(T0);
        log.CombatStarted(At(1));
        log.AddAction(At(2), 1, 10, [Hit()], []);
        log.AddAction(At(3), 2, 11, [Hit()], []);
        log.CombatEnded(At(40));
        log.CombatStarted(At(50));
        log.AddAction(At(51), 1, 12, [Hit()], []);

        Assert.Equal([10u, 12u], log.Query(1, null).Select(e => e.ActionId));
        Assert.Equal([10u, 11u], log.Query(0, 1).Select(e => e.ActionId));
        Assert.Equal([12u], log.Query(1, 2).Select(e => e.ActionId));
    }

    [Fact]
    public void HealedIsSummedOverTargets()
    {
        var log = new RecapLog();
        log.StartSession(T0);
        log.AddAction(At(1), 1, 10, [Hit(300), Hit(200)], []);
        Assert.Equal(500u, log.Events[0].Healed);
    }

    [Fact]
    public void LogSurvivesStopAndIsWipedByTheNextSession()
    {
        var log = new RecapLog();
        log.StartSession(T0);
        log.Touch(1, "A", "WAR");
        log.Touch(1, "A", "PLD");
        log.CombatStarted(At(1));
        log.AddAction(At(2), 1, 10, [Hit()], []);
        log.StopSession(At(3));

        Assert.Single(log.Events);
        Assert.Equal("PLD", log.Player(1)!.Job);
        Assert.Equal(TimeSpan.FromSeconds(3), log.Pulls[0].End);
        log.AddAction(At(4), 1, 11, [Hit()], []);
        Assert.Single(log.Events);

        log.StartSession(At(10));
        Assert.Empty(log.Events);
        Assert.Empty(log.Pulls);
        Assert.Empty(log.Players);
    }
}
