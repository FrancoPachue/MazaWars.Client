using System.Collections.Generic;

namespace MazeWars.Client.Shared.NetworkModels;

public class WorldStateMessage
{
    public List<RoomStateUpdate> Rooms { get; set; } = new();
    public List<ExtractionPointUpdate> ExtractionPoints { get; set; } = new();
    public WorldInfo WorldInfo { get; set; } = new();
}
