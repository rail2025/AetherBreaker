using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AetherBreaker.Audio;
using AetherBreaker.Networking;
using AetherBreaker.Windows;
using Dalamud.Interface.Utility;
using ImGuiNET;

namespace AetherBreaker.Game;

public class GameSession
{
    public GameState CurrentGameState { get; private set; }
    public int CurrentStage { get; private set; }
    public int Score { get; private set; }
    public int ShotsUntilDrop { get; private set; }
    public float TimeUntilDrop { get; private set; }
    public bool IsHelperLineActiveForStage { get; private set; }
    public GameBoard? GameBoard { get; private set; }
    public Bubble? ActiveBubble { get; private set; }
    public Bubble? NextBubble { get; private set; }
    public List<BubbleAnimation> ActiveBubbleAnimations { get; } = new();
    public List<TextAnimation> ActiveTextAnimations { get; } = new();

    // Multiplayer State
    private readonly NetworkManager? networkManager;
    public enum MultiplayerMatchState { None, WaitingForOpponent, RoundStarting, RoundInProgress, RoundOver, MatchOver }
    public MultiplayerMatchState CurrentMatchState { get; private set; }
    public int MyScore { get; private set; }
    public int OpponentScore { get; private set; }
    public bool IsMultiplayerMode => this.networkManager != null;
    public byte[]? OpponentBoardState { get; private set; }
    private float gameStateSendTimer = 0f;

    private readonly Configuration configuration;
    private readonly AudioManager audioManager;
    private readonly Random random = new();
    private float maxTimeForStage;

    private int shotsSinceBomb;
    private const int ShotsPerBomb = 30;

    private int shotsSinceStar;
    private const int ShotsPerStar = 100;

    private int shotsSincePaint;
    private const int ShotsPerPaint = 30;

    private int shotsSinceMirror;
    private const int ShotsPerMirror = 50;

    private float currentBubbleRadius;
    private readonly float bubbleSpeed = 1200f * ImGuiHelpers.GlobalScale;

    private const float BaseMaxTime = 30.0f;

    public GameSession(Configuration configuration, AudioManager audioManager, NetworkManager? networkManager = null)
    {
        this.configuration = configuration;
        this.audioManager = audioManager;
        this.networkManager = networkManager; // Store the network manager

        // Subscribe to network events if in multiplayer mode
        if (this.IsMultiplayerMode && this.networkManager != null)
        {
            this.networkManager.OnAttackReceived += HandleAttackReceived;
        }

        // Initialize state
        this.CurrentGameState = GameState.MainMenu;
        this.CurrentMatchState = MultiplayerMatchState.None;
        this.MyScore = 0;
        this.OpponentScore = 0;
    }

    private void HandleAttackReceived(int rowCount)
    {
        if (this.GameBoard != null)
        {
            this.GameBoard.AddJunkRows(rowCount);
        }
    }

    public void ReceiveOpponentBoardState(byte[] state)
    {
        this.OpponentBoardState = state;
    }

    public void RequestRematch()
    {
        if (IsMultiplayerMode && networkManager != null)
        {
            _ = networkManager.SendMatchControl(PayloadActionType.Rematch);
        }
    }

    private byte[]? SerializeBoardState()
    {
        if (GameBoard == null || !GameBoard.Bubbles.Any()) return null;

        var boardData = new List<byte>();
        foreach (var bubble in GameBoard.Bubbles.OrderBy(b => b.Position.Y).ThenBy(b => b.Position.X))
        {
            // Simple serialization: just the type.
            // Note: This loses position data and is only for a simple visual display.
            // A more complex representation would be needed for a perfect grid reconstruction.
            boardData.Add((byte)bubble.BubbleType);
        }
        return boardData.ToArray();
    }

    private int GetMaxShotsForStage(int stage)
    {
        if (stage >= 50) return 1;
        if (stage >= 40) return 2;
        if (stage >= 30) return 3;
        if (stage >= 20) return 5;
        if (stage >= 10) return 8;
        return 8; // Default for stages 1-9
    }

