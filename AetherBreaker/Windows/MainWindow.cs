using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using AetherBreaker.Game;
using Dalamud.Interface;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AetherBreaker.Windows;

/// <summary>
/// Represents the different states the game can be in.
/// </summary>
public enum GameState
{
    MainMenu,
    InGame,
    Paused,
    StageCleared,
    GameOver
}

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
    private GameState currentGameState;
    private int currentStage;
    private int score;
    private int shotsUntilDrop;
    private float timeUntilDrop;
    private float maxTimeForStage;

    /// <summary>
    /// A list of all loaded background textures.
    /// </summary>
    private readonly List<IDalamudTextureWrap> backgroundImages = new();

    // Game Constants
    /// <summary>
    /// The number of stages that will use the same background before cycling.
    /// </summary>
    private const int StagesPerBackground = 3;
    private const float BubbleRadius = 30f;
    private const int MaxShots = 8;
    private const float BaseMaxTime = 30.0f;
    public static readonly Vector2 WindowSize = new(BubbleRadius * 2 * 9, BubbleRadius * 2 * 12);
    private const float BubbleSpeed = 1200f;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    /// <param name="plugin">A reference to the main plugin instance.</param>
    public MainWindow(Plugin plugin) : base("AetherBreaker")
    {
        this.plugin = plugin;
        this.gameBoard = new GameBoard(BubbleRadius);
        this.Size = WindowSize;
        this.SizeCondition = ImGuiCond.Always;
        this.Flags |= ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;

        // Load all available background images from embedded resources.
        LoadBackgroundImages();

        // Start the game in the main menu.
        this.currentGameState = GameState.MainMenu;
    }

    /// <summary>
    /// Disposes of unmanaged resources used by the window, specifically the background textures.
    /// </summary>
    public void Dispose()
    {
        foreach (var bgImage in this.backgroundImages)
        {
            bgImage.Dispose();
        }
        this.backgroundImages.Clear();
    }

    /// <summary>
    /// Scans for and loads all background images from embedded resources.
    /// </summary>
    private void LoadBackgroundImages()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourcePathPrefix = "AetherBreaker.Images.";
        var backgroundResourceNames = assembly.GetManifestResourceNames()
            .Where(r => r.StartsWith(resourcePathPrefix + "background") && r.EndsWith(".png"))
            .OrderBy(r => r)
            .ToList();

        foreach (var resourcePath in backgroundResourceNames)
        {
            try
            {
                using var stream = assembly.GetManifestResourceStream(resourcePath);
                if (stream != null)
                {
                    using var image = Image.Load<Rgba32>(stream);
                    var rgbaBytes = new byte[image.Width * image.Height * 4];
                    image.CopyPixelDataTo(rgbaBytes);
                    var texture = Plugin.TextureProvider.CreateFromRaw(RawImageSpecification.Rgba32(image.Width, image.Height), rgbaBytes);
                    this.backgroundImages.Add(texture);
                    Plugin.Log.Info($"Successfully loaded background: {resourcePath}");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, $"Failed to load background image: {resourcePath}");
            }
        }
    }

    /// <summary>
    /// Resets the entire game to its initial state for a new game.
    /// </summary>
    private void StartNewGame()
    {
        this.score = 0;
        this.currentStage = 1;
        SetupStage();
        this.currentGameState = GameState.InGame;
    }

    /// <summary>
    /// Sets up the game board and state for the current stage.
    /// </summary>
    private void SetupStage()
    {
        this.shotsUntilDrop = MaxShots;
        this.maxTimeForStage = BaseMaxTime - ((this.currentStage - 1) / 2 * 0.5f);
        this.timeUntilDrop = this.maxTimeForStage;
        this.activeBubble = null;
        this.activeBubbleAnimations.Clear();
        this.activeTextAnimations.Clear();
        this.gameBoard.InitializeBoard(this.currentStage);
        this.nextBubble = CreateRandomBubble();
    }

    /// <summary>
    /// Overrides the base PreDraw method to enforce window lock state before drawing.
    /// </summary>
    public override void PreDraw()
    {
        if (this.plugin.Configuration.IsGameWindowLocked) this.Flags |= ImGuiWindowFlags.NoMove;
        else this.Flags &= ~ImGuiWindowFlags.NoMove;
    }

    /// <summary>
    /// The main drawing method for the window, called every frame.
    /// This now acts as a router to different drawing methods based on the current game state.
    /// </summary>
    public override void Draw()
    {
        // Always draw the background first.
        DrawBackground();

        // Route to the correct drawing method based on game state.
        switch (this.currentGameState)
        {
            case GameState.MainMenu:
                DrawMainMenu();
                break;
            case GameState.InGame:
                DrawInGame();
                break;
            case GameState.Paused:
                DrawPausedScreen();
                break;
            case GameState.StageCleared:
                DrawStageClearedScreen();
                break;
            case GameState.GameOver:
                DrawGameOverScreen();
                break;
        }
    }

    /// <summary>
    /// Draws the currently selected background image.
    /// </summary>
    private void DrawBackground()
    {
        if (!this.backgroundImages.Any()) return;

        // Use the first image for the menu, and cycle for in-game.
        var bgIndex = 0;
        if (this.currentGameState != GameState.MainMenu)
        {
            bgIndex = (this.currentStage - 1) / StagesPerBackground;
        }

        var textureToDraw = this.backgroundImages[bgIndex % this.backgroundImages.Count];

        ImGui.SetCursorPos(Vector2.Zero);
        ImGui.Image(textureToDraw.ImGuiHandle, ImGui.GetContentRegionAvail());
    }

    /// <summary>
    /// Draws the Main Menu UI.
    /// </summary>
    private void DrawMainMenu()
    {
        var drawList = ImGui.GetWindowDrawList();
        var windowPos = ImGui.GetWindowPos();
        var title = "AetherBreaker";
        var titleFontSize = ImGui.GetFontSize() * 3.5f;
        var titleSize = ImGui.CalcTextSize(title) * 3.5f;
        var titlePos = new Vector2(windowPos.X + (WindowSize.X - titleSize.X) * 0.5f, windowPos.Y + WindowSize.Y * 0.2f);
        drawList.AddText(ImGui.GetFont(), titleFontSize, titlePos, 0xFFFFFFFF, title);

        var buttonSize = new Vector2(140, 40);
        var startY = WindowSize.Y * 0.5f;

        // Start Game Button
        ImGui.SetCursorPos(new Vector2((WindowSize.X - buttonSize.X) * 0.5f, startY));
        if (ImGui.Button("Start Game", buttonSize))
        {
            StartNewGame();
        }

        // About Button
        ImGui.SetCursorPos(new Vector2((WindowSize.X - buttonSize.X) * 0.5f, startY + 50));
        if (ImGui.Button("About", buttonSize))
        {
            this.plugin.ToggleAboutUI();
        }

        // Settings Button (Bottom Left)
        var settingsButtonSize = new Vector2(80, 25);
        ImGui.SetCursorPos(new Vector2(10, WindowSize.Y - settingsButtonSize.Y - 10));
        if (ImGui.Button("Settings", settingsButtonSize))
        {
            this.plugin.ToggleConfigUI();
        }
    }

    /// <summary>
    /// Draws all UI and handles all logic for when the game is being played.
    /// </summary>
    private void DrawInGame()
    {
        var windowPos = ImGui.GetWindowPos();
        this.launcherPosition = new Vector2(windowPos.X + WindowSize.X * 0.5f, windowPos.Y + WindowSize.Y - 50f);
        var drawList = ImGui.GetWindowDrawList();

        if (!ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows))
        {
            this.currentGameState = GameState.Paused;
        }

        this.gameBoard.Draw(drawList, windowPos);

        UpdateGameLogic(windowPos, drawList);

        UpdateAndDrawBubbleAnimations(drawList, windowPos);
        UpdateAndDrawTextAnimations(drawList, windowPos);
        DrawGameUI(windowPos);
    }

    /// <summary>
    /// Handles the main game logic updates when the game is active.
    /// </summary>
    /// <param name="windowPos">The top-left position of the game window.</param>
    /// <param name="drawList">The ImGui draw list for rendering.</param>
    private void UpdateGameLogic(Vector2 windowPos, ImDrawListPtr drawList)
    {
        UpdateTimers();
        UpdateActiveBubble(windowPos, drawList);
        DrawLauncherAndAiming(drawList, windowPos);
        if (this.gameBoard.IsGameOver())
        {
            this.currentGameState = GameState.GameOver;
        }
    }

    /// <summary>
    /// Updates the timers for the ceiling advance mechanic.
    /// </summary>
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

    /// <summary>
    /// Draws the static in-game UI elements like score, stage, and buttons.
    /// </summary>
    /// <param name="windowPos">The top-left position of the game window.</param>
    private void DrawGameUI(Vector2 windowPos)
    {
        var drawList = ImGui.GetWindowDrawList();
        var baseFontSize = ImGui.GetFontSize();

        // Display High Score (Top Left)
        var highScoreText = $"High Score: {this.plugin.Configuration.HighScore}";
        var highScorePos = new Vector2(windowPos.X + 15, windowPos.Y + 15);
        drawList.AddText(ImGui.GetFont(), baseFontSize * 1.2f, highScorePos, ImGui.GetColorU32(new Vector4(1, 0.9f, 0.3f, 1)), highScoreText);

        // Display Current Score (Below High Score)
        var scoreText = $"Score: {this.score}";
        var scoreTextSize = ImGui.CalcTextSize(scoreText) * 1.5f;
        var scorePos = new Vector2(windowPos.X + 15, highScorePos.Y + scoreTextSize.Y * 0.8f);
        drawList.AddText(ImGui.GetFont(), baseFontSize * 1.5f, scorePos, ImGui.GetColorU32(new Vector4(1, 1, 1, 1)), scoreText);

        // Display Stage (Below Current Score)
        var stageText = $"Stage: {this.currentStage}";
        var stageTextSize = ImGui.CalcTextSize(stageText) * 1.5f;
        var stagePos = new Vector2(windowPos.X + 15, scorePos.Y + stageTextSize.Y);
        ImGui.SetCursorScreenPos(stagePos);
        ImGui.Text(stageText);

        // Pause Button
        var pauseButtonSize = new Vector2(50, 25);
        var pauseButtonPos = new Vector2(this.launcherPosition.X + 80, this.launcherPosition.Y - pauseButtonSize.Y / 2);
        ImGui.SetCursorScreenPos(pauseButtonPos);
        if (ImGui.Button("Pause", pauseButtonSize)) this.currentGameState = GameState.Paused;

        var shotsText = $"Shots: {this.shotsUntilDrop}";
        var timeText = $"Time: {this.timeUntilDrop:F1}s";
        var shotsTextSize = ImGui.CalcTextSize(shotsText);
        var timeTextSize = ImGui.CalcTextSize(timeText);
        ImGui.SetCursorScreenPos(new Vector2(windowPos.X + WindowSize.X - shotsTextSize.X - 10, windowPos.Y + WindowSize.Y - 45f));
        ImGui.Text(shotsText);
        ImGui.SetCursorScreenPos(new Vector2(windowPos.X + WindowSize.X - timeTextSize.X - 10, windowPos.Y + WindowSize.Y - 25f));
        ImGui.Text(timeText);
    }

    /// <summary>
    /// Renders the "Game Over" screen.
    /// </summary>
    private void DrawGameOverScreen()
    {
        // Check for and save a new high score
        if (this.score > this.plugin.Configuration.HighScore)
        {
            this.plugin.Configuration.HighScore = this.score;
            this.plugin.Configuration.Save();
        }

        var windowPos = ImGui.GetWindowPos();
        var drawList = ImGui.GetWindowDrawList();
        var text = "GAME OVER";
        var textSize = ImGui.CalcTextSize(text) * 2;
        var textPos = new Vector2(windowPos.X + (WindowSize.X - textSize.X) * 0.5f, windowPos.Y + (WindowSize.Y - textSize.Y) * 0.5f);
        drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize() * 2, textPos, ImGui.GetColorU32(new Vector4(1, 1, 1, 1)), text);

        var buttonSize = new Vector2(120, 30);
        var buttonPos = new Vector2(windowPos.X + (WindowSize.X - buttonSize.X) * 0.5f, textPos.Y + textSize.Y + 20);
        ImGui.SetCursorScreenPos(buttonPos);
        if (ImGui.Button("Main Menu", buttonSize))
        {
            this.currentGameState = GameState.MainMenu;
        }
    }

    /// <summary>
    /// Renders the "Stage Cleared" screen.
    /// </summary>
    private void DrawStageClearedScreen()
    {
        var windowPos = ImGui.GetWindowPos();
        var drawList = ImGui.GetWindowDrawList();
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
            this.currentGameState = GameState.InGame;
        }
    }

    /// <summary>
    /// Renders the "Paused" screen.
    /// </summary>
    private void DrawPausedScreen()
    {
        // We still want to draw the game board underneath the pause overlay
        var windowPos = ImGui.GetWindowPos();
        var drawList = ImGui.GetWindowDrawList();
        this.gameBoard.Draw(drawList, windowPos);
        DrawGameUI(windowPos);

        var text = "PAUSED";
        var textSize = ImGui.CalcTextSize(text) * 2;
        var textPos = new Vector2(windowPos.X + (WindowSize.X - textSize.X) * 0.5f, windowPos.Y + (WindowSize.Y - textSize.Y) * 0.5f);
        drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize() * 2, textPos, ImGui.GetColorU32(new Vector4(1, 1, 1, 1)), text);

        var buttonSize = new Vector2(100, 30);
        var resumePos = new Vector2(windowPos.X + (WindowSize.X / 2) - buttonSize.X - 10, textPos.Y + textSize.Y + 20);
        var menuPos = new Vector2(windowPos.X + (WindowSize.X / 2) + 10, textPos.Y + textSize.Y + 20);
        ImGui.SetCursorScreenPos(resumePos);
        if (ImGui.Button("Resume", buttonSize))
        {
            this.currentGameState = GameState.InGame;
        }
        ImGui.SetCursorScreenPos(menuPos);
        if (ImGui.Button("Main Menu", buttonSize))
        {
            this.currentGameState = GameState.MainMenu;
        }
    }

    /// <summary>
    /// Fires a bubble from the launcher.
    /// </summary>
    /// <param name="direction">The normalized direction vector for the shot.</param>
    /// <param name="windowPos">The top-left position of the game window.</param>
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

    // Unchanged methods from here down
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

                if (this.currentStage <= 2)
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

    private Bubble CreateRandomBubble()
    {
        var bubbleTypes = this.gameBoard.GetAvailableBubbleTypesOnBoard();
        var selectedType = bubbleTypes[this.random.Next(bubbleTypes.Length)];
        return new Bubble(Vector2.Zero, Vector2.Zero, BubbleRadius, selectedType.Color, selectedType.Type);
    }

    private void UpdateActiveBubble(Vector2 windowPos, ImDrawListPtr drawList)
    {
        if (this.activeBubble == null) return;

        this.activeBubble.Position += this.activeBubble.Velocity * ImGui.GetIO().DeltaTime;
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

        if (this.gameBoard.CheckCollision(this.activeBubble))
        {
            var clearResult = this.gameBoard.AddBubble(this.activeBubble);
            this.activeBubble = null;

            if (clearResult.TotalScore > 0)
            {
                this.score += clearResult.TotalScore;

               
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
                    this.score += 1000 * this.currentStage;
                    this.currentGameState = GameState.StageCleared;
                }
            }
            return;
        }

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
