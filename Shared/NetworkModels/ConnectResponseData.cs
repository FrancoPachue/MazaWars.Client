using MessagePack;

namespace MazeWars.Client.Shared.NetworkModels;

/// <summary>
/// Server response to initial connection with session token for reconnection.
/// </summary>
[MessagePackObject(keyAsPropertyName: false)]
public class ConnectResponseData
{
    [Key(0)]
    public string PlayerId { get; set; } = string.Empty;

    [Key(1)]
    public string WorldId { get; set; } = string.Empty;

    [Key(2)]
    public bool IsLobby { get; set; }

    /// <summary>
    /// Session token for reconnection.
    /// Client should save this and use it to reconnect if disconnected.
    /// </summary>
    [Key(3)]
    public string SessionToken { get; set; } = string.Empty;

    [Key(4)]
    public Vector2 SpawnPosition { get; set; }

    [Key(5)]
    public PlayerStatsData PlayerStats { get; set; } = new();

    [Key(6)]
    public ServerInfoData ServerInfo { get; set; } = new();

    [Key(7)]
    public ConnectedLobbyInfoData? LobbyInfo { get; set; }
}
