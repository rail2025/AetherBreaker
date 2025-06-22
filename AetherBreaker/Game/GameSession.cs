using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AetherBreaker.Audio;
using AetherBreaker.Networking;
using AetherBreaker.Windows;

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

    private readonly NetworkManager? networkManager;
    public bool IsMultiplayerMode => this.networkManager != null;

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
    private const float BubbleSpeed = 40f;

    private float abstractGameOverLineY;
    private const float BaseMaxTime = 30.0f;

    public GameSession(Configuration configuration, AudioManager audioManager, NetworkManager? networkManager = null)
    {
        this.configuration = configuration;
        this.audioManager = audioManager;
        this.networkManager = networkManager;
        this.CurrentGameState = GameState.MainMenu;
    }

    public float GetAbstractGameOverLineY() => this.abstractGameOverLineY;

    private int GetMaxShotsForStage(int stage)
    {
        if (stage >= 50) return 1;
        if (stage >= 40) return 2;
        if (stage >= 30) return 3;
        if (stage >= 20) return 5;
        if (stage >= 10) return 8;
        return 8;
    }

    public void Update(float deltaTime)
    {
        if (this.CurrentGameState != GameState.InGame) return;

        UpdateTimers(deltaTime);
        UpdateActiveBubble(deltaTime);

        if (this.GameBoard != null && this.GameBoard.Bubbles.Any(b => b.Position.Y + b.Radius >= this.abstractGameOverLineY))
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

        this.GameBoard = new GameBoard(this.CurrentStage);
        this.GameBoard.InitializeBoard(this.CurrentStage);

        this.currentBubbleRadius = this.GameBoard.GetBubbleRadius();

        float abstractGameHeight = this.GameBoard.AbstractWidth * (MainWindow.BaseWindowSize.Y / MainWindow.BaseWindowSize.X);
        float abstractHudHeight = this.GameBoard.AbstractWidth * (MainWindow.HudAreaHeight / MainWindow.BaseWindowSize.X);
        this.abstractGameOverLineY = abstractGameHeight - abstractHudHeight;

        this.ShotsUntilDrop = GetMaxShotsForStage(this.CurrentStage);
        this.shotsSinceBomb = 0;
        this.shotsSinceStar = 0;
        this.shotsSincePaint = 0;
        this.shotsSinceMirror = 0;

        if (this.CurrentStage >= 20) this.maxTimeForStage = 10.0f;
        else if (this.CurrentStage >= 15) this.maxTimeForStage = 20.0f;
        else this.maxTimeForStage = BaseMaxTime - ((this.CurrentStage - 1) / 2 * 0.5f);

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

    private void UpdateTimers(float deltaTime)
    {
        if (this.GameBoard == null) return;
        this.TimeUntilDrop -= deltaTime;
        if (this.TimeUntilDrop <= 0)
        {
            HandleCeilingAdvance();
            this.TimeUntilDrop = this.maxTimeForStage;
            this.ShotsUntilDrop = GetMaxShotsForStage(this.CurrentStage);
        }
    }

    private void UpdateActiveBubble(float deltaTime)
    {
        if (this.ActiveBubble == null || this.GameBoard == null) return;

        this.ActiveBubble.Position += this.ActiveBubble.Velocity * deltaTime;

        if (this.ActiveBubble.Position.X - this.currentBubbleRadius < 0)
        {
            this.ActiveBubble.Velocity.X *= -1;
            this.ActiveBubble.Position.X = this.currentBubbleRadius;
            this.audioManager.PlaySfx("bounce.wav");
        }
        else if (this.ActiveBubble.Position.X + this.currentBubbleRadius > this.GameBoard.AbstractWidth)
        {
            this.ActiveBubble.Velocity.X *= -1;
            this.ActiveBubble.Position.X = this.GameBoard.AbstractWidth - this.currentBubbleRadius;
            this.audioManager.PlaySfx("bounce.wav");
        }

        var collidedWith = this.GameBoard.FindCollision(this.ActiveBubble);
        if (collidedWith != null)
        {
            this.audioManager.PlaySfx("land.wav");
            var clearResult = ProcessBubbleCollision(this.ActiveBubble, collidedWith);
            this.ActiveBubble = null;
            HandleClearResult(clearResult);
        }
    }

    private ClearResult ProcessBubbleCollision(Bubble activeBubble, Bubble collidedWith)
    {
        if (this.GameBoard == null) return new ClearResult();

        ClearResult clearResult = new ClearResult();
        bool wasSpecialAction = true;

        switch (activeBubble.BubbleType)
        {
            case GameBoard.StarType:
                if (collidedWith.BubbleType >= 0)
                    clearResult = this.GameBoard.ActivateStar(collidedWith.BubbleType);
                clearResult.PoppedBubbles.Add(activeBubble);
                break;
            case GameBoard.PaintType:
                if (collidedWith.BubbleType >= 0)
                    this.GameBoard.ActivatePaint(collidedWith, collidedWith);
                clearResult.PoppedBubbles.Add(activeBubble);
                break;
            default:
                wasSpecialAction = false;
                break;
        }

        if (!wasSpecialAction)
        {
            if (activeBubble.BubbleType < 0)
            {
                clearResult = this.GameBoard.AddBubble(activeBubble, collidedWith);
            }
            else if (collidedWith.BubbleType == GameBoard.StarType)
            {
                clearResult = this.GameBoard.ActivateStar(activeBubble.BubbleType);
                if (this.GameBoard.Bubbles.Remove(collidedWith))
                    clearResult.PoppedBubbles.Add(collidedWith);
                clearResult.PoppedBubbles.Add(activeBubble);
            }
            else if (collidedWith.BubbleType == GameBoard.PaintType)
            {
                this.GameBoard.ActivatePaint(collidedWith, activeBubble);
                clearResult.PoppedBubbles.Add(activeBubble);
                if (this.GameBoard.Bubbles.Remove(collidedWith))
                    clearResult.PoppedBubbles.Add(collidedWith);
            }
            else if (collidedWith.BubbleType == GameBoard.MirrorType)
            {
                this.GameBoard.TransformMirrorBubble(collidedWith, activeBubble);
                clearResult = this.GameBoard.CheckForMatches(collidedWith);
                clearResult.PoppedBubbles.Add(activeBubble);
            }
            else
            {
                clearResult = this.GameBoard.AddBubble(activeBubble, collidedWith);
            }
        }

        return clearResult;
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
            var textPos = powerUpBubble?.Position ?? new Vector2(this.GameBoard.AbstractWidth / 2f, this.GameBoard.AbstractHeight / 2f);
            this.ActiveTextAnimations.Add(new TextAnimation("Aiming Helper!", textPos, 4289864447, 2.5f, TextAnimationType.FadeOut, 1.8f));
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
            foreach (var b in clearResult.DroppedBubbles) this.ActiveTextAnimations.Add(new TextAnimation("+20", b.Position, 4280252415, 0.7f, TextAnimationType.FloatAndFade));
        }
        foreach (var b in clearResult.PoppedBubbles) this.ActiveTextAnimations.Add(new TextAnimation("+10", b.Position, 4294967295, 0.7f, TextAnimationType.FloatAndFade));

        if (clearResult.ComboMultiplier > 1)
        {
            var droppedPositions = clearResult.DroppedBubbles.Select(b => b.Position).ToList();
            var bonusPosition = droppedPositions.Aggregate(Vector2.Zero, (acc, p) => acc + p) / droppedPositions.Count;
            this.ActiveTextAnimations.Add(new TextAnimation($"x{clearResult.ComboMultiplier} COMBO!", bonusPosition, 4280193279, 2.5f, TextAnimationType.FadeOut, 1.8f));
        }

        if (this.GameBoard.AreAllColoredBubblesCleared())
        {
            this.audioManager.PlaySfx("clearstage.mp3");
            this.Score += 1000 * this.CurrentStage;
            this.CurrentGameState = GameState.StageCleared;
        }
    }

    public void FireBubble(Vector2 direction, Vector2 abstractStartPosition)
    {
        if (this.NextBubble == null) return;
        this.audioManager.PlaySfx("fire.wav");

        this.ActiveBubble = this.NextBubble;
        this.ActiveBubble.Position = abstractStartPosition;
        this.ActiveBubble.Velocity = direction * BubbleSpeed;

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
        if (this.GameBoard == null) return new Bubble(Vector2.Zero, Vector2.Zero, 1.0f, 4280221439, 0);

        if (this.CurrentStage >= 11 && this.shotsSinceMirror >= ShotsPerMirror)
        {
            this.shotsSinceMirror = 0;
            var details = this.GameBoard.GetBubbleDetails(GameBoard.MirrorType);
            return new Bubble(Vector2.Zero, Vector2.Zero, this.currentBubbleRadius, details.Color, details.Type);
        }
        if (this.CurrentStage >= 7 && this.shotsSinceStar >= ShotsPerStar)
        {
            this.shotsSinceStar = 0;
            var details = this.GameBoard.GetBubbleDetails(GameBoard.StarType);
            return new Bubble(Vector2.Zero, Vector2.Zero, this.currentBubbleRadius, details.Color, details.Type);
        }
        if (this.CurrentStage >= 9 && this.shotsSincePaint >= ShotsPerPaint)
        {
            this.shotsSincePaint = 0;
            var details = this.GameBoard.GetBubbleDetails(GameBoard.PaintType);
            return new Bubble(Vector2.Zero, Vector2.Zero, this.currentBubbleRadius, details.Color, details.Type);
        }
        if (this.CurrentStage >= 5 && this.shotsSinceBomb >= ShotsPerBomb)
        {
            this.shotsSinceBomb = 0;
            var details = this.GameBoard.GetBubbleDetails(GameBoard.BombType);
            return new Bubble(Vector2.Zero, Vector2.Zero, this.currentBubbleRadius, details.Color, details.Type);
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
            var bubbleRadius = this.GameBoard.GetBubbleRadius();
            foreach (var sb in savedGame.Bubbles)
            {
                var bubbleTypeDetails = this.GameBoard.GetBubbleDetails(sb.BubbleType);
                this.GameBoard.Bubbles.Add(new Bubble(sb.Position, Vector2.Zero, bubbleRadius, bubbleTypeDetails.Color, bubbleTypeDetails.Type));
            }
            if (savedGame.NextBubble != null)
            {
                var nextBubbleDetails = this.GameBoard.GetBubbleDetails(savedGame.NextBubble.BubbleType);
                this.NextBubble = new Bubble(Vector2.Zero, Vector2.Zero, bubbleRadius, nextBubbleDetails.Color, nextBubbleDetails.Type);
            }
        }
    }

    public void ClearSavedGame()
    {
        this.configuration.SavedGame = null;
        this.configuration.Save();
    }
}
