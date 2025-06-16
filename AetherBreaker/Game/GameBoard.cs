using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AetherBreaker.Windows;
using ImGuiNET;

namespace AetherBreaker.Game;

/// <summary>
/// Manages the hexagonal grid of bubbles, including collision, snapping, and game logic.
/// </summary>
public class GameBoard
{
    private List<Bubble> bubbles = new();
    private readonly float bubbleRadius;
    private readonly float gridSpacing;
    private float ceilingY;
    private readonly Random random = new();

    private const int GameBoardWidth = 8;
    private readonly float gameOverLineY;
    private readonly (uint Color, int Type)[] allBubbleColorTypes;
    private readonly float boardWidth;

    public GameBoard(float bubbleRadius)
    {
        this.bubbleRadius = bubbleRadius;
        this.gridSpacing = bubbleRadius * 2;
        this.gameOverLineY = bubbleRadius * 2 * 10;
        this.boardWidth = (GameBoardWidth * this.gridSpacing) - this.bubbleRadius;
        this.allBubbleColorTypes = new[]
        {
            (Color: ImGui.GetColorU32(new Vector4(1.0f, 0.2f, 0.2f, 1.0f)), Type: 0),
            (Color: ImGui.GetColorU32(new Vector4(0.2f, 1.0f, 0.2f, 1.0f)), Type: 1),
            (Color: ImGui.GetColorU32(new Vector4(0.2f, 0.2f, 1.0f, 1.0f)), Type: 2),
            (Color: ImGui.GetColorU32(new Vector4(1.0f, 1.0f, 0.2f, 1.0f)), Type: 3)
        };
        InitializeBoard(1);
    }

    /// <summary>
    /// Sets up the board for a specific stage.
    /// </summary>
    public void InitializeBoard(int stage)
    {
        this.bubbles.Clear();
        this.ceilingY = this.bubbleRadius;
        var tempBubbles = new List<Bubble>();
        var padding = (MainWindow.WindowSize.X - this.boardWidth) / 2f;

        // Step 1: Generate a full board of colored bubbles.
        for (var row = 0; row < 5; row++)
        {
            for (var col = 0; col < (GameBoardWidth - (row % 2)); col++)
            {
                var x = padding + col * this.gridSpacing + (row % 2 == 1 ? this.bubbleRadius : 0);
                var y = row * (this.gridSpacing * 0.866f) + this.ceilingY;
                var bubbleType = this.allBubbleColorTypes[this.random.Next(this.allBubbleColorTypes.Length)];
                tempBubbles.Add(new Bubble(new Vector2(x, y), Vector2.Zero, this.bubbleRadius, bubbleType.Color, bubbleType.Type));
            }
        }

        // Step 2: From Stage 3+, add special bubbles.
        if (stage >= 3)
        {
            // Add one purple power-up bubble in a random valid position.
            int powerUpAttempts = 0;
            while (powerUpAttempts < 20)
            {
                var candidateIndex = this.random.Next(tempBubbles.Count);
                if (tempBubbles[candidateIndex].Position.Y > this.ceilingY) // Don't place in the top-most row
                {
                    var powerUpBubble = tempBubbles[candidateIndex];
                    powerUpBubble.BubbleType = -2; // Special type for the helper line power-up
                    powerUpBubble.Color = ImGui.GetColorU32(new Vector4(0.5f, 0.2f, 1.0f, 1.0f)); // Purple
                    tempBubbles[candidateIndex] = powerUpBubble;
                    break;
                }
                powerUpAttempts++;
            }

            // Convert a percentage of the remaining colored bubbles to black bubbles.
            var bubblesToConvert = (int)(tempBubbles.Count * 0.15f);
            for (int i = 0; i < bubblesToConvert; i++)
            {
                int blackBubbleAttempts = 0;
                while (blackBubbleAttempts < 10)
                {
                    var candidate = tempBubbles[this.random.Next(tempBubbles.Count)];
                    // Ensure we don't convert the power-up or a bubble in the top row.
                    if (candidate.BubbleType >= 0 && candidate.Position.Y > this.ceilingY)
                    {
                        candidate.BubbleType = -1; // Black bubble type
                        candidate.Color = ImGui.GetColorU32(new Vector4(0.1f, 0.1f, 0.1f, 1.0f));
                        break;
                    }
                    blackBubbleAttempts++;
                }
            }
        }
        this.bubbles = tempBubbles;
    }

    /// <summary>
    /// Renders all bubbles and their special icons if applicable.
    /// </summary>
    public void Draw(ImDrawListPtr drawList, Vector2 windowPos)
    {
        foreach (var bubble in this.bubbles)
        {
            var bubbleScreenPos = windowPos + bubble.Position;
            drawList.AddCircleFilled(bubbleScreenPos, bubble.Radius, bubble.Color);

            if (bubble.BubbleType == -1) // Black bubble
            {
                drawList.AddCircle(bubbleScreenPos, bubble.Radius, ImGui.GetColorU32(new Vector4(0.5f, 0.5f, 0.5f, 1.0f)), 12, 1.5f);
            }
            else if (bubble.BubbleType == -2) // Purple power-up bubble
            {
                var dashColor = ImGui.GetColorU32(new Vector4(1, 1, 1, 0.9f));
                drawList.AddLine(bubbleScreenPos - new Vector2(bubble.Radius * 0.5f, 0), bubbleScreenPos + new Vector2(bubble.Radius * 0.5f, 0), dashColor, 3f);
            }
        }

        var padding = (MainWindow.WindowSize.X - this.boardWidth) / 2f;
        drawList.AddLine(windowPos + new Vector2(padding - this.bubbleRadius, this.ceilingY - this.bubbleRadius), windowPos + new Vector2(this.boardWidth + padding + this.bubbleRadius, this.ceilingY - this.bubbleRadius), ImGui.GetColorU32(new Vector4(1, 1, 1, 0.5f)), 1f);
        drawList.AddLine(windowPos + new Vector2(0, this.gameOverLineY), windowPos + new Vector2(MainWindow.WindowSize.X, this.gameOverLineY), ImGui.GetColorU32(new Vector4(1, 0, 0, 0.5f)), 2f);
    }


