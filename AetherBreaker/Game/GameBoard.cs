using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AetherBreaker.Game;

public class GameBoard
{
    public List<Bubble> Bubbles { get; private set; } = new();

    public float AbstractWidth { get; private set; }
    public float AbstractHeight { get; private set; }

    private const float BubbleRadius = 1.0f;
    private const float GridSpacing = 2.0f;

    private readonly int gameBoardWidthInBubbles;
    private readonly (uint Color, int Type)[] allBubbleColorTypes;

    private float ceilingY;
    private readonly Random random = new();
    public readonly Bubble CeilingBubble;

    public const int PowerUpType = -2;
    public const int BombType = -3;
    public const int StarType = -4;
    public const int PaintType = -5;
    public const int MirrorType = -6;
    public const int ChestType = -7;

    public GameBoard(int stage)
    {
        this.CeilingBubble = new Bubble(Vector2.Zero, Vector2.Zero, 0, 0, -99);

        if (stage <= 2) this.gameBoardWidthInBubbles = 7;
        else if (stage >= 10) this.gameBoardWidthInBubbles = 11;
        else this.gameBoardWidthInBubbles = 8;

        this.AbstractWidth = this.gameBoardWidthInBubbles * GridSpacing;

        this.allBubbleColorTypes = new[]
        {
            ((uint)0xFF2727F5, 0), // Red
            ((uint)0xFF22B922, 1), // Green
            ((uint)0xFFD3B01A, 2), // Blue
            ((uint)0xFF1AD3D3, 3)  // Yellow
        };
    }

    public void InitializeBoard(int stage)
    {
        this.Bubbles.Clear();
        this.ceilingY = BubbleRadius;

        List<(uint Color, int Type)> allowedColorList = new();
        switch (stage)
        {
            case 1: allowedColorList.AddRange(allBubbleColorTypes.Take(2)); break;
            case 2: allowedColorList.AddRange(allBubbleColorTypes.Take(3)); break;
            default: allowedColorList.AddRange(allBubbleColorTypes); break;
        }

        var numRows = 5;
        if (this.gameBoardWidthInBubbles == 7) numRows = 4;
        else if (this.gameBoardWidthInBubbles == 11) numRows = 6;
        if (stage >= 20) numRows += 2;

        for (var row = 0; row < numRows; row++)
        {
            int bubblesInThisRow = this.gameBoardWidthInBubbles - (row % 2);
            for (var col = 0; col < bubblesInThisRow; col++)
            {
                var x = (col * GridSpacing) + BubbleRadius + (row % 2 == 1 ? BubbleRadius : 0);
                var y = row * (GridSpacing * (MathF.Sqrt(3) / 2f)) + this.ceilingY;
                var bubbleType = allowedColorList[this.random.Next(allowedColorList.Count)];
                this.Bubbles.Add(new Bubble(new Vector2(x, y), Vector2.Zero, BubbleRadius, bubbleType.Color, bubbleType.Type));
            }
        }

        this.AbstractHeight = (numRows > 0 ? (numRows - 1) * (GridSpacing * (MathF.Sqrt(3) / 2f)) : 0) + (2 * BubbleRadius);
        AddSpecialBubblesToBoard(stage, this.Bubbles);
    }

    public Vector2 GetSnappedPosition(Vector2 landingPosition, Bubble? collidedWith)
    {
        var occupiedSlots = this.Bubbles.Select(b => b.Position).ToHashSet();
        var stickableSlots = new HashSet<Vector2>();

        // Add the top row as valid snapping positions
        for (var col = 0; col < this.gameBoardWidthInBubbles; col++)
        {
            var x = (col * GridSpacing) + BubbleRadius;
            var y = this.ceilingY;
            var slot = new Vector2(x, y);
            if (!occupiedSlots.Contains(slot))
            {
                stickableSlots.Add(slot);
            }
        }

        // Find all empty slots that are adjacent to any existing bubble
        foreach (var bubble in this.Bubbles)
        {
            for (int i = 0; i < 6; i++)
            {
                var angle = MathF.PI / 3f * i;
                var neighborPos = bubble.Position + new Vector2(MathF.Sin(angle), MathF.Cos(angle)) * GridSpacing;

                // Round the position to snap it to the theoretical grid, preventing floating point errors
                int row = (int)Math.Round((neighborPos.Y - this.ceilingY) / (GridSpacing * (MathF.Sqrt(3) / 2f)));
                var gridX = (float)Math.Round((neighborPos.X - BubbleRadius - (row % 2 == 1 ? BubbleRadius : 0)) / GridSpacing);
                var snappedNeighborPos = new Vector2(
                    (gridX * GridSpacing) + BubbleRadius + (row % 2 == 1 ? BubbleRadius : 0),
                    row * (GridSpacing * (MathF.Sqrt(3) / 2f)) + this.ceilingY
                );

                if (!occupiedSlots.Contains(snappedNeighborPos))
                {
                    stickableSlots.Add(snappedNeighborPos);
                }
            }
        }

        if (!stickableSlots.Any())
        {
            return landingPosition; // Fallback, should rarely happen
        }

        // Return the closest valid, stickable slot to the landing position
        return stickableSlots.OrderBy(slot => Vector2.Distance(landingPosition, slot)).First();
    }

