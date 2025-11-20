# Solución: NetworkMessage con object Data

## ✅ Implementación Actual (Alineada con Servidor)

El cliente ahora usa `object Data` con `keyAsPropertyName: false`, que es el mismo approach que usa el servidor en el branch `claude/fix-object-parsing-01WShXZfZL9keK1mBTiPSuB8`.

## Estructura

```csharp
[MessagePackObject(keyAsPropertyName: false)]
public class NetworkMessage
{
    [Key(0)] public string Type { get; set; } = string.Empty;
    [Key(1)] public string PlayerId { get; set; } = string.Empty;
    [Key(2)] public object Data { get; set; } = null!;
    [Key(3)] public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
```

## Cómo Funciona

### 1. Cliente ENVÍA mensaje al servidor:

```csharp
var connectData = new ClientConnectData
{
    PlayerName = playerName,
    PlayerClass = playerClass,
    TeamId = teamId,
    AuthToken = string.Empty
};

// Pasar el objeto directamente - MessagePack serializa recursivamente
var message = new NetworkMessage
{
    Type = "connect",
    Data = connectData,  // ← Objeto directo, no pre-serializado
    Timestamp = DateTime.UtcNow
};

// Serializar todo el NetworkMessage
var bytes = MessagePackSerializer.Serialize(message);
_udpClient.Send(bytes, bytes.Length);
```

### 2. Cliente RECIBE mensaje del servidor:

```csharp
// Deserializar NetworkMessage
var networkMessage = MessagePackSerializer.Deserialize<NetworkMessage>(data);

// MessagePack deserializó Data como object (tipo dinámico)
// Re-serializar y deserializar al tipo correcto
var dataBytes = MessagePackSerializer.Serialize(networkMessage.Data);

switch (networkMessage.Type.ToLowerInvariant())
{
    case "player_states_batch":
        var batch = MessagePackSerializer.Deserialize<PlayerStatesBatch>(dataBytes);
        // Procesar batch...
        break;

    case "connect_response":
        var response = MessagePackSerializer.Deserialize<ConnectResponseData>(dataBytes);
        // Procesar response...
        break;
}
```

## Por Qué `keyAsPropertyName: false` es Crucial

```csharp
// ✅ CORRECTO - Usa índices numéricos (Key)
[MessagePackObject(keyAsPropertyName: false)]
public class ClientConnectData
{
    [Key(0)] public string PlayerName { get; set; }
    [Key(1)] public string PlayerClass { get; set; }
    [Key(2)] public string TeamId { get; set; }
    [Key(3)] public string AuthToken { get; set; }
}

// ❌ INCORRECTO - Usa nombres de propiedad como keys
[MessagePackObject]  // Sin keyAsPropertyName: false
public class ClientConnectData
{
    [Key(0)] public string PlayerName { get; set; }
    // MessagePack usará "PlayerName" como key en lugar del índice 0
}
```

Cuando `keyAsPropertyName: false`:
- MessagePack usa los índices numéricos `[Key(0)]`, `[Key(1)]`, etc.
- Más eficiente (números vs strings)
- Compatibilidad con el servidor

Sin `keyAsPropertyName: false` (por defecto es `true`):
- MessagePack ignora `[Key(N)]` y usa los nombres de propiedad
- Incompatible con el servidor que espera índices

## Ventajas de Este Approach

1. **Simple**: No requiere pre-serialización
2. **Automático**: MessagePack maneja la recursión
3. **Cohesión**: Cliente y servidor usan la misma estrategia
4. **Eficiente**: Serialización única, no doble

## Modelos Actualizados

Todos estos modelos ahora tienen `keyAsPropertyName: false`:

- ✅ NetworkMessage
- ✅ ClientConnectData
- ✅ ConnectResponseData
- ✅ PlayerStateUpdate
- ✅ PlayerStatesBatch
- ✅ WorldUpdateMessage
- ✅ ChatReceivedData
- ✅ CombatEvent
- ✅ Vector2
- ✅ ChatMessage
- ✅ LootGrabMessage
- ✅ PlayerInputMessage
- ✅ UseItemMessage
- ✅ ExtractionMessage

## Ejemplo Completo: Flujo de Mensajes

### Cliente → Servidor (connect)

```csharp
// 1. Cliente crea mensaje
var message = new NetworkMessage
{
    Type = "connect",
    Data = new ClientConnectData { PlayerName = "Juan", ... }
};

// 2. Cliente serializa
var bytes = MessagePackSerializer.Serialize(message);

// 3. Servidor deserializa
var received = MessagePackSerializer.Deserialize<NetworkMessage>(bytes);

// 4. Servidor extrae Data
var dataBytes = MessagePackSerializer.Serialize(received.Data);
var connectData = MessagePackSerializer.Deserialize<ClientConnectData>(dataBytes);
```

### Servidor → Cliente (player_states_batch)

```csharp
// 1. Servidor crea mensaje
var message = new NetworkMessage
{
    Type = "player_states_batch",
    Data = new PlayerStatesBatch { Players = [...], ... }
};

// 2. Servidor serializa
var bytes = MessagePackSerializer.Serialize(message);

// 3. Cliente deserializa
var received = MessagePackSerializer.Deserialize<NetworkMessage>(bytes);

// 4. Cliente extrae Data
var dataBytes = MessagePackSerializer.Serialize(received.Data);
var batch = MessagePackSerializer.Deserialize<PlayerStatesBatch>(dataBytes);
```

## Debugging

Si recibes errores de deserialización:

1. **Verifica keyAsPropertyName: false** en AMBOS lados (cliente y servidor)
2. **Verifica que los índices [Key(N)]** sean iguales en ambos lados
3. **Verifica que los tipos** tengan los mismos campos en ambos lados

## Comparación con Approaches Anteriores

### ❌ Approach 1 (byte[] Data - PR #20):
```csharp
// Requería pre-serialización
Data = MessagePackSerializer.Serialize(connectData)
```
- Más complejo
- Serialización doble
- No es lo que usa el servidor

### ✅ Approach 2 (object Data - ACTUAL):
```csharp
// Simple, directo
Data = connectData
```
- Más simple
- Una sola serialización
- **Alineado con el servidor**

## Resumen

**Usa `object Data` con `keyAsPropertyName: false` y pasa objetos directamente.**

Esto es lo que el servidor espera y es la forma más simple y eficiente.
