using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace AetherBreaker.Game;

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

    private bool nextRowIsStaggered = false;

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
            ((uint)0xFF2727F5, 0), // Red
            ((uint)0xFF22B922, 1), // Green
            ((uint)0xFFD3B01A, 2), // Blue
            ((uint)0xFF1AD3D3, 3)  // Yellow
        };
    }

    public void InitializeBoard()
    {
        this.Bubbles.Clear();
        this.ceilingY = BubbleRadius;
        var tempBubbles = new List<Bubble>();

        var numRows = 3;

        for (var row = 0; row < numRows; row++)
        {
            int bubblesInThisRow = this.gameBoardWidthInBubbles - (row % 2);
            for (var col = 0; col < bubblesInThisRow; col++)
            {
                var x = (col * GridSpacing) + BubbleRadius + (row % 2 == 1 ? BubbleRadius : 0);
                var y = row * (GridSpacing * (MathF.Sqrt(3) / 2f)) + this.ceilingY;
                var bubbleType = this.allBubbleColorTypes[this.random.Next(this.allBubbleColorTypes.Length)];
                tempBubbles.Add(new Bubble(new Vector2(x, y), Vector2.Zero, BubbleRadius, bubbleType.Color, bubbleType.Type));
            }
        }

        this.nextRowIsStaggered = (numRows % 2) != 0;
        this.AbstractHeight = (numRows > 0 ? (numRows - 1) * (GridSpacing * (MathF.Sqrt(3) / 2f)) : 0) + (2 * BubbleRadius);
        AddPowerUpsToBubbleList(tempBubbles);
        this.Bubbles = tempBubbles;
    }

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
        if (this.random.Next(100) < 30 && candidates.Any())
        {
            var bombBubble = candidates[this.random.Next(candidates.Count)];
            bombBubble.BubbleType = BombType;
            bombBubble.Color = GetBubbleDetails(BombType).Color;
            candidates.Remove(bombBubble);
        }
        if (this.random.Next(100) < 20 && candidates.Any())
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
        var rowHeight = GridSpacing * (MathF.Sqrt(3) / 2f);

        foreach (var bubble in this.Bubbles)
        {
            bubble.Position.Y += rowHeight;
        }
        this.AbstractHeight += rowHeight;

        var newRow = new List<Bubble>();
        int bubblesInThisRow = this.gameBoardWidthInBubbles - (this.nextRowIsStaggered ? 1 : 0);
        float xOffset = this.nextRowIsStaggered ? BubbleRadius : 0;

        for (var col = 0; col < bubblesInThisRow; col++)
        {
            var x = (col * GridSpacing) + BubbleRadius + xOffset;
            var y = this.ceilingY;
            var bubbleType = this.allBubbleColorTypes[this.random.Next(this.allBubbleColorTypes.Length)];
            newRow.Add(new Bubble(new Vector2(x, y), Vector2.Zero, BubbleRadius, bubbleType.Color, bubbleType.Type));
        }

        this.nextRowIsStaggered = !this.nextRowIsStaggered;

        AddPowerUpsToBubbleList(newRow);
        this.Bubbles.AddRange(newRow);

        
    }

    public void AddJunkToBottom(int junkBubbleCount)
    {
        if (!this.Bubbles.Any() || junkBubbleCount <= 0) return;

        var occupiedSlots = this.Bubbles.Select(b => b.Position).ToHashSet();
        var attachPoints = new HashSet<Vector2>();

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
                    var snappedAttachPoint = GetClosestTheoreticalSlot(neighborPos);
                    if (!occupiedSlots.Contains(snappedAttachPoint))
                    {
                        attachPoints.Add(snappedAttachPoint);
                    }
                }
            }
        }

        var validAttachPoints = attachPoints.ToList();
        if (!validAttachPoints.Any()) return;

        int bubblesRemaining = junkBubbleCount;
        while (bubblesRemaining > 0 && validAttachPoints.Any())
        {
            int clusterSize = random.Next(2, Math.Min(bubblesRemaining, 5) + 1);
            bubblesRemaining -= clusterSize;

            var startAttachPoint = validAttachPoints[this.random.Next(validAttachPoints.Count)];
            validAttachPoints.RemoveAll(p => Vector2.Distance(p, startAttachPoint) < GridSpacing * 3);

            var mirrorBubble = new Bubble(startAttachPoint, Vector2.Zero, BubbleRadius, GetBubbleDetails(MirrorType).Color, MirrorType);
            this.Bubbles.Add(mirrorBubble);
            occupiedSlots.Add(startAttachPoint);

            for (int i = 1; i < clusterSize; i++)
            {
                var parentBubble = this.Bubbles.Last(b => b.BubbleType == MirrorType);
                var angle = (MathF.PI / 3f) * this.random.Next(6);
                var newPos = parentBubble.Position + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * GridSpacing;

                var snappedNewPos = GetSnappedPosition(newPos, parentBubble);
                if (snappedNewPos != parentBubble.Position)
                {
                    var newMirrorBubble = new Bubble(snappedNewPos, Vector2.Zero, BubbleRadius, GetBubbleDetails(MirrorType).Color, MirrorType);
                    this.Bubbles.Add(newMirrorBubble);
                    occupiedSlots.Add(snappedNewPos);
                }
            }
        }
    }

    public Bubble? FindCollision(Bubble activeBubble)
    {
        if (activeBubble.Position.Y - activeBubble.Radius <= this.ceilingY)
            return this.CeilingBubble;

        var candidates = this.Bubbles.Where(b => Vector2.Distance(activeBubble.Position, b.Position) < GridSpacing);
        return candidates.OrderBy(b => Vector2.Distance(activeBubble.Position, b.Position)).FirstOrDefault();
    }

    public Vector2 GetSnappedPosition(Vector2 landingPosition, Bubble? collidedWith)
    {
        var occupiedSlots = this.Bubbles.Select(b => b.Position).ToHashSet();
        var stickableSlots = new HashSet<Vector2>();

        int topRowBubbleCount = this.gameBoardWidthInBubbles - (nextRowIsStaggered ? 1 : 0);
        float topRowXOffset = nextRowIsStaggered ? BubbleRadius : 0;

        for (var col = 0; col < topRowBubbleCount; col++)
        {
            var x = (col * GridSpacing) + BubbleRadius + topRowXOffset;
            var y = this.ceilingY;
            var slot = new Vector2(x, y);
            if (!occupiedSlots.Contains(slot))
            {
                stickableSlots.Add(slot);
            }
        }

        foreach (var bubble in this.Bubbles)
        {
            for (int i = 0; i < 6; i++)
            {
                var angle = MathF.PI / 3f * i;
                var neighborPos = bubble.Position + new Vector2(MathF.Sin(angle), MathF.Cos(angle)) * GridSpacing;

                var snappedNeighborPos = GetClosestTheoreticalSlot(neighborPos);

                if (!occupiedSlots.Contains(snappedNeighborPos))
                {
                    stickableSlots.Add(snappedNeighborPos);
                }
            }
        }

        if (!stickableSlots.Any())
        {
            return GetClosestTheoreticalSlot(landingPosition);
        }

        return stickableSlots.OrderBy(slot => Vector2.Distance(landingPosition, slot)).First();
    }

    private Vector2 GetClosestTheoreticalSlot(Vector2 position)
    {
        int row = (int)Math.Round((position.Y - this.ceilingY) / (GridSpacing * (MathF.Sqrt(3) / 2f)));

        // --- Start of Fix ---
        // Determine the stagger of the CURRENT top row (row 0).
        // The 'nextRowIsStaggered' flag tells us the stagger for the *next* row to be created,
        // which is the opposite of the current top row's stagger.
        bool isTopRowStaggered = !this.nextRowIsStaggered;

        // Determine if the target row should be staggered based on the top row's stagger.
        bool isTargetRowStaggered;
        if (isTopRowStaggered)
        {
            isTargetRowStaggered = (row % 2 == 0); // If top is staggered, all even rows are.
        }
        else
        {
            isTargetRowStaggered = (row % 2 != 0); // If top is not, all odd rows are.
        }

        float xOffset = isTargetRowStaggered ? BubbleRadius : 0;
        // --- End of Fix ---

        var gridX = (float)Math.Round((position.X - xOffset - BubbleRadius) / GridSpacing);

        var x = (gridX * GridSpacing) + BubbleRadius + xOffset;
        var y = row * (GridSpacing * (MathF.Sqrt(3) / 2f)) + this.ceilingY;

        return new Vector2(x, y);
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
    public float GetCeilingY() => this.ceilingY;

    public (uint Color, int Type) GetBubbleDetails(int bubbleType)
    {
        switch (bubbleType)
        {
            case PowerUpType: return ((uint)0xFFC000C0, PowerUpType);
            case BombType: return ((uint)0xFF1F75E6, BombType);
            case StarType: return ((uint)0xFF24C5F5, StarType);
            case PaintType: return ((uint)0xFF9A5CF5, PaintType);
            case MirrorType: return ((uint)0xFFCCCCCC, MirrorType);
        }
        foreach (var details in this.allBubbleColorTypes)
        {
            if (details.Type == bubbleType) return details;
        }
        return this.allBubbleColorTypes[0];
    }
}