    public void AddJunkRows(int rowCount)
    {
        if (rowCount <= 0) return;
        float yOffset = rowCount * (GridSpacing * (MathF.Sqrt(3) / 2f));
        foreach (var bubble in this.Bubbles)
        {
            bubble.Position.Y += yOffset;
        }
        this.ceilingY += yOffset;
        this.AbstractHeight += yOffset;

        for (var row = 0; row < rowCount; row++)
        {
            int bubblesInThisRow = this.gameBoardWidthInBubbles - (row % 2);
            for (var col = 0; col < bubblesInThisRow; col++)
            {
                var x = (col * GridSpacing) + BubbleRadius + (row % 2 == 1 ? BubbleRadius : 0);
                var y = (row * (GridSpacing * (MathF.Sqrt(3) / 2f))) + BubbleRadius;
                var junkBubble = new Bubble(new Vector2(x, y), Vector2.Zero, BubbleRadius, (uint)0xFF808080, -1);
                this.Bubbles.Add(junkBubble);
            }
        }
    }

    public List<Bubble> AdvanceCeiling()
    {
        var dropDistance = GridSpacing * (MathF.Sqrt(3) / 2f);
        this.ceilingY += dropDistance;
        this.AbstractHeight += dropDistance;
        foreach (var bubble in this.Bubbles)
            bubble.Position.Y += dropDistance;
        return RemoveDisconnectedBubbles();
    }

    public Bubble? FindCollision(Bubble activeBubble)
    {
        if (activeBubble.Position.Y - activeBubble.Radius <= this.ceilingY)
            return this.CeilingBubble;

        var candidates = this.Bubbles.Where(b => Vector2.Distance(activeBubble.Position, b.Position) < GridSpacing);
        return candidates.OrderBy(b => Vector2.Distance(activeBubble.Position, b.Position)).FirstOrDefault();
    }

