# Migración del Servidor: NetworkMessage con byte[] Data

## ⚠️ Cambio Importante

El cliente ahora usa `byte[] Data` en lugar de `object Data` en `NetworkMessage`. Este es el enfoque **correcto según la especificación de MessagePack**.

## ¿Por qué este cambio?

MessagePack **NO preserva información de tipo automáticamente**. La especificación define:
- Arrays, Maps, Strings, Numbers, Binary data
- **No hay "metadatos de tipo"** como en JSON.NET

El patrón correcto es:
1. **Campo Type como discriminador** ("player_states_batch", "connect_response", etc.)
2. **Campo Data como bytes pre-serializados** del payload real

## Cambios Necesarios en el Servidor

### ❌ Código Anterior (con object):
```csharp
// INCORRECTO - requería Typeless o manipulación compleja
var message = new NetworkMessage
{
    Type = "player_states_batch",
    Data = playerStatesBatch,  // object
    Timestamp = DateTime.UtcNow
};
var bytes = MessagePackSerializer.Typeless.Serialize(message);  // Typeless complicado
```

### ✅ Código Nuevo (con byte[]):
```csharp
// CORRECTO - simple y directo
var batch = new PlayerStatesBatch
{
    Players = players,
    ServerTime = serverTime,
    FrameNumber = frameNumber
};

var message = new NetworkMessage
{
    Type = "player_states_batch",
    Data = MessagePackSerializer.Serialize(batch),  // Pre-serializar a bytes
    Timestamp = DateTime.UtcNow
};

var bytes = MessagePackSerializer.Serialize(message);  // Serialización normal
await SendToClient(bytes);
```

## Actualización de NetworkMessage en el Servidor

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
    public byte[] Data { get; set; } = Array.Empty<byte>();  // ← CAMBIO AQUÍ

    [Key(3)]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
```

## Ejemplo Completo: Enviar player_states_batch

```csharp
// En tu NetworkService.cs o UdpService.cs

