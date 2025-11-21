using MessagePack;

namespace MazeWars.Client.Shared.NetworkModels;

/// <summary>
/// Frame synchronization marker for chunked updates.
/// </summary>
[MessagePackObject(keyAsPropertyName: false)]
public class FrameUpdateData
{
    [Key(0)]
    public ulong FrameNumber { get; set; }
}
