namespace MitRecap.Core;

public static class Cooldowns
{
    // A cooldown that only comes back this close to the end of a pull is not counted as missed.
    public static readonly TimeSpan ReadyGrace = TimeSpan.FromSeconds(20);

    public static readonly TimeSpan ShortestJudgedPull = TimeSpan.FromSeconds(15);

    public static List<Cooldown> NeverUsed(RecapLog log, uint playerId, IReadOnlyList<Cooldown> cooldowns)
    {
        var pressed = log.Query(playerId, null).Where(e => e.Kind == RecapKind.Action).Select(e => e.ActionId).ToHashSet();
        var attacked = log.Pulls.Any(p => p.AutoAttacked.Contains(playerId));
        return [.. cooldowns.Where(c => !c.PressIds.Overlaps(pressed) && (attacked || !c.OnlyWhenAttacked))];
    }

    public static List<Cooldown> Unused(RecapLog log, uint playerId, Pull pull, IReadOnlyList<Cooldown> cooldowns, TimeSpan now) =>
        [.. Underused(log, playerId, pull, cooldowns, now).Where(u => u.Used == 0).Select(u => u.Cooldown)];

    public static List<CooldownUse> Underused(RecapLog log, uint playerId, Pull pull, IReadOnlyList<Cooldown> cooldowns, TimeSpan now)
    {
        var underused = new List<CooldownUse>();
        var end = pull.End ?? now;
        if (end - pull.Start < ShortestJudgedPull)
        {
            return underused;
        }

        var readyBy = pull.End == null ? end : end - ReadyGrace;

        // A wipe resets every cooldown, so presses from before it do not count against this pull.
        var reset = log.Wipes.LastOrDefault(w => w < pull.Start, TimeSpan.MinValue);
        var presses = log.Query(playerId, null).Where(e => e.Kind == RecapKind.Action && e.Time > reset && e.Time <= end).ToList();

        foreach (var c in cooldowns)
        {
            if ((c.OnlyWhenAttacked && !pull.AutoAttacked.Contains(playerId)) || (c.PerDutyOnly && log.Pulls.Count > 1))
            {
                continue;
            }

            var own = presses.Where(e => c.PressIds.Contains(e.ActionId)).ToList();
            var used = own.Count(e => e.Pull == pull.Number);
            var possible = PossibleUses(c, own.Where(e => e.Time < pull.Start).Select(e => e.Time), pull.Start, readyBy);
            if (used < possible)
            {
                underused.Add(new CooldownUse(c, used, possible));
            }
        }

        return underused;
    }

    // Charges come back one at a time, and the timer only runs while one is missing.
    private static int PossibleUses(Cooldown cooldown, IEnumerable<TimeSpan> earlier, TimeSpan from, TimeSpan until)
    {
        if (cooldown.Recast <= TimeSpan.Zero)
        {
            return 0;
        }

        var charges = cooldown.Charges;
        var recharging = TimeSpan.Zero;

        void Recharge(TimeSpan at)
        {
            for (; charges < cooldown.Charges && recharging + cooldown.Recast <= at; recharging += cooldown.Recast)
            {
                charges++;
            }
        }

        void Spend(TimeSpan at)
        {
            // A real press the replay did not expect proves a charge was ready, so the clock restarts from it.
            if (charges == cooldown.Charges || charges == 0)
            {
                recharging = at;
            }

            charges = Math.Max(0, charges - 1);
        }

        foreach (var press in earlier)
        {
            Recharge(press);
            Spend(press);
        }

        var uses = 0;
        for (var at = from; at <= until; at = recharging + cooldown.Recast)
        {
            for (Recharge(at); charges > 0; uses++)
            {
                Spend(at);
            }
        }

        return uses;
    }
}
