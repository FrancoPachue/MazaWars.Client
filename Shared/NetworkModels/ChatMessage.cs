using MessagePack;

namespace MazeWars.Client.Shared.NetworkModels;

/// <summary>
/// Chat message from client to server.
/// </summary>
[MessagePackObject]
public class ChatMessage
{
    [Key(0)]
    public string Message { get; set; } = string.Empty;

    [Key(1)]
    public string ChatType { get; set; } = "team";
}
