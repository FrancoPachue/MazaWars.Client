using MessagePack;

namespace MazeWars.Client.Shared.NetworkModels;

/// <summary>
/// Extraction request from client to server.
/// </summary>
[MessagePackObject]
public class ExtractionMessage
{
    [Key(0)]
    public string Action { get; set; } = string.Empty;

    [Key(1)]
    public string ExtractionId { get; set; } = string.Empty;
}
