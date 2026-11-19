using MitRecap.Core;
using Xunit;

namespace MitRecap.Tests;

public class RecapExportTests
{
    private static readonly DateTime Start = new(2026, 1, 1, 20, 0, 0);

    private static DateTime At(double seconds) => Start.AddSeconds(seconds);

    [Fact]
    public void ExportNumbersPlayersInsteadOfNamingThem()
    {
        Cooldown[] all = [new Cooldown(10, new HashSet<uint> { 10 }, TimeSpan.FromSeconds(120), 1)];

        var log = new RecapLog();
        log.StartSession(Start);
        log.Touch(1, "Tank Name", "WAR");
        log.Touch(2, "Healer Name", "WHM");
        log.CombatStarted(At(10));
        log.AddAction(At(15), 2, 20, [new TargetHit(1, "Tank Name", true, 500, 1000, 0, 300)], [5]);
        log.AddDamage(At(18), 1, "Boss", 30, 900, 800, 1000);
        log.AddDeath(At(20), 1);
        log.CombatEnded(At(40));

        var text = RecapExport.Build(log, "1.0", TimeSpan.FromSeconds(50), id => $"Action{id}", _ => all);

        Assert.DoesNotContain("Name", text);
        Assert.DoesNotContain("Never used", text);
        Assert.Contains("  not pressed P1: Action10", text);
        Assert.Contains("  not pressed P2: Action10", text);
        Assert.Contains("0:15.0 p1 P2 Action20 (20) > [P1 500/1000 +300] heal 300 over 0 st 5", text);
        Assert.Contains("0:20.0 p1 P1 died; -900 Action30 (30) from Boss at 800/1000 2.0s before", text);
    }
}
