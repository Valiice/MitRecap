using MitRecap.Core;
using Xunit;

namespace MitRecap.Tests;

public class RecapShareTests
{
    private static readonly DateTime Start = new(2026, 1, 1, 20, 0, 0);

    private static DateTime At(double seconds) => Start.AddSeconds(seconds);

    [Fact]
    public void ShareCodeRoundTripsAWholeLog()
    {
        Cooldown[] all = [new Cooldown(10, new HashSet<uint> { 10 }, TimeSpan.FromSeconds(120), 1, OnlyWhenAttacked: true)];

        var log = new RecapLog { DutyName = "Some Duty" };
        log.StartSession(Start);
        log.Touch(1, "Tank Name", "WAR", 21, JobRole.Tank, 90);
        log.Touch(2, "Healer Name", "WHM", 24, JobRole.Healer, 90);
        log.CombatStarted(At(10));
        log.AddAction(At(12), 1, 40, [new TargetHit(1, "Tank Name", true, 900, 1000, 10, 0)], [7]);
        log.Credit(At(13), 1, 7, 0, 123.5);
        log.AddAction(At(15), 2, 20, [new TargetHit(1, "Tank Name", true, 500, 1000, 0, 300)], [5]);
        log.AddDamage(At(18), 1, "Boss", 30, 900, 800, 1000, autoAttack: true);
        log.AddDeath(At(20), 1);
        log.Wiped(At(25));
        log.CombatEnded(At(26));
        log.CombatStarted(At(60));

        var game = new GameData(
            new() { [40] = new GameText("Rampart", 801) },
            new() { [7] = new GameText("Rampart", 10152, "Damage taken is reduced.") },
            new() { [1] = [.. all.Select(CooldownData.From)] },
            [99],
            [40]);
        var copy = RecapShare.Decode(RecapShare.Encode(log, game));

        Assert.NotNull(copy);
        Assert.True(copy.Imported);
        Assert.False(copy.Recording);
        Assert.Equal("Tank Name", copy.Player(1)?.Name);
        Assert.Equal(log.Started, copy.Started);
        Assert.Equal("Rampart", copy.Game?.Actions[40].Name);
        Assert.Equal("Damage taken is reduced.", copy.Game?.Statuses[7].Description);
        Assert.Equal(all[0].PressIds, copy.Game?.Cooldowns[1][0].ToCooldown().PressIds);

        // The pull still going is closed on import, so the live one is judged at that same moment.
        string Text(RecapLog l) => RecapExport.Build(l, "1.0", TimeSpan.FromSeconds(60), id => $"Action{id}", _ => all).Replace("live", "1:00.0");
        Assert.Equal(Text(log), Text(copy));
    }

    [Fact]
    public void OlderPrefixStillImports()
    {
        var log = new RecapLog();
        log.StartSession(Start);
        var code = RecapShare.Encode(log, new GameData([], [], [], [], [])).Replace("MITRECAP2:", "MITRECAP1:");

        Assert.NotNull(RecapShare.Decode(code));
    }

    [Fact]
    public void NothingPressedIsNotSaidOfAPullTooShortToPressIn()
    {
        var log = new RecapLog();
        log.StartSession(Start);
        log.Touch(1, "Tank Name", "WAR", 21, JobRole.Tank, 90);
        log.CombatStarted(At(10));
        log.CombatEnded(At(11));
        log.CombatStarted(At(20));
        log.CombatEnded(At(60));

        var rows = new RecapRows((_, _) => []);
        rows.Build(log, 0, null, new HashSet<uint>());

        Assert.False(rows.NothingPressed(log.Pulls[0]));
        Assert.True(rows.NothingPressed(log.Pulls[1]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("MITRECAP1:not base64")]
    [InlineData("MITRECAP1:aGVsbG8=")]
    [InlineData("MITRECAP9:aGVsbG8=")]
    public void ShareCodeRefusesAnythingElse(string code)
    {
        Assert.Null(RecapShare.Decode(code));
    }

    [Fact]
    public void ImportedLogDoesNotReplaceTheCurrentOne()
    {
        var history = new History();
        var current = history.StartNew(At(0));
        var imported = RecapShare.Decode(RecapShare.Encode(current, new GameData([], [], [], [], [])))!;

        history.Import(imported);

        Assert.Equal([imported, current], history.Logs);
        Assert.Same(current, history.Current);
    }
}
