using System.Collections.Generic;
using MazeWars.Client.Shared.NetworkModels;

namespace MazeWars.Client.Shared.NetworkModels;

public class ServerInfo
{
    public int TickRate { get; set; }
    public Vector2 WorldBounds { get; set; }
    public Dictionary<string, object> GameConfig { get; set; } = new();
}
