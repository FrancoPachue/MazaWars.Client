using System;
using MessagePack;

namespace MazeWars.Client.Shared.NetworkModels;

/// <summary>
/// Generic network message that uses MessagePack's dynamic object handling
/// This allows us to deserialize the Data field properly regardless of its type
/// </summary>
[MessagePackObject]
public class GenericNetworkMessage
{
	[Key(0)]
	public string Type { get; set; } = string.Empty;

	[Key(1)]
	public string PlayerId { get; set; } = string.Empty;

	[Key(2)]
	public byte[] Data { get; set; } = null!; // Store as raw bytes instead of object

	[Key(3)]
	public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
