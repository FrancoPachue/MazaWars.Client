# Solución Correcta para Campos `object` en MessagePack

## El Problema

Cuando usas un campo de tipo `object` en una clase serializada con MessagePack:

```csharp
[MessagePackObject]
public class NetworkMessage
{
    [Key(0)] public string Type { get; set; }
    [Key(1)] public string PlayerId { get; set; }
    [Key(2)] public object Data { get; set; }  // ← PROBLEMA
    [Key(3)] public DateTime Timestamp { get; set; }
}
```

MessagePack no puede preservar el tipo real de `Data` durante la serialización/deserialización porque C# es un lenguaje estáticamente tipado.

## La Solución Oficial: MessagePack.Typeless

MessagePack proporciona un serializador especial llamado **`Typeless`** diseñado específicamente para manejar campos `object`:

### En el Cliente (C# - este proyecto)

```csharp
// Deserializar mensaje con campo object
var networkMessage = MessagePackSerializer.Typeless.Deserialize(data) as NetworkMessage;

// Re-serializar el campo Data
var dataBytes = MessagePackSerializer.Typeless.Serialize(networkMessage.Data);

// Deserializar al tipo específico
var specificData = MessagePackSerializer.Deserialize<PlayerStatesBatch>(dataBytes);
```

### En el Servidor (debe hacer lo mismo)

El servidor **también debe usar `Typeless`** para serializar/deserializar `NetworkMessage`:

```csharp
// Al ENVIAR mensajes al cliente:
var message = new NetworkMessage
{
    Type = "player_states_batch",
    Data = playerStatesBatch,  // El objeto específico
    Timestamp = DateTime.UtcNow
};

// Serializar usando Typeless
var bytes = MessagePackSerializer.Typeless.Serialize(message);
await SendToClient(bytes);

// Al RECIBIR mensajes del cliente:
var receivedMessage = MessagePackSerializer.Typeless.Deserialize(bytes) as NetworkMessage;
var dataBytes = MessagePackSerializer.Typeless.Serialize(receivedMessage.Data);
var clientConnectData = MessagePackSerializer.Deserialize<ClientConnectData>(dataBytes);
```

## Por Qué Funciona

`MessagePackSerializer.Typeless`:
1. **Preserva información de tipo** durante la serialización
2. **Puede serializar/deserializar campos `object`** correctamente
3. **Es el patrón oficial de MessagePack** para polimorfismo en lenguajes estáticos
4. **Mantiene cohesión** entre cliente y servidor

## Alternativas (NO Recomendadas)

### ❌ Deserialización Manual con MessagePackReader
- Compleja y propensa a errores
- Difícil de mantener
- No estándar

### ❌ Cambiar `object` a `byte[]`
```csharp
[Key(2)] public byte[] Data { get; set; }  // El servidor serializa por separado
```
- Requiere serialización doble
- Menos eficiente
- No aprovecha las capacidades de MessagePack

### ❌ Union Types / Discriminated Unions
```csharp
[Union(0, typeof(ConnectResponse))]
[Union(1, typeof(WorldUpdate))]
[Union(2, typeof(PlayerStatesBatch))]
public abstract class NetworkData { }
```
- Requiere conocer todos los tipos por adelantado
- Menos flexible
- Más código boilerplate

## Recomendación

**Usa `MessagePackSerializer.Typeless`** tanto en el cliente como en el servidor. Es:
- ✅ El patrón oficial
- ✅ Más simple
- ✅ Más mantenible
- ✅ Garantiza cohesión entre cliente/servidor

## Ejemplo Completo

### Cliente:
```csharp
// Scripts/Networking/UdpNetworkClient.cs (YA IMPLEMENTADO)
private void ProcessReceivedData(byte[] data)
{
    var networkMessage = MessagePackSerializer.Typeless.Deserialize(data) as NetworkMessage;
    var dataBytes = MessagePackSerializer.Typeless.Serialize(networkMessage.Data);

    switch (networkMessage.Type.ToLowerInvariant())
    {
        case "player_states_batch":
            var batch = MessagePackSerializer.Deserialize<PlayerStatesBatch>(dataBytes);
            // ... procesar batch
            break;
    }
}
```

### Servidor (DEBE IMPLEMENTAR):
```csharp
// Network/UdpService.cs o similar
public async Task SendPlayerStatesBatch(IPEndPoint client, PlayerStatesBatch batch)
{
    var message = new NetworkMessage
    {
        Type = "player_states_batch",
        Data = batch,
        Timestamp = DateTime.UtcNow
    };

    var bytes = MessagePackSerializer.Typeless.Serialize(message);
    await _udpClient.SendAsync(bytes, bytes.Length, client);
}

public void ProcessIncomingMessage(IPEndPoint client, byte[] data)
{
    var message = MessagePackSerializer.Typeless.Deserialize(data) as NetworkMessage;
    var dataBytes = MessagePackSerializer.Typeless.Serialize(message.Data);

    switch (message.Type.ToLowerInvariant())
    {
        case "connect":
            var connectData = MessagePackSerializer.Deserialize<ClientConnectData>(dataBytes);
            // ... procesar conexión
            break;
    }
}
```

## Referencias

- [MessagePack for C# - Typeless Documentation](https://github.com/neuecc/MessagePack-CSharp#object-serialization)
- [MessagePack Specification](https://github.com/msgpack/msgpack/blob/master/spec.md)
