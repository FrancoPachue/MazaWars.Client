using System.Collections.Generic;
using MazeWars.Client.Shared.NetworkModels;

namespace MazeWars.Client.Shared.NetworkModels;

public class ExtractionPointUpdate
{
    public string ExtractionId { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public Vector2 Position { get; set; }
    public List<ExtractionProgress> PlayersExtracting { get; set; } = new();
}
