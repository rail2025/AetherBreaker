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
    private readonly TextureManager textureManager;
    private readonly AudioManager audioManager;

    private GameSession? singlePlayerSession;
    private MultiplayerGameSession? multiplayerSession;

    private bool isMultiplayerMode = false;

    private Vector2 launcherPosition;
    private const int StagesPerBackground = 3;

    public static readonly Vector2 BaseWindowSize = new(540, 720);
    public static Vector2 ScaledWindowSize => BaseWindowSize * ImGuiHelpers.GlobalScale;
    public const float HudAreaHeight = 110f;

    public MainWindow(Plugin plugin, AudioManager audioManager, string idSuffix = "") : base("AetherBreaker" + idSuffix)
    {
        this.plugin = plugin;
        this.audioManager = audioManager;
        this.textureManager = new TextureManager();
        this.singlePlayerSession = null;
        this.multiplayerSession = null;
        this.Flags |= ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
    }

    public void StartSinglePlayerGame(bool isContinue)
    {
        this.isMultiplayerMode = false;
        this.multiplayerSession = null;
        this.singlePlayerSession = new GameSession(plugin.Configuration, audioManager, plugin.NetworkManager);
        if (isContinue) { this.singlePlayerSession.ContinueGame(); } else { this.singlePlayerSession.StartNewGame(); }
    }

    public void StartMultiplayerGame(string passphrase)
    {
        this.isMultiplayerMode = true;
        this.singlePlayerSession = null;
        this.multiplayerSession = new MultiplayerGameSession(plugin.NetworkManager, audioManager);
        this.multiplayerSession.StartNewMatch(passphrase);
    }

    public GameSession? GetGameSession() => this.singlePlayerSession;
    public MultiplayerGameSession? GetMultiplayerGameSession() => this.multiplayerSession;

    public void Dispose()
    {
        this.textureManager.Dispose();
    }

    public override void OnClose()
    {
        if (!isMultiplayerMode && singlePlayerSession?.CurrentGameState == GameState.InGame) { singlePlayerSession.SaveState(); }
        this.audioManager.EndPlaylist();
        this.singlePlayerSession = null;
        this.multiplayerSession = null;
        this.isMultiplayerMode = false;
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
        var isGameActive = isMultiplayerMode ? multiplayerSession != null : singlePlayerSession != null;
        var deltaTime = ImGui.GetIO().DeltaTime;

        if (!isGameActive)
        {
            DrawBackground();
            UIManager.DrawMainMenu(plugin, () => StartSinglePlayerGame(false), () => StartSinglePlayerGame(true), this.plugin.Configuration.SavedGame != null, this.plugin.ToggleConfigUI, this.plugin.ToggleAboutUI);
            return;
        }

        if (isMultiplayerMode && multiplayerSession != null)
        {
            multiplayerSession.Update(deltaTime);
            DrawMultiplayerView();
        }
        else if (!isMultiplayerMode && singlePlayerSession != null)
        {
            singlePlayerSession.Update(deltaTime);
            DrawSinglePlayerView();
        }
    }

    private void DrawSinglePlayerView()
    {
        if (this.singlePlayerSession == null) return;
        DrawBackground(this.singlePlayerSession.CurrentGameState, this.singlePlayerSession.CurrentStage);
        switch (this.singlePlayerSession.CurrentGameState)
        {
            case GameState.InGame: DrawInGame(this.singlePlayerSession); break;
            case GameState.Paused: DrawPausedScreen(this.singlePlayerSession); break;
            case GameState.StageCleared: UIManager.DrawStageClearedScreen(this.singlePlayerSession.CurrentStage + 1, this.singlePlayerSession.ContinueToNextStage); break;
            case GameState.GameOver: UIManager.DrawGameOverScreen(this.singlePlayerSession.GoToMainMenu); break;
            case GameState.MainMenu: this.singlePlayerSession = null; break;
        }
    }

    private void DrawMultiplayerView()
    {
        if (this.multiplayerSession == null) return;
        DrawBackground(this.multiplayerSession.CurrentGameState);
        switch (this.multiplayerSession.CurrentGameState)
        {
            case GameState.InGame: DrawInGame(this.multiplayerSession); break;
            case GameState.GameOver: UIManager.DrawMultiplayerGameOverScreen(this.multiplayerSession.PlayerWon, this.multiplayerSession.GoToMainMenu, this.multiplayerSession.RequestRematch); break;
            case GameState.MainMenu: this.multiplayerSession = null; break;
        }
    }

    private void DrawInGame(GameSession session)
    {
        if (session.GameBoard == null) return;
        var windowPos = ImGui.GetWindowPos();
        var contentMin = ImGui.GetWindowContentRegionMin();
        var contentMax = ImGui.GetWindowContentRegionMax();
        var drawList = ImGui.GetWindowDrawList();

        float availableWidth = contentMax.X - contentMin.X;
        float pixelsPerUnit = availableWidth / session.GameBoard.AbstractWidth;
        var contentOrigin = windowPos + contentMin;

        var abstractGameOverLineY = session.GetAbstractGameOverLineY();
        var scaledGameOverLineY = contentOrigin.Y + abstractGameOverLineY * pixelsPerUnit;
        var hudAreaPos = new Vector2(contentOrigin.X, scaledGameOverLineY);

        this.launcherPosition = new Vector2(contentOrigin.X + availableWidth * 0.5f, hudAreaPos.Y + (HudAreaHeight * ImGuiHelpers.GlobalScale / 2));

        DrawBoardChrome(drawList, contentOrigin, pixelsPerUnit, session);

        foreach (var bubble in session.GameBoard.Bubbles) { DrawBubble(drawList, contentOrigin, bubble, pixelsPerUnit); }
        DrawLauncherAndAiming(session, drawList, contentOrigin, pixelsPerUnit);
        if (session.ActiveBubble != null) DrawBubble(drawList, contentOrigin, session.ActiveBubble, pixelsPerUnit);
        UpdateAndDrawBubbleAnimations(drawList, contentOrigin, session.ActiveBubbleAnimations, pixelsPerUnit);
        UpdateAndDrawTextAnimations(drawList, contentOrigin, session.ActiveTextAnimations, pixelsPerUnit);
        UIManager.DrawGameUI(drawList, hudAreaPos, session, this.plugin, this.audioManager, this.textureManager, availableWidth);
       // DrawDebugInfo(drawList, pixelsPerUnit, session);
    }

    private void DrawInGame(MultiplayerGameSession session)
    {
        if (session.GameBoard == null) return;
        var windowPos = ImGui.GetWindowPos();
        var contentMin = ImGui.GetWindowContentRegionMin();
        var contentMax = ImGui.GetWindowContentRegionMax();
        var drawList = ImGui.GetWindowDrawList();

        float availableWidth = contentMax.X - contentMin.X;
        float pixelsPerUnit = availableWidth / session.GameBoard.AbstractWidth;
        var contentOrigin = windowPos + contentMin;

        var abstractGameOverLineY = session.GetAbstractGameOverLineY();
        var scaledGameOverLineY = contentOrigin.Y + abstractGameOverLineY * pixelsPerUnit;
        var hudAreaPos = new Vector2(contentOrigin.X, scaledGameOverLineY);

        this.launcherPosition = new Vector2(contentOrigin.X + availableWidth * 0.5f, hudAreaPos.Y + (HudAreaHeight * ImGuiHelpers.GlobalScale / 2));

        DrawBoardChrome(drawList, contentOrigin, pixelsPerUnit, session);

        foreach (var bubble in session.GameBoard.Bubbles) { DrawBubble(drawList, contentOrigin, bubble, pixelsPerUnit); }
        DrawLauncherAndAiming(session, drawList, contentOrigin, pixelsPerUnit);
        if (session.ActiveBubble != null) DrawBubble(drawList, contentOrigin, session.ActiveBubble, pixelsPerUnit);
        UpdateAndDrawBubbleAnimations(drawList, contentOrigin, session.ActiveBubbleAnimations, pixelsPerUnit);
        UpdateAndDrawTextAnimations(drawList, contentOrigin, session.ActiveTextAnimations, pixelsPerUnit);
        UIManager.DrawMultiplayerGameUI(drawList, hudAreaPos, session, this.plugin, this.audioManager, this.textureManager, availableWidth);
        //DrawDebugInfo(drawList, pixelsPerUnit, session);
    }

    private void DrawPausedScreen(GameSession session)
    {
        if (session.GameBoard == null) return;
        var windowPos = ImGui.GetWindowPos();
        var contentMin = ImGui.GetWindowContentRegionMin();
        var contentMax = ImGui.GetWindowContentRegionMax();
        var drawList = ImGui.GetWindowDrawList();
        var scale = ImGuiHelpers.GlobalScale;

        float availableWidth = contentMax.X - contentMin.X;
        float pixelsPerUnit = availableWidth / session.GameBoard.AbstractWidth;
        var contentOrigin = windowPos + contentMin;

        foreach (var bubble in session.GameBoard.Bubbles) { DrawBubble(drawList, contentOrigin, bubble, pixelsPerUnit); }

        DrawBoardChrome(drawList, contentOrigin, pixelsPerUnit, session);

        var abstractGameOverLineY = session.GetAbstractGameOverLineY();
        var scaledGameOverLineY = contentOrigin.Y + abstractGameOverLineY * pixelsPerUnit;
        var hudAreaPos = new Vector2(contentOrigin.X, scaledGameOverLineY);

        UIManager.DrawGameUI(drawList, hudAreaPos, session, this.plugin, this.audioManager, this.textureManager, availableWidth);
        UIManager.DrawPausedScreen(() => session.SetGameState(GameState.InGame), session.GoToMainMenu);
    }

    private void DrawBoardChrome(ImDrawListPtr drawList, Vector2 contentOrigin, float pixelsPerUnit, GameSession session)
    {
        if (session.GameBoard == null) return;

        // Draw Ceiling Line
        var abstractCeilingY = session.GameBoard.GetCeilingY();
        var scaledCeilingY = contentOrigin.Y + abstractCeilingY * pixelsPerUnit - (session.GameBoard.GetBubbleRadius() * pixelsPerUnit);
        var scaledContentWidth = session.GameBoard.AbstractWidth * pixelsPerUnit;
        drawList.AddLine(new Vector2(contentOrigin.X, scaledCeilingY), new Vector2(contentOrigin.X + scaledContentWidth, scaledCeilingY), (uint)0x80FFFFFF, 1f * ImGuiHelpers.GlobalScale);

        // Draw Game Over Line
        var abstractGameOverLineY = session.GetAbstractGameOverLineY();
        var scaledGameOverLineY = contentOrigin.Y + abstractGameOverLineY * pixelsPerUnit;
        drawList.AddLine(new Vector2(contentOrigin.X, scaledGameOverLineY), new Vector2(contentOrigin.X + scaledContentWidth, scaledGameOverLineY), (uint)0x800000FF, 2f * ImGuiHelpers.GlobalScale);
    }

    private void DrawBoardChrome(ImDrawListPtr drawList, Vector2 contentOrigin, float pixelsPerUnit, MultiplayerGameSession session)
    {
        if (session.GameBoard == null) return;

        // Draw Ceiling Line
        var abstractCeilingY = session.GameBoard.GetCeilingY();
        var scaledCeilingY = contentOrigin.Y + abstractCeilingY * pixelsPerUnit - (session.GameBoard.GetBubbleRadius() * pixelsPerUnit);
        var scaledContentWidth = session.GameBoard.AbstractWidth * pixelsPerUnit;
        drawList.AddLine(new Vector2(contentOrigin.X, scaledCeilingY), new Vector2(contentOrigin.X + scaledContentWidth, scaledCeilingY), (uint)0x80FFFFFF, 1f * ImGuiHelpers.GlobalScale);

        // Draw Game Over Line
        var abstractGameOverLineY = session.GetAbstractGameOverLineY();
        var scaledGameOverLineY = contentOrigin.Y + abstractGameOverLineY * pixelsPerUnit;
        drawList.AddLine(new Vector2(contentOrigin.X, scaledGameOverLineY), new Vector2(contentOrigin.X + scaledContentWidth, scaledGameOverLineY), (uint)0x800000FF, 2f * ImGuiHelpers.GlobalScale);
    }

    private void DrawBubble(ImDrawListPtr drawList, Vector2 contentOrigin, Bubble bubble, float pixelsPerUnit)
    {
        var finalPos = contentOrigin + bubble.Position * pixelsPerUnit;
        var finalRadius = bubble.Radius * pixelsPerUnit;
        var bubbleTexture = this.textureManager.GetBubbleTexture(bubble.BubbleType);
        if (bubbleTexture != null)
        {
            var p_min = finalPos - new Vector2(finalRadius, finalRadius);
            var p_max = finalPos + new Vector2(finalRadius, finalRadius);
            drawList.AddImageRounded(bubbleTexture.ImGuiHandle, p_min, p_max, Vector2.Zero, Vector2.One, (uint)0xFFFFFFFF, finalRadius);
        }
        else { drawList.AddCircleFilled(finalPos, finalRadius, bubble.Color); }

        var scale = ImGuiHelpers.GlobalScale;
        if (bubble.BubbleType >= 0 || bubble.BubbleType == -2)
        {
            drawList.AddCircle(finalPos, finalRadius, (uint)0xFF000000, 12, 3f * scale);
        }
        if (bubble.BubbleType == -1 || bubble.BubbleType == -6)
        {
            drawList.AddCircle(finalPos, finalRadius, (uint)0xFF808080, 12, 1.5f * scale);
        }
        else if (bubble.BubbleType == -2)
        {
            drawList.AddLine(finalPos - new Vector2(finalRadius * 0.5f, 0), finalPos + new Vector2(finalRadius * 0.5f, 0), (uint)0xE6FFFFFF, 3f * scale);
        }
    }

    private void DrawLauncherAndAiming(GameSession session, ImDrawListPtr drawList, Vector2 contentOrigin, float pixelsPerUnit)
    {
        if (session.NextBubble == null) return;
        var nextBubble = session.NextBubble;

        var scale = ImGuiHelpers.GlobalScale;
        var finalLauncherRadius = nextBubble.Radius * pixelsPerUnit;

        drawList.AddCircleFilled(this.launcherPosition, finalLauncherRadius * 1.2f, (uint)0xFFCCCCCC);
        var launcherBubble = new Bubble(this.launcherPosition, Vector2.Zero, finalLauncherRadius, nextBubble.Color, nextBubble.BubbleType);
        DrawBubble(drawList, Vector2.Zero, launcherBubble, 1.0f);

        if (ImGui.IsWindowHovered())
        {
            var mousePos = ImGui.GetMousePos();
            if (mousePos.Y < this.launcherPosition.Y - finalLauncherRadius)
            {
                var direction = Vector2.Normalize(mousePos - this.launcherPosition);
                if (direction.Y > -0.1f) { direction.Y = -0.1f; direction = Vector2.Normalize(direction); }
                if (session.IsHelperLineActiveForStage)
                    DrawHelperLine(session.GameBoard, drawList, direction, contentOrigin, pixelsPerUnit, nextBubble);
                else
                    drawList.AddLine(this.launcherPosition, this.launcherPosition + direction * 150f * scale, (uint)0x80FFFFFF, 3f * scale);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && session.ActiveBubble == null)
                {
                    var abstractLauncherPos = (this.launcherPosition - contentOrigin) / pixelsPerUnit;
                    session.FireBubble(direction, abstractLauncherPos);
                }
            }
        }
    }

    private void DrawLauncherAndAiming(MultiplayerGameSession session, ImDrawListPtr drawList, Vector2 contentOrigin, float pixelsPerUnit)
    {
        if (session.NextBubble == null) return;
        var nextBubble = session.NextBubble;

        var scale = ImGuiHelpers.GlobalScale;
        var finalLauncherRadius = nextBubble.Radius * pixelsPerUnit;

        drawList.AddCircleFilled(this.launcherPosition, finalLauncherRadius * 1.2f, (uint)0xFFCCCCCC);
        var launcherBubble = new Bubble(this.launcherPosition, Vector2.Zero, finalLauncherRadius, nextBubble.Color, nextBubble.BubbleType);
        DrawBubble(drawList, Vector2.Zero, launcherBubble, 1.0f);

        if (ImGui.IsWindowHovered())
        {
            var mousePos = ImGui.GetMousePos();
            if (mousePos.Y < this.launcherPosition.Y - finalLauncherRadius)
            {
                var direction = Vector2.Normalize(mousePos - this.launcherPosition);
                if (direction.Y > -0.1f) { direction.Y = -0.1f; direction = Vector2.Normalize(direction); }
                if (session.IsHelperLineActive)
                    DrawHelperLine(session.GameBoard, drawList, direction, contentOrigin, pixelsPerUnit, nextBubble);
                else
                    drawList.AddLine(this.launcherPosition, this.launcherPosition + direction * 150f * scale, (uint)0x80FFFFFF, 3f * scale);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && session.ActiveBubble == null)
                {
                    var abstractLauncherPos = (this.launcherPosition - contentOrigin) / pixelsPerUnit;
                    session.FireBubble(direction, abstractLauncherPos);
                }
            }
        }
    }

    private void DrawHelperLine(GameBoard? gameBoard, ImDrawListPtr drawList, Vector2 direction, Vector2 contentOrigin, float pixelsPerUnit, Bubble nextBubble)
    {
        if (gameBoard == null) return;
        var abstractStartPos = (this.launcherPosition - contentOrigin) / pixelsPerUnit;
        var abstractVelocity = direction * 60f;
        var abstractBubbleRadius = nextBubble.Radius;
        var pathPoints = PredictHelperLinePath(gameBoard, abstractStartPos, abstractVelocity, abstractBubbleRadius);
        uint color = (uint)0x4DFFFFFF;
        var scale = ImGuiHelpers.GlobalScale;
        // CHANGE: Increased thickness of aiming line.
        for (int i = 0; i < pathPoints.Count - 1; i++)
        {
            drawList.AddLine(contentOrigin + pathPoints[i] * pixelsPerUnit, contentOrigin + pathPoints[i + 1] * pixelsPerUnit, color, 3f * scale);
        }
        var lastPoint = pathPoints.LastOrDefault();
        var snappedPosition = gameBoard.GetSnappedPosition(lastPoint, null);
        drawList.AddCircle(contentOrigin + snappedPosition * pixelsPerUnit, abstractBubbleRadius * pixelsPerUnit, (uint)0x66FFFFFF, 12, 2f * scale);
    }

    private void DrawHelperLine(MultiplayerGameBoard? gameBoard, ImDrawListPtr drawList, Vector2 direction, Vector2 contentOrigin, float pixelsPerUnit, Bubble nextBubble)
    {
        if (gameBoard == null) return;
        var abstractStartPos = (this.launcherPosition - contentOrigin) / pixelsPerUnit;
        var abstractVelocity = direction * 60f;
        var abstractBubbleRadius = nextBubble.Radius;
        var pathPoints = PredictHelperLinePath(gameBoard, abstractStartPos, abstractVelocity, abstractBubbleRadius);
        uint color = (uint)0x4DFFFFFF;
        var scale = ImGuiHelpers.GlobalScale;
        // CHANGE: Increased thickness of aiming line.
        for (int i = 0; i < pathPoints.Count - 1; i++)
        {
            drawList.AddLine(contentOrigin + pathPoints[i] * pixelsPerUnit, contentOrigin + pathPoints[i + 1] * pixelsPerUnit, color, 3f * scale);
        }
        var lastPoint = pathPoints.LastOrDefault();
        var snappedPosition = gameBoard.GetSnappedPosition(lastPoint, null);
        drawList.AddCircle(contentOrigin + snappedPosition * pixelsPerUnit, abstractBubbleRadius * pixelsPerUnit, (uint)0x66FFFFFF, 12, 2f * scale);
    }

    private List<Vector2> PredictHelperLinePath(GameBoard gameBoard, Vector2 startPos, Vector2 velocity, float bubbleRadius)
    {
        var pathPoints = new List<Vector2> { startPos };
        var currentPos = startPos;
        var currentVel = velocity;
        int bounces = 0;
        float boardWidth = gameBoard.AbstractWidth;
        for (int i = 0; i < 400; i++)
        {
            currentPos += currentVel * 0.01f;
            if (currentPos.X - bubbleRadius < 0) { currentVel.X *= -1; currentPos.X = bubbleRadius; pathPoints.Add(currentPos); bounces++; }
            else if (currentPos.X + bubbleRadius > boardWidth) { currentVel.X *= -1; currentPos.X = boardWidth - bubbleRadius; pathPoints.Add(currentPos); bounces++; }
            var tempBubble = new Bubble(currentPos, currentVel, bubbleRadius, 0, 0);
            if (gameBoard.FindCollision(tempBubble) != null) { pathPoints.Add(currentPos); return pathPoints; }
            if (bounces >= 5) return pathPoints;
        }
        pathPoints.Add(currentPos);
        return pathPoints;
    }

    private List<Vector2> PredictHelperLinePath(MultiplayerGameBoard gameBoard, Vector2 startPos, Vector2 velocity, float bubbleRadius)
    {
        var pathPoints = new List<Vector2> { startPos };
        var currentPos = startPos;
        var currentVel = velocity;
        int bounces = 0;
        float boardWidth = gameBoard.AbstractWidth;
        for (int i = 0; i < 400; i++)
        {
            currentPos += currentVel * 0.01f;
            if (currentPos.X - bubbleRadius < 0) { currentVel.X *= -1; currentPos.X = bubbleRadius; pathPoints.Add(currentPos); bounces++; }
            else if (currentPos.X + bubbleRadius > boardWidth) { currentVel.X *= -1; currentPos.X = boardWidth - bubbleRadius; pathPoints.Add(currentPos); bounces++; }
            var tempBubble = new Bubble(currentPos, currentVel, bubbleRadius, 0, 0);
            if (gameBoard.FindCollision(tempBubble) != null) { pathPoints.Add(currentPos); return pathPoints; }
            if (bounces >= 5) return pathPoints;
        }
        pathPoints.Add(currentPos);
        return pathPoints;
    }

    private void DrawBackground(GameState? gameState = null, int stage = 1)
    {
        var bgCount = this.textureManager.GetBackgroundCount();
        if (bgCount == 0) return;
        var bgIndex = (stage - 1) / StagesPerBackground;
        var textureToDraw = this.textureManager.GetBackground(bgIndex);
        if (textureToDraw == null) return;
        ImGui.SetCursorPos(Vector2.Zero);
        ImGui.Image(textureToDraw.ImGuiHandle, ImGui.GetContentRegionAvail());
    }

    private void UpdateAndDrawBubbleAnimations(ImDrawListPtr drawList, Vector2 contentOrigin, List<BubbleAnimation> animations, float pixelsPerUnit)
    {
        for (int i = animations.Count - 1; i >= 0; i--)
        {
            var anim = animations[i];
            if (anim.Update(ImGui.GetIO().DeltaTime))
            {
                foreach (var bubble in anim.AnimatedBubbles)
                {
                    var animScale = anim.GetCurrentScale();
                    if (animScale > 0.01f)
                    {
                        var tempBubble = new Bubble(bubble.Position, Vector2.Zero, bubble.Radius * animScale, bubble.Color, bubble.BubbleType);
                        DrawBubble(drawList, contentOrigin, tempBubble, pixelsPerUnit);
                    }
                }
            }
            else { animations.RemoveAt(i); }
        }
    }

    private void UpdateAndDrawTextAnimations(ImDrawListPtr drawList, Vector2 contentOrigin, List<TextAnimation> animations, float pixelsPerUnit)
    {
        for (int i = animations.Count - 1; i >= 0; i--)
        {
            var anim = animations[i];
            if (anim.Update(ImGui.GetIO().DeltaTime))
            {
                var color = anim.GetCurrentColor();
                var textPos = contentOrigin + anim.Position * pixelsPerUnit;
                var fontSize = ImGui.GetFontSize() * anim.Scale * ImGuiHelpers.GlobalScale;
                if (anim.IsBonus)
                {
                    uint outlineColor = (uint)0xE6FFFFFF;
                    var outlineOffset = new Vector2(1, 1) * ImGuiHelpers.GlobalScale;
                    drawList.AddText(ImGui.GetFont(), fontSize, textPos - outlineOffset, outlineColor, anim.Text);
                    drawList.AddText(ImGui.GetFont(), fontSize, textPos + new Vector2(outlineOffset.X, -outlineOffset.Y), outlineColor, anim.Text);
                    drawList.AddText(ImGui.GetFont(), fontSize, textPos + new Vector2(-outlineOffset.X, outlineOffset.Y), outlineColor, anim.Text);
                    drawList.AddText(ImGui.GetFont(), fontSize, textPos + outlineOffset, outlineColor, anim.Text);
                }
                drawList.AddText(ImGui.GetFont(), fontSize, textPos, color, anim.Text);
            }
            else { animations.RemoveAt(i); }
        }
    }

   // private void DrawDebugInfo(ImDrawListPtr drawList, float pixelsPerUnit, GameSession session)
    //{
     //   if (session.GameBoard == null) return;
      //  var scale = ImGuiHelpers.GlobalScale;
       // ImGui.SetCursorPos(new Vector2(ImGui.GetWindowContentRegionMax().X - 220 * scale, 10 * scale));

        //var firstBubble = session.GameBoard.Bubbles.FirstOrDefault();

