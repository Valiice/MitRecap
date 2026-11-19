using System.Text.Json.Serialization;

namespace MitRecap.Core;

public enum RecapKind
{
    Action,
    Death,
}

public readonly record struct TargetHit(uint Id, string Name, bool Friendly, uint HpBefore, uint MaxHp, byte ShieldPercent, uint Healed);

public readonly record struct DamageHit(TimeSpan Time, string Source, uint ActionId, uint Amount, uint HpBefore, uint MaxHp);

// Pull is 0 before the first combat of the duty.
public sealed record RecapEvent(
    RecapKind Kind,
    TimeSpan Time,
    int Pull,
    bool InCombat,
    uint PlayerId,
    uint ActionId,
    uint Healed,
    IReadOnlyList<TargetHit> Targets,
    IReadOnlyList<uint> StatusIds)
{
    public double Mitigated { get; set; }

    public uint LifeSteal { get; set; }

    public uint LifeStealOverheal { get; set; }

    [JsonIgnore]
    public uint TotalHealed => this.Healed + this.LifeSteal;

    [JsonIgnore]
    public uint Overheal
    {
        get
        {
            var over = this.LifeStealOverheal;
            for (var i = 0; i < this.Targets.Count; i++)
            {
                var t = this.Targets[i];
                var room = t.MaxHp - Math.Min(t.HpBefore, t.MaxHp);
                if (t.MaxHp > 0 && t.Healed > room)
                {
                    over += t.Healed - room;
                }
            }

            return over;
        }
    }

    public IReadOnlyList<DamageHit> LastHits { get; init; } = [];
}
