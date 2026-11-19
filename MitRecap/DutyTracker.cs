using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using MitRecap.Core;
using StatusFlags = Dalamud.Game.ClientState.Objects.Enums.StatusFlags;

namespace MitRecap;

internal sealed class DutyTracker : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    private readonly History history;
    private readonly Dictionary<uint, JobRole> party = [];
    private readonly HashSet<uint> dead = [];
    private List<uint> lastFighters = [];
    private DateTime nextPoll;
    private uint dutyTerritory;

    public DutyTracker(History history)
    {
        this.history = history;
        Services.Condition.ConditionChange += this.OnConditionChange;
        Services.Framework.Update += this.OnUpdate;

        // Plugin constructors run off the framework thread, and the plugin can load mid duty.
        Services.Framework.RunOnFrameworkThread(() =>
        {
            if (InDuty && !this.Log.Recording)
            {
                this.StartSession();
            }
        });
    }

    public event Action? SessionStarted;

    public static bool InDuty => Services.Condition[ConditionFlag.BoundByDuty];

    public IReadOnlyDictionary<uint, JobRole> Party => this.party;

    private RecapLog Log => this.history.Current;

    public void Dispose()
    {
        Services.Framework.Update -= this.OnUpdate;
        Services.Condition.ConditionChange -= this.OnConditionChange;
    }

    private static bool InCombat(uint id) =>
        Services.ObjectTable.SearchByEntityId(id) is ICharacter c && c.StatusFlags.HasFlag(StatusFlags.InCombat);

    private void StartSession()
    {
        this.party.Clear();
        this.dead.Clear();
        this.lastFighters = [];
        this.dutyTerritory = 0;
        this.nextPoll = default;
        this.history.StartNew(DateTime.Now);
        this.SessionStarted?.Invoke();
    }

    private void OnConditionChange(ConditionFlag flag, bool value)
    {
        switch (flag)
        {
            case ConditionFlag.BoundByDuty when value:
                this.StartSession();
                break;
            case ConditionFlag.BoundByDuty:
                this.Log.StopSession(DateTime.Now);
                break;
            case ConditionFlag.InCombat when value:
                this.Log.CombatStarted(DateTime.Now);
                break;
        }
    }

    private void OnUpdate(IFramework framework)
    {
        if (!this.Log.Recording || DateTime.Now < this.nextPoll)
        {
            return;
        }

        this.nextPoll = DateTime.Now + PollInterval;

        try
        {
            this.UpdateDutyName();

            if (Services.PartyList.Length > 0)
            {
                foreach (var m in Services.PartyList)
                {
                    // Members in another zone have no entity and report 0 HP.
                    if (m.EntityId is not (0 or 0xE0000000))
                    {
                        this.Observe(m.EntityId, m.Name.TextValue, m.ClassJob.ValueNullable, m.Level, m.CurrentHP, m.MaxHP);
                    }
                }
            }
            else if (Services.ObjectTable.LocalPlayer is { } me)
            {
                this.Observe(me.EntityId, me.Name.TextValue, me.ClassJob.ValueNullable, me.Level, me.CurrentHp, me.MaxHp);
            }

            this.UpdateCombat();
        }
        catch (Exception e)
        {
            Services.Log.Error(e, "MitRecap: party poll failed");
        }
    }

    private void UpdateCombat()
    {
        var fighters = this.party.Keys.Where(InCombat).ToList();
        if (fighters.Count > 0)
        {
            this.lastFighters = fighters;
        }

        if (fighters.Count > 0 || Services.Condition[ConditionFlag.InCombat])
        {
            this.Log.CombatStarted(DateTime.Now);
            return;
        }

        // A wipe: the fight ends with its last fighters dead. Players who released early are alive again by then.
        if (this.lastFighters.Count > 0 && this.lastFighters.All(this.dead.Contains))
        {
            this.Log.Wiped(DateTime.Now);
        }

        this.lastFighters = [];
        this.Log.CombatEnded(DateTime.Now);
    }

    private void Observe(uint id, string name, Lumina.Excel.Sheets.ClassJob? job, byte level, uint hp, uint maxHp)
    {
        var role = (JobRole)(job?.Role ?? 0);
        this.party[id] = role;
        this.Log.Touch(id, name, job?.Abbreviation.ExtractText() ?? "", job?.RowId ?? 0, role, level);

        if (maxHp == 0)
        {
            return;
        }

        if (hp > 0)
        {
            this.dead.Remove(id);
        }
        else if (this.dead.Add(id))
        {
            this.Log.AddDeath(DateTime.Now, id);
        }
    }

    private void UpdateDutyName()
    {
        var territory = (uint)Services.ClientState.TerritoryType;
        if (territory == this.dutyTerritory)
        {
            return;
        }

        this.dutyTerritory = territory;
        var row = Services.DataManager.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>().GetRowOrDefault(territory);
        var name = row?.ContentFinderCondition.ValueNullable?.Name.ExtractText();
        if (!string.IsNullOrEmpty(name))
        {
            this.Log.DutyName = char.ToUpperInvariant(name[0]) + name[1..];
        }
    }
}
