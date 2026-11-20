namespace MitRecap.Core;

// Values are the game's own.
public enum EffectType : byte
{
    Miss = 1,
    FullResist = 2,
    Damage = 3,
    Heal = 4,
    BlockedDamage = 5,
    ParriedDamage = 6,
    Invulnerable = 7,
    ApplyStatusTarget = 14,
    ApplyStatusSource = 15,
    InstantDeath = 51,
}

public static class EnemyHit
{
    // What a hit takes from its target, or null when the effect is not a hit.
    // A wipe mechanic such as Ancient Flare kills outright and carries no damage number, so it takes all the HP left.
    public static uint? Amount(EffectType type, uint value, uint targetHp) => type switch
    {
        EffectType.Damage or EffectType.BlockedDamage or EffectType.ParriedDamage when value > 0 => value,
        EffectType.InstantDeath => targetHp,
        _ => null,
    };
}

public enum DamageType
{
    Slashing = 1,
    Piercing = 2,
    Blunt = 3,
    Shot = 4,
    Magic = 5,
    Physical = 7,
}

public static class ActionCategories
{
    public const uint AutoAttack = 1;
    public const uint Ability = 4;
}

public enum JobRole : byte
{
    None = 0,
    Tank = 1,
    Melee = 2,
    Ranged = 3,
    Healer = 4,
}
