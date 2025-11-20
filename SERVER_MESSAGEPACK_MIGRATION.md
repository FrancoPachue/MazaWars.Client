# Server MessagePack Migration Guide

## Overview
The client now uses **MessagePack consistently** for all network messages, including the initial connection handshake. The server needs to be updated to deserialize incoming messages using MessagePack instead of JSON.

## Current Server Implementation (JSON)
The server currently uses JSON deserialization in `NetworkService.cs`:

```csharp
private void ProcessIncomingMessage(IPEndPoint clientEndpoint, byte[] messageData)
{
    var messageJson = Encoding.UTF8.GetString(messageData);
    var message = JsonConvert.DeserializeObject<NetworkMessage>(messageJson);
    // ... rest of the code
}
```

## Required Changes

### 1. Add MessagePack NuGet Package
Ensure the server project has MessagePack installed:

```xml
<PackageReference Include="MessagePack" Version="2.5.140" />
<PackageReference Include="MessagePackAnalyzer" Version="2.5.140">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
```

### 2. Update NetworkService.cs

Replace JSON deserialization with MessagePack:

```csharp
using MessagePack; // Add this using statement

private void ProcessIncomingMessage(IPEndPoint clientEndpoint, byte[] messageData)
{
    try
    {
        // Deserialize with MessagePack instead of JSON
        var message = MessagePackSerializer.Deserialize<NetworkMessage>(messageData);

        // Rest of the code remains the same...
        if (message.Type == "connect")
        {
            HandleClientConnect(clientEndpoint, message);
        }
        // ... other message handlers
    }
    catch (MessagePackSerializationException ex)
    {
        _logger.LogError($"Failed to deserialize MessagePack from {clientEndpoint}: {ex.Message}");
    }
    catch (Exception ex)
    {
        _logger.LogError($"Error processing message from {clientEndpoint}: {ex.Message}");
    }
}
```

### 3. Update NetworkMessage Model

Ensure the server's `NetworkMessage` class has MessagePack attributes:

```csharp
using MessagePack;

[MessagePackObject]
public class NetworkMessage
{
    [Key(0)]
    public string Type { get; set; } = string.Empty;

    [Key(1)]
    public string PlayerId { get; set; } = string.Empty;

    [Key(2)]
    public object Data { get; set; } = null!;

    [Key(3)]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
```

### 4. Update Response Serialization (if needed)

If the server sends responses using JSON, update those to use MessagePack:

**Before (JSON):**
```csharp
var json = JsonConvert.SerializeObject(response);
var bytes = Encoding.UTF8.GetBytes(json);
await SendToClientAsync(clientEndpoint, bytes);
```

**After (MessagePack):**
```csharp
var bytes = MessagePackSerializer.Serialize(response);
await SendToClientAsync(clientEndpoint, bytes);
```

### 5. Update All DTOs

Ensure all DTOs have MessagePack attributes. Here are the key ones:

#### ClientConnectData
```csharp
[MessagePackObject]
public class ClientConnectData
{
    [Key(0)] public string PlayerName { get; set; } = string.Empty;
    [Key(1)] public string PlayerClass { get; set; } = string.Empty;
    [Key(2)] public string TeamId { get; set; } = string.Empty;
    [Key(3)] public string AuthToken { get; set; } = string.Empty;
}
```

#### ConnectResponseData
```csharp
[MessagePackObject]
public class ConnectResponseData
{
    [Key(0)] public bool Success { get; set; }
    [Key(1)] public string PlayerId { get; set; } = string.Empty;
    [Key(2)] public string SessionToken { get; set; } = string.Empty;
    [Key(3)] public string ErrorMessage { get; set; } = string.Empty;
}
```

#### PlayerStateUpdate
```csharp
[MessagePackObject]
public class PlayerStateUpdate
{
    [Key(0)] public string PlayerId { get; set; } = string.Empty;
    [Key(1)] public Vector2 Position { get; set; }
    [Key(2)] public Vector2 Velocity { get; set; }
    [Key(3)] public float Direction { get; set; }
    [Key(4)] public int Health { get; set; }
    [Key(5)] public int MaxHealth { get; set; }
    [Key(6)] public bool IsAlive { get; set; }
    [Key(7)] public bool IsMoving { get; set; }
    [Key(8)] public bool IsCasting { get; set; }
    [Key(9)] public string PlayerName { get; set; } = string.Empty;
    [Key(10)] public string PlayerClass { get; set; } = string.Empty;
}
```

**Note:** Keys 9 and 10 were recently added to PlayerStateUpdate. Make sure to add these properties to the server's DTO as well.

## Benefits of MessagePack

1. **Performance**: 5-10x faster serialization/deserialization than JSON
2. **Size**: 30-50% smaller payload size than JSON
3. **Consistency**: Same serialization format across all messages
4. **Type Safety**: Stronger typing with binary format
5. **UDP Optimization**: Smaller packets = less fragmentation, better for real-time games

## Testing

After making these changes:

1. Start the server with MessagePack deserialization
2. Connect from the client (which now sends MessagePack)
3. Verify connection succeeds and messages are properly exchanged
4. Check server logs for any MessagePackSerializationException errors

## Rollback Plan

If issues occur, you can temporarily support both formats:

```csharp
private void ProcessIncomingMessage(IPEndPoint clientEndpoint, byte[] messageData)
{
    NetworkMessage message = null;

    // Try MessagePack first (new clients)
    try
    {
        message = MessagePackSerializer.Deserialize<NetworkMessage>(messageData);
    }
    catch
    {
        // Fallback to JSON (old clients)
        try
        {
            var messageJson = Encoding.UTF8.GetString(messageData);
            message = JsonConvert.DeserializeObject<NetworkMessage>(messageJson);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to deserialize message: {ex.Message}");
            return;
        }
    }

    // Process message...
}
```

## Related Client Changes

The client has been updated in this commit to:
- Send connection messages using MessagePack (UdpNetworkClient.cs)
- Remove System.Text.Json dependency for connection messages
- Use consistent MessagePack serialization across all network communication

## Questions?

If you encounter any issues during migration, check:
1. MessagePack package is properly installed
2. All DTOs have [MessagePackObject] and [Key] attributes
3. No calculated properties without [IgnoreMember]
4. Vector2 has implicit conversion operators if using custom Vector2 type
