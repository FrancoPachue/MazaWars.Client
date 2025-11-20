using System.Collections.Generic;

namespace MazeWars.Client.Shared.NetworkModels;

public class InventoryUpdate
{
    public string PlayerId { get; set; } = string.Empty;
    public List<InventoryItem> Items { get; set; } = new();
    public int TotalValue { get; set; }
}
