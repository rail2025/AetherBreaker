using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using AetherBreaker.Serialization;

namespace AetherBreaker.Networking
{
    /// <summary>
    /// Manages the WebSocket connection for AetherBreaker multiplayer.
    /// </summary>
    public class NetworkManager : IDisposable
    {
        private ClientWebSocket? webSocket;
        private CancellationTokenSource? cancellationTokenSource;

        // Events for different network activities
        public event Action? OnConnected;
        public event Action? OnDisconnected;
        public event Action<string>? OnError;
        public event Action<int>? OnAttackReceived; // Carries junk row count
        public event Action<byte[]>? OnGameStateUpdateReceived; // Carries opponent board state
        public event Action<PayloadActionType>? OnMatchControlReceived; // Carries ready, rematch, etc.

        public bool IsConnected => webSocket?.State == WebSocketState.Open;

        public async Task ConnectAsync(string serverUri, string passphrase)
        {
            if (IsConnected) return;

            try
            {
                webSocket = new ClientWebSocket();
                cancellationTokenSource = new CancellationTokenSource();
                Uri connectUri = new Uri($"{serverUri}?passphrase={Uri.EscapeDataString(passphrase)}");

                await webSocket.ConnectAsync(connectUri, cancellationTokenSource.Token);

                OnConnected?.Invoke();
                _ = Task.Run(() => StartListening(cancellationTokenSource.Token));
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Connection failed: {ex.Message}");
                await DisconnectAsync();
            }
        }

        public async Task DisconnectAsync()
        {
            if (webSocket == null) return;

            cancellationTokenSource?.Cancel();

            if (webSocket.State == WebSocketState.Open)
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnecting", cts.Token);
                }
                catch (Exception) { /* Ignore errors on close */ }
            }

            webSocket?.Dispose();
            webSocket = null;
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;

            OnDisconnected?.Invoke();
        }

        private async Task StartListening(CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];
            try
            {
                while (webSocket?.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    using var ms = new MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                        ms.Write(buffer, 0, result.Count);
                    } while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close)
                        await DisconnectAsync();
                    else
                        HandleReceivedMessage(ms.ToArray());
                }
            }
            catch (OperationCanceledException) { /* Expected */ }
            catch (Exception ex)
            {
                OnError?.Invoke($"Network error: {ex.Message}");
                await DisconnectAsync();
            }
        }

        private void HandleReceivedMessage(byte[] messageBytes)
        {
            if (messageBytes.Length < 1) return;

            MessageType type = (MessageType)messageBytes[0];
            byte[] payloadBytes = new byte[messageBytes.Length - 1];
            Array.Copy(messageBytes, 1, payloadBytes, 0, payloadBytes.Length);

            var payload = PayloadSerializer.Deserialize(payloadBytes);
            if (payload == null) return;
            if (payload.Data == null) return;

            switch (type)
            {
                case MessageType.ATTACK:
                    if (payload.Data.Length >= 4)
                    {
                        int junkRows = BitConverter.ToInt32(payload.Data, 0);
                        OnAttackReceived?.Invoke(junkRows);
                    }
                    break;

                case MessageType.GAME_STATE_UPDATE:
                    OnGameStateUpdateReceived?.Invoke(payload.Data);
                    break;

                case MessageType.MATCH_CONTROL:
                    OnMatchControlReceived?.Invoke(payload.Action);
                    break;
            }
        }

        private async Task SendMessageAsync(MessageType type, NetworkPayload payload)
        {
            if (!IsConnected || webSocket == null || cancellationTokenSource == null) return;

            try
            {
                byte[] payloadBytes = PayloadSerializer.Serialize(payload);
                byte[] messageToSend = new byte[1 + payloadBytes.Length];
                messageToSend[0] = (byte)type;
                Array.Copy(payloadBytes, 0, messageToSend, 1, payloadBytes.Length);

                await webSocket.SendAsync(new ArraySegment<byte>(messageToSend), WebSocketMessageType.Binary, true, cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Failed to send message: {ex.Message}");
                await DisconnectAsync();
            }
        }

        public async Task SendAttackData(int rowCount)
        {
            var payload = new NetworkPayload
            {
                Action = PayloadActionType.SendJunkRows,
                Data = BitConverter.GetBytes(rowCount)
            };
            await SendMessageAsync(MessageType.ATTACK, payload);
        }

        public async Task SendGameState(byte[] boardState)
        {
            var payload = new NetworkPayload
            {
                Action = PayloadActionType.OpponentBoardState,
                Data = boardState
            };
            await SendMessageAsync(MessageType.GAME_STATE_UPDATE, payload);
        }

        public async Task SendMatchControl(PayloadActionType action)
        {
            var payload = new NetworkPayload { Action = action, Data = null };
            await SendMessageAsync(MessageType.MATCH_CONTROL, payload);
        }

        public void Dispose()
        {
            DisconnectAsync().GetAwaiter().GetResult();
        }
    }
}
