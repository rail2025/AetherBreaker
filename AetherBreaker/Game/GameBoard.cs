using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AetherBreaker.Windows;
using ImGuiNET;

namespace AetherBreaker.Game;

/// <summary>
/// Manages the hexagonal grid of bubbles, including collision, snapping, and game logic.
/// The layout is now dynamically calculated based on the provided bubble radius.
/// </summary>
public class GameBoard
{
    /// <summary>
    /// Gets the list of all bubbles currently on the game board.
    /// </summary>
    public List<Bubble> Bubbles { get; private set; } = new();

    private readonly float bubbleRadius;
    private readonly float gridSpacing;
    private float ceilingY;
    private readonly Random random = new();

    private readonly int gameBoardWidthInBubbles;
    private readonly float gameOverLineY;
    private readonly (uint Color, int Type)[] allBubbleColorTypes;
    private readonly float boardWidth;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameBoard"/> class.
    /// </summary>
    /// <param name="radius">The radius to use for all bubbles on this board.</param>
    public GameBoard(float radius)
    {
        this.bubbleRadius = radius;
        this.gridSpacing = this.bubbleRadius * 2;

        if (this.bubbleRadius > 35f) this.gameBoardWidthInBubbles = 7;
        else if (this.bubbleRadius < 25f) this.gameBoardWidthInBubbles = 11;
        else this.gameBoardWidthInBubbles = 8;

        this.boardWidth = (this.gameBoardWidthInBubbles * this.gridSpacing) - this.bubbleRadius;
        this.gameOverLineY = 30f * 2 * 10;

        this.allBubbleColorTypes = new[]
        {
            (Color: ImGui.GetColorU32(new Vector4(1.0f, 0.2f, 0.2f, 1.0f)), Type: 0), // Red = DPS
            (Color: ImGui.GetColorU32(new Vector4(0.2f, 1.0f, 0.2f, 1.0f)), Type: 1), // Green = Healer
            (Color: ImGui.GetColorU32(new Vector4(0.2f, 0.2f, 1.0f, 1.0f)), Type: 2), // Blue = Tank
            (Color: ImGui.GetColorU32(new Vector4(1.0f, 1.0f, 0.2f, 1.0f)), Type: 3)  // Yellow = Chocobo
        };
    }

    /// <summary>
    /// Generates the initial layout of bubbles for a given stage.
    /// </summary>
    /// <param name="stage">The current stage number, used to determine difficulty.</param>
    public void InitializeBoard(int stage)
    {
        this.Bubbles.Clear();
        this.ceilingY = this.bubbleRadius;
        var tempBubbles = new List<Bubble>();
        var padding = (MainWindow.WindowSize.X - this.boardWidth) / 2f;

        var numRows = 5;
        if (this.bubbleRadius > 35f) numRows = 4;
        else if (this.bubbleRadius < 25f) numRows = 6;

        for (var row = 0; row < numRows; row++)
        {
            for (var col = 0; col < (this.gameBoardWidthInBubbles - (row % 2)); col++)
            {
                var x = padding + col * this.gridSpacing + (row % 2 == 1 ? this.bubbleRadius : 0);
                var y = row * (this.gridSpacing * 0.866f) + this.ceilingY;
                var bubbleType = this.allBubbleColorTypes[this.random.Next(this.allBubbleColorTypes.Length)];
                tempBubbles.Add(new Bubble(new Vector2(x, y), Vector2.Zero, this.bubbleRadius, bubbleType.Color, bubbleType.Type));
            }
        }

        if (stage >= 3)
        {
            var middleRowY = (2 * (this.gridSpacing * 0.866f)) + this.ceilingY;
            var lowerHalfCandidates = tempBubbles.Where(b => b.Position.Y > middleRowY).ToList();
            if (lowerHalfCandidates.Any())
            {
                var powerUpCandidateIndex = this.random.Next(lowerHalfCandidates.Count);
                var powerUpBubble = lowerHalfCandidates[powerUpCandidateIndex];
                var originalIndex = tempBubbles.IndexOf(powerUpBubble);
                if (originalIndex != -1)
                {
                    powerUpBubble.BubbleType = -2;
                    powerUpBubble.Color = ImGui.GetColorU32(new Vector4(0.5f, 0.2f, 1.0f, 1.0f));
                    tempBubbles[originalIndex] = powerUpBubble;
                }
            }

            var bubblesToConvert = (int)(tempBubbles.Count * 0.15f);
            var leftBoundary = padding + this.gridSpacing;
            var rightBoundary = padding + this.boardWidth - this.gridSpacing;
            var blackBubblePositions = new List<Vector2>();

            for (int i = 0; i < bubblesToConvert; i++)
            {
                int attempts = 0;
                while (attempts < 20)
                {
                    attempts++;
                    var candidate = tempBubbles[this.random.Next(tempBubbles.Count)];
                    if (candidate.BubbleType < 0 || candidate.Position.X <= leftBoundary || candidate.Position.X >= rightBoundary) continue;
                    bool tooClose = blackBubblePositions.Any(pos => Vector2.Distance(candidate.Position, pos) < this.gridSpacing * 2.0f);
                    if (tooClose) continue;

                    candidate.BubbleType = -1;
                    candidate.Color = ImGui.GetColorU32(new Vector4(0.1f, 0.1f, 0.1f, 1.0f));
                    blackBubblePositions.Add(candidate.Position);
                    break;
                }
            }
        }

        if (stage >= 5)
        {
            int numBombs = stage >= 10 ? 3 : this.random.Next(1, 3); // 1-2 for stages 5-9, 3 for 10+
            var bombCandidates = tempBubbles.Where(b => b.BubbleType >= 0).ToList();

            for (int i = 0; i < numBombs && bombCandidates.Any(); i++)
            {
                int candidateIndex = this.random.Next(bombCandidates.Count);
                var bombBubble = bombCandidates[candidateIndex];

                bombBubble.BubbleType = -3; // Bomb type
                bombBubble.Color = ImGui.GetColorU32(new Vector4(0.9f, 0.4f, 0.1f, 1.0f));

                bombCandidates.RemoveAt(candidateIndex);
            }
        }

        this.Bubbles = tempBubbles;
    }

