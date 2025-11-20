using MessagePack;
using System.Collections.Generic;

namespace MazeWars.Client.Shared.NetworkModels;

/// <summary>
/// Combat events that occurred during the frame.
/// </summary>
[MessagePackObject(keyAsPropertyName: false)]
public class CombatEventsData
{
    [Key(0)]
    public List<CombatEvent> CombatEvents { get; set; } = new();
}
