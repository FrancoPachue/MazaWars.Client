using System.Collections.Generic;

namespace MazeWars.Client.Shared.NetworkModels;

public class TradeRequestMessage
{
    public string TargetPlayerId { get; set; } = string.Empty;
    public List<string> OfferedItemIds { get; set; } = new();
    public List<string> RequestedItemIds { get; set; } = new();
}
