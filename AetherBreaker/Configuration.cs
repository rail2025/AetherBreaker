using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace AetherBreaker;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    // General Settings
    public bool IsGameWindowLocked { get; set; } = true;

    // High Score
    public int HighScore { get; set; } = 0;

    // Audio Settings
    public bool IsBgmMuted { get; set; } = false;
    public bool IsSfxMuted { get; set; } = false;

    // The below exist just to make saving less cumbersome
    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    public void Initialize(IDalamudPluginInterface p)
    {
        this.pluginInterface = p;
    }

    public void Save()
    {
        this.pluginInterface?.SavePluginConfig(this);
    }
}
