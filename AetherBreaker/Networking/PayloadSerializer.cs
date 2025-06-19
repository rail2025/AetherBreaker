using System;
using System.IO;
using AetherBreaker.Networking;

namespace AetherBreaker.Serialization
{
    /// <summary>
    /// Handles the binary serialization and deserialization of the NetworkPayload object.
    /// </summary>
    public static class PayloadSerializer
    {
        /// <summary>
        /// Serializes a NetworkPayload object into a byte array.
        /// </summary>
        public static byte[] Serialize(NetworkPayload payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            using (var memoryStream = new MemoryStream())
            using (var writer = new BinaryWriter(memoryStream))
            {
                // Write the action type as a single byte.
                writer.Write((byte)payload.Action);

                // Write the data payload.
                if (payload.Data != null && payload.Data.Length > 0)
                {
                    writer.Write(payload.Data.Length);
                    writer.Write(payload.Data);
                }
                else
                {
                    writer.Write(0);
                }

                return memoryStream.ToArray();
            }
        }

        /// <summary>
        /// Deserializes a byte array back into a NetworkPayload object.
        /// </summary>
        public static NetworkPayload? Deserialize(byte[] data)
        {
            if (data == null || data.Length == 0)
                return null;

            try
            {
                using (var memoryStream = new MemoryStream(data))
                using (var reader = new BinaryReader(memoryStream))
                {
                    var payload = new NetworkPayload();

                    // Read Action
                    if (reader.BaseStream.Position + sizeof(byte) > reader.BaseStream.Length) return null;
                    payload.Action = (PayloadActionType)reader.ReadByte();

                    // Read data length
                    if (reader.BaseStream.Position + sizeof(int) > reader.BaseStream.Length) return null;
                    int dataLength = reader.ReadInt32();

                    // Read data array
                    if (dataLength > 0)
                    {
                        if (reader.BaseStream.Position + dataLength > reader.BaseStream.Length) return null;
                        payload.Data = reader.ReadBytes(dataLength);
                    }
                    else
                    {
                        payload.Data = null;
                    }

                    return payload;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error(ex, "Failed to deserialize NetworkPayload.");
                return null;
            }
        }
    }
}
