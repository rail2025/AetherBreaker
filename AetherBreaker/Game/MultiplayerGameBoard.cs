using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace AetherBreaker.Game;

/// <summary>
/// Manages the multiplayer game board using an abstract, unit-based coordinate system.
/// </summary>
public class MultiplayerGameBoard
{
    public List<Bubble> Bubbles { get; private set; } = new();

    public float AbstractWidth { get; private set; }
    public float AbstractHeight { get; private set; }

    private const float BubbleRadius = 1.0f;
    private const float GridSpacing = 2.0f;

    private readonly int gameBoardWidthInBubbles;
    public readonly (uint Color, int Type)[] allBubbleColorTypes;

    private float ceilingY;
    private readonly Random random;
    public readonly Bubble CeilingBubble;

    public const int PowerUpType = -2;
    public const int BombType = -3;
    public const int StarType = -4;
    public const int PaintType = -5;
    public const int MirrorType = -6;

    public MultiplayerGameBoard(int seed)
    {
        this.CeilingBubble = new Bubble(Vector2.Zero, Vector2.Zero, 0, 0, -99);
        this.random = new Random(seed);

        this.gameBoardWidthInBubbles = 9;
        this.AbstractWidth = this.gameBoardWidthInBubbles * GridSpacing;

        this.allBubbleColorTypes = new[]
        {
            (Color: 4280221439, Type: 0), // Red
            (Color: 4280123647, Type: 1), // Green
            (Color: 4294901760, Type: 2), // Blue
            (Color: 4280252415, Type: 3)  // Yellow
        };
    }

    public void InitializeBoard()
    {
        this.Bubbles.Clear();
        this.ceilingY = BubbleRadius;
        var tempBubbles = new List<Bubble>();

        var numRows = 5;

        for (var row = 0; row < numRows; row++)
        {
            int bubblesInThisRow = this.gameBoardWidthInBubbles - (row % 2);
            for (var col = 0; col < bubblesInThisRow; col++)
            {
                var x = (col * GridSpacing) + BubbleRadius + (row % 2 == 1 ? BubbleRadius : 0);
                var y = row * (GridSpacing * 0.866f) + this.ceilingY;
                var bubbleType = this.allBubbleColorTypes[this.random.Next(this.allBubbleColorTypes.Length)];
                tempBubbles.Add(new Bubble(new Vector2(x, y), Vector2.Zero, BubbleRadius, bubbleType.Color, bubbleType.Type));
            }
        }

        this.AbstractHeight = (numRows - 1) * (GridSpacing * 0.866f) + (2 * BubbleRadius);
        AddPowerUpsToBubbleList(tempBubbles);
        this.Bubbles = tempBubbles;
    }

    // NOTE: DrawBoardChrome has been removed.
    // NOTE: IsGameOver has been removed.

    private void AddPowerUpsToBubbleList(List<Bubble> bubbleList)
    {
        var candidates = bubbleList.Where(b => b.BubbleType >= 0).ToList();
        if (!candidates.Any()) return;
        if (this.random.Next(100) < 15)
        {
            var helperBubble = candidates[this.random.Next(candidates.Count)];
            helperBubble.BubbleType = PowerUpType;
            helperBubble.Color = GetBubbleDetails(PowerUpType).Color;
            candidates.Remove(helperBubble);
        }
        if (this.random.Next(100) < 10 && candidates.Any())
        {
            var bombBubble = candidates[this.random.Next(candidates.Count)];
            bombBubble.BubbleType = BombType;
            bombBubble.Color = GetBubbleDetails(BombType).Color;
            candidates.Remove(bombBubble);
        }
        if (this.random.Next(100) < 10 && candidates.Any())
        {
            var paintBubble = candidates[this.random.Next(candidates.Count)];
            paintBubble.BubbleType = PaintType;
            paintBubble.Color = GetBubbleDetails(PaintType).Color;
            candidates.Remove(paintBubble);
        }
        if (this.random.Next(100) < 5 && candidates.Any())
        {
            var starBubble = candidates[this.random.Next(candidates.Count)];
            starBubble.BubbleType = StarType;
            starBubble.Color = GetBubbleDetails(StarType).Color;
        }
    }

    public void AdvanceAndRefillBoard()
    {
        var rowHeight = GridSpacing * 0.866f;
        foreach (var bubble in this.Bubbles)
        {
            bubble.Position.Y += rowHeight;
        }
        this.ceilingY += rowHeight;
        this.AbstractHeight += rowHeight;

        var newRow = new List<Bubble>();
        for (var col = 0; col < this.gameBoardWidthInBubbles; col++)
        {
            var x = (col * GridSpacing) + BubbleRadius;
            var y = BubbleRadius; // Top-most row
            var bubbleType = this.allBubbleColorTypes[this.random.Next(this.allBubbleColorTypes.Length)];
            newRow.Add(new Bubble(new Vector2(x, y), Vector2.Zero, BubbleRadius, bubbleType.Color, bubbleType.Type));
        }
        AddPowerUpsToBubbleList(newRow);
        this.Bubbles.AddRange(newRow);
    }

