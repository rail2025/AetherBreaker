using System.Numerics;
using ImGuiNET;

namespace AetherBreaker.Game;

/// <summary>
/// Represents a bubble in the game with position, velocity, and visual properties.
/// </summary>
public class Bubble
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float Radius;
    public uint Color;

    /// <summary>
    /// The type/color of the bubble for matching purposes.
    /// </summary>
    public int BubbleType;

    public Bubble(Vector2 position, Vector2 velocity, float radius, uint color, int bubbleType)
    {
        this.Position = position;
        this.Velocity = velocity;
        this.Radius = radius;
        this.Color = color;
        this.BubbleType = bubbleType;
    }

    /// <summary>
    /// Draws the bubble on the screen.
    /// </summary>
    /// <param name="drawList">The ImGui draw list to render to.</param>
    /// <param name="windowPos">The top-left position of the game window.</param>
    public void Draw(ImDrawListPtr drawList, Vector2 windowPos)
    {
        drawList.AddCircleFilled(windowPos + this.Position, this.Radius, this.Color);
    }
}