    public void Update()
    {
        if (this.CurrentGameState != GameState.InGame) return;

        // Multiplayer: Send game state periodically
        if (IsMultiplayerMode && CurrentMatchState == MultiplayerMatchState.RoundInProgress)
        {
            gameStateSendTimer += ImGui.GetIO().DeltaTime;
            if (gameStateSendTimer >= 0.5f) // Send twice per second
            {
                gameStateSendTimer = 0f;
                var boardState = SerializeBoardState();
                if (boardState != null && networkManager != null)
                {
                    _ = networkManager.SendGameState(boardState);
                }
            }
        }

        UpdateTimers();
        UpdateActiveBubble();

        if (this.GameBoard != null && this.GameBoard.IsGameOver())
        {
            this.CurrentGameState = GameState.GameOver;
            ClearSavedGame();
        }
    }

    public void StartNewGame()
    {
        this.Score = 0;
        this.CurrentStage = 1;
        SetupStage();
        this.audioManager.StartBgmPlaylist();
        this.CurrentGameState = GameState.InGame;
        ClearSavedGame();
    }

    public void ContinueGame()
    {
        if (this.configuration.SavedGame != null)
        {
            LoadState(this.configuration.SavedGame);
            this.audioManager.StartBgmPlaylist();
            this.CurrentGameState = GameState.InGame;
        }
    }

    private void SetupStage()
    {
        this.IsHelperLineActiveForStage = false;

        var baseLargeRadius = 40f;
        var baseNormalRadius = 30f;
        var baseSmallRadius = 22.5f;

        if (this.CurrentStage <= 2) this.currentBubbleRadius = baseLargeRadius * ImGuiHelpers.GlobalScale;
        else if (this.CurrentStage >= 10) this.currentBubbleRadius = baseSmallRadius * ImGuiHelpers.GlobalScale;
        else this.currentBubbleRadius = baseNormalRadius * ImGuiHelpers.GlobalScale;

        this.GameBoard = new GameBoard(this.currentBubbleRadius);
        this.GameBoard.InitializeBoard(this.CurrentStage);

        this.ShotsUntilDrop = GetMaxShotsForStage(this.CurrentStage);

        this.shotsSinceBomb = 0;
        this.shotsSinceStar = 0;
        this.shotsSincePaint = 0;
        this.shotsSinceMirror = 0;

        if (this.CurrentStage >= 20)
        {
            this.maxTimeForStage = 10.0f;
        }
        else if (this.CurrentStage >= 15)
        {
            this.maxTimeForStage = 20.0f;
        }
        else
        {
            this.maxTimeForStage = BaseMaxTime - ((this.CurrentStage - 1) / 2 * 0.5f);
        }

        this.TimeUntilDrop = this.maxTimeForStage;
        this.ActiveBubble = null;
        this.ActiveBubbleAnimations.Clear();
        this.ActiveTextAnimations.Clear();
        this.NextBubble = CreateRandomBubble();
    }

    public void GoToMainMenu()
    {
        if (this.Score > this.configuration.HighScore)
        {
            this.configuration.HighScore = this.Score;
            this.configuration.Save();
        }
        this.CurrentGameState = GameState.MainMenu;
        // If in a match, also send a disconnect message
        if (IsMultiplayerMode && networkManager != null)
        {
            _ = networkManager.DisconnectAsync();
        }
    }

    public void ContinueToNextStage()
    {
        this.CurrentStage++;
        SetupStage();
        this.CurrentGameState = GameState.InGame;
    }

    public void SetGameState(GameState newState)
    {
        this.CurrentGameState = newState;
    }

    public void Debug_ClearStage()
    {
        if (this.CurrentGameState != GameState.InGame) return;
        this.Score += 1000 * this.CurrentStage;
        this.CurrentGameState = GameState.StageCleared;
    }

    private void UpdateTimers()
    {
        if (this.GameBoard == null) return;
        this.TimeUntilDrop -= ImGui.GetIO().DeltaTime;
        if (this.TimeUntilDrop <= 0)
        {
            HandleCeilingAdvance();
            this.TimeUntilDrop = this.maxTimeForStage;
            this.ShotsUntilDrop = GetMaxShotsForStage(this.CurrentStage);
        }
    }

