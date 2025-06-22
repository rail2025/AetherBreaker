using System.Numerics;
using Serilog.Filters;

namespace AetherBreaker.Game;

/// <summary>
/// Represents a bubble in the game with position, velocity, and visual properties.
/// All positional and size data is stored in an unscaled coordinate system.
/// </summary>
public class Bubble
{
    /// <summary>
    /// The unscaled position of the bubble within the game board's coordinate system.
    /// </summary>
    public Vector2 Position;

    /// <summary>
    /// The unscaled velocity of the bubble.
    /// </summary>
    public Vector2 Velocity;

    /// <summary>
    /// The unscaled radius of the bubble.
    /// </summary>
    public float Radius;

    /// <summary>
    /// The display color of the bubble.
    /// </summary>
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
