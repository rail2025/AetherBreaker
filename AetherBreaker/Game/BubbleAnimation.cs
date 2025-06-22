using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;

namespace AetherBreaker.Game;

/// <summary>
/// Defines the types of animations a bubble can have.
/// </summary>
public enum BubbleAnimationType
{
    Pop,
    Drop
}

/// <summary>
/// Manages the state and properties of a single animation, which can affect multiple bubbles.
/// </summary>
public class BubbleAnimation
{
    /// <summary>
    /// Gets the list of bubbles being affected by this animation instance.
    /// </summary>
    public List<Bubble> AnimatedBubbles { get; }

    /// <summary>
    /// Gets the type of animation being performed.
    /// </summary>
    public BubbleAnimationType Type { get; }

    private readonly float startTime;
    private readonly float duration;
    private Vector2 velocity;

    public BubbleAnimation(List<Bubble> bubbles, BubbleAnimationType type, float duration)
    {
        this.AnimatedBubbles = bubbles;
        this.Type = type;
        this.startTime = (float)ImGui.GetTime();
        this.duration = duration;
        this.velocity = new Vector2(0f, 400f);
    }

    /// <summary>
    /// Updates the animation's state over time.
    /// </summary>
    /// <returns>True if the animation is still ongoing, false if it has finished.</returns>
    // CHANGE: The Update method now correctly accepts a deltaTime parameter.
    public bool Update(float deltaTime)
    {
        var elapsedTime = (float)ImGui.GetTime() - this.startTime;
        if (elapsedTime > this.duration)
            return false;

        if (this.Type == BubbleAnimationType.Drop)
        {
            // Use the passed-in deltaTime instead of fetching it from ImGui.
            this.velocity.Y += 2000f * deltaTime;
            for (int i = 0; i < this.AnimatedBubbles.Count; i++)
            {
                var bubble = this.AnimatedBubbles[i];
                bubble.Position += this.velocity * deltaTime;
                this.AnimatedBubbles[i] = bubble;
            }
        }
        return true;
    }

    /// <summary>
    /// Gets the current scale of the bubble for the popping animation.
    /// </summary>
    /// <returns>A float representing the bubble's current scale, from 1.0 to 0.0.</returns>
    public float GetCurrentScale()
    {
        if (this.Type != BubbleAnimationType.Pop) return 1.0f;
        var elapsedTime = (float)ImGui.GetTime() - this.startTime;
        return 1.0f - (elapsedTime / this.duration);
    }
}
