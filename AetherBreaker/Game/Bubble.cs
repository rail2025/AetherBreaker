using System.Numerics;

namespace AetherBreaker.Game;

public class Bubble
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float Radius;
    public uint Color;

    public Bubble(Vector2 position, Vector2 velocity, float radius, uint color)
    {
        this.Position = position;
        this.Velocity = velocity;
        this.Radius = radius;
        this.Color = color;
    }
}
