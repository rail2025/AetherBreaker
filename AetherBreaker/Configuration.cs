using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace AetherBreaker;

[Serializable]
public class SavedGame
{
    public int Score { get; set; }
    public int CurrentStage { get; set; }
    public List<SerializableBubble> Bubbles { get; set; } = new();
    public SerializableBubble? NextBubble { get; set; }
    public int ShotsUntilDrop { get; set; }
    public float TimeUntilDrop { get; set; }
    public bool IsHelperLineActiveForStage { get; set; }
}

[Serializable]
public class SerializableBubble
{
    public Vector2 Position { get; set; }
    public int BubbleType { get; set; }
}

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    // General Settings
    public bool IsGameWindowLocked { get; set; } = true;

    // High Score
    public int HighScore { get; set; } = 0;

    // Audio Settings
    public bool IsBgmMuted { get; set; } = false;
    public bool IsSfxMuted { get; set; } = false;
    public float MusicVolume { get; set; } = 0.5f;

    // Saved Game State
    public SavedGame? SavedGame { get; set; }

    // Advanced Triggers
    public bool OpenOnDeath { get; set; } = true;
    public bool OpenInQueue { get; set; } = false;
    public bool OpenDuringCrafting { get; set; } = false;
    public bool OpenInPartyFinder { get; set; } = false;



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
