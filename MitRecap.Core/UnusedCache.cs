namespace MitRecap.Core;

public sealed class UnusedCache(Func<PlayerInfo, RecapLog, IReadOnlyList<Cooldown>> cooldownsFor)
{
    private const int WholeDuty = 0;

    // The count is in the key because a first press of a role action above the player's level adds it to their list.
    private readonly Dictionary<(int Pull, uint Player, uint Job, byte Level, int Cooldowns), List<CooldownUse>> settled = [];
    private readonly Dictionary<(int Pull, uint Player, uint Job, byte Level, int Cooldowns), List<CooldownUse>> live = [];
    private readonly Dictionary<uint, IReadOnlyList<Cooldown>> cooldowns = [];
    private RecapLog? log;
    private int events;
    private int pulls;
    private int second;
    private TimeSpan now;

    public void Refresh(RecapLog log)
    {
        if (this.log != log || log.Events.Count < this.events || log.Pulls.Count != this.pulls)
        {
            this.settled.Clear();
            this.live.Clear();
            this.cooldowns.Clear();
            this.log = log;
        }

        this.events = log.Events.Count;
        this.pulls = log.Pulls.Count;
        this.now = DateTime.Now - log.Started;
        if ((int)this.now.TotalSeconds != this.second)
        {
            this.second = (int)this.now.TotalSeconds;
            this.live.Clear();
            this.cooldowns.Clear();
        }
    }

    public IReadOnlyList<Cooldown> CooldownsOf(PlayerInfo p)
    {
        if (!this.cooldowns.TryGetValue(p.Id, out var list))
        {
            this.cooldowns[p.Id] = list = cooldownsFor(p, this.log!);
        }

        return list;
    }

    public List<CooldownUse> InPull(Pull pull, PlayerInfo p)
    {
        var cache = pull.End == null ? this.live : this.settled;
        var all = this.CooldownsOf(p);
        var key = (pull.Number, p.Id, p.JobId, p.Level, all.Count);
        if (!cache.TryGetValue(key, out var list))
        {
            cache[key] = list = Cooldowns.Underused(this.log!, p.Id, pull, all, this.now);
        }

        return list;
    }

    public List<CooldownUse> InDuty(PlayerInfo p)
    {
        var all = this.CooldownsOf(p);
        var key = (WholeDuty, p.Id, p.JobId, p.Level, all.Count);
        if (!this.live.TryGetValue(key, out var list))
        {
            this.live[key] = list = [.. Cooldowns.NeverUsed(this.log!, p.Id, all).Select(c => new CooldownUse(c, 0, 0))];
        }

        return list;
    }
}
