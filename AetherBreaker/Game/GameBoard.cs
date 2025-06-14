using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;

namespace AetherBreaker.Game;

/// <summary>
/// Manages the hexagonal grid system for bubble placement and game logic.
/// Handles bubble matching, clearing, and floating bubble detection.
/// </summary>
public class GameBoard
{
    /// <summary>
    /// Stores all bubbles currently placed on the game board.
    /// </summary>
    private List<Bubble> bubbles = new();

    /// <summary>
    /// The visual radius of each bubble in pixels.
    /// </summary>
    private readonly float bubbleRadius;

    /// <summary>
    /// The distance between centers of adjacent bubbles in the hexagonal grid.
    /// </summary>
    private readonly float gridSpacing;

    /// <summary>
    /// Initializes a new instance of the GameBoard class.
    /// </summary>
    /// <param name="bubbleRadius">The visual radius of bubbles in pixels.</param>
    public GameBoard(float bubbleRadius)
    {
        this.bubbleRadius = bubbleRadius;
        this.gridSpacing = bubbleRadius * 1.8f; // Hexagonal packing factor
    }

    /// <summary>
    /// Finds the nearest valid grid position that connects to existing bubbles or ceiling.
    /// </summary>
    /// <param name="position">The current world position of the bubble.</param>
    /// <param name="ceilingY">The Y-coordinate of the ceiling.</param>
    /// <returns>The closest valid grid position that connects to existing bubbles or ceiling.</returns>
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
                ceilingY + bubbleRadius
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

    /// <summary>
    /// Calculates potential grid positions around an impact point.
    /// </summary>
    /// <param name="position">The impact position to evaluate.</param>
    /// <returns>Array of potential grid positions.</returns>
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

    /// <summary>
    /// Checks if a bubble should stick to existing bubbles or the ceiling.
    /// </summary>
    /// <param name="activeBubble">The moving bubble to check.</param>
    /// <param name="ceilingY">The Y-coordinate of the ceiling.</param>
    /// <returns>True if the bubble should stick, false otherwise.</returns>
    public bool CheckCollision(Bubble activeBubble, float ceilingY)
    {
        // Check ceiling collision with small buffer
        if (activeBubble.Position.Y - activeBubble.Radius <= ceilingY + 2f)
        {
            return true;
        }

        // Check bubble-to-bubble collision with more precise detection
        foreach (var bubble in bubbles)
        {
            if (Vector2.Distance(activeBubble.Position, bubble.Position) <= bubbleRadius * 2.1f)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Adds a bubble to the game board at a valid grid position.
    /// </summary>
    /// <param name="bubble">The bubble to add.</param>
    /// <param name="ceilingY">The Y-coordinate of the ceiling.</param>
    /// <returns>True if the bubble was added successfully, false otherwise.</returns>
    public bool TryAddBubble(Bubble bubble, float ceilingY)
    {
        var snappedPosition = FindValidGridPosition(bubble.Position, ceilingY);
        if (snappedPosition.HasValue)
        {
            bubble.Position = snappedPosition.Value;
            bubbles.Add(bubble);

            // Check for matches after adding
            CheckForMatches(bubble);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Finds and clears groups of 3 or more same-colored bubbles.
    /// </summary>
    /// <param name="newBubble">The newly added bubble that might form a match.</param>
    private void CheckForMatches(Bubble newBubble)
    {
        var connected = FindConnectedBubbles(newBubble);
        if (connected.Count >= 3)
        {
            // Remove all bubbles in the matched group
            foreach (var bubble in connected)
            {
                bubbles.Remove(bubble);
            }

            // Check for floating bubbles after removal
            RemoveFloatingBubbles();
        }
    }

    /// <summary>
    /// Finds all bubbles connected to the starting bubble (same color).
    /// </summary>
    /// <param name="start">The bubble to start the connection search from.</param>
    /// <returns>List of all connected bubbles of the same color.</returns>
    private List<Bubble> FindConnectedBubbles(Bubble start)
    {
        var visited = new List<Bubble>();
        var queue = new Queue<Bubble>();
        queue.Enqueue(start);
        visited.Add(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var neighbor in GetNeighbors(current))
            {
                if (!visited.Contains(neighbor) &&
                    neighbor.BubbleType == start.BubbleType)
                {
                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }
        return visited;
    }

    /// <summary>
    /// Gets all bubbles adjacent to the given bubble.
    /// </summary>
    /// <param name="bubble">The bubble to find neighbors for.</param>
    /// <returns>List of adjacent bubbles.</returns>
    private List<Bubble> GetNeighbors(Bubble bubble)
    {
        var neighbors = new List<Bubble>();
        foreach (var other in bubbles)
        {
            if (bubble == other) continue;
            if (Vector2.Distance(bubble.Position, other.Position) <= gridSpacing * 1.1f)
            {
                neighbors.Add(other);
            }
        }
        return neighbors;
    }

    /// <summary>
    /// Removes bubbles that are not connected to the ceiling.
    /// </summary>
    private void RemoveFloatingBubbles()
    {
        var connectedToCeiling = new HashSet<Bubble>();
        var queue = new Queue<Bubble>();

        // Start with bubbles touching the ceiling (top row)
        foreach (var bubble in bubbles.Where(b => b.Position.Y - b.Radius <= bubbleRadius * 2))
        {
            queue.Enqueue(bubble);
            connectedToCeiling.Add(bubble);
        }

        // Flood fill to find all connected bubbles
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var neighbor in GetNeighbors(current))
            {
                if (!connectedToCeiling.Contains(neighbor))
                {
                    connectedToCeiling.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }

        // Only remove bubbles that are NOT connected to ceiling
        bubbles = bubbles.Where(b => connectedToCeiling.Contains(b)).ToList();
    }

    /// <summary>
    /// Draws all bubbles on the game board.
    /// </summary>
    /// <param name="drawList">The ImGui draw list to render to.</param>
    public void Draw(ImDrawListPtr drawList)
    {
        foreach (var bubble in bubbles)
        {
            drawList.AddCircleFilled(bubble.Position, bubble.Radius, bubble.Color);
        }
    }
}
