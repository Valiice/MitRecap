namespace MitRecap.Core;

// One table row: an event, a pull header, or a player's unused cooldowns (whole duty when Pull is null).
public readonly record struct RecapRow(RecapEvent? Event = null, Pull? Pull = null, PlayerInfo? Player = null, List<CooldownUse>? Cooldowns = null);

public sealed class RecapRows(Func<PlayerInfo, RecapLog, IReadOnlyList<Cooldown>> cooldownsFor)
{
    private readonly UnusedCache unused = new(cooldownsFor);
    private readonly List<RecapRow> rows = [];
    private readonly HashSet<int> pressedPulls = [];
    private RecapLog log = new();
    private uint player;
    private IReadOnlySet<uint> hidden = new HashSet<uint>();

    public IReadOnlyList<RecapRow> Rows => this.rows;

    public bool AnyEvents { get; private set; }

    private IEnumerable<PlayerInfo> ShownPlayers => this.log.Players.Where(p => this.player == 0 || p.Id == this.player);

    // player 0 is everyone, pull null is the whole duty.
    public void Build(RecapLog log, uint player, int? pull, IReadOnlySet<uint> hidden)
    {
        this.log = log;
        this.player = player;
        this.hidden = hidden;
        this.rows.Clear();
        this.pressedPulls.Clear();
        this.unused.Refresh(log);
        this.AnyEvents = false;
        var wholeDuty = pull == null;

        foreach (var e in log.Query(player, pull))
        {
            if (!hidden.Contains(e.ActionId))
            {
                this.pressedPulls.Add(e.Pull);
            }
        }

        if (wholeDuty && log.Pulls.Count > 1)
        {
            foreach (var p in this.ShownPlayers)
            {
                this.AddCooldownRow(null, p, this.unused.InDuty(p));
            }
        }

        var nextPull = 0;
        foreach (var e in log.Query(player, pull))
        {
            if (hidden.Contains(e.ActionId))
            {
                continue;
            }

            for (; wholeDuty && nextPull < e.Pull; nextPull++)
            {
                this.AddPullRows(log.Pulls[nextPull]);
            }

            this.rows.Add(new RecapRow(Event: e));
            this.AnyEvents = true;
        }

        for (; wholeDuty && nextPull < log.Pulls.Count; nextPull++)
        {
            this.AddPullRows(log.Pulls[nextPull]);
        }
    }

    public bool IsHidden(Cooldown c) => c.PressIds.Overlaps(this.hidden);

    // False for a pull too short to press in, and for a selected DPS below the level of their first mitigation.
    public bool NothingPressed(Pull pull)
    {
        var end = pull.End ?? DateTime.Now - this.log.Started;
        var nothingToPress = this.log.Player(this.player) is { Role: JobRole.Melee or JobRole.Ranged } selected &&
                             this.unused.CooldownsOf(selected).Count == 0;
        return end - pull.Start >= Cooldowns.ShortestJudgedPull && !nothingToPress && !this.pressedPulls.Contains(pull.Number);
    }

    private void AddPullRows(Pull pull)
    {
        this.rows.Add(new RecapRow(Pull: pull));
        foreach (var p in this.ShownPlayers)
        {
            this.AddCooldownRow(pull, p, this.unused.InPull(pull, p));
        }
    }

    private void AddCooldownRow(Pull? pull, PlayerInfo p, List<CooldownUse> cooldowns)
    {
        if (cooldowns.Exists(u => !this.IsHidden(u.Cooldown)))
        {
            this.rows.Add(new RecapRow(Pull: pull, Player: p, Cooldowns: cooldowns));
        }
    }
}
