namespace MitRecap.Core;

public sealed record LogData(DateTime Started, string DutyName, List<TimeSpan> Wipes, List<PlayerInfo> Players, List<PullData> Pulls, List<RecapEvent> Events, GameData? Game = null);

public sealed record PullData(int Number, TimeSpan Start, TimeSpan? End, uint[] AutoAttacked);

// What a reader without the game files needs to show the log. Cooldowns are per player id.
public sealed record GameData(
    Dictionary<uint, GameText> Actions,
    Dictionary<uint, GameText> Statuses,
    Dictionary<uint, List<CooldownData>> Cooldowns,
    uint[] Hidden,
    uint[] Mitigations);

public sealed record GameText(string Name, uint Icon, string Description = "");

public sealed record CooldownData(uint ActionId, uint[] PressIds, TimeSpan Recast, int Charges, bool OnlyWhenAttacked, bool PerDutyOnly)
{
    public static CooldownData From(Cooldown c) => new(c.ActionId, [.. c.PressIds], c.Recast, c.Charges, c.OnlyWhenAttacked, c.PerDutyOnly);

    public Cooldown ToCooldown() => new(this.ActionId, this.PressIds.ToHashSet(), this.Recast, this.Charges, this.OnlyWhenAttacked, this.PerDutyOnly);
}
