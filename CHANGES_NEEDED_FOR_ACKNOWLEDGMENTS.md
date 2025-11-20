# Cambios Necesarios para Arreglar Input Acknowledgments

## Problema Identificado

El cliente envía inputs al servidor pero nunca recibe confirmación (acknowledgments), causando:
- Buffer overflow después de ~1.67 segundos (100 inputs a 60 FPS)
- Client-side prediction no puede hacer reconciliation
- Inputs descartados sin poder recuperarlos

## Solución Implementada (Cliente - COMPLETADO ✅)

### 1. Reducir frecuencia de envío de inputs
**Archivo:** `Scripts/Networking/InputSender.cs`
**Cambio:** Rate limiting de 60 FPS → 20 FPS (enviar cada 3 frames)
**Estado:** ✅ COMPLETADO - Commit 35412ac

Esto da alivio inmediato al problema del buffer overflow.

---

## Cambios Necesarios en el Servidor (PENDIENTE ⚠️)

### 2. Agregar AcknowledgedInputs a PlayerStatesBatchData

**Archivo:** `Network/Models/PlayerStatesBatchData.cs`

**Cambio actual:**
```csharp
[MessagePackObject(keyAsPropertyName: false)]
public class PlayerStatesBatchData
{
    [Key(0)]
    public List<PlayerUpdateData> Players { get; set; } = new();

    [Key(1)]
    public int BatchIndex { get; set; }

    [Key(2)]
    public int TotalBatches { get; set; }
}
```

**AGREGAR este campo:**
```csharp
    [Key(3)]
    public Dictionary<string, uint> AcknowledgedInputs { get; set; } = new();
```

**Resultado final:**
```csharp
[MessagePackObject(keyAsPropertyName: false)]
public class PlayerStatesBatchData
{
    [Key(0)]
    public List<PlayerUpdateData> Players { get; set; } = new();

    [Key(1)]
    public int BatchIndex { get; set; }

    [Key(2)]
    public int TotalBatches { get; set; }

    [Key(3)]
    public Dictionary<string, uint> AcknowledgedInputs { get; set; } = new();
}
```

---

### 3. Poblar AcknowledgedInputs al enviar player_states_batch

**Archivo:** `Network/Services/NetworkService.cs`
**Método:** `SendOptimizedPlayerStateUpdates()`

**Ubicación del cambio:** Dentro del loop que crea los batches

**ANTES:**
```csharp
var message = CreateNetworkMessage("player_states_batch", string.Empty,
    new PlayerStatesBatchData
    {
        Players = playerUpdates,
        BatchIndex = i / maxPlayersPerBatch,
        TotalBatches = (players.Count + maxPlayersPerBatch - 1) / maxPlayersPerBatch
    });
```

**DESPUÉS (agregar acknowledgments):**
```csharp
// Collect acknowledged input sequences for this batch
var acknowledgedInputs = new Dictionary<string, uint>();
foreach (var player in batch)
{
    var ackSeq = _inputProcessor.GetLastAcknowledgedSequence(player.PlayerId);
    if (ackSeq > 0)
    {
        acknowledgedInputs[player.PlayerId] = ackSeq;
    }
}

var message = CreateNetworkMessage("player_states_batch", string.Empty,
    new PlayerStatesBatchData
    {
        Players = playerUpdates,
        BatchIndex = i / maxPlayersPerBatch,
        TotalBatches = (players.Count + maxPlayersPerBatch - 1) / maxPlayersPerBatch,
        AcknowledgedInputs = acknowledgedInputs  // ← NUEVO
    });
```

**Nota:** Necesitarás agregar una referencia a `_inputProcessor` en el método. Si no está disponible, usa `_gameEngine.GetInputProcessor()` o el método equivalente para acceder al InputBuffer.

---

## Cambios Necesarios en el Cliente (PENDIENTE ⚠️)

### 4. Actualizar PlayerStatesBatchData en el cliente

**Archivo:** `Shared/NetworkModels/PlayerStatesBatchData.cs`

**Cambio:** Agregar el mismo campo que en el servidor

```csharp
using MessagePack;
using System.Collections.Generic;

namespace MazeWars.Client.Shared.NetworkModels;

/// <summary>
/// Batch of player states to reduce network overhead.
/// </summary>
[MessagePackObject(keyAsPropertyName: false)]
public class PlayerStatesBatchData
{
    [Key(0)]
    public List<PlayerUpdateData> Players { get; set; } = new();

    [Key(1)]
    public int BatchIndex { get; set; }

    [Key(2)]
    public int TotalBatches { get; set; }

    [Key(3)]
    public Dictionary<string, uint> AcknowledgedInputs { get; set; } = new();  // ← NUEVO
}
```

---

### 5. Procesar acknowledgments en UdpNetworkClient

**Archivo:** `Scripts/Networking/UdpNetworkClient.cs`
**Método:** `ProcessReceivedData()` - case "player_states_batch"
**Línea:** ~373

**CAMBIO EN EL WORLDUPDATEMESSAGE:**

**ANTES:**
```csharp
var update = new WorldUpdateMessage
{
    Players = players,
    ServerTime = 0,
    FrameNumber = 0,
    AcknowledgedInputs = new(),  // ← Siempre vacío!
    CombatEvents = new(),
    LootUpdates = new(),
    MobUpdates = new()
};
```

**DESPUÉS:**
```csharp
var update = new WorldUpdateMessage
{
    Players = players,
    ServerTime = 0,
    FrameNumber = 0,
    AcknowledgedInputs = batch.AcknowledgedInputs ?? new(),  // ← Usar acknowledgments del batch!
    CombatEvents = new(),
    LootUpdates = new(),
    MobUpdates = new()
};
```

---

## Orden de Implementación Recomendado

1. ✅ **COMPLETADO** - Cliente: Reducir frecuencia de inputs (commit 35412ac)

2. ⚠️ **SERVIDOR** - Agregar campo AcknowledgedInputs a PlayerStatesBatchData
   - Archivo: `Network/Models/PlayerStatesBatchData.cs`

3. ⚠️ **SERVIDOR** - Poblar AcknowledgedInputs al enviar
   - Archivo: `Network/Services/NetworkService.cs`

4. ⚠️ **CLIENTE** - Actualizar modelo PlayerStatesBatchData
   - Archivo: `Shared/NetworkModels/PlayerStatesBatchData.cs`

5. ⚠️ **CLIENTE** - Procesar acknowledgments del batch
   - Archivo: `Scripts/Networking/UdpNetworkClient.cs`

6. 🧪 **TESTING** - Verificar que:
   - No hay más "Input buffer overflow" errors
   - El log muestra "[InputSender] Acknowledged sequence X, removed Y inputs"
   - El PendingInputCount se mantiene bajo (< 10)

---

## Verificación

Después de aplicar TODOS los cambios, deberías ver en los logs:

```
[InputSender] Acknowledged sequence 150, removed 3 inputs. Pending: 5
[InputSender] Acknowledged sequence 153, removed 3 inputs. Pending: 5
```

Y NO deberías ver más:
```
ERROR: [InputSender] Input buffer overflow! Oldest input discarded.
```

---

## Notas Técnicas

- El servidor YA trackea los sequence numbers en `InputBuffer.GetLastAcknowledgedSequence()`
- Solo falta ENVIAR esa información de vuelta al cliente
- El campo AcknowledgedInputs usa `Dictionary<string, uint>` para soportar múltiples jugadores
- Cada batch puede contener acknowledgments para los jugadores en ese batch
- El GameStateManager del cliente YA procesa acknowledgments correctamente (línea 69-71)
