using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using MitRecap.Core;
using LuminaStatus = Lumina.Excel.Sheets.Status;

namespace MitRecap.Windows;

internal sealed class RecapTable(Configuration config, ActionCache actions)
{
    private const string MitigatedHelp =
        "Estimate of the damage this press prevented.\n\n" +
        "For every hit taken while it was active, it works out how much harder the hit would have\n" +
        "landed with no mitigation. Mitigations multiply (20% and 10% together are 28%, not 30%),\n" +
        "so when several are up the damage saved is shared between them by strength.\n\n" +
        "For a shield it is the part of each hit the shield absorbed. The game only shows the total\n" +
        "shield on a player, so with several shields up the split between them is a guess.\n\n" +
        "Not counted: blocks, parries, invulnerabilities and damage over time.";

    private const int PlayerColumn = 3;
    private const float BarGap = 2;

    private readonly UnusedCache unused = new();

    private readonly List<Row> rows = [];
    private readonly HashSet<int> pressedPulls = [];
    private readonly List<TargetHit> hpTargets = [];
    private View view;

    private int AbilityColumn => this.view.Player == 0 ? PlayerColumn + 1 : PlayerColumn;

    private IEnumerable<PlayerInfo> ShownPlayers => this.view.Log.Players.Where(p => this.view.Player == 0 || p.Id == this.view.Player);

