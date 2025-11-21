using System;
using MessagePack;

namespace MazeWars.Client.Shared.NetworkModels;

/// <summary>
/// Base network message with MessagePack serialization support.
/// Data field is object type and MessagePack handles recursive serialization automatically.
/// </summary>
[MessagePackObject(keyAsPropertyName: false)]
public class NetworkMessage
{
    [Key(0)]
    public string Type { get; set; } = string.Empty;

    [Key(1)]
    public string PlayerId { get; set; } = string.Empty;

    [Key(2)]
    public object Data { get; set; } = null!;

    [Key(3)]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