public async Task SendPlayerStatesBatch(IPEndPoint clientEndpoint, List<PlayerStateUpdate> players)
{
    // 1. Crear el batch
    var batch = new PlayerStatesBatch
    {
        Players = players,
        ServerTime = GetServerTime(),
        FrameNumber = _currentFrame
    };

    // 2. PRE-serializar el batch a bytes
    var batchBytes = MessagePackSerializer.Serialize(batch);

    // 3. Crear el mensaje wrapper
    var message = new NetworkMessage
    {
        Type = "player_states_batch",
        PlayerId = "",  // Vacío para broadcasts
        Data = batchBytes,
        Timestamp = DateTime.UtcNow
    };

    // 4. Serializar el mensaje completo (serialización normal, no Typeless)
    var messageBytes = MessagePackSerializer.Serialize(message);

    // 5. Enviar
    await _udpClient.SendAsync(messageBytes, messageBytes.Length, clientEndpoint);
}
```

## Ejemplo: Recibir mensajes del cliente

```csharp
// Al recibir datos del cliente
public void ProcessIncomingMessage(IPEndPoint clientEndpoint, byte[] data)
{
    try
    {
        // 1. Deserializar el NetworkMessage (deserialización normal, no Typeless)
        var message = MessagePackSerializer.Deserialize<NetworkMessage>(data);

        // 2. Verificar tipo
        switch (message.Type.ToLowerInvariant())
        {
            case "connect":
                // 3. Deserializar el Data específico
                var connectData = MessagePackSerializer.Deserialize<ClientConnectData>(message.Data);
                HandleClientConnect(clientEndpoint, connectData);
                break;

            case "player_input":
                var input = MessagePackSerializer.Deserialize<PlayerInputMessage>(message.Data);
                HandlePlayerInput(clientEndpoint, input);
                break;

            // ... otros casos
        }
    }
    catch (Exception ex)
    {
        _logger.LogError($"Error processing message from {clientEndpoint}: {ex.Message}");
    }
}
```

## Ejemplo: Enviar connect_response

```csharp
public async Task SendConnectResponse(IPEndPoint clientEndpoint, ConnectResponseData responseData)
{
    // 1. Pre-serializar la respuesta
    var responseBytes = MessagePackSerializer.Serialize(responseData);

    // 2. Crear mensaje wrapper
    var message = new NetworkMessage
    {
        Type = "connect_response",
        PlayerId = responseData.PlayerId,
        Data = responseBytes,
        Timestamp = DateTime.UtcNow
    };

    // 3. Serializar el mensaje
    var messageBytes = MessagePackSerializer.Serialize(message);

    // 4. Enviar
    await _udpClient.SendAsync(messageBytes, messageBytes.Length, clientEndpoint);
}
```

## Ventajas de este Enfoque

✅ **Simple**: No requiere Typeless ni código complejo
✅ **Eficiente**: Una sola serialización del wrapper
✅ **Portable**: Funciona con cualquier implementación de MessagePack
✅ **Fácil de debuggear**: Los bytes son exactamente lo que esperas
✅ **Spec-compliant**: Sigue la especificación oficial de MessagePack
✅ **Type-safe**: El campo Type garantiza el tipo correcto

## Checklist de Migración

- [ ] Actualizar `NetworkMessage.cs` para usar `byte[] Data`
- [ ] Actualizar todos los lugares donde se ENVÍA NetworkMessage
  - [ ] player_states_batch
  - [ ] connect_response
  - [ ] world_update
  - [ ] chat_message
  - [ ] combat_event
- [ ] Actualizar todos los lugares donde se RECIBE NetworkMessage
  - [ ] connect (del cliente)
  - [ ] player_input (del cliente)
  - [ ] chat_send (del cliente)
- [ ] Remover cualquier uso de `MessagePackSerializer.Typeless`
- [ ] Probar que cliente y servidor se comunican correctamente

## Notas Importantes

1. **Serialización doble**: Sí, el Data se serializa por separado antes del NetworkMessage. Esto es CORRECTO y lo que MessagePack espera.

2. **No usar Typeless**: Ya no es necesario. La serialización y deserialización son estándar.

3. **Compatibilidad**: Este cambio NO es compatible con la versión anterior que usaba `object Data`. Cliente y servidor deben actualizarse juntos.

4. **Performance**: Más eficiente que Typeless porque no serializa metadata de tipo.

## Testing

Para verificar que funciona:

```csharp
// Test unitario
[Fact]
public void NetworkMessage_SerializeDeserialize_Works()
{
    // Arrange
    var originalBatch = new PlayerStatesBatch
    {
        Players = new List<PlayerStateUpdate> { /* ... */ },
        ServerTime = 123.45f,
        FrameNumber = 100
    };

    // Act - Simular servidor
    var message = new NetworkMessage
    {
        Type = "player_states_batch",
        Data = MessagePackSerializer.Serialize(originalBatch),
        Timestamp = DateTime.UtcNow
    };
    var messageBytes = MessagePackSerializer.Serialize(message);

    // Act - Simular cliente
    var receivedMessage = MessagePackSerializer.Deserialize<NetworkMessage>(messageBytes);
    var receivedBatch = MessagePackSerializer.Deserialize<PlayerStatesBatch>(receivedMessage.Data);

    // Assert
    Assert.Equal("player_states_batch", receivedMessage.Type);
    Assert.Equal(originalBatch.FrameNumber, receivedBatch.FrameNumber);
    Assert.Equal(originalBatch.ServerTime, receivedBatch.ServerTime);
}
```

## Recursos

- [MessagePack Specification](https://github.com/msgpack/msgpack/blob/master/spec.md)
- [MessagePack for C#](https://github.com/neuecc/MessagePack-CSharp)
- `MESSAGEPACK_SPEC_ANALYSIS.md` - Análisis detallado del spec
