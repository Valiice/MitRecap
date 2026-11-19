using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using MitRecap.Core;

namespace MitRecap.Windows;

internal sealed class RecapWindow : Window
{
    private readonly History history;
    private readonly Configuration config;
    private readonly ActionCache actions = new();
    private readonly RecapTable table;

    // Null follows the current duty.
    private RecapLog? viewed;
    private int? selectedPull;
    private float supportButtonWidth;

    public RecapWindow(History history, Configuration config)
        : base("Mit Recap###MitRecap")
    {
        this.history = history;
        this.config = config;
        this.table = new RecapTable(config, this.actions);
        this.SizeCondition = ImGuiCond.FirstUseEver;
        this.Size = new Vector2(800, 350);
    }

    // 0 shows everyone.
    public uint SelectedPlayer { get; set; }

    private RecapLog Log => this.viewed ?? this.history.Current;

    public void ShowCurrent(uint playerId)
    {
        this.viewed = null;
        this.SelectedPlayer = playerId;
        this.IsOpen = true;
    }

    public override void Draw()
    {
        if (this.selectedPull > this.Log.Pulls.Count)
        {
            this.selectedPull = null;
        }

        if (this.SelectedPlayer != 0 && this.Log.Player(this.SelectedPlayer) == null)
        {
            this.SelectedPlayer = 0;
        }

        this.DrawToolbar();
        ImGui.Separator();

        ImGui.Columns(2);
        ImGui.SetColumnWidth(0, 160 * ImGuiHelpers.GlobalScale);
        this.DrawPullList();
        ImGui.NextColumn();
        this.table.Draw(new RecapTable.View(this.Log, this.SelectedPlayer, this.selectedPull));
        ImGui.Columns(1);
    }

    private static string DutyLabel(RecapLog l)
    {
        if (l.Started == default)
        {
            return "waiting for a duty";
        }

        return $"{l.Started:HH:mm}  {(l.DutyName.Length > 0 ? l.DutyName : "Duty")}{(l.Recording ? " (recording)" : "")}{(l.Imported ? " (imported)" : "")}";
    }

    private static void Notify(string text, NotificationType type) =>
        Services.Notifications.AddNotification(new Notification { Content = text, Type = type });

    private string PlayerLabel(uint id) =>
        this.Log.Player(id) is { } p ? (p.Job.Length > 0 ? $"{p.Name} ({p.Job})" : p.Name) : "?";

    private void DrawToolbar()
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Player");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(220 * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo("##player", this.SelectedPlayer == 0 ? "Everyone" : this.PlayerLabel(this.SelectedPlayer)))
        {
            if (ImGui.Selectable("Everyone", this.SelectedPlayer == 0))
            {
                this.SelectedPlayer = 0;
            }

            foreach (var p in this.Log.Players)
            {
                if (p.JobId > 0)
                {
                    Ui.InlineIcon(RecapText.JobIcon(p.JobId));
                }

                ImGui.PushStyleColor(ImGuiCol.Text, Ui.RoleColor(p.Role));
                if (ImGui.Selectable($"{this.PlayerLabel(p.Id)}##{p.Id}", p.Id == this.SelectedPlayer))
                {
                    this.SelectedPlayer = p.Id;
                }

                ImGui.PopStyleColor();
            }

            ImGui.EndCombo();
        }

        ImGui.SameLine(0, 15);
        if (ImGuiComponents.IconButton("hidden", FontAwesomeIcon.Filter))
        {
            ImGui.OpenPopup("mitrecap_hidden");
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Hidden actions");
        }

        this.DrawHiddenPopup();

