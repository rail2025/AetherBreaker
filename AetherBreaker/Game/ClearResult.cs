using System.Collections.Generic;
using AetherBreaker.Game;

namespace AetherBreaker.Game;

/// <summary>
/// Represents the result of a bubble being added to the board, including any bubbles that were cleared as a consequence.
/// </summary>
public class ClearResult
{
    public List<Bubble> PoppedBubbles { get; } = new();
    public List<Bubble> DroppedBubbles { get; } = new();
    public bool HelperLineActivated { get; set; }
    public int BaseScore { get; private set; }
    public int BonusScore { get; private set; }
    public int ComboMultiplier { get; private set; } = 1;
    public int TotalScore => this.BaseScore + this.BonusScore;

    /// <summary>
    /// Calculates the score based on the bubbles cleared, applying a combo multiplier for large drops.
    /// </summary>
    public void CalculateScore()
    {
        var poppedScore = this.PoppedBubbles.Count * 10;
        var droppedScore = 0;

        // If more than 3 bubbles are dropped, apply a bonus multiplier.
        if (this.DroppedBubbles.Count > 3)
        {
            // The multiplier is based on how many extra bubbles were dropped.
            this.ComboMultiplier = this.DroppedBubbles.Count - 2;
            droppedScore = this.DroppedBubbles.Count * 20 * this.ComboMultiplier;
        }
        else
        {
            droppedScore = this.DroppedBubbles.Count * 20;
        }

        this.BaseScore = poppedScore + droppedScore;
        // For simplicity in this model, BonusScore isn't used separately. The multiplier is baked into the BaseScore.
    }
}
