using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AetherBreaker.Audio;
using AetherBreaker.Game;
using AetherBreaker.UI;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace AetherBreaker.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly GameSession gameSession;
    private readonly TextureManager textureManager;
    private readonly AudioManager audioManager;

    private Vector2 launcherPosition;
    private const int StagesPerBackground = 3;

    private static readonly Vector2 BaseWindowSize = new(540, 720);
    public static Vector2 ScaledWindowSize => BaseWindowSize * ImGuiHelpers.GlobalScale;
    public const float HudAreaHeight = 110f; // Unscaled height of the bottom UI area

    public MainWindow(Plugin plugin, AudioManager audioManager) : base("AetherBreaker")
    {
        this.plugin = plugin;
        this.audioManager = audioManager;
        this.textureManager = new TextureManager();
        this.gameSession = new GameSession(plugin.Configuration, audioManager);

        this.Flags |= ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
    }

    public void Dispose()
    {
        this.textureManager.Dispose();
    }

    public override void OnClose()
    {
        if (this.gameSession.CurrentGameState == GameState.InGame)
        {
            this.gameSession.SaveState();
        }
        this.audioManager.EndPlaylist();
        this.gameSession.SetGameState(GameState.MainMenu);
        base.OnClose();
    }

    public override void PreDraw()
    {
        this.Size = ScaledWindowSize;
        if (this.plugin.Configuration.IsGameWindowLocked) this.Flags |= ImGuiWindowFlags.NoMove;
        else this.Flags &= ~ImGuiWindowFlags.NoMove;
    }

    public override void Draw()
    {
        this.gameSession.Update();
        DrawBackground();

        switch (this.gameSession.CurrentGameState)
        {
            case GameState.MainMenu:
                UIManager.DrawMainMenu(this.gameSession.StartNewGame, this.gameSession.ContinueGame, this.plugin.Configuration.SavedGame != null, this.plugin.ToggleConfigUI, this.plugin.ToggleAboutUI);
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

    private void DrawInGame()
    {
        var windowPos = ImGui.GetWindowPos();
        var drawList = ImGui.GetWindowDrawList();

        var scaledHudHeight = HudAreaHeight * ImGuiHelpers.GlobalScale;
        this.launcherPosition = new Vector2(windowPos.X + ScaledWindowSize.X * 0.5f, windowPos.Y + ScaledWindowSize.Y - (scaledHudHeight / 2));

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
            drawList.AddCircle(bubblePos, bubble.Radius, outlineColor, 12, 3f * ImGuiHelpers.GlobalScale);
        }

        if (bubble.BubbleType == -1)
        {
            drawList.AddCircle(bubblePos, bubble.Radius, ImGui.GetColorU32(new Vector4(0.5f, 0.5f, 0.5f, 1.0f)), 12, 1.5f * ImGuiHelpers.GlobalScale);
        }
        else if (bubble.BubbleType == -2)
        {
            var dashColor = ImGui.GetColorU32(new Vector4(1, 1, 1, 0.9f));
            drawList.AddLine(bubblePos - new Vector2(bubble.Radius * 0.5f, 0), bubblePos + new Vector2(bubble.Radius * 0.5f, 0), dashColor, 3f * ImGuiHelpers.GlobalScale);
        }
    }

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
            if (mousePos.Y < this.launcherPosition.Y - nextBubble.Radius)
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
                    drawList.AddLine(this.launcherPosition, this.launcherPosition + direction * 150f * ImGuiHelpers.GlobalScale, ImGui.GetColorU32(new Vector4(1, 1, 1, 0.5f)), 3f * ImGuiHelpers.GlobalScale);

                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && this.gameSession.ActiveBubble == null)
                    this.gameSession.FireBubble(direction, this.launcherPosition, windowPos);
            }
        }
    }

    private void DrawHelperLine(ImDrawListPtr drawList, Vector2 direction, Vector2 windowPos)
    {
        if (this.gameSession.NextBubble == null) return;
        var pathPoints = PredictHelperLinePath(this.launcherPosition - windowPos, direction * (1200f * ImGuiHelpers.GlobalScale), this.gameSession.NextBubble.Radius);
        var color = ImGui.GetColorU32(new Vector4(1, 1, 1, 0.3f));
        for (int i = 0; i < pathPoints.Count - 1; i++)
        {
            drawList.AddLine(windowPos + pathPoints[i], windowPos + pathPoints[i + 1], color, 2f * ImGuiHelpers.GlobalScale);
        }
    }

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
                    var outlineOffset = new Vector2(1, 1);
                    drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize() * anim.Scale, textPos - outlineOffset, outlineColor, anim.Text);
                    drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize() * anim.Scale, textPos + new Vector2(outlineOffset.X, -outlineOffset.Y), outlineColor, anim.Text);
                    drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize() * anim.Scale, textPos + new Vector2(-outlineOffset.X, outlineOffset.Y), outlineColor, anim.Text);
                    drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize() * anim.Scale, textPos + outlineOffset, outlineColor, anim.Text);
                }
                drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize() * anim.Scale, textPos, color, anim.Text);
            }
            else
            {
                this.gameSession.ActiveTextAnimations.RemoveAt(i);
            }
        }
    }

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
            else if (currentPos.X + bubbleRadius > ScaledWindowSize.X)
            {
                currentVel.X *= -1;
                currentPos.X = ScaledWindowSize.X - bubbleRadius;
                pathPoints.Add(currentPos);
                bounces++;
            }

            var tempBubble = new Bubble(currentPos, currentVel, bubbleRadius, 0, 0);
            if (this.gameSession.GameBoard != null && this.gameSession.GameBoard.FindCollision(tempBubble) != null)
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
