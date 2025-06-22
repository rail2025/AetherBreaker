namespace AetherBreaker.Networking
{
    /// <summary>
    /// Defines the different types of messages that can be sent between the client and server.
    /// </summary>
    public enum MessageType : byte
    {
        ATTACK,
        GAME_STATE_UPDATE,
        MATCH_CONTROL,
    }

    /// <summary>
    /// Defines the specific action being performed within a message.
    /// </summary>
    public enum PayloadActionType : byte
    {
        SendJunkRows,
        UpdateScore,
        OpponentBoardState,
        Ready,
        Rematch,
        Disconnect,
    }

    /// <summary>
    /// Represents the data structure sent over the network.
    /// </summary>
    public class NetworkPayload
    {
        /// <summary>
        /// The specific action to be performed.
        /// </summary>
        public PayloadActionType Action { get; set; }

        /// <summary>
        /// The binary data associated with the action.
        /// </summary>
        public byte[]? Data { get; set; }
    }
}