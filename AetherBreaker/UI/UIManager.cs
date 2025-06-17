using System;
using System.Numerics;
using AetherBreaker.Audio;
using AetherBreaker.Game;
using AetherBreaker.Windows;
using ImGuiNET;

namespace AetherBreaker.UI;

/// <summary>
/// A static helper class responsible for drawing all UI elements for the game.
/// </summary>
public static class UIManager
{
    private static void DrawTextWithOutline(ImDrawListPtr drawList, string text, Vector2 pos, uint color, uint outlineColor, float size = 1f)
    {
        drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize() * size, pos + new Vector2(-1, -1), outlineColor, text);
        drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize() * size, pos + new Vector2(1, -1), outlineColor, text);
        drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize() * size, pos + new Vector2(-1, 1), outlineColor, text);
        drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize() * size, pos + new Vector2(1, 1), outlineColor, text);
        drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize() * size, pos, color, text);
    }

    /// <summary>
    /// Draws the main menu screen.
    /// </summary>
    public static void DrawMainMenu(Action startGame, Action openAbout)
    {
        var drawList = ImGui.GetWindowDrawList();
        var windowPos = ImGui.GetWindowPos();
        var title = "AetherBreaker";
        var titleFontSize = 3.5f;
        var titleSize = ImGui.CalcTextSize(title) * titleFontSize;
        var titlePos = new Vector2(windowPos.X + (MainWindow.WindowSize.X - titleSize.X) * 0.5f, windowPos.Y + MainWindow.WindowSize.Y * 0.2f);

        DrawTextWithOutline(drawList, title, titlePos, 0xFFFFFFFF, 0xFF000000, titleFontSize);

        var buttonSize = new Vector2(140, 40);
        var startY = MainWindow.WindowSize.Y * 0.5f;

        ImGui.SetCursorPos(new Vector2((MainWindow.WindowSize.X - buttonSize.X) * 0.5f, startY));
        if (ImGui.Button("Start Game", buttonSize))
        {
            startGame();
        }

        ImGui.SetCursorPos(new Vector2((MainWindow.WindowSize.X - buttonSize.X) * 0.5f, startY + 50));
        if (ImGui.Button("About", buttonSize))
        {
            openAbout();
        }
    }

    /// <summary>
    /// Draws the main in-game user interface, including scores, timers, and buttons.
    /// </summary>
    public static void DrawGameUI(
        ImDrawListPtr drawList,
        Vector2 windowPos,
        Vector2 launcherPosition,
        GameSession session,
        Plugin plugin,
        AudioManager audioManager)
    {
        var outlineColor = ImGui.GetColorU32(new Vector4(0, 0, 0, 0.8f));

        // --- Bottom Left ---
        var leftAlignX = windowPos.X + 15;
        var controlsBaseY = windowPos.Y + MainWindow.WindowSize.Y - 35;
        ImGui.SetCursorScreenPos(new Vector2(leftAlignX, controlsBaseY));

        var settingsButtonSize = new Vector2(80, 25);
        if (ImGui.Button("Settings", settingsButtonSize))
        {
            plugin.ToggleConfigUI();
        }
        ImGui.SameLine();

        var volume = plugin.Configuration.MusicVolume;
        ImGui.PushItemWidth(80);
        if (ImGui.SliderFloat("##MusicVol", ref volume, 0.0f, 1.0f, ""))
        {
            audioManager.SetMusicVolume(volume);
            plugin.Configuration.MusicVolume = volume;
            plugin.Configuration.Save();
        }
        ImGui.PopItemWidth();
        ImGui.SameLine();

        var isMuted = plugin.Configuration.IsBgmMuted;
        var muteTextPos = ImGui.GetCursorScreenPos() + new Vector2(0, 3);
        DrawTextWithOutline(drawList, "Mute", muteTextPos, 0xFFFFFFFF, outlineColor);
        ImGui.SetCursorScreenPos(ImGui.GetCursorScreenPos() + new Vector2(40, 0));
        if (ImGui.Checkbox("##Mute", ref isMuted))
        {
            plugin.Configuration.IsBgmMuted = isMuted;
            plugin.Configuration.Save();
            audioManager.UpdateBgmState();
        }

        var stageText = $"Stage: {session.CurrentStage}";
        var stagePos = new Vector2(leftAlignX, controlsBaseY - 30);
        DrawTextWithOutline(drawList, stageText, stagePos, ImGui.GetColorU32(new Vector4(0.8f, 0.8f, 0.8f, 1f)), outlineColor);

        var scoreText = $"Score: {session.Score}";
        var scorePos = new Vector2(leftAlignX, stagePos.Y - 20);
        DrawTextWithOutline(drawList, scoreText, scorePos, ImGui.GetColorU32(new Vector4(1, 1, 1, 1)), outlineColor, 1.5f);

        var highScoreText = $"High Score: {plugin.Configuration.HighScore}";
        var highScorePos = new Vector2(leftAlignX, scorePos.Y - 22);
        DrawTextWithOutline(drawList, highScoreText, highScorePos, ImGui.GetColorU32(new Vector4(1, 0.9f, 0.3f, 1)), outlineColor, 1.2f);

        // --- Bottom Right ---
        var rightAlignX = windowPos.X + MainWindow.WindowSize.X - 10;

        var timeText = $"Time: {session.TimeUntilDrop:F1}s";
        var timeTextSize = ImGui.CalcTextSize(timeText);
        var timePos = new Vector2(rightAlignX - timeTextSize.X, windowPos.Y + MainWindow.WindowSize.Y - 25f);
        DrawTextWithOutline(drawList, timeText, timePos, 0xFFFFFFFF, outlineColor);

        var shotsText = $"Shots: {session.ShotsUntilDrop}";
        var shotsTextSize = ImGui.CalcTextSize(shotsText);
        var shotsPos = new Vector2(rightAlignX - shotsTextSize.X, timePos.Y - 20);
        DrawTextWithOutline(drawList, shotsText, shotsPos, 0xFFFFFFFF, outlineColor);

        // --- Buttons ---
        var pauseButtonSize = new Vector2(80, 25);
        var pauseButtonPos = new Vector2(launcherPosition.X + 100, launcherPosition.Y - (pauseButtonSize.Y / 2));
        ImGui.SetCursorScreenPos(pauseButtonPos);
        if (ImGui.Button("Pause", pauseButtonSize))
        {
            session.SetGameState(GameState.Paused);
        }

        // Restore Debug Button
       // ImGui.SetCursorScreenPos(new Vector2(pauseButtonPos.X, pauseButtonPos.Y - 30));
        //if (ImGui.Button("Debug: Clear", new Vector2(80, 25)))
        //{
        //    session.Debug_ClearStage();
        //}
    }

    /// <summary>
    /// Draws the game over screen.
    /// </summary>
    public static void DrawGameOverScreen(Action goToMainMenu)
    {
        var windowPos = ImGui.GetWindowPos();
        var drawList = ImGui.GetWindowDrawList();
        var text = "GAME OVER";
        var textSize = ImGui.CalcTextSize(text) * 2;
        var textPos = new Vector2(windowPos.X + (MainWindow.WindowSize.X - textSize.X) * 0.5f, windowPos.Y + (MainWindow.WindowSize.Y - textSize.Y) * 0.5f);

        DrawTextWithOutline(drawList, text, textPos, ImGui.GetColorU32(new Vector4(1, 1, 1, 1)), 0xFF000000, 2f);

        var buttonSize = new Vector2(120, 30);
        var buttonPos = new Vector2(windowPos.X + (MainWindow.WindowSize.X - buttonSize.X) * 0.5f, textPos.Y + textSize.Y + 20);
        ImGui.SetCursorScreenPos(buttonPos);
        if (ImGui.Button("Main Menu", buttonSize))
        {
            goToMainMenu();
        }
    }

    /// <summary>
    /// Draws the stage cleared screen.
    /// </summary>
    public static void DrawStageClearedScreen(int nextStage, Action continueToNextStage)
    {
        var windowPos = ImGui.GetWindowPos();
        var drawList = ImGui.GetWindowDrawList();
        var text = "STAGE CLEARED!";
        var textSize = ImGui.CalcTextSize(text) * 2;
        var textPos = new Vector2(windowPos.X + (MainWindow.WindowSize.X - textSize.X) * 0.5f, windowPos.Y + (MainWindow.WindowSize.Y - textSize.Y) * 0.5f);

        DrawTextWithOutline(drawList, text, textPos, ImGui.GetColorU32(new Vector4(1, 1, 0, 1)), 0xFF000000, 2f);

        var buttonText = $"Continue to Stage {nextStage}";
        var buttonSize = ImGui.CalcTextSize(buttonText) + new Vector2(20, 10);
        var buttonPos = new Vector2(windowPos.X + (MainWindow.WindowSize.X - buttonSize.X) * 0.5f, textPos.Y + textSize.Y + 20);
        ImGui.SetCursorScreenPos(buttonPos);
        if (ImGui.Button(buttonText, buttonSize))
        {
            continueToNextStage();
        }
    }

    /// <summary>
    /// Draws the paused screen overlay.
    /// </summary>
    public static void DrawPausedScreen(Action resume, Action goToMainMenu)
    {
        var windowPos = ImGui.GetWindowPos();
        var drawList = ImGui.GetWindowDrawList();
        var text = "PAUSED";
        var textSize = ImGui.CalcTextSize(text) * 2;
        var textPos = new Vector2(windowPos.X + (MainWindow.WindowSize.X - textSize.X) * 0.5f, windowPos.Y + (MainWindow.WindowSize.Y - textSize.Y) * 0.5f);

        DrawTextWithOutline(drawList, text, textPos, ImGui.GetColorU32(new Vector4(1, 1, 1, 1)), 0xFF000000, 2f);

        var buttonSize = new Vector2(100, 30);
        var resumePos = new Vector2(windowPos.X + (MainWindow.WindowSize.X / 2) - buttonSize.X - 10, textPos.Y + textSize.Y + 20);
        var menuPos = new Vector2(windowPos.X + (MainWindow.WindowSize.X / 2) + 10, textPos.Y + textSize.Y + 20);

        ImGui.SetCursorScreenPos(resumePos);
        if (ImGui.Button("Resume", buttonSize))
        {
            resume();
        }

        ImGui.SetCursorScreenPos(menuPos);
        if (ImGui.Button("Main Menu", buttonSize))
        {
            goToMainMenu();
        }
    }
}
