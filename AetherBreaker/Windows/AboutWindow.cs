using System;
using System.Numerics;
using System.Reflection;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using ImGuiNET;

namespace AetherBreaker.Windows;

/// <summary>
/// A window to display information about the plugin, such as version, author, and support links.
/// </summary>
public class AboutWindow : Window, IDisposable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AboutWindow"/> class.
    /// </summary>
    public AboutWindow() : base("About AetherBreaker")
    {
        this.Size = new Vector2(300, 200);
        this.SizeCondition = ImGuiCond.FirstUseEver;
        this.Flags |= ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
    }

    /// <summary>
    /// Disposes of resources used by the window.
    /// </summary>
    public void Dispose() { }

    /// <summary>
    /// Draws the content of the About window.
    /// </summary>
    public override void Draw()
    {
        // Get and display the plugin's assembly version.
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
        ImGui.Text($"Version: {version}");
        ImGui.Text("Release Date: 6/16/2025"); // As requested 
        ImGui.Separator();

        ImGui.Text("Created by: rail");
        ImGui.Text("With special thanks to the Dalamud Discord community.");
        ImGui.Text("Check out my other projects on github.com/rail2025/");
        ImGui.Text("AetherDraw and WDIGViewer.");
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Support Button, using the provided template 
        string buttonText = "Donate & Support";
       
        var buttonColor = new Vector4(0.9f, 0.2f, 0.2f, 1.0f); // A reddish color

        ImGui.PushStyleColor(ImGuiCol.Button, buttonColor);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, buttonColor * 1.2f);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, buttonColor * 0.8f);

        // Center the button
        float buttonWidth = ImGui.CalcTextSize(buttonText).X + ImGui.GetStyle().FramePadding.X * 2.0f;
        float windowWidth = ImGui.GetWindowSize().X;
        ImGui.SetCursorPosX((windowWidth - buttonWidth) * 0.5f);

        if (ImGui.Button(buttonText, new Vector2(buttonWidth, 0)))
        {
            Util.OpenLink("https://ko-fi.com/rail2025");
        }

        ImGui.PopStyleColor(3);
    }
}
