using MitRecap.Core;
using LuminaStatus = Lumina.Excel.Sheets.Status;

namespace MitRecap.Windows;

internal static class ShareGame
{
    public static GameData Build(RecapLog log, ActionCache actions)
    {
        var cooldowns = log.Players.ToDictionary(p => p.Id, p => CooldownTable.For(p, log).Select(CooldownData.From).ToList());
        var actionIds = log.Events.Select(e => e.ActionId)
            .Concat(log.Events.SelectMany(e => e.LastHits).Select(h => h.ActionId))
            .Concat(cooldowns.Values.SelectMany(list => list).Select(c => c.ActionId))
            .Where(id => id != 0)
            .Distinct();

        var statusSheet = Services.DataManager.GetExcelSheet<LuminaStatus>();
        var statuses = new Dictionary<uint, GameText>();
        foreach (var id in log.Events.SelectMany(e => e.StatusIds).Distinct())
        {
            if (statusSheet.GetRowOrDefault(id) is { Icon: not 0 } status)
            {
                statuses[id] = new GameText(status.Name.ExtractText(), status.Icon, status.Description.ExtractText());
            }
        }

        return new GameData(
            actionIds.ToDictionary(id => id, id => new GameText(actions.Get(id).Name, actions.Get(id).Icon)),
            statuses,
            cooldowns,
            [.. Configuration.DefaultHidden],
            [.. log.Events.Where(MitTable.Tracks).Select(e => e.ActionId).Distinct()]);
    }
}
