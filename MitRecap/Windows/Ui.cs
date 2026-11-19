using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using MitRecap.Core;

namespace MitRecap.Windows;

// Table layout, colours and HP bar adapted from Death Recap by Kouzukii (MIT).
internal static class Ui
{
    public const uint ColorOverlayHealed = 0x50FFFFFF;
    public const uint ColorBarrier = 0xFF33FFFF;
    public const uint ColorHp = 0xFF058837;
    public const uint ColorHealingText = 0xFF99FAD1;
    public const uint ColorDeath = 0xFF6680E6;
    public const uint ColorAction = 0xFFF0A8B8;
    public const uint ColorGrey = 0xFF808080;
    public const uint ColorTank = 0xFFFFA060;
    public const uint ColorHealer = 0xFF70D878;
    public const uint ColorDps = 0xFF7070F0;
    public const uint ColorMitigated = 0xFFF0C080;
    public const uint ColorPullRow = 0x40808080;
    public const uint ColorDeathRow = 0x302020C0;
    public const uint ColorWhite = 0xFFFFFFFF;

    // Job icons sit at this id plus the job's row id.
    public const uint JobIconBase = 62100;

    public static string Clock(TimeSpan t) => $"{(int)t.TotalMinutes}:{t.Seconds:00}";

    public static void TextColored(uint color, string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
    }

    public static void InlineIcon(uint icon)
    {
        if (icon == 0)
        {
            return;
        }

        var img = Services.TextureProvider.GetFromGameIcon(new GameIconLookup(icon)).GetWrapOrEmpty();
        var pos = ImGui.GetCursorScreenPos() + new Vector2(0, 4);
        var size = ImGuiHelpers.ScaledVector2(16f * img.Width / Math.Max(1, img.Height), 16);
        ImGui.Dummy(size);
        ImGui.GetWindowDrawList().AddImage(img.Handle, pos, pos + size);
        ImGui.SameLine(0, 4);
    }

    public static uint RoleColor(JobRole role) => role switch
    {
        JobRole.Tank => ColorTank,
        JobRole.Healer => ColorHealer,
        JobRole.Melee or JobRole.Ranged => ColorDps,
        _ => ColorWhite,
    };

    public static void HpBar(TargetHit target, float width, bool labelled)
    {
        var before = (float)target.HpBefore / target.MaxHp;
        var heal = (float)target.Healed / target.MaxHp;
        var fraction = Math.Min(1f, before + heal);

        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, ColorHp);
        ImGui.ProgressBar(fraction, new Vector2(width, 0), "");
        ImGui.PopStyleColor();

        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var drawList = ImGui.GetWindowDrawList();

        if (heal > 0)
        {
            drawList.AddRectFilled(new Vector2(min.X + (width * before), min.Y), new Vector2(min.X + (width * fraction), max.Y), ColorOverlayHealed);
        }

        if (target.ShieldPercent > 0)
        {
            var shield = Math.Min(1f, target.ShieldPercent / 100f);
            drawList.AddRectFilled(new Vector2(min.X, min.Y + ((max.Y - min.Y) * 0.8f)), new Vector2(min.X + (width * shield), max.Y), ColorBarrier);
        }

        if (labelled)
        {
            var text = $"{target.HpBefore:N0}";
            var textSize = ImGui.CalcTextSize(text);
            var style = ImGui.GetStyle();
            drawList.AddText(
                new Vector2(
                    Math.Clamp(min.X + (width * fraction) + style.ItemSpacing.X, min.X, Math.Max(min.X, max.X - textSize.X - style.ItemInnerSpacing.X)),
                    min.Y + ((max.Y - min.Y - textSize.Y) * 0.5f)),
                ColorWhite,
                text);
        }

        if (ImGui.IsItemHovered())
        {
            var healed = target.Healed > 0 ? $", healed {target.Healed:N0}" : "";
            ImGui.SetTooltip($"{target.Name}: {target.HpBefore:N0} / {target.MaxHp:N0} ({before:P0}){healed}");
        }
    }
}
