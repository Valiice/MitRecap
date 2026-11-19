using MitRecap.Core;
using Xunit;

namespace MitRecap.Tests;

public class CooldownsTests
{
    private static readonly DateTime Start = new(2026, 1, 1, 20, 0, 0);

    private static DateTime At(double seconds) => Start.AddSeconds(seconds);

    [Fact]
    public void UnusedListsWhatWasReadyAndNotPressed()
    {
        var a = new Cooldown(10, new HashSet<uint> { 10 }, TimeSpan.FromSeconds(120), 1);
        var b = new Cooldown(20, new HashSet<uint> { 20, 21 }, TimeSpan.FromSeconds(60), 1);
        var c = new Cooldown(30, new HashSet<uint> { 30 }, TimeSpan.FromSeconds(60), 2);
        Cooldown[] all = [a, b, c];

        var log = new RecapLog();
        log.StartSession(Start);
        log.CombatStarted(At(10));
        log.AddAction(At(15), 1, 10, [], []);
        log.AddAction(At(20), 1, 21, [], []);
        log.AddAction(At(20), 1, 30, [], []);
        log.CombatEnded(At(40));
        log.CombatStarted(At(100));
        log.CombatEnded(At(130));
        log.CombatStarted(At(300));
        log.AddAction(At(310), 1, 20, [], []);
        log.CombatEnded(At(330));
        log.CombatStarted(At(400));

        Assert.Empty(Cooldowns.Unused(log, 1, log.Pulls[0], all, default));
        Assert.Equal(all, Cooldowns.Unused(log, 2, log.Pulls[0], all, default));

        // A was still recharging for all of pull 2; B had come back; C had its second charge left.
        Assert.Equal([b, c], Cooldowns.Unused(log, 1, log.Pulls[1], all, default));
        Assert.Equal([a, c], Cooldowns.Unused(log, 1, log.Pulls[2], all, default));

        // A pull still in progress is judged up to now, once it has run long enough to judge at all.
        Assert.Empty(Cooldowns.Unused(log, 1, log.Pulls[3], all, TimeSpan.FromSeconds(405)));
        Assert.Equal(all, Cooldowns.Unused(log, 1, log.Pulls[3], all, TimeSpan.FromSeconds(415)));

        Assert.Empty(Cooldowns.NeverUsed(log, 1, all));
        Assert.Equal(all, Cooldowns.NeverUsed(log, 2, all));
    }

    [Fact]
    public void WipeResetsCooldowns()
    {
        Cooldown[] all = [new Cooldown(10, new HashSet<uint> { 10 }, TimeSpan.FromSeconds(120), 1)];

        var log = new RecapLog();
        log.StartSession(Start);
        log.CombatStarted(At(10));
        log.AddAction(At(15), 1, 10, [], []);
        log.CombatEnded(At(40));
        log.CombatStarted(At(60));
        log.CombatEnded(At(90));
        Assert.Empty(Cooldowns.Unused(log, 1, log.Pulls[1], all, default));

        log.Wiped(At(95));
        log.CombatStarted(At(100));
        log.CombatEnded(At(130));
        Assert.Equal(all, Cooldowns.Unused(log, 1, log.Pulls[2], all, default));
        Assert.Empty(Cooldowns.Unused(log, 1, log.Pulls[1], all, default));
    }

    [Fact]
    public void ArmsLengthIsOnlyMissedWhenAutoAttacked()
    {
        Cooldown[] all = [new Cooldown(10, new HashSet<uint> { 10 }, TimeSpan.FromSeconds(120), 1, OnlyWhenAttacked: true)];

        var log = new RecapLog();
        log.StartSession(Start);
        log.CombatStarted(At(10));
        log.AddDamage(At(15), 1, "Mob", 7, 100, 1000, 1000, autoAttack: true);
        log.AddDamage(At(15), 2, "Mob", 8, 100, 1000, 1000);
        log.CombatEnded(At(40));

        Assert.Equal(all, Cooldowns.Unused(log, 1, log.Pulls[0], all, default));
        Assert.Equal(all, Cooldowns.NeverUsed(log, 1, all));
        Assert.Empty(Cooldowns.Unused(log, 2, log.Pulls[0], all, default));
        Assert.Empty(Cooldowns.NeverUsed(log, 2, all));
    }

    [Fact]
    public void LongPullsCountHowOftenACooldownWasReady()
    {
        Cooldown[] all = [new Cooldown(10, new HashSet<uint> { 10 }, TimeSpan.FromSeconds(90), 1)];

        var log = new RecapLog();
        log.StartSession(Start);
        log.CombatStarted(At(0));
        log.AddAction(At(100), 1, 10, [], []);
        log.CombatEnded(At(275));

        // Ready at 0, 90 and 180. The fourth time, at 270, is too close to the end to count.
        Assert.Equal([new CooldownUse(all[0], 1, 3)], Cooldowns.Underused(log, 1, log.Pulls[0], all, default));
        Assert.Empty(Cooldowns.Unused(log, 1, log.Pulls[0], all, default));
        Assert.Equal([new CooldownUse(all[0], 0, 3)], Cooldowns.Underused(log, 2, log.Pulls[0], all, default));
    }

    [Fact]
    public void PerDutyCooldownsAreOnlyJudgedPerPullWhenThereIsOnePull()
    {
        Cooldown[] all = [new Cooldown(10, new HashSet<uint> { 10 }, TimeSpan.FromSeconds(90), 1, PerDutyOnly: true)];

        var log = new RecapLog();
        log.StartSession(Start);
        log.CombatStarted(At(0));
        log.CombatEnded(At(60));
        Assert.Equal(all, Cooldowns.Unused(log, 1, log.Pulls[0], all, default));

        log.CombatStarted(At(100));
        log.CombatEnded(At(160));
        Assert.Empty(Cooldowns.Unused(log, 1, log.Pulls[0], all, default));
        Assert.Equal(all, Cooldowns.NeverUsed(log, 1, all));
    }

    [Fact]
    public void ChargesComeBackOneAtATime()
    {
        Cooldown[] all = [new Cooldown(10, new HashSet<uint> { 10 }, TimeSpan.FromSeconds(60), 2)];

        var log = new RecapLog();
        log.StartSession(Start);
        log.CombatStarted(At(10));
        log.AddAction(At(12), 1, 10, [], []);
        log.AddAction(At(14), 1, 10, [], []);
        log.CombatEnded(At(30));

        // Both charges were spent at 12 and 14, so the first is back at 72.
        log.CombatStarted(At(40));
        log.CombatEnded(At(80));
        Assert.Empty(Cooldowns.Unused(log, 1, log.Pulls[1], all, default));

        log.CombatStarted(At(90));
        log.CombatEnded(At(120));
        Assert.Equal(all, Cooldowns.Unused(log, 1, log.Pulls[2], all, default));
    }
}
