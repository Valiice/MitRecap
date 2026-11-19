using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using MitRecap.Core;
using LuminaStatus = Lumina.Excel.Sheets.Status;

namespace MitRecap.Windows;

internal sealed class RecapTable(Configuration config, ActionCache actions)
{
    private const int PlayerColumn = 3;
    private const float BarGap = 2;

    private readonly RecapRows rows = new(CooldownTable.For);
    private readonly List<TargetHit> hpTargets = [];
    private View view;

    // With one player in the log the player column would say the same on every row.
    private bool Everyone => this.view.Player == 0 && this.view.Log.Players.Count > 1;

    private int AbilityColumn => this.Everyone ? PlayerColumn + 1 : PlayerColumn;

    public void Draw(View view)
    {
        this.view = view;
        var everyone = this.Everyone;
        const ImGuiTableFlags flags = ImGuiTableFlags.Borders | ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable |
                                      ImGuiTableFlags.Reorderable | ImGuiTableFlags.Hideable;
        if (!ImGui.BeginTable(everyone ? "mitrecap_all2" : "mitrecap2", everyone ? 8 : 7, flags))
        {
            return;
        }

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("Healed", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("Mitigated (est.)", ImGuiTableColumnFlags.WidthFixed);
        if (everyone)
        {
            ImGui.TableSetupColumn("Player");
        }

        ImGui.TableSetupColumn("Ability");
        ImGui.TableSetupColumn("Target");
        ImGui.TableSetupColumn("HP Before");
        ImGui.TableSetupColumn("Status Effects");
        ImGui.TableHeadersRow();

        this.rows.Build(view.Log, view.Player, view.Pull, config.HiddenActions);
        uint? hide = null;
        var clipper = new ImGuiListClipper();
        clipper.Begin(this.rows.Rows.Count);
        while (clipper.Step())
        {
            for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
            {
                var row = this.rows.Rows[i];
                ImGui.PushID(i);
                ImGui.TableNextRow();
                if (row.Event is { } e)
                {
                    if (this.DrawEventRow(e, everyone))
                    {
                        hide = e.ActionId;
                    }
                }
                else if (row.Cooldowns is { } cooldowns)
                {
                    this.DrawCooldownRow(row.Player!, row.Pull == null, cooldowns);
                }
                else
                {
                    this.DrawPullRow(row.Pull!);
                }

                ImGui.PopID();
            }
        }

        clipper.End();

        if (!this.rows.AnyEvents)
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(this.AbilityColumn);
            ImGui.AlignTextToFramePadding();
            Ui.TextColored(Ui.ColorGrey, this.view.Log.Recording || this.view.Log.Players.Count > 0
                ? "No mitigations or heals pressed."
                : "Recording starts when you enter a duty.");
        }

        ImGui.EndTable();

        if (hide is { } hidden)
        {
            config.HiddenActions.Add(hidden);
            config.Save();
        }
    }

    private void DrawHp(RecapEvent e)
    {
        this.view.Log.HpTargets(e, this.hpTargets);

        var count = this.hpTargets.Count;
        var gap = count > 1 ? BarGap * ImGuiHelpers.GlobalScale : 0;
        var width = Math.Max(1, (ImGui.GetContentRegionAvail().X - (gap * (count - 1))) / Math.Max(1, count));
        for (var i = 0; i < count; i++)
        {
            if (i > 0)
            {
                ImGui.SameLine(0, gap);
            }

            Ui.HpBar(this.hpTargets[i], width, labelled: count == 1);
        }
    }

    private bool DrawEventRow(RecapEvent e, bool everyone)
    {
        ImGui.TableNextColumn(); // Time
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(RecapText.Clock(e.Time));
        if (ImGui.IsItemHovered() && e.Pull > 0)
        {
            ImGui.SetTooltip(RecapText.PullTime(this.view.Log, e));
        }

        if (e.Kind == RecapKind.Death)
        {
            this.DrawDeathRow(e, everyone);
            return false;
        }

        return this.DrawActionRow(e, everyone);
    }

