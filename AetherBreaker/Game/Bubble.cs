using System.Numerics;

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
}
