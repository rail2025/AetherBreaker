using System;
using System.Numerics;
using AetherBreaker.Game;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace AetherBreaker.Windows
{
    public class MainWindow : Window, IDisposable
    {
        private readonly Plugin plugin;
        private readonly GameBoard gameBoard;
        private Vector2 launcherPosition;
        private Bubble? activeBubble;
        private Bubble? nextBubble;
        private readonly Random random = new Random();

        public MainWindow(Plugin plugin) : base("AetherBreaker")
        {
            this.plugin = plugin;
            this.gameBoard = new GameBoard(15f);

            Size = new Vector2(450, 600);
            SizeCondition = ImGuiCond.Always;
            Flags |= ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;

            nextBubble = CreateRandomBubble(Vector2.Zero);
        }

        public void Dispose() { }

        public override void PreDraw()
        {
            if (plugin.Configuration.IsGameWindowLocked)
            {
                Flags |= ImGuiWindowFlags.NoMove;
            }
            else
            {
                Flags &= ~ImGuiWindowFlags.NoMove;
            }
        }

        public override void Draw()
        {
            try
            {
                var windowPos = ImGui.GetWindowPos();
                var windowSize = ImGui.GetWindowSize();
                launcherPosition = new Vector2(
                    windowPos.X + windowSize.X * 0.5f,
                    windowPos.Y + windowSize.Y - 50f
                );

                var drawList = ImGui.GetWindowDrawList();
                float launcherRadius = 20f;

                drawList.AddCircleFilled(
                    launcherPosition,
                    launcherRadius,
                    ImGui.GetColorU32(new Vector4(0.8f, 0.8f, 0.8f, 1.0f))
                );

                if (nextBubble != null)
                {
                    var previewPos = new Vector2(
                        launcherPosition.X + 40f,
                        launcherPosition.Y - 10f
                    );
                    drawList.AddCircleFilled(previewPos, nextBubble.Radius, nextBubble.Color);
                }

                if (ImGui.IsWindowHovered())
                {
                    HandleAiming(drawList, windowPos, launcherRadius);
                }

                UpdateActiveBubble(windowPos, windowSize, drawList);
                gameBoard.Draw(drawList);
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Error during MainWindow.Draw()");
            }
        }

        private void HandleAiming(ImDrawListPtr drawList, Vector2 windowPos, float launcherRadius)
        {
            var mousePos = ImGui.GetMousePos();
            var direction = Vector2.Normalize(mousePos - launcherPosition);

            if (direction.Y > -0.1f)
            {
                direction.Y = -0.1f;
                direction = Vector2.Normalize(direction);
            }

            var aimLineStart = launcherPosition + (direction * (launcherRadius + 2f));
            var aimLineEnd = launcherPosition + (direction * 100f);
            drawList.AddLine(
                aimLineStart,
                aimLineEnd,
                ImGui.GetColorU32(new Vector4(1.0f, 1.0f, 1.0f, 0.5f)),
                3f
            );

            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && activeBubble == null)
            {
                FireBubble(direction, launcherRadius);
            }
        }

        private void FireBubble(Vector2 direction, float launcherRadius)
        {
            const float bubbleRadius = 15f;
            const float bubbleSpeed = 500f;
            var startPos = launcherPosition + (direction * (launcherRadius + bubbleRadius));

            if (nextBubble != null)
            {
                activeBubble = nextBubble;
                activeBubble.Position = startPos;
                activeBubble.Velocity = direction * bubbleSpeed;
            }
            else
            {
                activeBubble = CreateRandomBubble(startPos);
                activeBubble.Velocity = direction * bubbleSpeed;
            }

            nextBubble = CreateRandomBubble(Vector2.Zero);
        }

        private Bubble CreateRandomBubble(Vector2 position)
        {
            var bubbleTypes = new[]
            {
                (Color: ImGui.GetColorU32(new Vector4(1.0f, 0.2f, 0.2f, 1.0f)), Type: 0),
                (Color: ImGui.GetColorU32(new Vector4(0.2f, 1.0f, 0.2f, 1.0f)), Type: 1),
                (Color: ImGui.GetColorU32(new Vector4(0.2f, 0.2f, 1.0f, 1.0f)), Type: 2),
                (Color: ImGui.GetColorU32(new Vector4(1.0f, 1.0f, 0.2f, 1.0f)), Type: 3)
            };

            var selectedType = bubbleTypes[random.Next(bubbleTypes.Length)];
            return new Bubble(
                position,
                Vector2.Zero,
                15f,
                selectedType.Color,
                selectedType.Type
            );
        }

        private void UpdateActiveBubble(Vector2 windowPos, Vector2 windowSize, ImDrawListPtr drawList)
        {
            if (activeBubble == null) return;

            activeBubble.Position += activeBubble.Velocity * ImGui.GetIO().DeltaTime;

            if (activeBubble.Position.X - activeBubble.Radius < windowPos.X ||
                activeBubble.Position.X + activeBubble.Radius > windowPos.X + windowSize.X)
            {
                activeBubble.Velocity.X *= -1;
            }

            if (gameBoard.CheckCollision(activeBubble, windowPos.Y))
            {
                if (gameBoard.TryAddBubble(activeBubble, windowPos.Y))
                {
                    activeBubble = null;
                }
            }

            if (activeBubble != null &&
                activeBubble.Position.Y > windowPos.Y + windowSize.Y + activeBubble.Radius)
            {
                activeBubble = null;
            }
            else if (activeBubble != null)
            {
                drawList.AddCircleFilled(activeBubble.Position, activeBubble.Radius, activeBubble.Color);
            }
        }
    }
}