    /// <summary>
    /// Draws the non-bubble elements of the game board, like the ceiling and game-over lines.
    /// </summary>
    public void DrawBoardChrome(ImDrawListPtr drawList, Vector2 windowPos)
    {
        var padding = (MainWindow.WindowSize.X - this.boardWidth) / 2f;
        drawList.AddLine(windowPos + new Vector2(padding - this.bubbleRadius, this.ceilingY - this.bubbleRadius), windowPos + new Vector2(this.boardWidth + padding + this.bubbleRadius, this.ceilingY - this.bubbleRadius), ImGui.GetColorU32(new Vector4(1, 1, 1, 0.5f)), 1f);
        drawList.AddLine(windowPos + new Vector2(0, this.gameOverLineY), windowPos + new Vector2(MainWindow.WindowSize.X, this.gameOverLineY), ImGui.GetColorU32(new Vector4(1, 0, 0, 0.5f)), 2f);
    }

    /// <summary>
    /// Determines the correct grid position for a bubble that has just collided with the board.
    /// </summary>
    private Vector2 SnapToGridOnCollision(Vector2 landingPosition)
    {
        var nearbyBubbles = this.Bubbles.Where(b => Vector2.Distance(b.Position, landingPosition) < this.gridSpacing * 1.5f);
        var closestBubble = nearbyBubbles.OrderBy(b => Vector2.Distance(b.Position, landingPosition)).FirstOrDefault();

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

            if (!this.Bubbles.Any(b => Vector2.Distance(b.Position, neighborPos) < this.gridSpacing * 0.9f))
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

    /// <summary>
    /// Advances the ceiling downwards and returns any bubbles that were pushed past the game-over line.
    /// </summary>
    public List<Bubble> AdvanceCeiling()
    {
        var dropDistance = this.bubbleRadius;
        this.ceilingY += dropDistance;
        foreach (var bubble in this.Bubbles)
            bubble.Position.Y += dropDistance;
        return RemoveDisconnectedBubbles();
    }

    /// <summary>
    /// Checks if the provided active bubble is colliding with any bubble on the board or the ceiling.
    /// </summary>
    public bool CheckCollision(Bubble activeBubble)
    {
        if (activeBubble.Position.Y - activeBubble.Radius <= this.ceilingY) return true;
        return this.Bubbles.Any(bubble => Vector2.Distance(activeBubble.Position, bubble.Position) < this.gridSpacing);
    }

    /// <summary>
    /// Detonates a bomb, removing all bubbles within a specified radius.
    /// </summary>
    /// <param name="bombPosition">The center position of the explosion.</param>
    /// <returns>A list of bubbles removed by the explosion.</returns>
    public List<Bubble> DetonateBomb(Vector2 bombPosition)
    {
        var blastRadius = this.gridSpacing * 2f;
        var clearedBubbles = new List<Bubble>();

        for (int i = this.Bubbles.Count - 1; i >= 0; i--)
        {
            var bubble = this.Bubbles[i];
            if (Vector2.Distance(bombPosition, bubble.Position) <= blastRadius)
            {
                clearedBubbles.Add(bubble);
                this.Bubbles.RemoveAt(i);
            }
        }
        return clearedBubbles;
    }

    /// <summary>
    /// Adds a new bubble to the board, snapping it to the grid and checking for matches.
    /// </summary>
    public ClearResult AddBubble(Bubble bubble)
    {
        bubble.Position = SnapToGridOnCollision(bubble.Position);
        bubble.Velocity = Vector2.Zero;

        if (bubble.BubbleType == -3) // It's a bomb
        {
            this.Bubbles.Add(bubble);
            var result = new ClearResult();
            var blastVictims = DetonateBomb(bubble.Position);
            result.PoppedBubbles.AddRange(blastVictims);
            result.CalculateScore();
            return result;
        }

        this.Bubbles.Add(bubble);
        return CheckForMatches(bubble);
    }

    /// <summary>
    /// Checks for a color match of 3 or more bubbles starting from the newly added bubble.
    /// </summary>
    private ClearResult CheckForMatches(Bubble newBubble)
    {
        var result = new ClearResult();
        var connected = FindConnectedBubbles(newBubble);
        if (connected.Count >= 3)
        {
            var neighbors = connected.SelectMany(GetNeighbors).Distinct().ToList();
            var bystanderBombs = neighbors.Where(n => n.BubbleType == -3).ToList();
            var bystanderPowerUps = neighbors.Where(n => n.BubbleType == -2).ToList();

            foreach (var bubble in connected)
                this.Bubbles.Remove(bubble);
            result.PoppedBubbles.AddRange(connected);

            if (bystanderPowerUps.Any())
            {
                result.HelperLineActivated = true;
                foreach (var powerUp in bystanderPowerUps)
                {
                    if (this.Bubbles.Remove(powerUp))
                    {
                        result.PoppedBubbles.Add(powerUp);
                    }
                }
            }

            if (bystanderBombs.Any())
            {
                foreach (var bomb in bystanderBombs)
                {
                    if (this.Bubbles.Contains(bomb))
                    {
                        var blastVictims = DetonateBomb(bomb.Position);
                        result.PoppedBubbles.AddRange(blastVictims);
                    }
                }
            }

            result.DroppedBubbles.AddRange(RemoveDisconnectedBubbles());
            result.CalculateScore();
        }
        return result;
    }

    /// <summary>
    /// Finds and removes all bubbles that are no longer connected to the ceiling.
    /// </summary>
    private List<Bubble> RemoveDisconnectedBubbles()
    {
        if (!this.Bubbles.Any()) return new List<Bubble>();
        var connectedToCeiling = new HashSet<Bubble>();
        var queue = new Queue<Bubble>();
        foreach (var bubble in this.Bubbles.Where(b => b.Position.Y - b.Radius <= this.ceilingY))
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
        var dropped = this.Bubbles.Where(b => !connectedToCeiling.Contains(b)).ToList();
        this.Bubbles.RemoveAll(b => !connectedToCeiling.Contains(b));
        return dropped;
    }

    /// <summary>
    /// Finds all bubbles of the same type connected to a starting bubble.
    /// </summary>
    private List<Bubble> FindConnectedBubbles(Bubble start)
    {
        if (start.BubbleType < 0) return new List<Bubble>();
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

    /// <summary>
    /// Gets all bubbles that are adjacent to a given bubble on the hexagonal grid.
    /// </summary>
    private IEnumerable<Bubble> GetNeighbors(Bubble bubble)
    {
        return this.Bubbles.Where(other => bubble != other && Vector2.Distance(bubble.Position, other.Position) <= this.gridSpacing * 1.1f);
    }

    /// <summary>
    /// Checks if any bubble has crossed the game over line.
    /// </summary>
    public bool IsGameOver() => this.Bubbles.Any(bubble => bubble.Position.Y + this.bubbleRadius >= this.gameOverLineY);

    /// <summary>
    /// Checks if all colored bubbles have been cleared from the board.
    /// </summary>
    public bool AreAllColoredBubblesCleared() => !this.Bubbles.Any(b => b.BubbleType >= 0);

    /// <summary>
    /// Gets a list of bubble types currently present on the board.
    /// </summary>
    public (uint Color, int Type)[] GetAvailableBubbleTypesOnBoard()
    {
        var activeTypes = this.Bubbles.Where(b => b.BubbleType >= 0).Select(b => b.BubbleType).Distinct().ToList();
        var availableColors = this.allBubbleColorTypes.Where(c => activeTypes.Contains(c.Type)).ToArray();
        return availableColors.Any() ? availableColors : this.allBubbleColorTypes;
    }
}
