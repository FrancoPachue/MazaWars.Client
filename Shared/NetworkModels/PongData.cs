using MessagePack;
using System;

namespace MazeWars.Client.Shared.NetworkModels;

/// <summary>
/// Server response to ping request.
/// </summary>
[MessagePackObject(keyAsPropertyName: false)]
public class PongData
{
    [Key(0)]
    public object ClientData { get; set; } = null!;

    [Key(1)]
    public DateTime ServerTime { get; set; }
}