    public void ClearAllColoredBubbles()
    {
        this.bubbles.RemoveAll(b => b.BubbleType != -1);
    }

    public List<Bubble> AdvanceCeiling()
    {
        var dropDistance = this.bubbleRadius;
        this.ceilingY += dropDistance;
        foreach (var bubble in this.bubbles)
            bubble.Position.Y += dropDistance;
        return RemoveDisconnectedBubbles();
    }

    public bool CheckCollision(Bubble activeBubble)
    {
        if (activeBubble.Position.Y - activeBubble.Radius <= this.ceilingY) return true;
        return this.bubbles.Any(bubble => Vector2.Distance(activeBubble.Position, bubble.Position) < this.gridSpacing);
    }

    public ClearResult AddBubble(Bubble bubble)
    {
        bubble.Position = SnapToGridOnCollision(bubble.Position);
        bubble.Velocity = Vector2.Zero;
        this.bubbles.Add(bubble);
        return CheckForMatches(bubble);
    }

    private Vector2 SnapToGridOnCollision(Vector2 landingPosition)
    {
        var closestBubble = this.bubbles.OrderBy(b => Vector2.Distance(b.Position, landingPosition)).FirstOrDefault();
        if (closestBubble == null)
        {
            var padding = (MainWindow.WindowSize.X - this.boardWidth) / 2f;
            var x = (float)Math.Round((landingPosition.X - padding) / this.gridSpacing) * this.gridSpacing + padding;
            return new Vector2(x, this.ceilingY);
        }
        Vector2 bestPosition = landingPosition;
        float closestDist = float.MaxValue;
        for (int i = 0; i < 6; i++)
        {
            var angle = MathF.PI / 3f * i;
            var neighborPos = closestBubble.Position + new Vector2(MathF.Sin(angle), MathF.Cos(angle)) * this.gridSpacing;
            if (!this.bubbles.Any(b => Vector2.Distance(b.Position, neighborPos) < this.bubbleRadius))
            {
                float dist = Vector2.Distance(landingPosition, neighborPos);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    bestPosition = neighborPos;
                }
            }
        }
        return bestPosition;
    }

    private ClearResult CheckForMatches(Bubble newBubble)
    {
        var result = new ClearResult();
        var connected = FindConnectedBubbles(newBubble);
        if (connected.Count >= 3)
        {
            foreach (var bubble in connected)
                this.bubbles.Remove(bubble);
            result.PoppedBubbles.AddRange(connected);
            result.DroppedBubbles.AddRange(RemoveDisconnectedBubbles());
            result.CalculateScore();
        }
        return result;
    }

    private List<Bubble> RemoveDisconnectedBubbles()
    {
        if (!this.bubbles.Any()) return new List<Bubble>();
        var connectedToCeiling = new HashSet<Bubble>();
        var queue = new Queue<Bubble>();
        foreach (var bubble in this.bubbles.Where(b => b.Position.Y - b.Radius <= this.ceilingY))
        {
            if (connectedToCeiling.Add(bubble)) queue.Enqueue(bubble);
        }
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var neighbor in GetNeighbors(current))
            {
                if (connectedToCeiling.Add(neighbor)) queue.Enqueue(neighbor);
            }
        }
        var dropped = this.bubbles.Where(b => !connectedToCeiling.Contains(b)).ToList();
        this.bubbles.RemoveAll(b => !connectedToCeiling.Contains(b));
        return dropped;
    }

    private List<Bubble> FindConnectedBubbles(Bubble start)
    {
        if (start.BubbleType < 0) return new List<Bubble>(); // Black or Purple bubbles don't match
        var connected = new HashSet<Bubble>();
        var queue = new Queue<Bubble>();
        if (connected.Add(start)) queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var neighbor in GetNeighbors(current))
            {
                if (neighbor.BubbleType == start.BubbleType && connected.Add(neighbor))
                    queue.Enqueue(neighbor);
            }
        }
        return connected.ToList();
    }

    private IEnumerable<Bubble> GetNeighbors(Bubble bubble)
    {
        return this.bubbles.Where(other => bubble != other && Vector2.Distance(bubble.Position, other.Position) <= this.gridSpacing * 1.1f);
    }

    public bool IsGameOver() => this.bubbles.Any(bubble => bubble.Position.Y + this.bubbleRadius >= this.gameOverLineY);

    public bool AreAllColoredBubblesCleared() => !this.bubbles.Any(b => b.BubbleType >= 0);

    public (uint Color, int Type)[] GetAvailableBubbleTypesOnBoard()
    {
        var activeTypes = this.bubbles.Where(b => b.BubbleType >= 0).Select(b => b.BubbleType).Distinct().ToList();
        var availableColors = this.allBubbleColorTypes.Where(c => activeTypes.Contains(c.Type)).ToArray();
        return availableColors.Any() ? availableColors : this.allBubbleColorTypes;
    }
}