    private void UpdateActiveBubble()
    {
        if (this.ActiveBubble == null || this.GameBoard == null) return;

        this.ActiveBubble.Position += this.ActiveBubble.Velocity * ImGui.GetIO().DeltaTime;

        if (this.ActiveBubble.Position.X - this.currentBubbleRadius < 0)
        {
            this.ActiveBubble.Velocity.X *= -1;
            this.ActiveBubble.Position.X = this.currentBubbleRadius;
            this.audioManager.PlaySfx("bounce.wav");
        }
        else if (this.ActiveBubble.Position.X + this.currentBubbleRadius > MainWindow.ScaledWindowSize.X)
        {
            this.ActiveBubble.Velocity.X *= -1;
            this.ActiveBubble.Position.X = MainWindow.ScaledWindowSize.X - this.currentBubbleRadius;
            this.audioManager.PlaySfx("bounce.wav");
        }

        var collidedWith = this.GameBoard.FindCollision(this.ActiveBubble);
        if (collidedWith != null)
        {
            this.audioManager.PlaySfx("land.wav");
            ClearResult clearResult = new ClearResult();
            bool wasSpecialAction = true;

            switch (this.ActiveBubble.BubbleType)
            {
                case GameBoard.StarType:
                    if (collidedWith.BubbleType >= 0)
                        clearResult = this.GameBoard.ActivateStar(collidedWith.BubbleType);
                    clearResult.PoppedBubbles.Add(this.ActiveBubble);
                    break;

                case GameBoard.PaintType:
                    if (collidedWith.BubbleType >= 0)
                        this.GameBoard.ActivatePaint(collidedWith, collidedWith);
                    clearResult.PoppedBubbles.Add(this.ActiveBubble);
                    break;

                default:
                    wasSpecialAction = false;
                    break;
            }

            if (!wasSpecialAction)
            {
                if (this.ActiveBubble.BubbleType < 0)
                {
                    clearResult = this.GameBoard.AddBubble(this.ActiveBubble, collidedWith);
                }
                else if (collidedWith.BubbleType == GameBoard.StarType)
                {
                    clearResult = this.GameBoard.ActivateStar(this.ActiveBubble.BubbleType);
                    clearResult.PoppedBubbles.Add(collidedWith);
                    this.GameBoard.Bubbles.Remove(collidedWith);
                    clearResult.PoppedBubbles.Add(this.ActiveBubble);
                }
                else if (collidedWith.BubbleType == GameBoard.PaintType)
                {
                    this.GameBoard.ActivatePaint(collidedWith, this.ActiveBubble);
                    clearResult.PoppedBubbles.Add(this.ActiveBubble);
                    clearResult.PoppedBubbles.Add(collidedWith);
                    this.GameBoard.Bubbles.Remove(collidedWith);
                }
                else if (collidedWith.BubbleType == GameBoard.MirrorType)
                {
                    this.GameBoard.TransformMirrorBubble(collidedWith, this.ActiveBubble);
                    clearResult = this.GameBoard.CheckForMatches(collidedWith);
                    clearResult.PoppedBubbles.Add(this.ActiveBubble);
                }
                else
                {
                    clearResult = this.GameBoard.AddBubble(this.ActiveBubble, collidedWith);
                }
            }

            this.ActiveBubble = null;
            HandleClearResult(clearResult);
        }
    }

    private void HandleCeilingAdvance()
    {
        if (this.GameBoard == null) return;

        var droppedBubbles = this.GameBoard.AdvanceCeiling();
        this.audioManager.PlaySfx("advance.wav");

        var bombs = droppedBubbles.Where(b => b.BubbleType == GameBoard.BombType).ToList();
        var nonBombs = droppedBubbles.Except(bombs).ToList();

        if (nonBombs.Any())
        {
            this.ActiveBubbleAnimations.Add(new BubbleAnimation(nonBombs, BubbleAnimationType.Drop, 1.5f));
            this.audioManager.PlaySfx("drop.wav");
        }

        if (bombs.Any())
        {
            var result = new ClearResult();
            foreach (var bomb in bombs)
            {
                var blastVictims = this.GameBoard.DetonateBomb(bomb.Position);
                result.PoppedBubbles.AddRange(blastVictims);
            }
            result.PoppedBubbles.AddRange(bombs);
            HandleClearResult(result);
        }
    }

