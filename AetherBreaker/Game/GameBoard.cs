using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;

namespace AetherBreaker.Game
{
    /// <summary>
    /// Manages the hexagonal grid system for bubble placement and game logic.
    /// Handles bubble matching, clearing, and floating bubble detection.
    /// </summary>
    public class GameBoard
    {
        /// <summary>
        /// Stores all bubbles currently placed on the game board.
        /// </summary>
        private List<Bubble> bubbles = new List<Bubble>();

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
            this.gridSpacing = bubbleRadius * 1.8f;
        }

        /// <summary>
        /// Finds the nearest valid grid position that connects to existing bubbles or ceiling.
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

            List<Vector2> positions = new List<Vector2>();
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
            if (activeBubble.Position.Y - activeBubble.Radius <= ceilingY + 2f)
            {
                return true;
            }

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

                CheckForMatches(bubble);
                return true;
            }
            return false;
        }

        private void CheckForMatches(Bubble newBubble)
        {
            var connected = FindConnectedBubbles(newBubble);
            if (connected.Count >= 3)
            {
                foreach (var bubble in connected)
                {
                    bubbles.Remove(bubble);
                }
            }
        }

        public void RemoveDisconnectedBubbles()
        {
            var connectedToCeiling = new HashSet<Bubble>();
            var queue = new Queue<Bubble>();

            var ceilingBubbles = bubbles.Where(b => b.Position.Y - bubbleRadius <= bubbleRadius * 2);
            foreach (var bubble in ceilingBubbles)
            {
                queue.Enqueue(bubble);
                connectedToCeiling.Add(bubble);
            }

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

            bubbles.RemoveAll(b => !connectedToCeiling.Contains(b));
        }

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
                    if (!visited.Contains(neighbor) && neighbor.BubbleType == start.BubbleType)
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }
            return visited;
        }

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

        public void Draw(ImDrawListPtr drawList)
        {
            foreach (var bubble in bubbles)
            {
                drawList.AddCircleFilled(bubble.Position, bubble.Radius, bubble.Color);
            }
        }
    }
}
