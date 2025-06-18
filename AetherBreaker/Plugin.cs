using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using AetherBreaker.Windows;
using AetherBreaker.Audio;
using Dalamud.Game.ClientState.Conditions;

namespace AetherBreaker;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;

    private const string CommandName = "/abreaker";

    public Configuration Configuration { get; init; }
    public AudioManager AudioManager { get; init; }
    public readonly WindowSystem WindowSystem = new("AetherBreaker");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }
    private AboutWindow AboutWindow { get; init; }

    private bool wasDead = false;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(PluginInterface);
        AudioManager = new AudioManager(this.Configuration);

        ConfigWindow = new ConfigWindow(this, this.AudioManager);
        MainWindow = new MainWindow(this, this.AudioManager);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        AboutWindow = new AboutWindow();
        WindowSystem.AddWindow(AboutWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the AetherBreaker game window."
        });

        ClientState.TerritoryChanged += OnTerritoryChanged;
        Condition.ConditionChange += OnConditionChanged;

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();
        this.AudioManager.Dispose();
        MainWindow.Dispose();
        CommandManager.RemoveHandler(CommandName);
        ClientState.TerritoryChanged -= OnTerritoryChanged;
        Condition.ConditionChange -= OnConditionChanged;
    }

    private void OnCommand(string command, string args)
    {
        ToggleMainUI();
        AboutWindow.Dispose();
    }

    private void DrawUI() => WindowSystem.Draw();
    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();
    public void ToggleAboutUI() => AboutWindow.Toggle();

    private void OnTerritoryChanged(ushort territoryTypeId)
    {
        if (MainWindow.IsOpen)
        {
            MainWindow.IsOpen = false;
        }
    }

    private void OnConditionChanged(ConditionFlag flag, bool value)
    {
        if (flag == ConditionFlag.InCombat && !value)
        {
            bool isDead = ClientState.LocalPlayer?.CurrentHp == 0;
            if (isDead && !wasDead && Configuration.OpenOnDeath)
            {
                MainWindow.IsOpen = true;
            }
            wasDead = isDead;
        }

        if (flag == ConditionFlag.InDutyQueue && value && Configuration.OpenInQueue)
        {
            MainWindow.IsOpen = true;
        }

        if (flag == ConditionFlag.UsingPartyFinder && value && Configuration.OpenInPartyFinder)
        {
            MainWindow.IsOpen = true;
        }

        if (flag == ConditionFlag.Crafting && value && Configuration.OpenDuringCrafting)
        {
            MainWindow.IsOpen = true;
        }
    }
}
