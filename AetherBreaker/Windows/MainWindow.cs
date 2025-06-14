using System;
using System.Numerics;
using AetherBreaker.Game;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace AetherBreaker.Windows;

/// <summary>
/// The main game window that handles bubble shooting mechanics and rendering.
/// Manages player input, bubble physics, and game state.
/// </summary>
public class MainWindow : Window, IDisposable
{
    /// <summary>
    /// Reference to the parent plugin instance.
    /// </summary>
    private readonly Plugin plugin;

    /// <summary>
    /// The game board managing bubble placement.
    /// </summary>
    private readonly GameBoard gameBoard;

    /// <summary>
    /// Position of the bubble launcher at the bottom center.
    /// </summary>
    private Vector2 launcherPosition;

    /// <summary>
    /// The currently active bubble that's in motion.
    /// </summary>
    private Bubble? activeBubble;

    /// <summary>
    /// Initializes a new instance of the MainWindow class.
    /// </summary>
    /// <param name="plugin">The parent plugin instance.</param>
    public MainWindow(Plugin plugin) : base("AetherBreaker")
    {
        this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        this.gameBoard = new GameBoard(15f); // 15px bubble radius

        // Configure window properties
        this.Size = new Vector2(450, 600);
        this.SizeCondition = ImGuiCond.Always;
        this.Flags |= ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
    }

    /// <summary>
    /// Cleans up resources when the window is disposed.
    /// </summary>
    public void Dispose() { }

    /// <summary>
    /// Updates window state before drawing.
    /// </summary>
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

    /// <summary>
    /// Main rendering and game logic method.
    /// </summary>
    public override void Draw()
    {
        try
        {
            // Get window dimensions
            var windowPos = ImGui.GetWindowPos();
            var windowSize = ImGui.GetWindowSize();

            // Position launcher at bottom center
            launcherPosition = new Vector2(
                windowPos.X + windowSize.X * 0.5f,
                windowPos.Y + windowSize.Y - 50f
            );

            var drawList = ImGui.GetWindowDrawList();
            const float launcherRadius = 20f;

            // Draw launcher base
            drawList.AddCircleFilled(
                launcherPosition,
                launcherRadius,
                ImGui.GetColorU32(new Vector4(0.8f, 0.8f, 0.8f, 1.0f))
            );

            // Handle aiming when window is hovered
            if (ImGui.IsWindowHovered())
            {
                HandleAiming(drawList, windowPos, launcherRadius);
            }

            // Update active bubble physics
            UpdateActiveBubble(windowPos, windowSize, drawList);

            // Draw all stuck bubbles
            gameBoard.Draw(drawList);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error during MainWindow.Draw()");
        }
    }

    /// <summary>
    /// Handles aiming mechanics and bubble firing.
    /// </summary>
    /// <param name="drawList">The ImGui draw list to render to.</param>
    /// <param name="windowPos">The window position.</param>
    /// <param name="launcherRadius">The radius of the launcher.</param>
    private void HandleAiming(ImDrawListPtr drawList, Vector2 windowPos, float launcherRadius)
    {
        var mousePos = ImGui.GetMousePos();
        var direction = Vector2.Normalize(mousePos - launcherPosition);

        // Prevent perfectly horizontal shots
        if (direction.Y > -0.1f)
        {
            direction.Y = -0.1f;
            direction = Vector2.Normalize(direction);
        }

        // Draw aiming guide line
        var aimLineStart = launcherPosition + (direction * (launcherRadius + 2f));
        var aimLineEnd = launcherPosition + (direction * 100f);
        drawList.AddLine(
            aimLineStart,
            aimLineEnd,
            ImGui.GetColorU32(new Vector4(1.0f, 1.0f, 1.0f, 0.5f)),
            3f
        );

        // Fire new bubble on left click
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && activeBubble == null)
        {
            FireBubble(direction, launcherRadius);
        }
    }

    /// <summary>
    /// Fires a new bubble from the launcher.
    /// </summary>
    /// <param name="direction">The normalized firing direction.</param>
    /// <param name="launcherRadius">The radius of the launcher.</param>
    private void FireBubble(Vector2 direction, float launcherRadius)
    {
        const float bubbleRadius = 15f;
        const float bubbleSpeed = 500f;
        var startPos = launcherPosition + (direction * (launcherRadius + bubbleRadius));

        activeBubble = new Bubble(
            startPos,
            direction * bubbleSpeed,
            bubbleRadius,
            ImGui.GetColorU32(new Vector4(1.0f, 0.2f, 0.2f, 1.0f)) // Red color
        );
    }

    /// <summary>
    /// Updates the active bubble's physics and collision state.
    /// </summary>
    /// <param name="windowPos">The window position.</param>
    /// <param name="windowSize">The window size.</param>
    /// <param name="drawList">The ImGui draw list to render to.</param>
    private void UpdateActiveBubble(Vector2 windowPos, Vector2 windowSize, ImDrawListPtr drawList)
    {
        if (activeBubble == null) return;

        // Update position based on velocity
        activeBubble.Position += activeBubble.Velocity * ImGui.GetIO().DeltaTime;

        // Handle wall collisions
        HandleWallCollisions(windowPos, windowSize);

        // Check for sticking collisions
        if (gameBoard.CheckCollision(activeBubble, windowPos.Y))
        {
            HandleBubbleSticking(windowPos);
            return; // Bubble is now stuck, no need for further processing
        }

        // Handle out-of-bounds and rendering
        HandleBubbleBoundsAndRendering(windowPos, windowSize, drawList);
    }

    /// <summary>
    /// Handles bouncing off side walls.
    /// </summary>
    /// <param name="windowPos">The window position.</param>
    /// <param name="windowSize">The window size.</param>
    private void HandleWallCollisions(Vector2 windowPos, Vector2 windowSize)
    {
        if (activeBubble == null) return;

        if (activeBubble.Position.X - activeBubble.Radius < windowPos.X ||
            activeBubble.Position.X + activeBubble.Radius > windowPos.X + windowSize.X)
        {
            activeBubble.Velocity.X *= -1;
        }
    }

    /// <summary>
    /// Handles bubble sticking to ceiling or other bubbles.
    /// </summary>
    /// <param name="windowPos">The window position.</param>
    private void HandleBubbleSticking(Vector2 windowPos)
    {
        if (activeBubble == null) return;

        if (gameBoard.CheckCollision(activeBubble, windowPos.Y))
        {
            if (gameBoard.TryAddBubble(activeBubble, windowPos.Y))
            {
                activeBubble = null;
            }
            // If we can't find a valid position, let it keep moving
        }
    }

    /// <summary>
    /// Handles bubble boundary checks and rendering.
    /// </summary>
    /// <param name="windowPos">The window position.</param>
    /// <param name="windowSize">The window size.</param>
    /// <param name="drawList">The ImGui draw list to render to.</param>
    private void HandleBubbleBoundsAndRendering(Vector2 windowPos, Vector2 windowSize, ImDrawListPtr drawList)
    {
        if (activeBubble == null) return;

        // Remove if out of bounds at bottom
        if (activeBubble.Position.Y > windowPos.Y + windowSize.Y + activeBubble.Radius)
        {
            activeBubble = null;
            return;
        }

        // Draw the active bubble
        drawList.AddCircleFilled(
            activeBubble.Position,
            activeBubble.Radius,
            activeBubble.Color
        );
    }
}
