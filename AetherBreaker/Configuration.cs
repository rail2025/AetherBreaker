using Dalamud.Configuration;
using System;

namespace AetherBreaker;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool IsConfigWindowMovable { get; set; } = true;
    public bool SomePropertyToBeSavedAndWithADefault { get; set; } = true;

    // New property to store the lock state of the main game window.
    // It defaults to 'true', so the window is locked by default.
    public bool IsGameWindowLocked { get; set; } = true;

    // The below exist just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
