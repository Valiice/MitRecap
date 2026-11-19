namespace MitRecap.Core;

// Wording the plugin window and the website share.
public static class RecapText
{
    public const string MitigatedHelp =
        "Estimate of the damage this press prevented.\n\n" +
        "For every hit taken while it was active, it works out how much harder the hit would have\n" +
        "landed with no mitigation. Mitigations multiply (20% and 10% together are 28%, not 30%),\n" +
        "so when several are up the damage saved is shared between them by strength.\n\n" +
        "For a shield it is the part of each hit the shield absorbed. The game only shows the total\n" +
        "shield on a player, so with several shields up the split between them is a guess.\n\n" +
        "Not counted: blocks, parries, invulnerabilities and damage over time.";

    public const string NoDirectHit = "No direct hit in the 10 seconds before this death.\nLikely damage over time or a mechanic that kills outright.";

    public const string SurvivedLastHit = "The last hit left them alive. Damage over time, or something the plugin cannot see, probably finished it.";

    // Job icons sit at this id plus the job's row id.
    private const uint JobIconBase = 62100;

    public static uint JobIcon(uint jobId) => JobIconBase + jobId;

    public static string Clock(TimeSpan t) => $"{(int)t.TotalMinutes}:{t.Seconds:00}";

    public static string PullHeader(Pull pull) =>
        pull.End is { } end ? $"Pull {pull.Number}, lasted {Clock(end - pull.Start)}" : $"Pull {pull.Number}, in progress";

    public static string PullTime(RecapLog log, RecapEvent e)
    {
        var pullTime = log.PullTime(e);
        var abs = pullTime.Duration();
        var into = $"{(int)abs.TotalMinutes}:{abs.Seconds:00}.{abs.Milliseconds / 100}";
        return pullTime < TimeSpan.Zero
            ? $"{into} before pull {e.Pull} started"
            : $"{into} into pull {e.Pull}{(e.InCombat ? "" : ", out of combat")}";
    }

    public static string Target(RecapEvent e)
    {
        switch (e.Targets.Count)
        {
            case 0:
                return "ground";
            case 1:
                return e.Targets[0].Id == e.PlayerId ? "self" : e.Targets[0].Name;
            default:
                var friendly = e.Targets.Count(t => t.Friendly);
                return friendly == e.Targets.Count ? $"{friendly} allies"
                    : friendly == 0 ? $"{e.Targets.Count} enemies"
                    : $"{e.Targets.Count} targets";
        }
    }

    public static string Hp(TargetHit t)
    {
        var healed = t.Healed > 0 ? $", healed {t.Healed:N0}" : "";
        return $"{t.Name}: {t.HpBefore:N0} / {t.MaxHp:N0} ({(float)t.HpBefore / t.MaxHp:P0}){healed}";
    }

    public static string Overheal(RecapEvent e) =>
        $"Healed {e.TotalHealed:N0} in total, of which {e.Overheal:N0} was overheal: the target was already (nearly) full.\n" +
        "HP is read when the heal lands, so two heals landing together can both count as needed.";

    public static string LifeSteal(RecapEvent e) => $"{e.LifeSteal:N0} of this came from hits landed while it was up.";

    public static string Cooldown(string name, CooldownUse use, bool wholeDuty)
    {
        var what = use.Used > 0 ? $"Pressed {use.Used} of the {use.Possible} times it was ready in this pull."
            : wholeDuty ? "Not pressed once in this duty."
            : "Ready during this pull and not pressed.";
        return $"{name}\n{what} Cooldown: {use.Cooldown.Recast.TotalSeconds:0}s.";
    }

    public static string DeathVerb(RecapEvent death) =>
        death.LastHits.Count == 0 ? "Died" : death.SurvivedLastHit ? "Died after" : "Died to";

    public static string LastHit(RecapEvent death, DamageHit h, string actionName) =>
        $"{(death.Time - h.Time).TotalSeconds:0.0}s before   -{h.Amount:N0}   {actionName}   ({h.Source})   at {h.HpBefore:N0} HP";
}