    public Bubble? FindCollision(Bubble activeBubble)
    {
        if (activeBubble.Position.Y - activeBubble.Radius <= this.ceilingY)
            return this.CeilingBubble;
        return this.Bubbles.FirstOrDefault(bubble => Vector2.Distance(activeBubble.Position, bubble.Position) < GridSpacing);
    }

    public Vector2 GetSnappedPosition(Vector2 landingPosition, Bubble? collidedWith)
    {
        var nearbyBubbles = this.Bubbles.Where(b => Vector2.Distance(b.Position, landingPosition) < GridSpacing * 1.5f);
        var closestBubble = collidedWith ?? nearbyBubbles.OrderBy(b => Vector2.Distance(b.Position, landingPosition)).FirstOrDefault();

        if (closestBubble == null || closestBubble == this.CeilingBubble)
        {
            int row = (int)Math.Round((landingPosition.Y - this.ceilingY) / (GridSpacing * 0.866f));
            var gridX = (float)Math.Round((landingPosition.X - BubbleRadius - (row % 2 == 1 ? BubbleRadius : 0)) / GridSpacing);
            var x = (gridX * GridSpacing) + BubbleRadius + (row % 2 == 1 ? BubbleRadius : 0);
            return new Vector2(x, landingPosition.Y);
        }
        Vector2 bestPosition = landingPosition;
        float closestDist = float.MaxValue;
        for (int i = 0; i < 6; i++)
        {
            var angle = MathF.PI / 3f * i;
            var neighborPos = closestBubble.Position + new Vector2(MathF.Sin(angle), MathF.Cos(angle)) * GridSpacing;
            if (!this.Bubbles.Any(b => Vector2.Distance(b.Position, neighborPos) < GridSpacing * 0.9f))
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
                        result.PoppedBubbles.Add(powerUp);
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
        foreach (var neighbor in GetNeighbors(locationBubble))
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

    public void AddJunkScatterShotToBottom(int attackStrength)
    {
        if (!this.Bubbles.Any()) return;
        var attachPoints = new List<Vector2>();
        var lowestY = this.Bubbles.Max(b => b.Position.Y);
        var bottomBubbles = this.Bubbles.Where(b => b.Position.Y > lowestY - GridSpacing).ToList();
        foreach (var bubble in bottomBubbles)
        {
            for (int i = 0; i < 6; i++)
            {
                var angle = (MathF.PI / 3f * i) + (MathF.PI / 6f);
                var neighborPos = bubble.Position + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * GridSpacing;
                if (neighborPos.Y > bubble.Position.Y)
                {
                    if (!this.Bubbles.Any(b => Vector2.Distance(b.Position, neighborPos) < BubbleRadius))
                        attachPoints.Add(neighborPos);
                }
            }
        }
        if (!attachPoints.Any()) return;
        var junkCluster = new List<Bubble>();
        var startAttachPoint = attachPoints[this.random.Next(attachPoints.Count)];
        var mirrorBubble = new Bubble(startAttachPoint, Vector2.Zero, BubbleRadius, GetBubbleDetails(MirrorType).Color, MirrorType);
        junkCluster.Add(mirrorBubble);
        for (int i = 1; i < attackStrength && junkCluster.Count < 50; i++)
        {
            var parentBubble = junkCluster[this.random.Next(junkCluster.Count)];
            var angle = (MathF.PI / 3f) * this.random.Next(6);
            var newPos = parentBubble.Position + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * GridSpacing;
            bool isSpotFree = !junkCluster.Any(b => Vector2.Distance(b.Position, newPos) < BubbleRadius)
                              && !this.Bubbles.Any(b => Vector2.Distance(b.Position, newPos) < BubbleRadius);
            if (isSpotFree && newPos.X > 0 && newPos.X < this.AbstractWidth)
            {
                var newMirrorBubble = new Bubble(newPos, Vector2.Zero, BubbleRadius, GetBubbleDetails(MirrorType).Color, MirrorType);
                junkCluster.Add(newMirrorBubble);
            }
        }
        this.Bubbles.AddRange(junkCluster);
    }

    public byte[] SerializeBoardState()
    {
        if (!this.Bubbles.Any()) return Array.Empty<byte>();
        using (var ms = new MemoryStream())
        using (var writer = new BinaryWriter(ms))
        {
            writer.Write(this.Bubbles.Count);
            foreach (var bubble in this.Bubbles)
            {
                writer.Write(bubble.Position.X);
                writer.Write(bubble.Position.Y);
                writer.Write((byte)bubble.BubbleType);
            }
            return ms.ToArray();
        }
    }

    public float GetBubbleRadius() => BubbleRadius;

    public (uint Color, int Type) GetBubbleDetails(int bubbleType)
    {
        switch (bubbleType)
        {
            case PowerUpType: return (4289864447, PowerUpType);
            case BombType: return (4280153855, BombType);
            case StarType: return (4280252159, StarType);
            case PaintType: return (4294934527, PaintType);
            case MirrorType: return (4294309324, MirrorType);
        }
        foreach (var details in this.allBubbleColorTypes)
        {
            if (details.Type == bubbleType) return details;
        }
        return this.allBubbleColorTypes[0];
    }
}
