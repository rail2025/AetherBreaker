using System;
using System.Numerics;
using AetherBreaker.Audio;
using AetherBreaker.Game;
using AetherBreaker.Windows;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using ImGuiNET;

namespace AetherBreaker.UI;

public static class UIManager
{
    private static void DrawTextWithOutline(ImDrawListPtr drawList, string text, Vector2 pos, uint color, uint outlineColor, float size = 1f)
    {
        var fontSize = ImGui.GetFontSize() * size;
        var outlineOffset = new Vector2(1, 1);

        drawList.AddText(ImGui.GetFont(), fontSize, pos - outlineOffset, outlineColor, text);
        drawList.AddText(ImGui.GetFont(), fontSize, pos + new Vector2(outlineOffset.X, -outlineOffset.Y), outlineColor, text);
        drawList.AddText(ImGui.GetFont(), fontSize, pos + new Vector2(-outlineOffset.X, outlineOffset.Y), outlineColor, text);
        drawList.AddText(ImGui.GetFont(), fontSize, pos + outlineOffset, outlineColor, text);
        drawList.AddText(ImGui.GetFont(), fontSize, pos, color, text);
    }

    public static void DrawMainMenu(Plugin plugin, Action startGame, Action continueGame, bool hasSavedGame, Action openSettings, Action openAbout)
    {
        var drawList = ImGui.GetWindowDrawList();
        var windowPos = ImGui.GetWindowPos();
        var title = "AetherBreaker";
        var titleFontSize = 3.5f;
        var titleSize = ImGui.CalcTextSize(title) * titleFontSize;
        var titlePos = new Vector2(windowPos.X + (MainWindow.ScaledWindowSize.X - titleSize.X) * 0.5f, windowPos.Y + MainWindow.ScaledWindowSize.Y * 0.2f);

        DrawTextWithOutline(drawList, title, titlePos, 0xFFFFFFFF, 0xFF000000, titleFontSize);

        var buttonSize = new Vector2(140, 40) * ImGuiHelpers.GlobalScale;
        var startY = MainWindow.ScaledWindowSize.Y * 0.45f;
        uint buttonTextColor = 0xFFFFFFFF;
        uint buttonOutlineColor = 0xFF000000;

        void DrawButtonWithOutline(string label, string id, Vector2 position, Vector2 size, Action onClick)
        {
            ImGui.SetCursorPos(position);
            if (ImGui.Button($"##{id}", size))
            {
                onClick();
            }
            var textSize = ImGui.CalcTextSize(label) * 1.2f;
            var textPos = windowPos + position + new Vector2((size.X - textSize.X) * 0.5f, (size.Y - textSize.Y) * 0.5f);
            DrawTextWithOutline(drawList, label, textPos, buttonTextColor, buttonOutlineColor, 1.2f);
        }

        float currentY = startY;
        var buttonSpacing = 50f * ImGuiHelpers.GlobalScale;
        var buttonX = (MainWindow.ScaledWindowSize.X - buttonSize.X) * 0.5f;

        DrawButtonWithOutline("Start Game", "Start", new Vector2(buttonX, currentY), buttonSize, startGame);
        currentY += buttonSpacing;

        if (hasSavedGame)
        {
            DrawButtonWithOutline("Continue", "Continue", new Vector2(buttonX, currentY), buttonSize, continueGame);
            currentY += buttonSpacing;
        }

        DrawButtonWithOutline("Multiplayer", "Multiplayer", new Vector2(buttonX, currentY), buttonSize, plugin.ToggleMultiplayerUI);
        currentY += buttonSpacing;

        DrawButtonWithOutline("Settings", "Settings", new Vector2(buttonX, currentY), buttonSize, openSettings);
        currentY += buttonSpacing;

        DrawButtonWithOutline("About", "About", new Vector2(buttonX, currentY), buttonSize, openAbout);
    }

    public static void DrawGameUI(
        ImDrawListPtr drawList,
        Vector2 windowPos,
        GameSession session,
        Plugin plugin,
        AudioManager audioManager,
        TextureManager textureManager)
    {
        var globalScale = ImGuiHelpers.GlobalScale;
        var hudY = MainWindow.ScaledWindowSize.Y - (MainWindow.HudAreaHeight * globalScale);
        ImGui.SetCursorPos(new Vector2(0, hudY));
        ImGui.Columns(3, "hudColumns", false);

        var leftColumnWidth = 220 * globalScale;
        var rightColumnWidth = 100 * globalScale;
        ImGui.SetColumnWidth(0, leftColumnWidth);
        ImGui.SetColumnWidth(2, rightColumnWidth);

        // --- Left Column ---
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 10 * globalScale);
        ImGui.BeginGroup();

