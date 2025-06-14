using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace AetherBreaker.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    public ConfigWindow(Plugin plugin) : base("A Wonderful Configuration Window###With a constant ID")
    {
        this.Flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;

        // Increase window size to fit the new checkbox
        this.Size = new Vector2(232, 120);
        this.SizeCondition = ImGuiCond.Always;

        this.configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        // Flags must be added or removed before Draw() is being called, or they won't apply
        if (this.configuration.IsConfigWindowMovable)
        {
            this.Flags &= ~ImGuiWindowFlags.NoMove;
        }
        else
        {
            this.Flags |= ImGuiWindowFlags.NoMove;
        }
    }

    public override void Draw()
    {
        // can't ref a property, so use a local copy
        var configValue = this.configuration.SomePropertyToBeSavedAndWithADefault;
        if (ImGui.Checkbox("Random Config Bool", ref configValue))
        {
            this.configuration.SomePropertyToBeSavedAndWithADefault = configValue;
            this.configuration.Save();
        }

        var movable = this.configuration.IsConfigWindowMovable;
        if (ImGui.Checkbox("Movable Config Window", ref movable))
        {
            this.configuration.IsConfigWindowMovable = movable;
            this.configuration.Save();
        }

        // Add the new checkbox for locking the game window
        var gameWindowLocked = this.configuration.IsGameWindowLocked;
        if (ImGui.Checkbox("Lock Game Window", ref gameWindowLocked))
        {
            this.configuration.IsGameWindowLocked = gameWindowLocked;
            this.configuration.Save();
        }
    }
}
