using MessagePack;

namespace MazeWars.Client.Shared.NetworkModels;

/// <summary>
/// World boundary dimensions.
/// </summary>
[MessagePackObject(keyAsPropertyName: false)]
public class WorldBoundsData
{
    [Key(0)]
    public int X { get; set; }

    [Key(1)]
    public int Y { get; set; }
}
