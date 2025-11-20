using System.Collections.Generic;
using MazeWars.Client.Shared.NetworkModels;
using MessagePack;

namespace MazeWars.Client.Shared.NetworkModels;

[MessagePackObject]
public class CombatEvent
{
    [Key(0)]
    public string EventType { get; set; } = string.Empty;

    [Key(1)]
    public string SourceId { get; set; } = string.Empty;

    [Key(2)]
    public string TargetId { get; set; } = string.Empty;

    [Key(3)]
    public int Value { get; set; }

    [Key(4)]
    public Vector2 Position { get; set; }

    [IgnoreMember] // Not serializable with MessagePack (Dictionary<string, object>)
    public Dictionary<string, object> AdditionalData { get; internal set; }
}
