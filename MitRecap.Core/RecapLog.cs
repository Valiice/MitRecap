namespace MitRecap.Core;

public sealed class RecapLog
{
    public const int MaxEvents = 50_000;
    public const int MaxDeathHits = 5;

    public static readonly TimeSpan PrePullWindow = TimeSpan.FromSeconds(30);

    public static readonly TimeSpan DeathWindow = TimeSpan.FromSeconds(10);

    public static readonly TimeSpan CreditWindow = TimeSpan.FromMinutes(2);

    // Damage lands a moment after its packet arrives, so the HP read for a hit that follows closely can still be the old value.
    public static readonly TimeSpan DamageDelay = TimeSpan.FromSeconds(2);

    private readonly List<RecapEvent> events = [];
    private readonly List<Pull> pulls = [];
    private readonly List<PlayerInfo> players = [];
    private readonly Dictionary<uint, List<DamageHit>> recentHits = [];
    private readonly Dictionary<uint, uint> lastReadHp = [];
    private readonly List<TimeSpan> wipes = [];
    private DateTime start;

    public bool Recording { get; private set; }

    public bool InCombat => this.pulls.Count > 0 && this.pulls[^1].End == null;

    public bool Imported { get; private init; }

    public DateTime Started => this.start;

    public string DutyName { get; set; } = "";

    public IReadOnlyList<RecapEvent> Events => this.events;

    public IReadOnlyList<Pull> Pulls => this.pulls;

    public IReadOnlyList<PlayerInfo> Players => this.players;

    public IReadOnlyList<TimeSpan> Wipes => this.wipes;

    public static RecapLog FromData(LogData data)
    {
        var log = new RecapLog { start = data.Started, DutyName = data.DutyName, Imported = true };
        log.wipes.AddRange(data.Wipes);
        log.players.AddRange(data.Players);
        log.events.AddRange(data.Events);

        // A pull that was still going when the log was shared ends at the last event.
        var last = data.Events.Count > 0 ? data.Events[^1].Time : TimeSpan.Zero;
        foreach (var p in data.Pulls)
        {
            var pull = new Pull(p.Number, p.Start) { End = p.End ?? (last > p.Start ? last : p.Start) };
            pull.AutoAttacked.UnionWith(p.AutoAttacked);
            log.pulls.Add(pull);
        }

        return log;
    }

    public LogData ToData() =>
        new(this.start, this.DutyName, this.wipes, this.players, [.. this.pulls.Select(p => new PullData(p.Number, p.Start, p.End, [.. p.AutoAttacked]))], this.events);

    public void StartSession(DateTime now)
    {
        this.Clear();
        this.start = now;
        this.Recording = true;
    }

    public void StopSession(DateTime now)
    {
        this.CombatEnded(now);
        this.Recording = false;
    }

    public void Clear()
    {
        this.events.Clear();
        this.pulls.Clear();
        this.players.Clear();
        this.recentHits.Clear();
        this.lastReadHp.Clear();
        this.wipes.Clear();
        this.DutyName = "";
    }

    public void Wiped(DateTime now)
    {
        if (this.Recording)
        {
            this.wipes.Add(now - this.start);
        }
    }

    public void CombatStarted(DateTime now)
    {
        if (!this.Recording || this.InCombat)
        {
            return;
        }

        var pull = new Pull(this.pulls.Count + 1, now - this.start);
        this.pulls.Add(pull);

        for (var i = this.events.Count - 1; i >= 0; i--)
        {
            var e = this.events[i];
            if (e.InCombat || pull.Start - e.Time > PrePullWindow)
            {
                break;
            }

            this.events[i] = e with { Pull = pull.Number };
        }
    }

    public void CombatEnded(DateTime now)
    {
        if (this.pulls.Count > 0 && this.pulls[^1].End == null)
        {
            this.pulls[^1].End = now - this.start;
        }
    }

    public void Touch(uint id, string name, string job, uint jobId = 0, JobRole role = JobRole.None, byte level = 0)
    {
        if (!this.Recording)
        {
            return;
        }

        var info = new PlayerInfo(id, name, job, jobId, role, level);
        var index = this.players.FindIndex(p => p.Id == id);
        if (index < 0)
        {
            this.players.Add(info);
        }
        else if (this.players[index] != info)
        {
            this.players[index] = info;
        }
    }