//        ImGui.BeginGroup();
  //      ImGui.Text($"--- DEBUG INFO ---");
    //    ImGui.Text($"Global Scale: {scale:F2}");
      //  ImGui.Text($"Pixels Per Unit: {pixelsPerUnit:F2}");
        //ImGui.Text($"Board Abstract W: {session.GameBoard.AbstractWidth:F2}");
        //if (firstBubble != null)
        //{
         //   ImGui.Text($"First Bubble Abstract: {firstBubble.Position.X:F1}, {firstBubble.Position.Y:F1}");
          //  ImGui.Text($"First Bubble Scaled: {(int)(firstBubble.Position.X * pixelsPerUnit)}, {(int)(firstBubble.Position.Y * pixelsPerUnit)}");
        //}
        //ImGui.EndGroup();
    //}

    //private void DrawDebugInfo(ImDrawListPtr drawList, float pixelsPerUnit, MultiplayerGameSession session)
    //{
       // if (session.GameBoard == null) return;
        //var scale = ImGuiHelpers.GlobalScale;
        //ImGui.SetCursorPos(new Vector2(ImGui.GetWindowContentRegionMax().X - 220 * scale, 10 * scale));

        //var firstBubble = session.GameBoard.Bubbles.FirstOrDefault();

        //ImGui.BeginGroup();
        //ImGui.Text($"--- DEBUG INFO ---");
        //ImGui.Text($"Global Scale: {scale:F2}");
        //ImGui.Text($"Pixels Per Unit: {pixelsPerUnit:F2}");
        //ImGui.Text($"Board Abstract W: {session.GameBoard.AbstractWidth:F2}");
        //if (firstBubble != null)
        //{
            //ImGui.Text($"First Bubble Abstract: {firstBubble.Position.X:F1}, {firstBubble.Position.Y:F1}");
          //  ImGui.Text($"First Bubble Scaled: {(int)(firstBubble.Position.X * pixelsPerUnit)}, {(int)(firstBubble.Position.Y * pixelsPerUnit)}");
        //}
        //ImGui.EndGroup();
    //}
}
