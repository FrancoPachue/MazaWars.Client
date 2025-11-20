using MessagePack;

namespace MazeWars.Client.Shared.NetworkModels;

/// <summary>
/// Loot grab request from client to server.
/// </summary>
[MessagePackObject(keyAsPropertyName: false)]
public class LootGrabMessage
{
    [Key(0)]
    public string LootId { get; set; } = string.Empty;
}
