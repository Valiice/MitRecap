namespace MitRecap.Core;

public readonly record struct CooldownUse(Cooldown Cooldown, int Used, int Possible);