        ImGui.SameLine(0, 15);
        this.DrawClearButton();
        ImGui.SameLine(0, 15);
        this.DrawDutyPicker();
        ImGui.SameLine(0, 15);
        this.DrawCopyButton();
        ImGui.SameLine();
        this.DrawShareButtons();
        ImGui.SameLine(Math.Max(ImGui.GetCursorPosX() + ImGui.GetStyle().ItemSpacing.X, ImGui.GetContentRegionMax().X - this.supportButtonWidth));
        this.DrawSupportButton();
    }

    private void DrawSupportButton()
    {
        if (ImGuiComponents.IconButtonWithText(
                FontAwesomeIcon.Coffee,
                "Support",
                new Vector4(1.0f, 0.35f, 0.35f, 0.9f),
                new Vector4(1.0f, 0.25f, 0.25f, 1.0f),
                new Vector4(1.0f, 0.35f, 0.35f, 0.75f)))
        {
            Dalamud.Utility.Util.OpenLink("https://ko-fi.com/valiice");
        }

        this.supportButtonWidth = ImGui.GetItemRectSize().X;
    }

    private void DrawClearButton()
    {
        if (ImGuiComponents.IconButton("clear", FontAwesomeIcon.Trash))
        {
            ImGui.OpenPopup("mitrecap_clear");
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Clear the log");
        }

        if (ImGui.BeginPopup("mitrecap_clear"))
        {
            ImGui.TextUnformatted("Are you sure?");
            if (ImGui.Button("Yes"))
            {
                var duty = this.Log.DutyName;
                this.Log.Clear();
                this.Log.DutyName = duty;
                ImGui.CloseCurrentPopup();
            }

            ImGui.SameLine();
            if (ImGui.Button("No"))
            {
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }
    }

    private void DrawDutyPicker()
    {
        ImGui.SetNextItemWidth(280 * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo("##duty", DutyLabel(this.Log)))
        {
            for (var i = this.history.Logs.Count - 1; i >= 0; i--)
            {
                var l = this.history.Logs[i];
                if (ImGui.Selectable($"{DutyLabel(l)}##{i}", l == this.Log))
                {
                    this.viewed = l == this.history.Current ? null : l;
                    this.selectedPull = null;
                }
            }

            ImGui.EndCombo();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Duties since you started the game, newest first.\nThe list is emptied when the game closes.");
        }
    }

    private void DrawCopyButton()
    {
        if (ImGuiComponents.IconButton("copy", FontAwesomeIcon.Copy))
        {
            ImGui.SetClipboardText(RecapExport.Build(
                this.Log,
                typeof(RecapWindow).Assembly.GetName().Version?.ToString() ?? "",
                DateTime.Now - this.Log.Started,
                id => this.actions.Get(id).Name,
                p => CooldownTable.For(p, this.Log)));
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Copy the whole duty as readable text, for a bug report.\nPlayer names are replaced by P1, P2 and so on.");
        }
    }

    private void DrawShareButtons()
    {
        if (ImGuiComponents.IconButton("share", FontAwesomeIcon.ShareAlt))
        {
            ImGui.SetClipboardText(this.ShareCode());
            Notify("Share code copied.", NotificationType.Success);
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Copy a share code for this duty.\nSomeone else can import it in Mit Recap, or paste it at " + BrowserView.Site);
        }

        ImGui.SameLine();
        if (ImGuiComponents.IconButton("import", FontAwesomeIcon.FileImport))
        {
            this.Import(ImGui.GetClipboardText() ?? "");
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Import a share code from your clipboard.");
        }

        ImGui.SameLine();
        if (ImGuiComponents.IconButton("browser", FontAwesomeIcon.ExternalLinkAlt))
        {
            this.OpenInBrowser();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Open this duty in your browser.");
        }
    }

    private string ShareCode() => RecapShare.Encode(this.Log, ShareGame.Build(this.Log, this.actions));

    private void OpenInBrowser()
    {
        try
        {
            BrowserView.Open(this.ShareCode());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Notify("Could not open the browser. Use the share button and paste the code on the site.", NotificationType.Error);
        }
    }

    private void Import(string code)
    {
        if (RecapShare.Decode(code) is not { } log)
        {
            Notify("Your clipboard does not hold a share code this version of Mit Recap can read.", NotificationType.Error);
            return;
        }

        this.history.Import(log);
        this.viewed = log;
        this.selectedPull = null;
        this.SelectedPlayer = 0;
    }

    private void DrawHiddenPopup()
    {
        if (!ImGui.BeginPopup("mitrecap_hidden"))
        {
            return;
        }

        ImGui.TextUnformatted("Hidden actions");
        Ui.TextColored(Ui.ColorGrey, "Right-click an ability in the table to hide it.");
        ImGui.Separator();

        uint? show = null;
        if (ImGui.BeginChild("hiddenlist", ImGuiHelpers.ScaledVector2(300, 250)))
        {
            foreach (var id in this.config.HiddenActions.OrderBy(id => this.actions.Get(id).Name))
            {
                var (name, icon) = this.actions.Get(id);
                if (ImGui.SmallButton($"Show##{id}"))
                {
                    show = id;
                }

                ImGui.SameLine();
                Ui.InlineIcon(icon);
                ImGui.TextUnformatted(name);
            }
        }

        ImGui.EndChild();

        if (show is { } shown)
        {
            this.config.HiddenActions.Remove(shown);
            this.config.Save();
        }

        if (ImGui.Button("Restore defaults"))
        {
            this.config.HiddenActions = [.. Configuration.DefaultHidden];
            this.config.Save();
        }

        ImGui.EndPopup();
    }

    private void DrawPullList()
    {
        ImGui.TextUnformatted("Pulls");
        ImGui.Spacing();
        if (ImGui.BeginChild("pulls"))
        {
            if (ImGui.Selectable("Whole duty", this.selectedPull == null))
            {
                this.selectedPull = null;
            }

            foreach (var pull in this.Log.Pulls)
            {
                if (ImGui.Selectable($"Pull {pull.Number}  ({RecapText.Clock(pull.Start)})", this.selectedPull == pull.Number))
                {
                    this.selectedPull = pull.Number;
                }
            }
        }

        ImGui.EndChild();
    }
}
