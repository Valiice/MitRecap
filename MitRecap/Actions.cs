using Dalamud.Game;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace MitRecap;

internal static class Actions
{
    public static LuminaAction? Row(uint id) => Services.DataManager.GetExcelSheet<LuminaAction>().GetRowOrDefault(id);

    public static HashSet<uint> Resolve(IEnumerable<string> names) => [.. Resolve(names.ToDictionary(n => n, _ => true)).Keys];

    public static Dictionary<uint, T> Resolve<T>(IReadOnlyDictionary<string, T> byName)
    {
        var wanted = new Dictionary<string, T>(byName, StringComparer.OrdinalIgnoreCase);
        var byId = new Dictionary<uint, T>();
        foreach (var row in Services.DataManager.GetExcelSheet<LuminaAction>(ClientLanguage.English))
        {
            if (wanted.TryGetValue(row.Name.ExtractText(), out var value))
            {
                byId[row.RowId] = value;
            }
        }

        return byId;
    }
}
