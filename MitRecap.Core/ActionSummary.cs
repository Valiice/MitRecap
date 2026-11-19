namespace MitRecap.Core;

public sealed class ActionSummary
{
    private readonly List<uint> statusIds = [];

    public bool Attacked { get; private set; }

    public bool Healed { get; private set; }

    public IReadOnlyList<uint> StatusIds => this.statusIds;

    public bool IsMitOrHeal => !this.Attacked && (this.Healed || this.statusIds.Count > 0);

    public uint AddEffect(EffectType type, uint value)
    {
        switch (type)
        {
            case EffectType.Miss or EffectType.FullResist or EffectType.Damage or EffectType.BlockedDamage or EffectType.ParriedDamage or EffectType.Invulnerable:
                this.Attacked = true;
                return 0;
            case EffectType.Heal:
                this.Healed = true;
                return value;
            case EffectType.ApplyStatusTarget or EffectType.ApplyStatusSource:
                if (value != 0 && !this.statusIds.Contains(value))
                {
                    this.statusIds.Add(value);
                }

                return 0;
            default:
                return 0;
        }
    }
}