        if (session.IsMultiplayerMode)
        {
            // Multiplayer HUD
            ImGui.Text($"YOU: {session.MyScore} | OPPONENT: {session.OpponentScore}");

            string statusText = session.CurrentMatchState switch
            {
                GameSession.MultiplayerMatchState.WaitingForOpponent => "Waiting for opponent...",
                GameSession.MultiplayerMatchState.RoundStarting => "Round Starting...",
                GameSession.MultiplayerMatchState.RoundInProgress => "Round in Progress",
                GameSession.MultiplayerMatchState.RoundOver => "Round Over",
                GameSession.MultiplayerMatchState.MatchOver => "Match Over",
                _ => "Connecting..."
            };
            ImGui.Text(statusText);

            var disconnectButtonSize = new Vector2(100, 25) * globalScale;
            if (ImGui.Button("Disconnect", disconnectButtonSize))
            {
                _ = plugin.NetworkManager.DisconnectAsync();
            }
        }
        else
        {
            // Single Player HUD
            ImGui.Text($"High Score: {plugin.Configuration.HighScore}");
            ImGui.Text($"Score: {session.Score}");
            ImGui.Text($"Stage: {session.CurrentStage}");

            var settingsButtonSize = new Vector2(80, 25) * globalScale;
            if (ImGui.Button("Settings", settingsButtonSize))
            {
                plugin.ToggleConfigUI();
            }
            ImGui.SameLine();
            ImGui.PushItemWidth(40 * globalScale);
            var volume = plugin.Configuration.MusicVolume;
            if (ImGui.SliderFloat("##MusicVol", ref volume, 0.0f, 1.0f, ""))
            {
                audioManager.SetMusicVolume(volume);
                plugin.Configuration.MusicVolume = volume;
                plugin.Configuration.Save();
            }
            ImGui.PopItemWidth();
            ImGui.SameLine();
            var isMuted = plugin.Configuration.IsBgmMuted;
            if (ImGui.Checkbox("##Mute", ref isMuted))
            {
                plugin.Configuration.IsBgmMuted = isMuted;
                plugin.Configuration.Save();
                audioManager.UpdateBgmState();
            }
            ImGui.SameLine();
            ImGui.Text("Mute");
        }

        ImGui.EndGroup();

        // --- Center Column (Opponent Preview) ---
        ImGui.NextColumn();
        if (session.IsMultiplayerMode && session.OpponentBoardState != null)
        {
            var previewAreaPos = windowPos + new Vector2(ImGui.GetColumnOffset(1) + 5 * globalScale, hudY - 180f * globalScale);
            var previewAreaSize = new Vector2(ImGui.GetColumnWidth(1) - 10 * globalScale, 175f * globalScale);
            drawList.AddRectFilled(previewAreaPos, previewAreaPos + previewAreaSize, 0x80000000);

            Vector2 iconSize = new Vector2(10f, 10f) * globalScale;
            Vector2 currentIconPos = previewAreaPos + new Vector2(5, 5);

            foreach (byte bubbleTypeByte in session.OpponentBoardState)
            {
                var bubbleTexture = textureManager.GetBubbleTexture((int)(sbyte)bubbleTypeByte); // Cast to signed byte then int
                if (bubbleTexture != null)
                {
                    drawList.AddImage(bubbleTexture.ImGuiHandle, currentIconPos, currentIconPos + iconSize);
                }

                currentIconPos.X += iconSize.X + (2 * globalScale);
                if (currentIconPos.X + iconSize.X > previewAreaPos.X + previewAreaSize.X)
                {
                    currentIconPos.X = previewAreaPos.X + 5;
                    currentIconPos.Y += iconSize.Y + (2 * globalScale);
                }
            }
        }


        // --- Right Column ---
        ImGui.NextColumn();

        var debugButtonSize = new Vector2(80, 25) * globalScale;
        if (ImGui.Button("Debug: Clear", debugButtonSize))
        {
            session.Debug_ClearStage();
        }

        var pauseButtonSize = new Vector2(80, 25) * globalScale;
        if (ImGui.Button("Pause", pauseButtonSize))
        {
            session.SetGameState(GameState.Paused);
        }

