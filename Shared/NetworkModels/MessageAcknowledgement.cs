using MessagePack;

namespace MazeWars.Client.Shared.NetworkModels;

/// <summary>
/// Message acknowledgement from client to server.
/// </summary>
[MessagePackObject(keyAsPropertyName: false)]
public class MessageAcknowledgement
{
    [Key(0)]
    public string MessageId { get; set; } = string.Empty;

    [Key(1)]
    public bool Success { get; set; } = true;

    [Key(2)]
    public string ErrorMessage { get; set; } = string.Empty;
}
