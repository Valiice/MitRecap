using MitRecap.Core;

namespace MitRecap.Windows;

internal sealed class ActionCache
{
    private readonly Dictionary<uint, (string Name, uint Icon)> actions = [];

    public (string Name, uint Icon) Get(uint id)
    {
        if (!this.actions.TryGetValue(id, out var info))
        {
            var row = Actions.Row(id);
            var name = row?.ActionCategory.RowId == ActionCategories.AutoAttack ? "Auto-attack" : row?.Name.ExtractText();
            this.actions[id] = info = (string.IsNullOrEmpty(name) ? $"Action {id}" : name, row?.Icon ?? 0);
        }

        return info;
    }
}