        ImGui.Spacing();
        ImGui.Text($"Shots: {session.ShotsUntilDrop}");
        ImGui.Text($"Time: {session.TimeUntilDrop:F1}s");

        ImGui.Columns(1);
    }

    public static void DrawGameOverScreen(Action goToMainMenu)
    {
        var windowPos = ImGui.GetWindowPos();
        var drawList = ImGui.GetWindowDrawList();
        var text = "GAME OVER";
        var textSize = ImGui.CalcTextSize(text) * 2;
        var textPos = new Vector2(windowPos.X + (MainWindow.ScaledWindowSize.X - textSize.X) * 0.5f, windowPos.Y + (MainWindow.ScaledWindowSize.Y - textSize.Y) * 0.5f);

        DrawTextWithOutline(drawList, text, textPos, ImGui.GetColorU32(new Vector4(1, 1, 1, 1)), 0xFF000000, 2f);

        var buttonSize = new Vector2(120, 30) * ImGuiHelpers.GlobalScale;
        var buttonPos = new Vector2((MainWindow.ScaledWindowSize.X - buttonSize.X) * 0.5f, (textPos - windowPos).Y + textSize.Y + 20 * ImGuiHelpers.GlobalScale);
        ImGui.SetCursorPos(buttonPos);
        if (ImGui.Button("Main Menu", buttonSize))
        {
            goToMainMenu();
        }
    }

    public static void DrawStageClearedScreen(int nextStage, Action continueToNextStage)
    {
        var windowPos = ImGui.GetWindowPos();
        var drawList = ImGui.GetWindowDrawList();
        var text = "STAGE CLEARED!";
        var textSize = ImGui.CalcTextSize(text) * 2;
        var textPos = new Vector2(windowPos.X + (MainWindow.ScaledWindowSize.X - textSize.X) * 0.5f, windowPos.Y + (MainWindow.ScaledWindowSize.Y - textSize.Y) * 0.5f);

        DrawTextWithOutline(drawList, text, textPos, ImGui.GetColorU32(new Vector4(1, 1, 0, 1)), 0xFF000000, 2f);

        var buttonText = $"Continue to Stage {nextStage}";
        var buttonSize = ImGui.CalcTextSize(buttonText) + new Vector2(20, 10) * ImGuiHelpers.GlobalScale;
        var buttonPos = new Vector2((MainWindow.ScaledWindowSize.X - buttonSize.X) * 0.5f, (textPos - windowPos).Y + textSize.Y + 20 * ImGuiHelpers.GlobalScale);
        ImGui.SetCursorPos(buttonPos);
        if (ImGui.Button(buttonText, buttonSize))
        {
            continueToNextStage();
        }
    }

    public static void DrawPausedScreen(Action resume, Action goToMainMenu)
    {
        var windowPos = ImGui.GetWindowPos();
        var drawList = ImGui.GetWindowDrawList();
        var text = "PAUSED";
        var textSize = ImGui.CalcTextSize(text) * 2;
        var textPos = new Vector2(windowPos.X + (MainWindow.ScaledWindowSize.X - textSize.X) * 0.5f, windowPos.Y + (MainWindow.ScaledWindowSize.Y - textSize.Y) * 0.5f);
        var relativeTextPos = textPos - windowPos;

        DrawTextWithOutline(drawList, text, textPos, ImGui.GetColorU32(new Vector4(1, 1, 1, 1)), 0xFF000000, 2f);

        var buttonSize = new Vector2(100, 30) * ImGuiHelpers.GlobalScale;
        var resumePos = new Vector2((MainWindow.ScaledWindowSize.X / 2) - buttonSize.X - 10 * ImGuiHelpers.GlobalScale, relativeTextPos.Y + textSize.Y + 20 * ImGuiHelpers.GlobalScale);
        var menuPos = new Vector2((MainWindow.ScaledWindowSize.X / 2) + 10 * ImGuiHelpers.GlobalScale, relativeTextPos.Y + textSize.Y + 20 * ImGuiHelpers.GlobalScale);

        ImGui.SetCursorPos(resumePos);
        if (ImGui.Button("Resume", buttonSize))
        {
            resume();
        }

        ImGui.SetCursorPos(menuPos);
        if (ImGui.Button("Main Menu", buttonSize))
        {
            goToMainMenu();
        }
    }
}
