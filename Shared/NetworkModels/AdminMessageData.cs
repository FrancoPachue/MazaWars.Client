using MessagePack;
using System;

namespace MazeWars.Client.Shared.NetworkModels;

/// <summary>
/// Administrative message broadcast to players.
/// </summary>
[MessagePackObject(keyAsPropertyName: false)]
public class AdminMessageData
{
    [Key(0)]
    public string Message { get; set; } = string.Empty;

    [Key(1)]
    public DateTime Timestamp { get; set; }

    [Key(2)]
    public bool IsSystemMessage { get; set; }
}
