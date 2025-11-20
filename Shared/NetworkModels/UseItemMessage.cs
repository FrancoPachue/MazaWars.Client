using MazeWars.Client.Shared.NetworkModels;
using MessagePack;

namespace MazeWars.Client.Shared.NetworkModels;

/// <summary>
/// Use item request from client to server.
/// </summary>
[MessagePackObject]
public class UseItemMessage
{
    [Key(0)]
    public string ItemId { get; set; } = string.Empty;

    [Key(1)]
    public string ItemType { get; set; } = string.Empty;

    [Key(2)]
    public Vector2 TargetPosition { get; set; }
}
