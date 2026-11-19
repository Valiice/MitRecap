using System.Globalization;
using System.Text;

namespace MitRecap.Core;

// Players are numbered, never named.
public static class RecapExport
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string Build(
        RecapLog log,
        string version,
        TimeSpan now,
        Func<uint, string> actionName,
        Func<PlayerInfo, IReadOnlyList<Cooldown>> cooldownsFor)
    {
        var labels = log.Players.Select((p, i) => (p.Id, Label: $"P{i + 1}")).ToDictionary(x => x.Id, x => x.Label);

        var text = new StringBuilder("```\n");

        void AppendList(string label, PlayerInfo p, IEnumerable<string> entries)
        {
            var list = string.Join(", ", entries);
            if (list.Length > 0)
            {
                text.AppendLine(Invariant, $"{label} {labels[p.Id]}: {list}");
            }
        }

        text.AppendLine(Invariant, $"MitRecap {version} | {log.DutyName} | {log.Started:yyyy-MM-dd HH:mm}");
        text.AppendLine(Invariant, $"Players: {string.Join(", ", log.Players.Select(p => $"{labels[p.Id]} {p.Job} {p.Level}"))}");
        text.AppendLine(Invariant, $"Wipes: {string.Join(", ", log.Wipes.Select(Clock))}");

        foreach (var p in log.Pulls.Count > 1 ? log.Players : [])
        {
            AppendList("Never used", p, Cooldowns.NeverUsed(log, p.Id, cooldownsFor(p)).Select(c => actionName(c.ActionId)));
        }

        foreach (var pull in log.Pulls)
        {
            var attacked = string.Join(" ", pull.AutoAttacked.Select(id => labels.GetValueOrDefault(id, "?")));
            text.AppendLine(Invariant, $"Pull {pull.Number} {Clock(pull.Start)}-{(pull.End is { } end ? Clock(end) : "live")} auto-attacked: {attacked}");
            foreach (var p in log.Players)
            {
                var uses = Cooldowns.Underused(log, p.Id, pull, cooldownsFor(p), now);
                AppendList("  not pressed", p, uses.Where(u => u.Used == 0).Select(u => actionName(u.Cooldown.ActionId)));
                AppendList("  underused", p, uses.Where(u => u.Used > 0).Select(u => string.Create(Invariant, $"{actionName(u.Cooldown.ActionId)} {u.Used}/{u.Possible}")));
            }
        }

        foreach (var e in log.Events)
        {
            var what = e.Kind == RecapKind.Death ? Death(e, actionName) : Press(e, labels, actionName);
            text.AppendLine(Invariant, $"{Clock(e.Time)} p{e.Pull} {labels.GetValueOrDefault(e.PlayerId, "?")} {what}");
        }

        return text.Append("```").ToString();
    }

    private static string Clock(TimeSpan t) => string.Create(Invariant, $"{(int)t.TotalMinutes}:{t.Seconds:00}.{t.Milliseconds / 100}");

    private static string Death(RecapEvent e, Func<uint, string> actionName) =>
        "died" + string.Concat(e.LastHits.Select(h => string.Create(
            Invariant,
            $"; -{h.Amount} {actionName(h.ActionId)} ({h.ActionId}) from {h.Source} at {h.HpBefore}/{h.MaxHp} {(e.Time - h.Time).TotalSeconds:0.0}s before")));

    private static string Press(RecapEvent e, Dictionary<uint, string> labels, Func<uint, string> actionName)
    {
        var line = new StringBuilder();
        line.Append(Invariant, $"{actionName(e.ActionId)} ({e.ActionId}) >");
        foreach (var t in e.Targets.Where(t => t.Friendly))
        {
            var who = t.Id == e.PlayerId ? "self" : labels.GetValueOrDefault(t.Id, "other");
            var shield = t.ShieldPercent > 0 ? string.Create(Invariant, $" shield {t.ShieldPercent}%") : "";
            var healed = t.Healed > 0 ? string.Create(Invariant, $" +{t.Healed}") : "";
            line.Append(Invariant, $" [{who} {t.HpBefore}/{t.MaxHp}{shield}{healed}]");
        }

        var enemies = e.Targets.Count(t => !t.Friendly);
        if (e.Targets.Count == 0)
        {
            line.Append(" ground");
        }
        else if (enemies > 0)
        {
            line.Append(Invariant, $" {enemies} enemies");
        }

        if (e.TotalHealed > 0)
        {
            line.Append(Invariant, $" heal {e.TotalHealed} over {e.Overheal}");
        }

        if (e.LifeSteal > 0)
        {
            line.Append(Invariant, $" lifesteal {e.LifeSteal}");
        }

        if (e.Mitigated > 0)
        {
            line.Append(Invariant, $" mit {e.Mitigated:0}");
        }

        if (e.StatusIds.Count > 0)
        {
            line.Append(Invariant, $" st {string.Join(",", e.StatusIds)}");
        }

        return line.ToString();
    }
}