    public PlayerInfo? Player(uint id) => this.players.Find(p => p.Id == id);

    public void AddAction(DateTime now, uint playerId, uint actionId, IReadOnlyList<TargetHit> targets, IReadOnlyList<uint> statusIds)
    {
        uint healed = 0;
        for (var i = 0; i < targets.Count; i++)
        {
            healed += targets[i].Healed;
        }

        this.Add(new RecapEvent(RecapKind.Action, now - this.start, this.pulls.Count, this.InCombat, playerId, actionId, healed, targets, statusIds));
    }

    public void AddDamage(DateTime now, uint playerId, string source, uint actionId, uint amount, uint hpBefore, uint maxHp, bool autoAttack = false)
    {
        if (!this.Recording)
        {
            return;
        }

        if (autoAttack && this.InCombat && this.pulls.Count > 0)
        {
            this.pulls[^1].AutoAttacked.Add(playerId);
        }

        if (!this.recentHits.TryGetValue(playerId, out var hits))
        {
            this.recentHits[playerId] = hits = [];
        }

        if (hits.Count == MaxDeathHits)
        {
            hits.RemoveAt(0);
        }

        var time = now - this.start;
        var stale = hits.Count > 0 && time - hits[^1].Time < DamageDelay && this.lastReadHp.GetValueOrDefault(playerId) == hpBefore;
        this.lastReadHp[playerId] = hpBefore;
        if (stale)
        {
            var last = hits[^1];
            hpBefore = last.HpBefore - Math.Min(last.Amount, last.HpBefore);
        }

        hits.Add(new DamageHit(time, source, actionId, amount, hpBefore, maxHp));
    }

    public void AddDeath(DateTime now, uint playerId)
    {
        var time = now - this.start;
        DamageHit[] lastHits = [];
        if (this.recentHits.Remove(playerId, out var hits))
        {
            lastHits = [.. hits.Where(h => time - h.Time <= DeathWindow)];
        }

        this.Add(new RecapEvent(RecapKind.Death, time, this.pulls.Count, this.InCombat, playerId, 0, 0, [], []) { LastHits = lastHits });
    }

    public void Credit(DateTime now, uint playerId, uint statusId, uint actionId, double amount)
    {
        if (this.FindPress(now, playerId, statusId, actionId) is { } press)
        {
            press.Mitigated += amount;
        }
    }

    public void CreditHeal(DateTime now, uint playerId, uint statusId, uint actionId, uint healed, uint overheal)
    {
        if (this.FindPress(now, playerId, statusId, actionId) is { } press)
        {
            press.LifeSteal += healed;
            press.LifeStealOverheal += overheal;
        }
    }

    // playerId 0 is everyone, pull null is the whole duty.
    public IEnumerable<RecapEvent> Query(uint playerId, int? pull)
    {
        for (var i = 0; i < this.events.Count; i++)
        {
            var e = this.events[i];
            if ((playerId == 0 || e.PlayerId == playerId) && (pull == null || e.Pull == pull))
            {
                yield return e;
            }
        }
    }

    // Negative for pre-pull presses.
    public TimeSpan PullTime(RecapEvent e) => e.Pull > 0 ? e.Time - this.pulls[e.Pull - 1].Start : e.Time;

    private RecapEvent? FindPress(DateTime now, uint playerId, uint statusId, uint actionId)
    {
        var oldest = now - this.start - CreditWindow;
        for (var i = this.events.Count - 1; i >= 0 && this.events[i].Time >= oldest; i--)
        {
            var e = this.events[i];
            if (e.Kind == RecapKind.Action && e.PlayerId == playerId &&
                ((actionId != 0 && e.ActionId == actionId) || e.StatusIds.Contains(statusId)))
            {
                return e;
            }
        }

        return null;
    }

    private void Add(RecapEvent e)
    {
        if (this.Recording && this.events.Count < MaxEvents)
        {
            this.events.Add(e);
        }
    }
}