    private void HandleClearResult(ClearResult clearResult)
    {
        if (this.GameBoard == null || clearResult == null) return;

        // Multiplayer: Send attack if conditions are met
        if (IsMultiplayerMode && clearResult.DroppedBubbles.Count > 3 && networkManager != null)
        {
            _ = networkManager.SendAttackData(clearResult.DroppedBubbles.Count);
        }

        var droppedBombs = clearResult.DroppedBubbles.Where(b => b.BubbleType == GameBoard.BombType).ToList();
        if (droppedBombs.Any())
        {
            foreach (var bomb in droppedBombs)
            {
                this.audioManager.PlaySfx("bomb.mp3");
                var blastVictims = this.GameBoard.DetonateBomb(bomb.Position);
                clearResult.PoppedBubbles.AddRange(blastVictims);
            }
            clearResult.PoppedBubbles.AddRange(droppedBombs);
            clearResult.DroppedBubbles.RemoveAll(b => b.BubbleType == GameBoard.BombType);
        }

        clearResult.CalculateScore();

        if (clearResult.TotalScore > 0)
        {
            this.Score += clearResult.TotalScore;
        }

        if (clearResult.HelperLineActivated)
        {
            this.IsHelperLineActiveForStage = true;
            var powerUpBubble = clearResult.PoppedBubbles.FirstOrDefault(b => b.BubbleType == GameBoard.PowerUpType);
            var textPos = powerUpBubble?.Position ?? new Vector2(MainWindow.ScaledWindowSize.X / 2, MainWindow.ScaledWindowSize.Y / 2);
            this.ActiveTextAnimations.Add(new TextAnimation("Aiming Helper!", textPos, ImGui.GetColorU32(new Vector4(0.7f, 0.4f, 1f, 1f)), 2.5f, TextAnimationType.FadeOut, 1.8f));
        }

        if (clearResult.PoppedBubbles.Any())
        {
            this.ActiveBubbleAnimations.Add(new BubbleAnimation(clearResult.PoppedBubbles, BubbleAnimationType.Pop, 0.2f));
            foreach (var _ in clearResult.PoppedBubbles)
            {
                this.audioManager.PlaySfx("pop.wav");
            }
        }

        if (clearResult.DroppedBubbles.Any())
        {
            this.ActiveBubbleAnimations.Add(new BubbleAnimation(clearResult.DroppedBubbles, BubbleAnimationType.Drop, 1.5f));
            this.audioManager.PlaySfx("drop.wav");
            foreach (var b in clearResult.DroppedBubbles) this.ActiveTextAnimations.Add(new TextAnimation("+20", b.Position, ImGui.GetColorU32(new Vector4(1, 1, 0.5f, 1)), 0.7f, TextAnimationType.FloatAndFade));
        }
        foreach (var b in clearResult.PoppedBubbles) this.ActiveTextAnimations.Add(new TextAnimation("+10", b.Position, ImGui.GetColorU32(new Vector4(1, 1, 1, 1)), 0.7f, TextAnimationType.FloatAndFade));

        if (clearResult.ComboMultiplier > 1)
        {
            var droppedPositions = clearResult.DroppedBubbles.Select(b => b.Position).ToList();
            var bonusPosition = droppedPositions.Aggregate(Vector2.Zero, (acc, p) => acc + p) / droppedPositions.Count;
            this.ActiveTextAnimations.Add(new TextAnimation($"x{clearResult.ComboMultiplier} COMBO!", bonusPosition, ImGui.GetColorU32(new Vector4(1f, 0.6f, 0f, 1f)), 2.5f, TextAnimationType.FadeOut, 1.8f));
        }

        if (this.GameBoard.AreAllColoredBubblesCleared())
        {
            this.audioManager.PlaySfx("clearstage.mp3");
            this.Score += 1000 * this.CurrentStage;
            this.CurrentGameState = GameState.StageCleared;
        }
    }

    public void FireBubble(Vector2 direction, Vector2 launcherPosition, Vector2 windowPos)
    {
        if (this.NextBubble == null) return;

        this.audioManager.PlaySfx("fire.wav");

        this.ActiveBubble = this.NextBubble;
        this.ActiveBubble.Position = launcherPosition - windowPos;
        this.ActiveBubble.Velocity = direction * this.bubbleSpeed;

        if (this.ActiveBubble.BubbleType >= 0)
        {
            this.shotsSinceBomb++;
            this.shotsSinceStar++;
            this.shotsSincePaint++;
            this.shotsSinceMirror++;
        }

        this.NextBubble = CreateRandomBubble();
        this.ShotsUntilDrop--;
        this.TimeUntilDrop = this.maxTimeForStage;

        if (this.ShotsUntilDrop <= 0)
        {
            HandleCeilingAdvance();
            this.ShotsUntilDrop = GetMaxShotsForStage(this.CurrentStage);
        }
    }

