using MessagePack;

namespace MazeWars.Client.Shared.NetworkModels;

/// <summary>
/// Essential world state updates sent periodically.
/// </summary>
[MessagePackObject(keyAsPropertyName: false)]
public class WorldStateEssentialData
{
    [Key(0)]
    public WorldInfoEssentialData WorldInfo { get; set; } = new();
}
