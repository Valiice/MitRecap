namespace MitRecap.Core;

// PressIds: every action that counts as pressing it (upgrades, pet versions, shared cooldowns).
public sealed record Cooldown(uint ActionId, IReadOnlySet<uint> PressIds, TimeSpan Recast, int Charges, bool OnlyWhenAttacked = false, bool PerDutyOnly = false);
