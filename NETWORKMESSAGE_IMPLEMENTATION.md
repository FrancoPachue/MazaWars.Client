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
// Usar MessagePackReader para extraer el campo Data manualmente
var sequence = new ReadOnlySequence<byte>(data);
var reader = new MessagePackReader(sequence);

// Leer NetworkMessage manualmente
var arrayLength = reader.ReadArrayHeader(); // 4 campos
var messageType = reader.ReadString();      // Type (Key 0)
var playerId = reader.ReadString();         // PlayerId (Key 1)

// Extraer Data (Key 2) como bytes crudos sin deserializar
var dataStartPos = reader.Consumed;
reader.Skip(); // Saltar el campo Data para encontrar su fin
var dataEndPos = reader.Consumed;

// Extraer los bytes del campo Data
var dataLength = (int)(dataEndPos - dataStartPos);
var dataBytes = new byte[dataLength];
Array.Copy(data, (int)dataStartPos, dataBytes, 0, dataLength);

// ⚠️ IMPORTANTE: El servidor envía OBJETOS ANÓNIMOS, no clases tipadas
// Deserializar como object, luego mapear manualmente
switch (messageType.ToLowerInvariant())
{
    case "connected":
        // Servidor envía: { PlayerId, WorldId, IsInLobby, SessionToken, ... }
        var dataObj = MessagePackSerializer.Deserialize<object>(dataBytes);
        if (dataObj is object[] dataArray && dataArray.Length >= 5)
        {
            var response = new ConnectResponseData
            {
                Success = true,
                PlayerId = dataArray[0]?.ToString() ?? string.Empty,
                WorldId = dataArray[1]?.ToString() ?? string.Empty,
                IsInLobby = dataArray[2] is bool b && b,
                SessionToken = dataArray[3]?.ToString() ?? string.Empty
            };
            // Procesar response...
        }
        break;

    case "player_states_batch":
        // Servidor envía: { Players = [...], BatchIndex, TotalBatches }
        var batchObj = MessagePackSerializer.Deserialize<object>(dataBytes);
        if (batchObj is object[] batchArray && batchArray.Length >= 1)
        {
            if (batchArray[0] is object[] playersArray)
            {
                foreach (var playerObj in playersArray)
                {
                    if (playerObj is object[] playerData && playerData.Length >= 13)
                    {
                        var player = new PlayerStateUpdate
                        {
                            PlayerId = playerData[0]?.ToString() ?? string.Empty,
                            Position = ParseVector2(playerData[1]),
                            Velocity = ParseVector2(playerData[2]),
                            Direction = Convert.ToSingle(playerData[3]),
                            Health = Convert.ToInt32(playerData[4]),
                            MaxHealth = Convert.ToInt32(playerData[5])
                            // ... otros campos
                        };
                    }
                }
            }
        }
        break;
}

// Helper para parsear Vector2
private Vector2 ParseVector2(object data)
{
    if (data is object[] arr && arr.Length >= 2)
    {
        return new Vector2
        {
            X = Convert.ToSingle(arr[0]),
            Y = Convert.ToSingle(arr[1])
        };
    }
    return new Vector2();
}
```

**⚠️ Por qué deserializar como object[]:**
1. El servidor envía **objetos anónimos** usando `new { ... }`, NO clases tipadas
2. MessagePack serializa objetos con `keyAsPropertyName: false` como arrays
3. Necesitamos deserializar como `object` primero, luego castear a `object[]`
4. Finalmente mapeamos manualmente los índices a propiedades de clases

Esta es la **misma estrategia que usa el servidor** en `NetworkService.cs` con `ConvertMessageData<T>()` y `MapArrayToType<T>()`.

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
- ⚠️ ~~PlayerStatesBatch~~ (NO EXISTE EN EL SERVIDOR - Ver nota abajo)
- ✅ WorldUpdateMessage
- ✅ ChatReceivedData
- ✅ CombatEvent
- ✅ Vector2
- ✅ ChatMessage
- ✅ LootGrabMessage
- ✅ PlayerInputMessage
- ✅ UseItemMessage
- ✅ ExtractionMessage

### ⚠️ Importante: Objetos Anónimos del Servidor

**El servidor envía OBJETOS ANÓNIMOS, no clases tipadas completas.**

El servidor usa `CreateNetworkMessage()` con objetos anónimos:

```csharp
// NetworkService.cs en el servidor
CreateNetworkMessage("connected", player.PlayerId, new {
    player.PlayerId,
    WorldId = worldId,
    IsInLobby = isLobby,
    SessionToken = sessionToken,
    SpawnPosition = player.Position,
    PlayerStats = new { /* stats */ },
    ServerInfo = new { /* config */ },
    LobbyInfo = /* ... */
})

CreateNetworkMessage("player_states_batch", string.Empty, new {
    Players = playerUpdates,  // Lista de objetos anónimos
    BatchIndex = i / maxPlayersPerBatch,
    TotalBatches = totalBatches
})
```

**Consecuencias para el cliente:**
1. NO puedes deserializar directamente a `ConnectResponseData` o `WorldUpdateMessage`
2. Debes deserializar como `object` → `object[]` → mapear manualmente
3. Los índices de array corresponden al orden de las propiedades en el objeto anónimo
4. Esta es la misma estrategia que usa el servidor en `NetworkService.cs`

**PlayerStatesBatch:** NO existe en el servidor. El servidor envía `"player_states_batch"` con un objeto anónimo que tiene solo `Players`, `BatchIndex`, y `TotalBatches`.

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
