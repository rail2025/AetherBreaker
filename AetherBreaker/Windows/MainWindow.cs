using System;
using System.Numerics;
using AetherBreaker.Game;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace AetherBreaker.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private Vector2 launcherPosition;
    private Bubble? activeBubble;

    public MainWindow(Plugin plugin) : base("AetherBreaker")
    {
        this.Size = new Vector2(450, 600);
        this.SizeCondition = ImGuiCond.Always;
        this.Flags |= ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        if (this.plugin.Configuration.IsGameWindowLocked)
        {
            this.Flags |= ImGuiWindowFlags.NoMove;
        }
        else
        {
            this.Flags &= ~ImGuiWindowFlags.NoMove;
        }
    }

    public override void Draw()
    {
        var windowPos = ImGui.GetWindowPos();
        var windowSize = ImGui.GetWindowSize();
        this.launcherPosition = new Vector2(windowPos.X + windowSize.X * 0.5f, windowPos.Y + windowSize.Y - 50f);

        var drawList = ImGui.GetWindowDrawList();
        var launcherRadius = 20f;

        drawList.AddCircleFilled(this.launcherPosition, launcherRadius, ImGui.GetColorU32(new Vector4(0.8f, 0.8f, 0.8f, 1.0f)));

        // --- Aiming & Firing Logic ---
        if (ImGui.IsWindowHovered())
        {
            var mousePos = ImGui.GetMousePos();
            var direction = Vector2.Normalize(mousePos - this.launcherPosition);

            // --- Angle Failsafe for 90-degree shots ---
            // We ensure there is always a minimum upward angle. In screen coordinates, a negative Y is "up".
            // If the upward velocity is too small (i.e., direction.Y is greater than -0.1f),
            // we clamp it to a minimum upward angle. This prevents perfectly horizontal shots.
            if (direction.Y > -0.1f)
            {
                direction.Y = -0.1f;
                direction = Vector2.Normalize(direction); // Re-normalize to keep the vector's length at 1.
            }

            // Draw aiming line using the potentially corrected angle
            var aimLineStart = this.launcherPosition + (direction * (launcherRadius + 2f));
            var aimLineEnd = this.launcherPosition + (direction * 100f);
            drawList.AddLine(aimLineStart, aimLineEnd, ImGui.GetColorU32(new Vector4(1.0f, 1.0f, 1.0f, 0.5f)), 3f);

            // Firing Logic
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && this.activeBubble == null)
            {
                var bubbleRadius = 15f;
                var bubbleSpeed = 500f;
                var startPos = this.launcherPosition + (direction * (launcherRadius + bubbleRadius));

                // Fire the bubble using the corrected direction
                this.activeBubble = new Bubble(
                    startPos,
                    direction * bubbleSpeed,
                    bubbleRadius,
                    ImGui.GetColorU32(new Vector4(1.0f, 0.2f, 0.2f, 1.0f))
                );
            }
        }

        // --- Bubble Update & Drawing Logic ---
        if (this.activeBubble != null)
        {
            this.activeBubble.Position += this.activeBubble.Velocity * ImGui.GetIO().DeltaTime;

            var bubblePos = this.activeBubble.Position;
            var bubbleRadiusValue = this.activeBubble.Radius;

            // --- Wall Collision (Ricochet) ---
            if (bubblePos.X - bubbleRadiusValue < windowPos.X || bubblePos.X + bubbleRadiusValue > windowPos.X + windowSize.X)
            {
                this.activeBubble.Velocity.X *= -1;
            }

            // --- Top & Bottom Boundary Failsafe ---
            // Destroy the bubble if it leaves the top OR bottom of the play area.
            if (bubblePos.Y < windowPos.Y - bubbleRadiusValue || bubblePos.Y > windowPos.Y + windowSize.Y + bubbleRadiusValue)
            {
                this.activeBubble = null;
            }
            else
            {
                drawList.AddCircleFilled(this.activeBubble.Position, this.activeBubble.Radius, this.activeBubble.Color);
            }
        }
    }
}
