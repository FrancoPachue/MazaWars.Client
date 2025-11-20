using System.Collections.Generic;

namespace MazeWars.Client.Shared.NetworkModels;

public class EnhancedClientConnectedData : ClientConnectedData
{
    public PlayerStats PlayerStats { get; set; } = new();
    public List<string> AvailableCommands { get; set; } = new();
    public TeamInfo TeamInfo { get; set; } = new();
}
