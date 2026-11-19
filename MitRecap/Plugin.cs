using Dalamud.Game.Command;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using MitRecap.Core;
using MitRecap.Windows;

namespace MitRecap;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/mitrecap";

    private readonly WindowSystem windowSystem = new("MitRecap");
    private readonly History history = new();
    private readonly RecapWindow window;
    private DutyTracker? duty;
    private Capture? capture;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Services>();
        var config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        if (config.Version < Configuration.CurrentVersion)
        {
            config.HiddenActions.UnionWith(Configuration.DefaultHidden);
            config.Version = Configuration.CurrentVersion;
            config.Save();
        }

        this.window = new RecapWindow(this.history, config);
        this.windowSystem.AddWindow(this.window);

        // Dalamud never calls Dispose on a plugin whose ctor threw, so tear down ourselves.
        try
        {
            this.duty = new DutyTracker(this.history);
            this.capture = new Capture(this.history, this.duty);
            pluginInterface.UiBuilder.Draw += this.windowSystem.Draw;
            pluginInterface.UiBuilder.OpenMainUi += this.OpenWindow;
            Services.ContextMenu.OnMenuOpened += this.OnMenuOpened;
            Services.CommandManager.AddHandler(CommandName, new CommandInfo((_, _) => this.window.Toggle())
            {
                HelpMessage = "Open the Mit Recap window.",
            });
        }
        catch
        {
            this.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        Services.CommandManager.RemoveHandler(CommandName);
        Services.ContextMenu.OnMenuOpened -= this.OnMenuOpened;
        Services.PluginInterface.UiBuilder.OpenMainUi -= this.OpenWindow;
        Services.PluginInterface.UiBuilder.Draw -= this.windowSystem.Draw;
        this.windowSystem.RemoveAllWindows();
        this.capture?.Dispose();
        this.capture = null;
        this.duty?.Dispose();
        this.duty = null;
        Windows.BrowserView.Cleanup();
    }

    private void OpenWindow() => this.window.IsOpen = true;

    private void OnMenuOpened(IMenuOpenedArgs args)
    {
        if (!DutyTracker.InDuty || args.Target is not MenuTargetDefault target)
        {
            return;
        }

        var player = this.history.Current.Player((uint)target.TargetObjectId)
                     ?? this.history.Current.Players.FirstOrDefault(p => p.Name == target.TargetName);
        if (player == null)
        {
            return;
        }

        args.AddMenuItem(new MenuItem
        {
            Name = "Mit Recap",
            PrefixChar = 'M',
            OnClicked = _ => this.window.ShowCurrent(player.Id),
        });
    }
}
