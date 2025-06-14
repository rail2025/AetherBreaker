using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;

namespace AetherBreaker.Game;

public class GameBoard
{
    private readonly List<Bubble> bubbles = new();
    private readonly float bubbleRadius;
    private readonly float gridSpacing;

    public GameBoard(float bubbleRadius)
    {
        this.bubbleRadius = bubbleRadius;
        this.gridSpacing = bubbleRadius * 1.8f;
    }

    /// <summary>
    /// Finds the nearest valid grid position that connects to existing bubbles or ceiling
    /// </summary>
    public Vector2? FindValidGridPosition(Vector2 position, float ceilingY)
    {
        // First check if we're hitting the ceiling
        if (position.Y - bubbleRadius <= ceilingY + 2f)
        {
            int row = 0;
            int col = (int)(position.X / (gridSpacing * 1.5f));
            float xOffset = (row % 2 == 0) ? 0 : gridSpacing * 0.75f;

            return new Vector2(
                col * gridSpacing * 1.5f + xOffset,
                ceilingY + bubbleRadius // Ensure it sits right on the ceiling
            );
        }

        // Then check for nearby bubbles
        Vector2[] potentialPositions = CalculatePotentialGridPositions(position);
        Vector2? closestValidPosition = null;
        float minDistance = float.MaxValue;

        foreach (var potentialPos in potentialPositions)
        {
            float dist = Vector2.Distance(position, potentialPos);

            // Check if this position touches any existing bubbles
            bool touchesBubble = bubbles.Any(b =>
                Vector2.Distance(potentialPos, b.Position) <= bubbleRadius * 2.1f);

            if (touchesBubble && dist < minDistance)
            {
                minDistance = dist;
                closestValidPosition = potentialPos;
            }
        }

        return closestValidPosition;
    }

    private Vector2[] CalculatePotentialGridPositions(Vector2 position)
    {
        int row = (int)(position.Y / (gridSpacing * 0.75f));
        int col = (int)(position.X / (gridSpacing * 1.5f));

        List<Vector2> positions = new();
        for (int r = -1; r <= 1; r++)
        {
            for (int c = -1; c <= 1; c++)
            {
                float xOffset = ((row + r) % 2 == 0) ? 0 : gridSpacing * 0.75f;
                positions.Add(new Vector2(
                    (col + c) * gridSpacing * 1.5f + xOffset,
                    (row + r) * gridSpacing * 0.75f
                ));
            }
        }
        return positions.ToArray();
    }

    public bool CheckCollision(Bubble activeBubble, float ceilingY)
    {
        // Check ceiling collision
        if (activeBubble.Position.Y - activeBubble.Radius <= ceilingY + 2f)
        {
            return true;
        }

        // Check bubble-to-bubble collision
        foreach (var bubble in bubbles)
        {
            if (Vector2.Distance(activeBubble.Position, bubble.Position) <= bubbleRadius * 2.1f)
            {
                return true;
            }
        }

        return false;
    }

    public bool TryAddBubble(Bubble bubble, float ceilingY)
    {
        var snappedPosition = FindValidGridPosition(bubble.Position, ceilingY);
        if (snappedPosition.HasValue)
        {
            bubble.Position = snappedPosition.Value;
            bubbles.Add(bubble);
            return true;
        }
        return false;
    }

    public void Draw(ImDrawListPtr drawList)
    {
        foreach (var bubble in bubbles)
        {
            drawList.AddCircleFilled(bubble.Position, bubble.Radius, bubble.Color);
        }
    }
}
