using System;
using System.Numerics;
using ImGuiNET;

namespace AetherBreaker.Game;

/// <summary>
/// Defines the behavior types for text animations.
/// </summary>
public enum TextAnimationType
{
    /// <summary>
    /// Text floats upwards and fades out over its duration.
    /// </summary>
    FloatAndFade,

    /// <summary>
    /// Text remains stationary and fades out over its duration.
    /// </summary>
    FadeOut
}

/// <summary>
/// Manages a piece of text being animated on screen, such as a score popup.
/// </summary>
public class TextAnimation
{
    /// <summary>
    /// Gets the text content to be displayed.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// The current position of the text, relative to the window's content area.
    /// </summary>
    public Vector2 Position;

    /// <summary>
    /// Gets the initial color of the text before fading is applied.
    /// </summary>
    public uint InitialColor { get; }

    /// <summary>
    /// Gets the behavior type of the animation.
    /// </summary>
    public TextAnimationType Type { get; }

    /// <summary>
    /// Gets the font scaling factor for this text animation.
    /// </summary>
    public float Scale { get; }

    /// <summary>
    /// Gets a value indicating whether this animation is for a special bonus text.
    /// </summary>
    public bool IsBonus => this.Type == TextAnimationType.FadeOut;

    private readonly float startTime;
    private readonly float duration;
    private readonly Vector2 velocity;

    /// <summary>
    /// Initializes a new instance of the <see cref="TextAnimation"/> class.
    /// </summary>
    /// <param name="text">The text to display.</param>
    /// <param name="startPosition">The starting position of the text, relative to the window.</param>
    /// <param name="color">The starting color of the text.</param>
    /// <param name="duration">The total duration of the animation in seconds.</param>
    /// <param name="type">The type of animation behavior.</param>
    /// <param name="scale">The font scaling factor.</param>
    public TextAnimation(string text, Vector2 startPosition, uint color, float duration, TextAnimationType type, float scale = 1.0f)
    {
        this.Text = text;
        this.Position = startPosition;
        this.InitialColor = color;
        this.duration = duration;
        this.Type = type;
        this.Scale = scale;
        this.startTime = (float)ImGui.GetTime();

        if (this.Type == TextAnimationType.FloatAndFade)
        {
            this.velocity = new Vector2(0, -1f); // Move upwards
        }
    }

    /// <summary>
    /// Updates the animation's state for the current frame.
    /// </summary>
    /// <returns>True if the animation is still ongoing; otherwise, false.</returns>
    // CHANGE: The Update method now correctly accepts a deltaTime parameter.
    public bool Update(float deltaTime)
    {
        // For animations that move, update their position based on velocity and frame time.
        if (this.Type == TextAnimationType.FloatAndFade)
        {
            // Use the passed-in deltaTime instead of fetching it from ImGui.
            this.Position += this.velocity * deltaTime;
        }

        // The animation is considered finished once its duration has elapsed.
        return (float)ImGui.GetTime() - this.startTime < this.duration;
    }

    /// <summary>
    /// Calculates the current color of the text, including the fade-out effect.
    /// </summary>
    /// <returns>The color of the text for the current frame as a U32.</returns>
    public uint GetCurrentColor()
    {
        var elapsedTime = (float)ImGui.GetTime() - this.startTime;
        var alpha = 1.0f - (elapsedTime / this.duration);

        var color = ImGui.ColorConvertU32ToFloat4(this.InitialColor);
        color.W = Math.Clamp(alpha, 0.0f, 1.0f); // Adjust alpha for fade effect, ensuring it's between 0 and 1.
        return ImGui.ColorConvertFloat4ToU32(color);
    }
}
