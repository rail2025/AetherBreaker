using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AetherBreaker.Audio;
using AetherBreaker.Game;
using AetherBreaker.UI;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace AetherBreaker.Windows;

/// <summary>
/// The main window for the AetherBreaker game.
/// This class is responsible for orchestrating the game loop and rendering all visual elements.
/// It owns the GameSession (for logic) and TextureManager (for assets).
/// </summary>
public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly GameSession gameSession;
    private readonly TextureManager textureManager;
    private readonly AudioManager audioManager;

    private Vector2 launcherPosition;
    private const int StagesPerBackground = 3;
    public static readonly Vector2 WindowSize = new(30f * 2 * 9, 30f * 2 * 12);

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow(Plugin plugin, AudioManager audioManager) : base("AetherBreaker")
    {
        this.plugin = plugin;
        this.audioManager = audioManager;
        this.textureManager = new TextureManager();
        this.gameSession = new GameSession(plugin.Configuration, audioManager);

        this.Size = WindowSize;
        this.SizeCondition = ImGuiCond.Always;
        this.Flags |= ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
    }

    /// <summary>
    /// Disposes of managed resources, specifically the texture manager.
    /// </summary>
    public void Dispose()
    {
        this.textureManager.Dispose();
    }

    /// <summary>
    /// This method is called by the window system when the window is closed via the 'X' button.
    /// </summary>
    public override void OnClose()
    {
        this.audioManager.EndPlaylist();
        this.gameSession.SetGameState(GameState.MainMenu); // Reset state
        base.OnClose();
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
    /// The main drawing method for the window, called every frame by Dalamud.
    /// </summary>
    public override void Draw()
    {
        // 1. Update the game logic state first.
        this.gameSession.Update();

        // 2. Then, draw all visual elements based on the current state.
        DrawBackground();

        switch (this.gameSession.CurrentGameState)
        {
            case GameState.MainMenu:
                UIManager.DrawMainMenu(this.gameSession.StartNewGame, this.plugin.ToggleAboutUI);
                break;
            case GameState.InGame:
                DrawInGame();
                break;
            case GameState.Paused:
                DrawPausedScreen();
                break;
            case GameState.StageCleared:
                UIManager.DrawStageClearedScreen(this.gameSession.CurrentStage + 1, this.gameSession.ContinueToNextStage);
                break;
            case GameState.GameOver:
                UIManager.DrawGameOverScreen(this.gameSession.GoToMainMenu);
                break;
        }
    }

    /// <summary>
    /// Handles all drawing for the main "InGame" state.
    /// </summary>
    private void DrawInGame()
    {
        var windowPos = ImGui.GetWindowPos();
        var drawList = ImGui.GetWindowDrawList();
        this.launcherPosition = new Vector2(windowPos.X + WindowSize.X * 0.5f, windowPos.Y + WindowSize.Y - 50f);

        if (this.gameSession.GameBoard != null)
        {
            foreach (var bubble in this.gameSession.GameBoard.Bubbles)
            {
                DrawBubble(drawList, windowPos, bubble);
            }
            this.gameSession.GameBoard.DrawBoardChrome(drawList, windowPos);
        }

        DrawLauncherAndAiming(drawList, windowPos);

        if (this.gameSession.ActiveBubble != null)
            DrawBubble(drawList, windowPos, this.gameSession.ActiveBubble);

        UpdateAndDrawBubbleAnimations(drawList, windowPos);
        UpdateAndDrawTextAnimations(drawList, windowPos);
        UIManager.DrawGameUI(drawList, windowPos, this.launcherPosition, this.gameSession, this.plugin, this.audioManager);
    }

    /// <summary>
    /// Handles drawing for the "Paused" state.
    /// </summary>
    private void DrawPausedScreen()
    {
        var windowPos = ImGui.GetWindowPos();
        var drawList = ImGui.GetWindowDrawList();

        if (this.gameSession.GameBoard != null)
        {
            foreach (var bubble in this.gameSession.GameBoard.Bubbles)
            {
                DrawBubble(drawList, windowPos, bubble);
            }
            this.gameSession.GameBoard.DrawBoardChrome(drawList, windowPos);
        }

        UIManager.DrawGameUI(drawList, windowPos, this.launcherPosition, this.gameSession, this.plugin, this.audioManager);

        UIManager.DrawPausedScreen(
            () => this.gameSession.SetGameState(GameState.InGame),
            this.gameSession.GoToMainMenu
        );
    }

    /// <summary>
    /// Draws a single bubble, rendering its texture and then an outline on top.
    /// </summary>
    private void DrawBubble(ImDrawListPtr drawList, Vector2 windowPos, Bubble bubble)
    {
        var bubblePos = windowPos + bubble.Position;
        var bubbleTexture = this.textureManager.GetBubbleTexture(bubble.BubbleType);

        if (bubbleTexture != null)
        {
            var p_min = bubblePos - new Vector2(bubble.Radius, bubble.Radius);
            var p_max = bubblePos + new Vector2(bubble.Radius, bubble.Radius);
            drawList.AddImageRounded(bubbleTexture.ImGuiHandle, p_min, p_max, Vector2.Zero, Vector2.One, 0xFFFFFFFF, bubble.Radius);
        }
        else
        {
            drawList.AddCircleFilled(bubblePos, bubble.Radius, bubble.Color);
        }

        if (bubble.BubbleType >= 0 || bubble.BubbleType == -2)
        {
            var outlineColor = ImGui.GetColorU32(new Vector4(0, 0, 0, 1f));
            drawList.AddCircle(bubblePos, bubble.Radius, outlineColor, 12, 3f);
        }

        if (bubble.BubbleType == -1) // Black Bubble
        {
            drawList.AddCircle(bubblePos, bubble.Radius, ImGui.GetColorU32(new Vector4(0.5f, 0.5f, 0.5f, 1.0f)), 12, 1.5f);
        }
        else if (bubble.BubbleType == -2) // Purple Power-up dash on top of outline
        {
            var dashColor = ImGui.GetColorU32(new Vector4(1, 1, 1, 0.9f));
            drawList.AddLine(bubblePos - new Vector2(bubble.Radius * 0.5f, 0), bubblePos + new Vector2(bubble.Radius * 0.5f, 0), dashColor, 3f);
        }
    }

    /// <summary>
    /// Draws the launcher, the next bubble, and the aiming line.
    /// </summary>
    private void DrawLauncherAndAiming(ImDrawListPtr drawList, Vector2 windowPos)
    {
        var nextBubble = this.gameSession.NextBubble;
        if (nextBubble == null) return;

        var launcherBaseRadius = nextBubble.Radius * 1.2f;
        drawList.AddCircleFilled(this.launcherPosition, launcherBaseRadius, ImGui.GetColorU32(new Vector4(0.8f, 0.8f, 0.8f, 1.0f)));

        DrawBubble(drawList, Vector2.Zero, new Bubble(this.launcherPosition, Vector2.Zero, nextBubble.Radius, nextBubble.Color, nextBubble.BubbleType));

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

                if (this.gameSession.CurrentStage <= 2 || this.gameSession.IsHelperLineActiveForStage)
                    DrawHelperLine(drawList, direction, windowPos);
                else
                    drawList.AddLine(this.launcherPosition, this.launcherPosition + direction * 150f, ImGui.GetColorU32(new Vector4(1, 1, 1, 0.5f)), 3f);

                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && this.gameSession.ActiveBubble == null)
                    this.gameSession.FireBubble(direction, this.launcherPosition, windowPos);
            }
        }
    }

    /// <summary>
    /// Draws the predictive aiming helper line.
    /// </summary>
    private void DrawHelperLine(ImDrawListPtr drawList, Vector2 direction, Vector2 windowPos)
    {
        if (this.gameSession.NextBubble == null) return;
        var pathPoints = PredictHelperLinePath(this.launcherPosition - windowPos, direction * 1200f, this.gameSession.NextBubble.Radius);
        var color = ImGui.GetColorU32(new Vector4(1, 1, 1, 0.3f));
        for (int i = 0; i < pathPoints.Count - 1; i++)
        {
            drawList.AddLine(windowPos + pathPoints[i], windowPos + pathPoints[i + 1], color, 2f);
        }
    }

    #region Helpers

    /// <summary>
    /// Draws the background image for the current game state.
    /// </summary>
    private void DrawBackground()
    {
        var bgCount = this.textureManager.GetBackgroundCount();
        if (bgCount == 0) return;

        var bgIndex = 0;
        if (this.gameSession.CurrentGameState != GameState.MainMenu)
        {
            bgIndex = (this.gameSession.CurrentStage - 1) / StagesPerBackground;
        }

        var textureToDraw = this.textureManager.GetBackground(bgIndex);
        if (textureToDraw == null) return;

        ImGui.SetCursorPos(Vector2.Zero);
        ImGui.Image(textureToDraw.ImGuiHandle, ImGui.GetContentRegionAvail());
    }

    /// <summary>
    /// Updates and draws all active bubble animations (popping, dropping).
    /// </summary>
    private void UpdateAndDrawBubbleAnimations(ImDrawListPtr drawList, Vector2 windowPos)
    {
        for (int i = this.gameSession.ActiveBubbleAnimations.Count - 1; i >= 0; i--)
        {
            var anim = this.gameSession.ActiveBubbleAnimations[i];
            if (anim.Update())
            {
                foreach (var bubble in anim.AnimatedBubbles)
                {
                    var scale = anim.GetCurrentScale();
                    if (scale > 0.01f)
                    {
                        var tempBubble = new Bubble(bubble.Position, Vector2.Zero, bubble.Radius * scale, bubble.Color, bubble.BubbleType);
                        DrawBubble(drawList, windowPos, tempBubble);
                    }
                }
            }
            else
            {
                this.gameSession.ActiveBubbleAnimations.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Updates and draws all active text animations (score popups, combos).
    /// </summary>
    private void UpdateAndDrawTextAnimations(ImDrawListPtr drawList, Vector2 windowPos)
    {
        for (int i = this.gameSession.ActiveTextAnimations.Count - 1; i >= 0; i--)
        {
            var anim = this.gameSession.ActiveTextAnimations[i];
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
                this.gameSession.ActiveTextAnimations.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Predicts the path of the bubble for the helper line.
    /// </summary>
    private List<Vector2> PredictHelperLinePath(Vector2 startPos, Vector2 velocity, float bubbleRadius)
    {
        var pathPoints = new List<Vector2> { startPos };
        var currentPos = startPos;
        var currentVel = velocity;
        int bounces = 0;

        for (int i = 0; i < 400; i++)
        {
            currentPos += currentVel * 0.01f;

            if (currentPos.X - bubbleRadius < 0)
            {
                currentVel.X *= -1;
                currentPos.X = bubbleRadius;
                pathPoints.Add(currentPos);
                bounces++;
            }
            else if (currentPos.X + bubbleRadius > WindowSize.X)
            {
                currentVel.X *= -1;
                currentPos.X = WindowSize.X - bubbleRadius;
                pathPoints.Add(currentPos);
                bounces++;
            }

            var tempBubble = new Bubble(currentPos, currentVel, bubbleRadius, 0, 0);
            if (this.gameSession.GameBoard != null && this.gameSession.GameBoard.CheckCollision(tempBubble))
            {
                pathPoints.Add(currentPos);
                return pathPoints;
            }
            if (bounces >= 5) return pathPoints;
        }
        pathPoints.Add(currentPos);
        return pathPoints;
    }

    #endregion
}
