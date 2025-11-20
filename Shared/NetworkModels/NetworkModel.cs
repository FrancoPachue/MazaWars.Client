using System;
using System.Net;
using MazeWars.Client.Shared.NetworkModels;

namespace MazeWars.Client.Shared.NetworkModels;

public class ConnectedClient
{
    public IPEndPoint EndPoint { get; set; } = null!;
    public RealTimePlayer Player { get; set; } = null!;
    public string WorldId { get; set; } = string.Empty;
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
}