    private void DrawPullRow(Pull pull)
    {
        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, Ui.ColorPullRow);
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(RecapText.Clock(pull.Start));
        ImGui.TableSetColumnIndex(this.AbilityColumn);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(RecapText.PullHeader(pull));
        if (this.rows.NothingPressed(pull))
        {
            ImGui.SameLine();
            Ui.TextColored(Ui.ColorDeath, "nothing pressed");
        }
    }

    private void DrawCooldownRow(PlayerInfo p, bool wholeDuty, List<CooldownUse> cooldowns)
    {
        if (this.Everyone)
        {
            ImGui.TableSetColumnIndex(PlayerColumn);
            this.DrawPlayer(p.Id);
        }

        ImGui.TableSetColumnIndex(this.AbilityColumn);
        this.DrawCooldowns(wholeDuty ? "Never used" : "Not pressed", Ui.ColorDeath, cooldowns, pressed: false, wholeDuty);
        this.DrawCooldowns("Could press more", Ui.ColorGrey, cooldowns, pressed: true, wholeDuty);

        // The last item leaves a SameLine pending, which would pull the next row's text up into this one.
        ImGui.NewLine();
    }

    private void DrawCooldowns(string label, uint color, List<CooldownUse> cooldowns, bool pressed, bool wholeDuty)
    {
        var first = true;
        foreach (var use in cooldowns)
        {
            if (use.Used > 0 != pressed || this.rows.IsHidden(use.Cooldown))
            {
                continue;
            }

            if (first)
            {
                first = false;
                ImGui.AlignTextToFramePadding();
                Ui.TextColored(color, label);
                ImGui.SameLine(0, 6);
            }

            var (name, icon) = actions.Get(use.Cooldown.ActionId);
            Ui.InlineIcon(icon);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(RecapText.Cooldown(name, use, wholeDuty));
            }

            if (pressed)
            {
                ImGui.AlignTextToFramePadding();
                Ui.TextColored(Ui.ColorGrey, $"{use.Used}/{use.Possible}");
                ImGui.SameLine(0, 8);
            }
        }
    }

    private void DrawPlayer(uint id)
    {
        if (this.view.Log.Player(id) is not { } p)
        {
            ImGui.TextUnformatted("?");
            return;
        }

        if (p.JobId > 0)
        {
            Ui.InlineIcon(RecapText.JobIcon(p.JobId));
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(p.Job);
            }
        }

        ImGui.AlignTextToFramePadding();
        Ui.TextColored(Ui.RoleColor(p.Role), p.Name);
    }

    private void DrawDeathRow(RecapEvent e, bool everyone)
    {
        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, Ui.ColorDeathRow);
        var killer = e.KillingBlow;

        ImGui.TableNextColumn(); // Healed, used here for the damage of the killing blow
        if (killer is { } k)
        {
            ImGui.AlignTextToFramePadding();
            Ui.TextColored(Ui.ColorDeath, $"-{k.Amount:N0}");
        }

        ImGui.TableNextColumn(); // Mitigated
        if (everyone)
        {
            ImGui.TableNextColumn();
            this.DrawPlayer(e.PlayerId);
        }

        ImGui.TableNextColumn(); // Ability
        ImGui.BeginGroup();
        ImGui.AlignTextToFramePadding();
        Ui.TextColored(Ui.ColorDeath, RecapText.DeathVerb(e));
        if (killer is { } hit)
        {
            var (name, icon) = actions.Get(hit.ActionId);
            ImGui.SameLine(0, 4);
            Ui.InlineIcon(icon);
            ImGui.AlignTextToFramePadding();
            Ui.TextColored(Ui.ColorAction, name);
        }

        ImGui.EndGroup();
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            if (e.LastHits.Count == 0)
            {
                ImGui.TextUnformatted(RecapText.NoDirectHit);
            }
            else
            {
                if (e.SurvivedLastHit)
                {
                    ImGui.TextUnformatted(RecapText.SurvivedLastHit);
                }

                ImGui.TextUnformatted("Last hits taken:");
                foreach (var h in e.LastHits)
                {
                    ImGui.TextUnformatted(RecapText.LastHit(e, h, actions.Get(h.ActionId).Name));
                }
            }

            ImGui.EndTooltip();
        }

        if (killer is not { } last)
        {
            return;
        }

        ImGui.TableNextColumn(); // Target, used here for who dealt the killing blow
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(last.Source);

        ImGui.TableNextColumn(); // HP Before
        if (last.MaxHp > 0)
        {
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, Ui.ColorHp);
            ImGui.ProgressBar(Math.Min(1f, (float)last.HpBefore / last.MaxHp), new Vector2(-1, 0), $"{last.HpBefore:N0}");
            ImGui.PopStyleColor();
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip($"{last.HpBefore:N0} / {last.MaxHp:N0} before the killing blow");
            }
        }
    }

    private bool DrawActionRow(RecapEvent e, bool everyone)
    {
        var hide = false;

        ImGui.TableNextColumn(); // Healed
        if (e.TotalHealed > 0)
        {
            ImGui.AlignTextToFramePadding();
            var over = e.Overheal;
            Ui.TextColored(Ui.ColorHealingText, $"+{e.TotalHealed - over:N0}");
            if (e.LifeSteal > 0 && ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(RecapText.LifeSteal(e));
            }

            if (over > 0)
            {
                ImGui.SameLine(0, 4);
                Ui.TextColored(Ui.ColorGrey, $"({over:N0} over)");
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(RecapText.Overheal(e));
                }
            }
        }

        ImGui.TableNextColumn(); // Mitigated
        if (MitTable.Tracks(e))
        {
            ImGui.AlignTextToFramePadding();
            Ui.TextColored(e.Mitigated >= 0.5 ? Ui.ColorMitigated : Ui.ColorGrey, $"~{e.Mitigated:N0}");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(RecapText.MitigatedHelp);
            }
        }

        if (everyone)
        {
            ImGui.TableNextColumn();
            this.DrawPlayer(e.PlayerId);
        }

        ImGui.TableNextColumn(); // Ability
        var (name, icon) = actions.Get(e.ActionId);
        Ui.InlineIcon(icon);
        ImGui.AlignTextToFramePadding();
        Ui.TextColored(Ui.ColorAction, name);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Right-click to hide this action");
            hide = ImGui.IsMouseClicked(ImGuiMouseButton.Right);
        }

        ImGui.TableNextColumn(); // Target
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(RecapText.Target(e));
        if (e.Targets.Count > 1 && ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            foreach (var t in e.Targets)
            {
                ImGui.TextUnformatted(t.Healed > 0 ? $"{t.Name}  +{t.Healed:N0}" : t.Name);
            }

            ImGui.EndTooltip();
        }

        ImGui.TableNextColumn(); // HP Before
        this.DrawHp(e);

        ImGui.TableNextColumn(); // Status Effects
        var statusSheet = Services.DataManager.GetExcelSheet<LuminaStatus>();
        foreach (var id in e.StatusIds)
        {
            if (statusSheet.GetRowOrDefault(id) is not { } status || status.Icon == 0)
            {
                continue;
            }

            Ui.InlineIcon(status.Icon);
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted(status.Name.ExtractText());
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 25);
                ImGui.TextUnformatted(status.Description.ExtractText());
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }
        }

        return hide;
    }

    internal readonly record struct View(RecapLog Log, uint Player, int? Pull);
}