    public void Draw(View view)
    {
        this.view = view;
        var everyone = this.view.Player == 0;
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

        var anyEvents = this.BuildRows();
        uint? hide = null;
        var clipper = new ImGuiListClipper();
        clipper.Begin(this.rows.Count);
        while (clipper.Step())
        {
            for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
            {
                var row = this.rows[i];
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

        if (!anyEvents)
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

    private static string TargetLabel(RecapEvent e)
    {
        switch (e.Targets.Count)
        {
            case 0:
                return "ground";
            case 1:
                return e.Targets[0].Id == e.PlayerId ? "self" : e.Targets[0].Name;
            default:
                var friendly = e.Targets.Count(t => t.Friendly);
                return friendly == e.Targets.Count ? $"{friendly} allies"
                    : friendly == 0 ? $"{e.Targets.Count} enemies"
                    : $"{e.Targets.Count} targets";
        }
    }

    private void DrawHp(RecapEvent e)
    {
        this.hpTargets.Clear();
        foreach (var p in this.view.Log.Players)
        {
            foreach (var t in e.Targets)
            {
                if (t.Id == p.Id && t.MaxHp > 0)
                {
                    this.hpTargets.Add(t);
                }
            }
        }

        if (this.hpTargets.Count == 0)
        {
            this.hpTargets.AddRange(e.Targets.Where(t => t.Friendly && t.MaxHp > 0));
        }

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

    private bool BuildRows()
    {
        this.rows.Clear();
        this.pressedPulls.Clear();
        this.unused.Refresh(this.view.Log);
        var wholeDuty = this.view.Pull == null;
        var anyEvents = false;

        foreach (var e in this.view.Log.Query(this.view.Player, this.view.Pull))
        {
            if (!this.IsHidden(e))
            {
                this.pressedPulls.Add(e.Pull);
            }
        }

        if (wholeDuty && this.view.Log.Pulls.Count > 1)
        {
            foreach (var p in this.ShownPlayers)
            {
                this.AddCooldownRow(null, p, this.unused.InDuty(p));
            }
        }

        var nextPull = 0;
        foreach (var e in this.view.Log.Query(this.view.Player, this.view.Pull))
        {
            if (this.IsHidden(e))
            {
                continue;
            }

            for (; wholeDuty && nextPull < e.Pull; nextPull++)
            {
                this.AddPullRows(this.view.Log.Pulls[nextPull]);
            }

            this.rows.Add(new Row(Event: e));
            anyEvents = true;
        }

        for (; wholeDuty && nextPull < this.view.Log.Pulls.Count; nextPull++)
        {
            this.AddPullRows(this.view.Log.Pulls[nextPull]);
        }

        return anyEvents;
    }

    private void AddPullRows(Pull pull)
    {
        this.rows.Add(new Row(Pull: pull));
        foreach (var p in this.ShownPlayers)
        {
            this.AddCooldownRow(pull, p, this.unused.InPull(pull, p));
        }
    }

    private void AddCooldownRow(Pull? pull, PlayerInfo p, List<CooldownUse> cooldowns)
    {
        if (cooldowns.Exists(u => !this.IsHidden(u.Cooldown)))
        {
            this.rows.Add(new Row(Pull: pull, Player: p, Cooldowns: cooldowns));
        }
    }

    private bool IsHidden(RecapEvent e) => config.HiddenActions.Contains(e.ActionId);

    private bool IsHidden(Cooldown c) => c.PressIds.Overlaps(config.HiddenActions);

    private bool DrawEventRow(RecapEvent e, bool everyone)
    {
        ImGui.TableNextColumn(); // Time
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(Ui.Clock(e.Time));
        if (ImGui.IsItemHovered() && e.Pull > 0)
        {
            var pullTime = this.view.Log.PullTime(e);
            var abs = pullTime.Duration();
            var into = $"{(int)abs.TotalMinutes}:{abs.Seconds:00}.{abs.Milliseconds / 100}";
            ImGui.SetTooltip(pullTime < TimeSpan.Zero
                ? $"{into} before pull {e.Pull} started"
                : $"{into} into pull {e.Pull}{(e.InCombat ? "" : ", out of combat")}");
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
        ImGui.TextUnformatted(Ui.Clock(pull.Start));
        ImGui.TableSetColumnIndex(this.AbilityColumn);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(pull.End is { } end ? $"Pull {pull.Number}, lasted {Ui.Clock(end - pull.Start)}" : $"Pull {pull.Number}, in progress");

        var nothingToPress = this.view.Log.Player(this.view.Player) is { Role: JobRole.Melee or JobRole.Ranged } selected &&
                             this.unused.CooldownsOf(selected).Count == 0;
        if (!nothingToPress && !this.pressedPulls.Contains(pull.Number))
        {
            ImGui.SameLine();
            Ui.TextColored(Ui.ColorDeath, "nothing pressed");
        }
    }

    private void DrawCooldownRow(PlayerInfo p, bool wholeDuty, List<CooldownUse> cooldowns)
    {
        if (this.view.Player == 0)
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
            if (use.Used > 0 != pressed || this.IsHidden(use.Cooldown))
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
                var what = pressed ? $"Pressed {use.Used} of the {use.Possible} times it was ready in this pull."
                    : wholeDuty ? "Not pressed once in this duty."
                    : "Ready during this pull and not pressed.";
                ImGui.SetTooltip($"{name}\n{what} Cooldown: {use.Cooldown.Recast.TotalSeconds:0}s.");
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
            Ui.InlineIcon(Ui.JobIconBase + p.JobId);
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
        DamageHit? killer = e.LastHits.Count > 0 ? e.LastHits[^1] : null;

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
        var survivedIt = killer is { } blow && blow.Amount < blow.HpBefore && e.Time - blow.Time > RecapLog.DamageDelay;
        Ui.TextColored(Ui.ColorDeath, killer == null ? "Died" : survivedIt ? "Died after" : "Died to");
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
                ImGui.TextUnformatted("No direct hit in the 10 seconds before this death.\nLikely damage over time or a mechanic that kills outright.");
            }
            else
            {
                if (survivedIt)
                {
                    ImGui.TextUnformatted("The last hit left them alive. Damage over time, or something the plugin cannot see, probably finished it.");
                }

                ImGui.TextUnformatted("Last hits taken:");
                foreach (var h in e.LastHits)
                {
                    ImGui.TextUnformatted($"{(e.Time - h.Time).TotalSeconds:0.0}s before   -{h.Amount:N0}   {actions.Get(h.ActionId).Name}   ({h.Source})   at {h.HpBefore:N0} HP");
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
                ImGui.SetTooltip($"{e.LifeSteal:N0} of this came from hits landed while it was up.");
            }

            if (over > 0)
            {
                ImGui.SameLine(0, 4);
                Ui.TextColored(Ui.ColorGrey, $"({over:N0} over)");
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(
                        $"Healed {e.TotalHealed:N0} in total, of which {over:N0} was overheal: the target was already (nearly) full.\n" +
                        "HP is read when the heal lands, so two heals landing together can both count as needed.");
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
                ImGui.SetTooltip(MitigatedHelp);
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
        ImGui.TextUnformatted(TargetLabel(e));
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

    private readonly record struct Row(RecapEvent? Event = null, Pull? Pull = null, PlayerInfo? Player = null, List<CooldownUse>? Cooldowns = null);
}