    private Bubble CreateRandomBubble()
    {
        if (this.CurrentStage >= 11 && this.shotsSinceMirror >= ShotsPerMirror)
        {
            this.shotsSinceMirror = 0;
            return new Bubble(Vector2.Zero, Vector2.Zero, this.currentBubbleRadius, ImGui.GetColorU32(new Vector4(0.8f, 0.9f, 0.95f, 1f)), GameBoard.MirrorType);
        }

        if (this.CurrentStage >= 7 && this.shotsSinceStar >= ShotsPerStar)
        {
            this.shotsSinceStar = 0;
            return new Bubble(Vector2.Zero, Vector2.Zero, this.currentBubbleRadius, ImGui.GetColorU32(new Vector4(1f, 0.85f, 0.2f, 1f)), GameBoard.StarType);
        }

        if (this.CurrentStage >= 9 && this.shotsSincePaint >= ShotsPerPaint)
        {
            this.shotsSincePaint = 0;
            return new Bubble(Vector2.Zero, Vector2.Zero, this.currentBubbleRadius, ImGui.GetColorU32(new Vector4(0.9f, 0.5f, 1f, 1f)), GameBoard.PaintType);
        }

        if (this.CurrentStage >= 5 && this.shotsSinceBomb >= ShotsPerBomb)
        {
            this.shotsSinceBomb = 0;
            return new Bubble(Vector2.Zero, Vector2.Zero, this.currentBubbleRadius, ImGui.GetColorU32(new Vector4(0.9f, 0.4f, 0.1f, 1.0f)), GameBoard.BombType);
        }

        if (this.GameBoard == null)
        {
            var radius = this.currentBubbleRadius > 0 ? this.currentBubbleRadius : (30f * ImGuiHelpers.GlobalScale);
            return new Bubble(Vector2.Zero, Vector2.Zero, radius, ImGui.GetColorU32(new Vector4(1.0f, 0.2f, 0.2f, 1.0f)), 0);
        }

        var bubbleTypes = this.GameBoard.GetAvailableBubbleTypesOnBoard();
        var selectedType = bubbleTypes[this.random.Next(bubbleTypes.Length)];
        return new Bubble(Vector2.Zero, Vector2.Zero, this.currentBubbleRadius, selectedType.Color, selectedType.Type);
    }

    public void SaveState()
    {
        if (this.CurrentGameState != GameState.InGame || this.GameBoard == null) return;

        var savedGame = new SavedGame
        {
            Score = this.Score,
            CurrentStage = this.CurrentStage,
            ShotsUntilDrop = this.ShotsUntilDrop,
            TimeUntilDrop = this.TimeUntilDrop,
            IsHelperLineActiveForStage = this.IsHelperLineActiveForStage,
            Bubbles = this.GameBoard.Bubbles.Select(b => new SerializableBubble { Position = b.Position, BubbleType = b.BubbleType }).ToList(),
            NextBubble = this.NextBubble != null ? new SerializableBubble { Position = this.NextBubble.Position, BubbleType = this.NextBubble.BubbleType } : null
        };
        this.configuration.SavedGame = savedGame;
        this.configuration.Save();
    }

    public void LoadState(SavedGame savedGame)
    {
        this.Score = savedGame.Score;
        this.CurrentStage = savedGame.CurrentStage;
        this.ShotsUntilDrop = savedGame.ShotsUntilDrop;
        this.TimeUntilDrop = savedGame.TimeUntilDrop;
        this.IsHelperLineActiveForStage = savedGame.IsHelperLineActiveForStage;

        SetupStage();

        if (this.GameBoard != null)
        {
            this.GameBoard.Bubbles.Clear();
            foreach (var sb in savedGame.Bubbles)
            {
                var bubbleTypeDetails = this.GameBoard.GetBubbleDetails(sb.BubbleType);
                this.GameBoard.Bubbles.Add(new Bubble(sb.Position, Vector2.Zero, this.currentBubbleRadius, bubbleTypeDetails.Color, bubbleTypeDetails.Type));
            }

            if (savedGame.NextBubble != null)
            {
                var nextBubbleDetails = this.GameBoard.GetBubbleDetails(savedGame.NextBubble.BubbleType);
                this.NextBubble = new Bubble(Vector2.Zero, Vector2.Zero, this.currentBubbleRadius, nextBubbleDetails.Color, nextBubbleDetails.Type);
            }
        }
    }

    public void ClearSavedGame()
    {
        this.configuration.SavedGame = null;
        this.configuration.Save();
    }
}
