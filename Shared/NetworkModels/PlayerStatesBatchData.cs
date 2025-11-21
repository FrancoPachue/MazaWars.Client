using MessagePack;
using System.Collections.Generic;

namespace MazeWars.Client.Shared.NetworkModels;

/// <summary>
/// Batch of player states to reduce network overhead.
/// Includes acknowledged input sequence numbers for client-side prediction reconciliation.
/// </summary>
[MessagePackObject(keyAsPropertyName: false)]
public class PlayerStatesBatchData
{
    [Key(0)]
    public List<PlayerUpdateData> Players { get; set; } = new();

    [Key(1)]
    public int BatchIndex { get; set; }

    [Key(2)]
    public int TotalBatches { get; set; }

    /// <summary>
    /// Dictionary mapping player IDs to their last acknowledged input sequence number.
    /// Used for client-side prediction reconciliation.
    /// </summary>
    [Key(3)]
    public Dictionary<string, uint> AcknowledgedInputs { get; set; } = new();
}
