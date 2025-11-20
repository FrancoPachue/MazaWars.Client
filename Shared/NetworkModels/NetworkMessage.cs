using System;
using MessagePack;

namespace MazeWars.Client.Shared.NetworkModels;

/// <summary>
/// Base network message with MessagePack serialization support.
///
/// This follows the MessagePack specification where the Type field acts as a discriminator
/// and the Data field contains pre-serialized MessagePack bytes of the actual message payload.
/// This approach is portable across all MessagePack implementations.
/// </summary>
[MessagePackObject]
public class NetworkMessage
{
    [Key(0)]
    public string Type { get; set; } = string.Empty;

    [Key(1)]
    public string PlayerId { get; set; } = string.Empty;

    /// <summary>
    /// Pre-serialized MessagePack data.
    /// Deserialize based on the Type field using: MessagePackSerializer.Deserialize&lt;T&gt;(Data)
    /// </summary>
    [Key(2)]
    public byte[] Data { get; set; } = Array.Empty<byte>();

    [Key(3)]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
