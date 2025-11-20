using System;
using MessagePack;

namespace MazeWars.Client.Shared.NetworkModels;

/// <summary>
/// Base network message with MessagePack serialization support.
/// Uses MessagePackSerializer.Typeless to properly handle the object Data field.
/// </summary>
[MessagePackObject]
public class NetworkMessage
{
    [Key(0)]
    public string Type { get; set; } = string.Empty;

    [Key(1)]
    public string PlayerId { get; set; } = string.Empty;

    /// <summary>
    /// Data field - must be deserialized using MessagePackSerializer.Typeless
    /// to preserve type information across serialization boundaries.
    /// </summary>
    [Key(2)]
    public object Data { get; set; } = null!;

    [Key(3)]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
