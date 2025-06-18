using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using AetherBreaker.Audio;


namespace AetherBreaker.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;
    private readonly AudioManager audioManager;

    public ConfigWindow(Plugin plugin, AudioManager audioManager) : base("AetherBreaker Configuration")
    {
        this.Size = new Vector2(300, 250);
        this.SizeCondition = ImGuiCond.FirstUseEver;
        this.configuration = plugin.Configuration;
        this.audioManager = audioManager;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var lockGameWindow = this.configuration.IsGameWindowLocked;
        if (ImGui.Checkbox("Lock Game Window Position", ref lockGameWindow))
        {
            this.configuration.IsGameWindowLocked = lockGameWindow;
            this.configuration.Save();
        }

        ImGui.Separator();

        var isBgmMuted = this.configuration.IsBgmMuted;
        if (ImGui.Checkbox("Mute Music", ref isBgmMuted))
        {
            this.configuration.IsBgmMuted = isBgmMuted;
            this.configuration.Save();
            this.audioManager.UpdateBgmState();
        }

        var isSfxMuted = this.configuration.IsSfxMuted;
        if (ImGui.Checkbox("Mute Sound Effects", ref isSfxMuted))
        {
            this.configuration.IsSfxMuted = isSfxMuted;
            this.configuration.Save();
        }

        ImGui.Separator();
        ImGui.Text("Advanced Triggers");

        var openOnDeath = this.configuration.OpenOnDeath;
        if (ImGui.Checkbox("Open on Death", ref openOnDeath))
        {
            this.configuration.OpenOnDeath = openOnDeath;
            this.configuration.Save();
        }

        var openInQueue = this.configuration.OpenInQueue;
        if (ImGui.Checkbox("Open in Duty Queue", ref openInQueue))
        {
            this.configuration.OpenInQueue = openInQueue;
            this.configuration.Save();
        }

        var openInPartyFinder = this.configuration.OpenInPartyFinder;
        if (ImGui.Checkbox("Open in Party Finder Queue", ref openInPartyFinder))
        {
            this.configuration.OpenInPartyFinder = openInPartyFinder;
            this.configuration.Save();
        }

        var openDuringCrafting = this.configuration.OpenDuringCrafting;
        if (ImGui.Checkbox("Open during long craft", ref openDuringCrafting))
        {
            this.configuration.OpenDuringCrafting = openDuringCrafting;
            this.configuration.Save();
        }

        ImGui.Separator();

        if (ImGui.Button("Reset High Score"))
        {
            this.configuration.HighScore = 0;
            this.configuration.Save();
        }
    }
}
