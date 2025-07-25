using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using AetherBreaker.Windows;
using AetherBreaker.Audio;
using Dalamud.Game.ClientState.Conditions;
using AetherBreaker.Networking;
using AetherBreaker.Game;
using System.Collections.Concurrent;
using System;

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
    [PluginService] internal static IPartyList? PartyList { get; private set; } = null!;

    private const string CommandName = "/abreaker";
   // private const string SecondWindowCommandName = "/abreaker2";

    public Configuration Configuration { get; init; }
    public NetworkManager NetworkManager { get; init; }
    public AudioManager AudioManager { get; init; }
    public readonly WindowSystem WindowSystem = new("AetherBreaker");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }
    private AboutWindow AboutWindow { get; init; }
    private MultiplayerWindow MultiplayerWindow { get; init; }

    private MainWindow? secondWindow;
    private bool wasDead = false;

    // This queue holds actions that need to be run on the main UI thread.
    private readonly ConcurrentQueue<Action> mainThreadActions = new();

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(PluginInterface);
        NetworkManager = new NetworkManager();
        AudioManager = new AudioManager(this.Configuration);

        ConfigWindow = new ConfigWindow(this, this.AudioManager);
        MainWindow = new MainWindow(this, this.AudioManager, "");
        AboutWindow = new AboutWindow();
        MultiplayerWindow = new MultiplayerWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(AboutWindow);
        WindowSystem.AddWindow(MultiplayerWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the AetherBreaker game window."
        });

       // CommandManager.AddHandler(SecondWindowCommandName, new CommandInfo(OnSecondWindowCommand)
        //{
           // HelpMessage = "Opens a second AetherBreaker window for testing."
        //});

        ClientState.TerritoryChanged += OnTerritoryChanged;
        Condition.ConditionChange += OnConditionChanged;
        NetworkManager.OnConnected += OnNetworkConnected;
        NetworkManager.OnDisconnected += OnNetworkDisconnected;
        NetworkManager.OnError += OnNetworkError;
        NetworkManager.OnGameStateUpdateReceived += OnGameStateUpdateReceived;

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
    }

    public void Dispose()
    {
        ClientState.TerritoryChanged -= OnTerritoryChanged;
        Condition.ConditionChange -= OnConditionChanged;
        NetworkManager.OnConnected -= OnNetworkConnected;
        NetworkManager.OnDisconnected -= OnNetworkDisconnected;
        NetworkManager.OnError -= OnNetworkError;
        NetworkManager.OnGameStateUpdateReceived -= OnGameStateUpdateReceived;

        PluginInterface.UiBuilder.Draw -= DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUI;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUI;

        CommandManager.RemoveHandler(CommandName);
        //CommandManager.RemoveHandler(SecondWindowCommandName);

        this.WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();
        MainWindow.Dispose();
        AboutWindow.Dispose();
        MultiplayerWindow.Dispose();
        this.secondWindow?.Dispose();
        this.AudioManager.Dispose();
        this.NetworkManager.Dispose();
    }

    private void OnCommand(string command, string args) => ToggleMainUI();

    private void OnSecondWindowCommand(string command, string args)
    {
        if (this.secondWindow == null)
        {
            this.secondWindow = new MainWindow(this, this.AudioManager, " 2");
            this.WindowSystem.AddWindow(this.secondWindow);
        }
        this.secondWindow.Toggle();
    }

    private void DrawUI()
    {
        // Execute any pending actions on the main thread.
        while (this.mainThreadActions.TryDequeue(out var action))
        {
            action.Invoke();
        }
        this.WindowSystem.Draw();
    }

    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();
    public void ToggleAboutUI() => AboutWindow.Toggle();
    public void ToggleMultiplayerUI() => MultiplayerWindow.Toggle();

    // Network Event Handlers now queue their UI actions to be run on the main thread.
    private void OnNetworkConnected(string passphrase)
    {
        mainThreadActions.Enqueue(() => {
            this.MultiplayerWindow.SetConnectionStatus("Connected", false);
            this.MultiplayerWindow.IsOpen = false;
            this.MainWindow.IsOpen = true;
            this.MainWindow.StartMultiplayerGame(passphrase);
        });
    }

    private void OnNetworkDisconnected()
    {
        mainThreadActions.Enqueue(() => {
            this.MultiplayerWindow.SetConnectionStatus("Disconnected", true);
            if (this.MainWindow.IsOpen && this.MainWindow.GetMultiplayerGameSession() != null)
            {
                this.MainWindow.GetMultiplayerGameSession()?.GoToMainMenu();
            }
        });
    }

    private void OnNetworkError(string message) => mainThreadActions.Enqueue(() => this.MultiplayerWindow.SetConnectionStatus(message, true));

    private void OnGameStateUpdateReceived(byte[] state) => mainThreadActions.Enqueue(() => this.MainWindow.GetMultiplayerGameSession()?.ReceiveOpponentBoardState(state));

    private void OnTerritoryChanged(ushort territoryTypeId)
    {
        if (MainWindow.IsOpen) { MainWindow.IsOpen = false; }
        if (secondWindow != null && secondWindow.IsOpen) { secondWindow.IsOpen = false; }
    }

    private void OnConditionChanged(ConditionFlag flag, bool value)
    {
        if (flag == ConditionFlag.InCombat && !value)
        {
            bool isDead = ClientState.LocalPlayer?.CurrentHp == 0;
            if (isDead && !wasDead && Configuration.OpenOnDeath) { MainWindow.IsOpen = true; }
            wasDead = isDead;
        }

        if (flag == ConditionFlag.InDutyQueue && value && Configuration.OpenInQueue) { MainWindow.IsOpen = true; }
        if (flag == ConditionFlag.UsingPartyFinder && value && Configuration.OpenInPartyFinder) { MainWindow.IsOpen = true; }
        if (flag == ConditionFlag.Crafting && value && Configuration.OpenDuringCrafting) { MainWindow.IsOpen = true; }
    }
}
