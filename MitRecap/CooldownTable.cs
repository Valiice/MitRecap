using Dalamud.Game;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using MitRecap.Core;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace MitRecap;

internal static class CooldownTable
{
    // Names in one entry upgrade each other or share a cooldown. The last one the player has is shown.
    private static readonly string[][] Groups =
    [
        ["Rampart"], ["Reprisal"], [ArmsLength],
        ["Sentinel", "Guardian"], ["Bulwark"], ["Divine Veil"],
        ["Vengeance", "Damnation"], ["Thrill of Battle"], ["Nascent Flash", "Raw Intuition", "Bloodwhetting"], ["Shake It Off"],
        ["Shadow Wall", "Shadowed Vigil"], ["Dark Mind"], ["Dark Missionary"], ["Oblation"], ["The Blackest Night"],
        ["Nebula", "Great Nebula"], ["Camouflage"], ["Heart of Light"], ["Heart of Stone", "Heart of Corundum"],
        ["Divine Benison"], ["Temperance"], ["Aquaveil"],
        ["Seraphic Illumination", "Fey Illumination"], ["Expedient"],
        ["Collective Unconscious"], ["Exaltation"], ["Neutral Sect"],
        ["Haima"], ["Panhaima"], ["Holos"],
        ["Feint"], ["Addle"], ["Troubadour"], ["Tactician"], ["Shield Samba"], ["Dismantle"], ["Magick Barrier"], ["Mantra"],
    ];

    private const string ArmsLength = "Arm's Length";
    private const int SoulCrystalLevel = 30;
    private const int AnyLevel = byte.MaxValue;

    private static readonly Dictionary<string, (int Group, int Rank)> ByName = Index();
    private static readonly Dictionary<(uint Job, int Level), Cooldown[]> Cache = [];
    private static readonly HashSet<uint> RoleActions = [];

    // Whether a synced player has a role action above their level cannot be known, so one they pressed in this duty is added.
    public static IReadOnlyList<Cooldown> For(PlayerInfo player, RecapLog log)
    {
        var atLevel = For(player.JobId, player.Level);
        var pressed = log.Query(player.Id, null).Where(e => e.Kind == RecapKind.Action).Select(e => e.ActionId).ToHashSet();
        var proven = For(player.JobId, AnyLevel)
            .Where(c => RoleActions.Contains(c.ActionId) && c.PressIds.Overlaps(pressed) && !atLevel.Any(known => known.ActionId == c.ActionId))
            .ToList();
        return proven.Count == 0 ? atLevel : [.. atLevel, .. proven];
    }

    private static Cooldown[] For(uint jobId, int level)
    {
        if (Cache.TryGetValue((jobId, level), out var known))
        {
            return known;
        }

        var shown = new (uint Id, int Rank, ushort Recast)[Groups.Length];
        var ids = new HashSet<uint>[Groups.Length];

        // ClassJobCategory has one bool column per job, named by its abbreviation.
        var job = Services.DataManager.GetExcelSheet<ClassJob>(ClientLanguage.English).GetRowOrDefault(jobId);
        var abbreviation = job?.Abbreviation.ExtractText();

        var dps = (JobRole)(job?.Role ?? 0) is JobRole.Melee or JobRole.Ranged;
        var hasJob = string.IsNullOrEmpty(abbreviation) ? null : typeof(ClassJobCategory).GetProperty(abbreviation);

        // Level sync keeps role actions, and only the job tells a player's lowest real level (Viper 80, soul crystal 30).
        var soulCrystal = job is { } j && j.ClassJobParent.RowId != j.RowId ? SoulCrystalLevel : 0;
        var roleActionLevel = Math.Max(level, Math.Max(job?.StartingLevel ?? 0, soulCrystal));
        if (hasJob != null && level > 0)
        {
            foreach (var a in Services.DataManager.GetExcelSheet<LuminaAction>(ClientLanguage.English))
            {
                if (!ByName.TryGetValue(a.Name.ExtractText(), out var at))
                {
                    continue;
                }

                (ids[at.Group] ??= []).Add(a.RowId);
                if (a.IsRoleAction)
                {
                    RoleActions.Add(a.RowId);
                }

                if (a.IsPlayerAction && !a.IsPvP && a.ClassJobLevel > 0 && a.ClassJobLevel <= (a.IsRoleAction ? roleActionLevel : level) && at.Rank >= shown[at.Group].Rank &&
                    a.ClassJobCategory.ValueNullable is { } category && hasJob.GetValue(category) is true)
                {
                    shown[at.Group] = (a.RowId, at.Rank, a.Recast100ms);
                }
            }
        }

        var result = new List<Cooldown>();
        for (var g = 0; g < Groups.Length; g++)
        {
            if (shown[g].Id != 0)
            {
                // Traits add charges at higher levels, which only the game works out.
                var charges = Math.Max(1, (int)ActionManager.GetMaxCharges(shown[g].Id, (uint)level));
                var armsLength = Groups[g][0] == ArmsLength;
                result.Add(new Cooldown(shown[g].Id, ids[g], TimeSpan.FromSeconds(shown[g].Recast / 10.0), charges, armsLength, dps && !armsLength));
            }
        }

        return Cache[(jobId, level)] = [.. result];
    }

    private static Dictionary<string, (int Group, int Rank)> Index()
    {
        var index = new Dictionary<string, (int Group, int Rank)>(StringComparer.OrdinalIgnoreCase);
        for (var g = 0; g < Groups.Length; g++)
        {
            for (var r = 0; r < Groups[g].Length; r++)
            {
                index[Groups[g][r]] = (g, r);
            }
        }

        return index;
    }
}
