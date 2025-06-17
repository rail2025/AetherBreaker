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
        this.Size = new Vector2(300, 150);
        this.SizeCondition = ImGuiCond.FirstUseEver;
        this.configuration = plugin.Configuration;
        this.audioManager = audioManager;

        this.configuration = plugin.Configuration;
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

        if (ImGui.Button("Reset High Score"))
        {
            this.configuration.HighScore = 0;
            this.configuration.Save();
        }
    }
}
