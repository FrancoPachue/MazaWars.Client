using System.Collections.Generic;
using MessagePack;

namespace MazeWars.Client.Shared.NetworkModels;

/// <summary>
/// Batch of player states sent by server
/// </summary>
[MessagePackObject]
public class PlayerStatesBatch
{
	[Key(0)]
	public List<PlayerStateUpdate> Players { get; set; } = new();

	[Key(1)]
	public float ServerTime { get; set; }

	[Key(2)]
	public int FrameNumber { get; set; }
}
