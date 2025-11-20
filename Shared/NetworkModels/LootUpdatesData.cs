using MessagePack;
using System.Collections.Generic;

namespace MazeWars.Client.Shared.NetworkModels;

/// <summary>
/// Loot updates that occurred during the frame.
/// </summary>
[MessagePackObject(keyAsPropertyName: false)]
public class LootUpdatesData
{
    [Key(0)]
    public List<LootUpdate> LootUpdates { get; set; } = new();
}