    public List<Bubble> DetonateBomb(Vector2 bombPosition)
    {
        var blastRadius = GridSpacing * 2f;
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

    public ClearResult AddBubble(Bubble bubble, Bubble? collidedWith)
    {
        bubble.Position = GetSnappedPosition(bubble.Position, collidedWith);
        bubble.Velocity = Vector2.Zero;
        if (bubble.BubbleType == BombType)
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

    public ClearResult CheckForMatches(Bubble newBubble)
    {
        var result = new ClearResult();
        var connected = FindConnectedBubbles(newBubble);
        if (connected.Count >= 3)
        {
            var neighbors = connected.SelectMany(GetNeighbors).Distinct().ToList();
            var bystanderBombs = neighbors.Where(n => n.BubbleType == BombType).ToList();
            var bystanderPowerUps = neighbors.Where(n => n.BubbleType == PowerUpType).ToList();
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

    private List<Bubble> RemoveDisconnectedBubbles()
    {
        if (!this.Bubbles.Any()) return new List<Bubble>();
        var connectedToCeiling = new HashSet<Bubble>();
        var queue = new Queue<Bubble>();
        foreach (var bubble in this.Bubbles.Where(b => b.Position.Y - b.Radius <= this.ceilingY * 1.1f))
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

    public IEnumerable<Bubble> GetNeighbors(Bubble bubble)
    {
        return this.Bubbles.Where(other => bubble != other && Vector2.Distance(bubble.Position, other.Position) <= GridSpacing * 1.1f);
    }

    public bool AreAllColoredBubblesCleared() => !this.Bubbles.Any(b => b.BubbleType >= 0);

    public (uint Color, int Type)[] GetAvailableBubbleTypesOnBoard()
    {
        var activeTypes = this.Bubbles.Where(b => b.BubbleType >= 0).Select(b => b.BubbleType).Distinct().ToList();
        var availableColors = this.allBubbleColorTypes.Where(c => activeTypes.Contains(c.Type)).ToArray();
        return availableColors.Any() ? availableColors : this.allBubbleColorTypes;
    }

    public float GetBubbleRadius() => BubbleRadius;

    public float GetCeilingY() => this.ceilingY;

    public (uint Color, int Type)[] GetAllBubbleColorTypes() => this.allBubbleColorTypes;

    public List<Bubble> ClearBubblesByType(int bubbleType)
    {
        var bubblesToClear = this.Bubbles.Where(b => b.BubbleType == bubbleType).ToList();
        if (bubblesToClear.Any())
        {
            this.Bubbles.RemoveAll(b => b.BubbleType == bubbleType);
        }
        return bubblesToClear;
    }

    public ClearResult ActivateStar(int colorType)
    {
        var result = new ClearResult();
        result.PoppedBubbles.AddRange(ClearBubblesByType(colorType));
        result.DroppedBubbles.AddRange(RemoveDisconnectedBubbles());
        result.CalculateScore();
        return result;
    }

    public void ActivatePaint(Bubble locationBubble, Bubble colorSourceBubble)
    {
        var targetType = colorSourceBubble.BubbleType;
        var targetColor = colorSourceBubble.Color;
        if (targetType < 0) return;
        var neighbors = GetNeighbors(locationBubble);
        foreach (var neighbor in neighbors)
        {
            if (neighbor.BubbleType >= 0)
            {
                neighbor.BubbleType = targetType;
                neighbor.Color = targetColor;
            }
        }
    }

    public void TransformMirrorBubble(Bubble mirrorBubble, Bubble colorSourceBubble)
    {
        if (colorSourceBubble.BubbleType < 0) return;
        mirrorBubble.BubbleType = colorSourceBubble.BubbleType;
        mirrorBubble.Color = colorSourceBubble.Color;
    }

    public (uint Color, int Type) GetBubbleDetails(int bubbleType)
    {
        foreach (var details in this.allBubbleColorTypes)
        {
            if (details.Type == bubbleType) return details;
        }
        switch (bubbleType)
        {
            case PowerUpType: return ((uint)0xFFC000C0, PowerUpType);
            case BombType: return ((uint)0xFF1F75E6, BombType);
            case StarType: return ((uint)0xFF24C5F5, StarType);
            case PaintType: return ((uint)0xFF9A5CF5, PaintType);
            case MirrorType: return ((uint)0xFFCCCCCC, MirrorType);
            case ChestType: return ((uint)0xFF3C64A8, ChestType);
            case -1: return ((uint)0xFF000000, -1);
            default: return (this.allBubbleColorTypes[0].Color, this.allBubbleColorTypes[0].Type);
        }
    }

    private void AddSpecialBubblesToBoard(int stage, List<Bubble> tempBubbles)
    {
        if (stage >= 3)
        {
            var middleRowY = (2 * (GridSpacing * (MathF.Sqrt(3) / 2f))) + this.ceilingY;
            var lowerHalfCandidates = tempBubbles.Where(b => b.Position.Y > middleRowY).ToList();
            if (lowerHalfCandidates.Any())
            {
                var powerUpBubble = lowerHalfCandidates[this.random.Next(lowerHalfCandidates.Count)];
                powerUpBubble.BubbleType = PowerUpType;
                powerUpBubble.Color = GetBubbleDetails(PowerUpType).Color;
            }

            var bubblesToConvert = (int)(tempBubbles.Count * 0.15f);
            var leftBoundary = GridSpacing;
            var rightBoundary = this.AbstractWidth - GridSpacing;
            var blackBubblePositions = new List<Vector2>();
            for (int i = 0; i < bubblesToConvert; i++)
            {
                int attempts = 0;
                while (attempts < 20)
                {
                    attempts++;
                    var candidate = tempBubbles[this.random.Next(tempBubbles.Count)];
                    if (candidate.BubbleType < 0 || candidate.Position.X <= leftBoundary || candidate.Position.X >= rightBoundary) continue;
                    if (blackBubblePositions.Any(pos => Vector2.Distance(candidate.Position, pos) < GridSpacing * 2.0f)) continue;
                    candidate.BubbleType = -1;
                    candidate.Color = GetBubbleDetails(-1).Color;
                    blackBubblePositions.Add(candidate.Position);
                    break;
                }
            }
        }
        if (stage >= 5)
        {
            int numBombs = stage >= 10 ? 3 : this.random.Next(1, 3);
            var bombCandidates = tempBubbles.Where(b => b.BubbleType >= 0).ToList();
            for (int i = 0; i < numBombs && bombCandidates.Any(); i++)
            {
                var bombBubble = bombCandidates[this.random.Next(bombCandidates.Count)];
                bombCandidates.Remove(bombBubble);
                bombBubble.BubbleType = BombType;
                bombBubble.Color = GetBubbleDetails(BombType).Color;
            }
        }
        if (stage >= 7 && (stage - 7) % 6 == 0)
        {
            var starCandidates = tempBubbles.Where(b => b.BubbleType >= 0).ToList();
            if (starCandidates.Any())
            {
                var starBubble = starCandidates[this.random.Next(starCandidates.Count)];
                starBubble.BubbleType = StarType;
                starBubble.Color = GetBubbleDetails(StarType).Color;
            }
        }
        if (stage >= 9)
        {
            var paintCandidates = tempBubbles.Where(b => b.BubbleType >= 0).ToList();
            for (int i = 0; i < 2 && paintCandidates.Any(); i++)
            {
                var paintBubble = paintCandidates[this.random.Next(paintCandidates.Count)];
                paintCandidates.Remove(paintBubble);
                paintBubble.BubbleType = PaintType;
                paintBubble.Color = GetBubbleDetails(PaintType).Color;
            }
        }
        if (stage >= 11)
        {
            var mirrorCandidates = tempBubbles.Where(b => b.BubbleType >= 0).ToList();
            for (int i = 0; i < 5 && mirrorCandidates.Any(); i++)
            {
                var mirrorBubble = mirrorCandidates[this.random.Next(mirrorCandidates.Count)];
                mirrorCandidates.Remove(mirrorBubble);
                mirrorBubble.BubbleType = MirrorType;
                mirrorBubble.Color = GetBubbleDetails(MirrorType).Color;
            }
        }
    }
}
