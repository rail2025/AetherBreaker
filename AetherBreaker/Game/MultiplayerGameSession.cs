using System;
using System.Collections.Generic;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using AetherBreaker.Audio;
using AetherBreaker.Networking;
using AetherBreaker.Windows;

namespace AetherBreaker.Game
{
    public class MultiplayerGameSession
    {
        public GameState CurrentGameState { get; private set; }
        public MultiplayerGameBoard? GameBoard { get; private set; }
        public Bubble? ActiveBubble { get; private set; }
        public Bubble? NextBubble { get; private set; }
        public List<BubbleAnimation> ActiveBubbleAnimations { get; } = new();
        public List<TextAnimation> ActiveTextAnimations { get; } = new();

        public enum MultiplayerMatchState { None, WaitingForOpponent, RoundStarting, RoundInProgress, RoundOver, MatchOver }
        public MultiplayerMatchState CurrentMatchState { get; private set; }
        public int MyScore { get; private set; }
        public int OpponentScore { get; private set; }
        public byte[]? OpponentBoardState { get; private set; }
        public bool PlayerWon { get; private set; } = false;
        public bool IsHelperLineActive { get; private set; } = false;
        private int attackCharge = 0;
        private bool hasRequestedRematch = false;

        private readonly NetworkManager networkManager;
        private readonly AudioManager audioManager;

        private const float BubbleSpeed = 60f;

        private float abstractGameOverLineY;
        private float topDownPressureTimer;
        private const float TopDownPressureInterval = 10f;
        private float gameStateSendTimer;
        private const float GameStateSendInterval = 0.5f;

        private readonly List<int> roundSeeds = new();
        private int currentRound = 0;
        private readonly List<Bubble> bubbleQueue = new();
        private Random bubbleRandom = new();

        private readonly Queue<int> incomingJunkQueue = new();

        public MultiplayerGameSession(NetworkManager networkManager, AudioManager audioManager)
        {
            this.networkManager = networkManager;
            this.audioManager = audioManager;
            this.CurrentGameState = GameState.MainMenu;
            this.CurrentMatchState = MultiplayerMatchState.WaitingForOpponent;

            this.networkManager.OnMatchControlReceived += HandleMatchControl;
            this.networkManager.OnAttackReceived += HandleAttackReceived;
        }

        public float GetAbstractGameOverLineY() => this.abstractGameOverLineY;

        private void HandleMatchControl(PayloadActionType action)
        {
            if (action == PayloadActionType.Rematch)
            {
                StartNewRound();
                this.CurrentGameState = GameState.InGame;
            }
        }

        public void StartNewMatch(string passphrase)
        {
            this.roundSeeds.Clear();
            using (var sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(passphrase));
                for (int i = 0; i < 9; i++)
                {
                    int seed = BitConverter.ToInt32(hash, (i * 4) % hash.Length);
                    this.roundSeeds.Add(seed);
                }
            }

            this.currentRound = 0;
            this.MyScore = 0;
            this.OpponentScore = 0;
            StartNewRound();
            this.audioManager.StartBgmPlaylist();
            this.CurrentGameState = GameState.InGame;
        }

        public void StartNewRound()
        {
            int seed = this.roundSeeds[Math.Min(this.currentRound, this.roundSeeds.Count - 1)];
            this.bubbleRandom = new Random(seed);

            this.GameBoard = new MultiplayerGameBoard(seed);
            this.GameBoard.InitializeBoard();

            float abstractGameHeight = this.GameBoard.AbstractWidth * (MainWindow.BaseWindowSize.Y / MainWindow.BaseWindowSize.X);
            float abstractHudHeight = this.GameBoard.AbstractWidth * (MainWindow.HudAreaHeight / MainWindow.BaseWindowSize.X);
            this.abstractGameOverLineY = abstractGameHeight - abstractHudHeight;

            this.ActiveBubble = null;
            this.ActiveBubbleAnimations.Clear();
            this.ActiveTextAnimations.Clear();
            this.attackCharge = 0;
            this.topDownPressureTimer = TopDownPressureInterval;
            this.gameStateSendTimer = 0;
            this.hasRequestedRematch = false;
            this.IsHelperLineActive = false;
            this.currentRound++;

            GenerateBubbleQueue();
            PopulateNextBubble();
        }

        private void GenerateBubbleQueue()
        {
            this.bubbleQueue.Clear();
            if (this.GameBoard == null) return;
            var bubbleRadius = this.GameBoard.GetBubbleRadius();
            var availableTypes = this.GameBoard.allBubbleColorTypes;
            if (!availableTypes.Any()) return;
            for (int i = 0; i < 500; i++)
            {
                var bubbleType = availableTypes[this.bubbleRandom.Next(availableTypes.Length)];
                var newBubble = new Bubble(Vector2.Zero, Vector2.Zero, bubbleRadius, bubbleType.Color, bubbleType.Type);
                this.bubbleQueue.Add(newBubble);
            }
        }

        private void PopulateNextBubble()
        {
            if (this.bubbleQueue.Any())
            {
                this.NextBubble = this.bubbleQueue[0];
                this.bubbleQueue.RemoveAt(0);
            }
            else
            {
                GenerateBubbleQueue();
                if (this.bubbleQueue.Any())
                {
                    this.NextBubble = this.bubbleQueue[0];
                    this.bubbleQueue.RemoveAt(0);
                }
                else
                {
                    this.NextBubble = null;
                }
            }
        }

