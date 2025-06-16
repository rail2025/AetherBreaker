using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AetherBreaker.Game;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace AetherBreaker.Windows;

/// <summary>
/// The main window for the AetherBreaker game.
/// It handles rendering all game elements, processing user input, and managing the overall game state.
/// </summary>
public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly GameBoard gameBoard;
    private Vector2 launcherPosition;
    private Bubble? activeBubble;
    private Bubble? nextBubble;
    private readonly Random random = new();

    private readonly List<BubbleAnimation> activeBubbleAnimations = new();
    private readonly List<TextAnimation> activeTextAnimations = new();

    // Game State Fields
    private int currentStage;
    private bool isGameOver;
    private bool isPaused;
    private bool isStageCleared;
    private bool showHelperLine;
    private int score;
    private int shotsUntilDrop;
    private float timeUntilDrop;
    private float maxTimeForStage;

    // Game Constants
    private const float BubbleRadius = 30f;
    private const int MaxShots = 8;
    private const float BaseMaxTime = 30.0f;
    public static readonly Vector2 WindowSize = new(BubbleRadius * 2 * 9, BubbleRadius * 2 * 12);
    private const float BubbleSpeed = 1200f;

    public MainWindow(Plugin plugin) : base("AetherBreaker")
    {
        this.plugin = plugin;
        this.gameBoard = new GameBoard(BubbleRadius);
        this.Size = WindowSize;
        this.SizeCondition = ImGuiCond.Always;
        this.Flags |= ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        StartNewGame();
    }

    private void StartNewGame()
    {
        this.currentStage = 1;
        SetupStage();
    }

    private void SetupStage()
    {
        this.isGameOver = false;
        this.isPaused = false;
        this.isStageCleared = false;
        this.shotsUntilDrop = MaxShots;
        this.maxTimeForStage = BaseMaxTime - ((this.currentStage - 1) / 2 * 0.5f);
        this.timeUntilDrop = this.maxTimeForStage;
        this.activeBubble = null;
        this.activeBubbleAnimations.Clear();
        this.activeTextAnimations.Clear();
        this.gameBoard.InitializeBoard(this.currentStage);
        this.nextBubble = CreateRandomBubble();
        this.showHelperLine = this.currentStage <= 2;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        if (this.plugin.Configuration.IsGameWindowLocked) this.Flags |= ImGuiWindowFlags.NoMove;
        else this.Flags &= ~ImGuiWindowFlags.NoMove;
    }

    public override void Draw()
    {
        var windowPos = ImGui.GetWindowPos();
        this.launcherPosition = new Vector2(windowPos.X + WindowSize.X * 0.5f, windowPos.Y + WindowSize.Y - 50f);
        var drawList = ImGui.GetWindowDrawList();

        if (!this.isGameOver && !this.isStageCleared && !this.isPaused && !ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows))
            this.isPaused = true;

        this.gameBoard.Draw(drawList, windowPos);

        if (this.isGameOver) HandleGameOver(windowPos, drawList);
        else if (this.isStageCleared) HandleStageCleared(windowPos, drawList);
        else if (this.isPaused) HandlePaused(windowPos, drawList);
        else UpdateGameLogic(windowPos, drawList);

        UpdateAndDrawBubbleAnimations(drawList, windowPos);
        UpdateAndDrawTextAnimations(drawList, windowPos);
        DrawGameUI(windowPos);
    }

    private void UpdateGameLogic(Vector2 windowPos, ImDrawListPtr drawList)
    {
        UpdateTimers();
        UpdateActiveBubble(windowPos, drawList);
        DrawLauncherAndAiming(drawList, windowPos); // Draw launcher last to cover bubble tail
        if (this.gameBoard.IsGameOver()) this.isGameOver = true;
    }

    private void UpdateTimers()
    {
        this.timeUntilDrop -= ImGui.GetIO().DeltaTime;
        if (this.timeUntilDrop <= 0)
        {
            var droppedBubbles = this.gameBoard.AdvanceCeiling();
            if (droppedBubbles.Any())
                this.activeBubbleAnimations.Add(new BubbleAnimation(droppedBubbles, BubbleAnimationType.Drop, 1.5f));

            this.timeUntilDrop = this.maxTimeForStage;
            this.shotsUntilDrop = MaxShots;
        }
    }

    private void DrawGameUI(Vector2 windowPos)
    {
        var drawList = ImGui.GetWindowDrawList();
        var scoreText = $"Score: {this.score}";
        var stageText = $"Stage: {this.currentStage}";
        var scorePos = new Vector2(windowPos.X + 15, windowPos.Y + WindowSize.Y - 60f);
        drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize() * 1.5f, scorePos, ImGui.GetColorU32(new Vector4(1, 1, 1, 1)), scoreText);
        var scoreTextSize = ImGui.CalcTextSize(scoreText) * 1.5f;
        ImGui.SetCursorScreenPos(new Vector2(scorePos.X, scorePos.Y + scoreTextSize.Y));
        ImGui.Text(stageText);

        if (!isGameOver && !isStageCleared && !isPaused)
        {
            var pauseButtonSize = new Vector2(50, 25);
            var pauseButtonPos = new Vector2(this.launcherPosition.X + 80, this.launcherPosition.Y - pauseButtonSize.Y / 2);
            ImGui.SetCursorScreenPos(pauseButtonPos);
            if (ImGui.Button("Pause", pauseButtonSize)) this.isPaused = true;

            var debugButtonSize = ImGui.CalcTextSize("Win Stage") + new Vector2(10, 5);
            var debugButtonPos = new Vector2(this.launcherPosition.X - 80 - debugButtonSize.X, this.launcherPosition.Y - debugButtonSize.Y / 2);
            ImGui.SetCursorScreenPos(debugButtonPos);
            if (ImGui.Button("Win Stage", debugButtonSize))
            {
                this.gameBoard.ClearAllColoredBubbles();
                this.isStageCleared = true;
                this.score += 1000 * this.currentStage;
            }
        }

        var shotsText = $"Shots: {this.shotsUntilDrop}";
        var timeText = $"Time: {this.timeUntilDrop:F1}s";
        var shotsTextSize = ImGui.CalcTextSize(shotsText);
        var timeTextSize = ImGui.CalcTextSize(timeText);
        ImGui.SetCursorScreenPos(new Vector2(windowPos.X + WindowSize.X - shotsTextSize.X - 10, windowPos.Y + WindowSize.Y - 45f));
        ImGui.Text(shotsText);
        ImGui.SetCursorScreenPos(new Vector2(windowPos.X + WindowSize.X - timeTextSize.X - 10, windowPos.Y + WindowSize.Y - 25f));
        ImGui.Text(timeText);
    }

    private void HandleGameOver(Vector2 windowPos, ImDrawListPtr drawList)
    {
        var text = "GAME OVER";
        var textSize = ImGui.CalcTextSize(text) * 2;
        var textPos = new Vector2(windowPos.X + (WindowSize.X - textSize.X) * 0.5f, windowPos.Y + (WindowSize.Y - textSize.Y) * 0.5f);
        drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize() * 2, textPos, ImGui.GetColorU32(new Vector4(1, 1, 1, 1)), text);
        var buttonSize = new Vector2(100, 30);
        var buttonPos = new Vector2(windowPos.X + (WindowSize.X - buttonSize.X) * 0.5f, textPos.Y + textSize.Y + 20);
        ImGui.SetCursorScreenPos(buttonPos);
        if (ImGui.Button("Restart", buttonSize)) StartNewGame();
    }

    private void HandleStageCleared(Vector2 windowPos, ImDrawListPtr drawList)
    {
        var text = "STAGE CLEARED!";
        var textSize = ImGui.CalcTextSize(text) * 2;
        var textPos = new Vector2(windowPos.X + (WindowSize.X - textSize.X) * 0.5f, windowPos.Y + (WindowSize.Y - textSize.Y) * 0.5f);
        drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize() * 2, textPos, ImGui.GetColorU32(new Vector4(1, 1, 0, 1)), text);
        var buttonText = $"Continue to Stage {this.currentStage + 1}";
        var buttonSize = ImGui.CalcTextSize(buttonText) + new Vector2(20, 10);
        var buttonPos = new Vector2(windowPos.X + (WindowSize.X - buttonSize.X) * 0.5f, textPos.Y + textSize.Y + 20);
        ImGui.SetCursorScreenPos(buttonPos);
        if (ImGui.Button(buttonText, buttonSize))
        {
            this.currentStage++;
            SetupStage();
        }
    }

    private void HandlePaused(Vector2 windowPos, ImDrawListPtr drawList)
    {
        var text = "PAUSED";
        var textSize = ImGui.CalcTextSize(text) * 2;
        var textPos = new Vector2(windowPos.X + (WindowSize.X - textSize.X) * 0.5f, windowPos.Y + (WindowSize.Y - textSize.Y) * 0.5f);
        drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize() * 2, textPos, ImGui.GetColorU32(new Vector4(1, 1, 1, 1)), text);
        var buttonSize = new Vector2(80, 30);
        var restartPos = new Vector2(windowPos.X + (WindowSize.X / 2) - buttonSize.X - 10, textPos.Y + textSize.Y + 20);
        var resumePos = new Vector2(windowPos.X + (WindowSize.X / 2) + 10, textPos.Y + textSize.Y + 20);
        ImGui.SetCursorScreenPos(restartPos);
        if (ImGui.Button("Restart", buttonSize)) StartNewGame();
        ImGui.SetCursorScreenPos(resumePos);
        if (ImGui.Button("Resume", buttonSize)) this.isPaused = false;
    }

    private void DrawLauncherAndAiming(ImDrawListPtr drawList, Vector2 windowPos)
    {
        var launcherBaseRadius = BubbleRadius * 1.2f;
        drawList.AddCircleFilled(this.launcherPosition, launcherBaseRadius, ImGui.GetColorU32(new Vector4(0.8f, 0.8f, 0.8f, 1.0f)));
        if (this.nextBubble != null)
            drawList.AddCircleFilled(this.launcherPosition, this.nextBubble.Radius, this.nextBubble.Color);
        if (ImGui.IsWindowHovered())
        {
            var mousePos = ImGui.GetMousePos();
            if (mousePos.Y < this.launcherPosition.Y)
            {
                var direction = Vector2.Normalize(mousePos - this.launcherPosition);
                if (direction.Y > -0.1f)
                {
                    direction.Y = -0.1f;
                    direction = Vector2.Normalize(direction);
                }

                if (this.showHelperLine)
                    DrawHelperLine(drawList, direction, windowPos);
                else
                    drawList.AddLine(this.launcherPosition, this.launcherPosition + direction * 150f, ImGui.GetColorU32(new Vector4(1, 1, 1, 0.5f)), 3f);

                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && this.activeBubble == null)
                    FireBubble(direction, windowPos);
            }
        }
    }

    private void DrawHelperLine(ImDrawListPtr drawList, Vector2 direction, Vector2 windowPos)
    {
        var pathPoints = PredictHelperLinePath(this.launcherPosition - windowPos, direction * BubbleSpeed);
        var color = ImGui.GetColorU32(new Vector4(1, 1, 1, 0.3f));
        for (int i = 0; i < pathPoints.Count - 1; i++)
        {
            drawList.AddLine(windowPos + pathPoints[i], windowPos + pathPoints[i + 1], color, 2f);
        }
    }

    private void FireBubble(Vector2 direction, Vector2 windowPos)
    {
        if (this.nextBubble == null) return;
        this.activeBubble = this.nextBubble;
        this.activeBubble.Position = this.launcherPosition - windowPos;
        this.activeBubble.Velocity = direction * BubbleSpeed;
        this.nextBubble = CreateRandomBubble();
        this.shotsUntilDrop--;
        this.timeUntilDrop = this.maxTimeForStage;
        if (this.shotsUntilDrop <= 0)
        {
            var droppedBubbles = this.gameBoard.AdvanceCeiling();
            if (droppedBubbles.Any())
                this.activeBubbleAnimations.Add(new BubbleAnimation(droppedBubbles, BubbleAnimationType.Drop, 1.5f));
            this.shotsUntilDrop = MaxShots;
        }
    }

    private Bubble CreateRandomBubble()
    {
        var bubbleTypes = this.gameBoard.GetAvailableBubbleTypesOnBoard();
        var selectedType = bubbleTypes[this.random.Next(bubbleTypes.Length)];
        return new Bubble(Vector2.Zero, Vector2.Zero, BubbleRadius, selectedType.Color, selectedType.Type);
    }

    private void UpdateActiveBubble(Vector2 windowPos, ImDrawListPtr drawList)
    {
        if (this.activeBubble == null) return;

        // Move the bubble one step.
        this.activeBubble.Position += this.activeBubble.Velocity * ImGui.GetIO().DeltaTime;

        // Check for wall collisions and correct the position to prevent getting stuck.
        if (this.activeBubble.Position.X - BubbleRadius < 0)
        {
            this.activeBubble.Velocity.X *= -1;
            this.activeBubble.Position.X = BubbleRadius;
        }
        else if (this.activeBubble.Position.X + BubbleRadius > WindowSize.X)
        {
            this.activeBubble.Velocity.X *= -1;
            this.activeBubble.Position.X = WindowSize.X - BubbleRadius;
        }

        // Check for collision with the static bubbles on the board.
        if (this.gameBoard.CheckCollision(this.activeBubble))
        {
            var clearResult = this.gameBoard.AddBubble(this.activeBubble);
            this.activeBubble = null; // Bubble is no longer active
            if (clearResult.TotalScore > 0)
            {
                this.score += clearResult.TotalScore;

                if (clearResult.PoppedBubbles.Any(b => b.BubbleType == -2) || clearResult.DroppedBubbles.Any(b => b.BubbleType == -2))
                    this.showHelperLine = true;

                if (clearResult.PoppedBubbles.Any())
                    this.activeBubbleAnimations.Add(new BubbleAnimation(clearResult.PoppedBubbles, BubbleAnimationType.Pop, 0.2f));
                if (clearResult.DroppedBubbles.Any())
                {
                    this.activeBubbleAnimations.Add(new BubbleAnimation(clearResult.DroppedBubbles, BubbleAnimationType.Drop, 1.5f));
                    foreach (var b in clearResult.DroppedBubbles) this.activeTextAnimations.Add(new TextAnimation("+20", b.Position, ImGui.GetColorU32(new Vector4(1, 1, 0.5f, 1)), 0.7f, TextAnimationType.FloatAndFade));
                }
                foreach (var b in clearResult.PoppedBubbles) this.activeTextAnimations.Add(new TextAnimation("+10", b.Position, ImGui.GetColorU32(new Vector4(1, 1, 1, 1)), 0.7f, TextAnimationType.FloatAndFade));

                if (clearResult.ComboMultiplier > 1)
                {
                    var bonusPosition = clearResult.DroppedBubbles.Aggregate(Vector2.Zero, (acc, b) => acc + b.Position) / clearResult.DroppedBubbles.Count;
                    this.activeTextAnimations.Add(new TextAnimation($"x{clearResult.ComboMultiplier} COMBO!", bonusPosition, ImGui.GetColorU32(new Vector4(1f, 0.6f, 0f, 1f)), 2.5f, TextAnimationType.FadeOut, 1.8f));
                }

                if (this.gameBoard.AreAllColoredBubblesCleared())
                {
                    this.isStageCleared = true;
                    this.score += 1000 * this.currentStage;
                }
            }
            return;
        }

        // If no collision, draw the active bubble at its new position.
        drawList.AddCircleFilled(windowPos + this.activeBubble.Position, this.activeBubble.Radius, this.activeBubble.Color);
    }

    private void UpdateAndDrawBubbleAnimations(ImDrawListPtr drawList, Vector2 windowPos)
    {
        for (int i = this.activeBubbleAnimations.Count - 1; i >= 0; i--)
        {
            var anim = this.activeBubbleAnimations[i];
            if (anim.Update())
            {
                foreach (var bubble in anim.AnimatedBubbles)
                {
                    var scale = anim.GetCurrentScale();
                    if (scale > 0.01f)
                        drawList.AddCircleFilled(windowPos + bubble.Position, bubble.Radius * scale, bubble.Color);
                }
            }
            else
            {
                this.activeBubbleAnimations.RemoveAt(i);
            }
        }
    }

    private void UpdateAndDrawTextAnimations(ImDrawListPtr drawList, Vector2 windowPos)
    {
        for (int i = this.activeTextAnimations.Count - 1; i >= 0; i--)
        {
            var anim = this.activeTextAnimations[i];
            if (anim.Update())
            {
                var color = anim.GetCurrentColor();
                var textPos = windowPos + anim.Position;
                if (anim.IsBonus)
                {
                    var outlineColor = ImGui.GetColorU32(new Vector4(1, 1, 1, (color >> 24) / 255f));
                    drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize() * anim.Scale, textPos + new Vector2(-1, -1), outlineColor, anim.Text);
                    drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize() * anim.Scale, textPos + new Vector2(1, -1), outlineColor, anim.Text);
                    drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize() * anim.Scale, textPos + new Vector2(-1, 1), outlineColor, anim.Text);
                    drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize() * anim.Scale, textPos + new Vector2(1, 1), outlineColor, anim.Text);
                }
                drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize() * anim.Scale, textPos, color, anim.Text);
            }
            else
            {
                this.activeTextAnimations.RemoveAt(i);
            }
        }
    }

    private List<Vector2> PredictHelperLinePath(Vector2 startPos, Vector2 velocity)
    {
        var pathPoints = new List<Vector2> { startPos };
        var currentPos = startPos;
        var currentVel = velocity;
        int bounces = 0;

        for (int i = 0; i < 400; i++)
        {
            currentPos += currentVel * 0.01f;

            if (currentPos.X - BubbleRadius < 0)
            {
                currentVel.X *= -1;
                currentPos.X = BubbleRadius;
                pathPoints.Add(currentPos);
                bounces++;
            }
            else if (currentPos.X + BubbleRadius > WindowSize.X)
            {
                currentVel.X *= -1;
                currentPos.X = WindowSize.X - BubbleRadius;
                pathPoints.Add(currentPos);
                bounces++;
            }

            var tempBubble = new Bubble(currentPos, currentVel, BubbleRadius, 0, 0);
            if (this.gameBoard.CheckCollision(tempBubble))
            {
                pathPoints.Add(currentPos);
                return pathPoints;
            }
            if (bounces >= 5) return pathPoints;
        }
        pathPoints.Add(currentPos);
        return pathPoints;
    }
}
