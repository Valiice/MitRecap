using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace MitRecap;

internal static class Effects
{
    public const int PerTarget = 8;

    private const byte LargeValue = 0x40;
    private const byte OnCaster = 0x80;

    // Amounts over 65535 carry their upper bits in a second field.
    public static uint Value(in ActionEffectHandler.Effect effect) =>
        (effect.Param4 & LargeValue) != 0 ? effect.Value + ((uint)effect.Param3 << 16) : effect.Value;

    // A heal that goes to the caster instead of the target, as with life steal.
    public static bool AppliesToCaster(in ActionEffectHandler.Effect effect) => (effect.Param4 & OnCaster) != 0;
}
