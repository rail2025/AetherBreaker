using System;
using System.Numerics;
using System.Reflection;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

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
        // CHANGE: Increased window width to prevent text wrapping.
        this.Size = new Vector2(380, 250);
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
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
        ImGui.Text($"Version: {version}");
        ImGui.Text("Release Date: 6/16/2025");
        ImGui.Separator();

        ImGui.Text("Created by: rail");
        ImGui.Text("With special thanks to the Dalamud Discord community.");
        ImGui.Text("Check out my other projects on github.com/rail2025/");
        ImGui.Text("AetherDraw and WDIGViewer.");
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        float btnWidthFull = ImGui.GetContentRegionAvail().X;

        // --- GitHub Issues Button ---
        float bugReportButtonHeight = ImGui.CalcTextSize("Bug report/\nFeature request").Y + ImGui.GetStyle().FramePadding.Y * 2.0f;

        using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0.1f, 0.4f, 0.1f, 1.0f)))
        using (ImRaii.PushColor(ImGuiCol.ButtonHovered, new Vector4(0.1f, 0.5f, 0.1f, 1.0f)))
        using (ImRaii.PushColor(ImGuiCol.ButtonActive, new Vector4(0.2f, 0.6f, 0.2f, 1.0f)))
        {
            
            if (ImGui.Button("Bug report/\nFeature request", new Vector2(btnWidthFull, bugReportButtonHeight)))
            {
                Util.OpenLink("https://github.com/rail2025/AetherBreaker/issues");
            }
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Opens the GitHub Issues page in your browser.");
        }

        ImGui.Spacing();

        // --- Support Button ---
        string buttonText = "Donate & Support";

        var buttonColor = new Vector4(0.9f, 0.2f, 0.2f, 1.0f);

        ImGui.PushStyleColor(ImGuiCol.Button, buttonColor);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, buttonColor * 1.2f);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, buttonColor * 0.8f);

        
        if (ImGui.Button(buttonText, new Vector2(btnWidthFull, 0)))
        {
            Util.OpenLink("https://ko-fi.com/rail2025");
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Buy me a coffee!");
        }

        ImGui.PopStyleColor(3);
    }
}