        public void Update(float deltaTime)
        {
            if (this.CurrentGameState != GameState.InGame) return;
            this.topDownPressureTimer -= deltaTime;
            if (this.topDownPressureTimer <= 0f)
            {
                this.GameBoard?.AdvanceAndRefillBoard();
                this.topDownPressureTimer = TopDownPressureInterval;
                this.audioManager.PlaySfx("advance.wav");
            }
            this.gameStateSendTimer += deltaTime;
            if (this.gameStateSendTimer >= GameStateSendInterval)
            {
                this.gameStateSendTimer = 0f;
                if (this.GameBoard != null)
                {
                    var boardState = this.GameBoard.SerializeBoardState();
                    if (boardState.Length > 0)
                    {
                        _ = this.networkManager.SendGameState(boardState);
                    }
                }
            }
            UpdateActiveBubble(deltaTime);
            if (this.GameBoard != null && this.GameBoard.Bubbles.Any(b => b.Position.Y + b.Radius >= this.abstractGameOverLineY))
            {
                this.PlayerWon = false;
                this.OpponentScore++;
                this.CurrentGameState = GameState.GameOver;
            }

            if (this.incomingJunkQueue.Count > 0)
            {
                int junkToProcess = this.incomingJunkQueue.Dequeue();
                this.GameBoard?.AddJunkToBottom(junkToProcess);
            }
        }

        private void UpdateActiveBubble(float deltaTime)
        {
            if (this.ActiveBubble == null || this.GameBoard == null) return;
            this.ActiveBubble.Position += this.ActiveBubble.Velocity * deltaTime;

            if (this.ActiveBubble.Position.X - this.ActiveBubble.Radius < 0)
            {
                this.ActiveBubble.Velocity.X *= -1;
                this.ActiveBubble.Position.X = this.ActiveBubble.Radius;
                this.audioManager.PlaySfx("bounce.wav");
            }
            else if (this.ActiveBubble.Position.X + this.ActiveBubble.Radius > this.GameBoard.AbstractWidth)
            {
                this.ActiveBubble.Velocity.X *= -1;
                this.ActiveBubble.Position.X = this.GameBoard.AbstractWidth - this.ActiveBubble.Radius;
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
            var clearResult = new ClearResult();
            bool wasSpecialAction = true;
            switch (activeBubble.BubbleType)
            {
                case MultiplayerGameBoard.StarType:
                    if (collidedWith.BubbleType >= 0)
                        clearResult = this.GameBoard.ActivateStar(collidedWith.BubbleType);
                    clearResult.PoppedBubbles.Add(activeBubble);
                    break;
                case MultiplayerGameBoard.PaintType:
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
                else if (collidedWith.BubbleType == MultiplayerGameBoard.StarType)
                {
                    clearResult = this.GameBoard.ActivateStar(activeBubble.BubbleType);
                    if (this.GameBoard.Bubbles.Remove(collidedWith))
                        clearResult.PoppedBubbles.Add(collidedWith);
                    clearResult.PoppedBubbles.Add(activeBubble);
                }
                else if (collidedWith.BubbleType == MultiplayerGameBoard.PaintType)
                {
                    this.GameBoard.ActivatePaint(collidedWith, activeBubble);
                    clearResult.PoppedBubbles.Add(activeBubble);
                    if (this.GameBoard.Bubbles.Remove(collidedWith))
                        clearResult.PoppedBubbles.Add(collidedWith);
                }
                else if (collidedWith.BubbleType == MultiplayerGameBoard.MirrorType)
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

        private void HandleClearResult(ClearResult clearResult)
        {
            if (this.GameBoard == null || clearResult == null) return;
            if (clearResult.HelperLineActivated)
            {
                this.IsHelperLineActive = true;
            }
            var totalPopped = clearResult.PoppedBubbles.Count + clearResult.DroppedBubbles.Count;
            if (totalPopped > 0)
            {
                this.attackCharge += totalPopped;
                if (this.attackCharge >= 6)
                {
                    int junkToSend = (int)(this.attackCharge * 0.75f);
                    if (junkToSend > 0)
                    {
                        _ = this.networkManager.SendAttackData(junkToSend);
                    }
                    this.attackCharge = 0;
                }
            }
            if (clearResult.PoppedBubbles.Any())
            {
                this.ActiveBubbleAnimations.Add(new BubbleAnimation(clearResult.PoppedBubbles, BubbleAnimationType.Pop, 0.2f));
                foreach (var _ in clearResult.PoppedBubbles) this.audioManager.PlaySfx("pop.wav");
            }
            if (clearResult.DroppedBubbles.Any())
            {
                this.ActiveBubbleAnimations.Add(new BubbleAnimation(clearResult.DroppedBubbles, BubbleAnimationType.Drop, 1.5f));
                this.audioManager.PlaySfx("drop.wav");
            }
        }

        public void GoToMainMenu()
        {
            this.CurrentGameState = GameState.MainMenu;
            this.networkManager.OnMatchControlReceived -= HandleMatchControl;
            this.networkManager.OnAttackReceived -= HandleAttackReceived;
            _ = this.networkManager.DisconnectAsync();
        }

        public void SetGameState(GameState newState)
        {
            this.CurrentGameState = newState;
        }

        public void FireBubble(Vector2 direction, Vector2 abstractStartPosition)
        {
            if (this.NextBubble == null) return;
            this.audioManager.PlaySfx("fire.wav");
            this.ActiveBubble = this.NextBubble;
            this.ActiveBubble.Position = abstractStartPosition;
            this.ActiveBubble.Velocity = direction * BubbleSpeed;
            PopulateNextBubble();
        }

        private void HandleAttackReceived(int junkAmount)
        {
            this.incomingJunkQueue.Enqueue(junkAmount);
        }

        public void ReceiveOpponentBoardState(byte[] state)
        {
            this.OpponentBoardState = state;
        }

        public void RequestRematch()
        {
            if (hasRequestedRematch) return;
            _ = networkManager.SendMatchControl(PayloadActionType.Rematch);
            this.hasRequestedRematch = true;
        }
    }
}
