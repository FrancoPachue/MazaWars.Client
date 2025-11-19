# Phase 2: Client-Side Prediction & Server Reconciliation

## Overview

Phase 2 implements full client-side prediction with server reconciliation, making player movement feel instant and responsive even with network latency.

## What's New in Phase 2

### ⚡ Client-Side Prediction
Your player now moves **INSTANTLY** when you press WASD - no waiting for server confirmation!

**How it works**:
1. You press W → Player moves immediately (prediction)
2. Input sent to server via UDP
3. Server processes and sends back confirmation
4. Client compares prediction with server reality
5. If different, client corrects smoothly

### 🔄 Server Reconciliation
When the server says "you're actually here", the client adjusts seamlessly.

**Features**:
- **Error Detection**: Compares predicted position vs server position
- **Smart Correction**: Only corrects if error > 2.0 units (configurable)
- **Input Replay**: Re-applies pending inputs after correction
- **Visual Feedback**: Yellow flash when reconciliation occurs

### 📊 Enhanced Input Tracking
- **Sequence Numbers**: Every input gets a unique ID
- **Acknowledgment**: Server tells us which inputs it processed
- **Buffer Management**: Stores unacknowledged inputs
- **Packet Loss Detection**: Tracks and displays loss rate

### 🎯 Prediction Accuracy
- **Error Tracking**: Shows current and max prediction error
- **Reconciliation Count**: How many times we had to correct
- **Pending Inputs**: How many inputs are waiting for server confirmation

## Technical Implementation

### 1. Input Sender Enhancement

**New PredictedInput struct**:
```csharp
public struct PredictedInput
{
    public uint SequenceNumber;        // Unique ID
    public PlayerInputMessage Input;   // The actual input
    public Vector2 PredictedPosition; // Where we think we'll be
    public Vector2 PredictedVelocity; // Our predicted velocity
    public float Timestamp;            // When we sent it
    public float DeltaTime;            // Frame delta
}
```

**New Methods**:
- `UpdateLastPredictedPosition()` - Updates prediction after movement
- `OnServerUpdate()` - Removes acknowledged inputs
- `GetPendingInputs()` - Gets inputs waiting for confirmation
- `GetPacketLossRate()` - Calculates packet loss

### 2. Player Reconciliation

**New Methods**:
```csharp
// Reconciles with server position
public bool ReconcileWithServer(Vector2 serverPosition)
{
    var error = (Position - serverPosition).Length();

    if (error > ReconciliationThreshold) // Default: 2.0 units
    {
        // Snap to server position
        Position = serverPosition;

        // Yellow flash for visual feedback
        _sprite.Modulate = new Color(1, 1, 0, 1);

        return true; // Misprediction occurred
    }

    return false; // Prediction was accurate
}

// Replays a single input
public void ReplayInput(Vector2 moveInput, bool isSprinting, float deltaTime)
{
    // Re-apply the movement
    Velocity = moveInput * (isSprinting ? MoveSpeed * 1.5f : MoveSpeed);
    MoveAndSlide();
}
```

**Prediction State**:
- `_predictedPosition` - Where we think we are
- `_predictedVelocity` - Our predicted velocity
- `_lastPredictionError` - Latest error measurement
- `_maxPredictionError` - Worst error we've seen
- `_reconciliationsPerformed` - Total corrections

### 3. Game State Manager Reconciliation

**Reconciliation Flow**:
```csharp
private void ReconcileLocalPlayer(Player player, PlayerStateUpdate serverState)
{
    // 1. Update health (non-predicted)
    player.UpdateHealth(serverState.CurrentHealth, serverState.MaxHealth);

    // 2. Check prediction accuracy
    bool mispredicted = player.ReconcileWithServer(serverPosition);

    // 3. If mispredicted, replay pending inputs
    if (mispredicted && EnablePrediction)
    {
        ReplayPendingInputs(player);
    }
}

private void ReplayPendingInputs(Player player)
{
    var pendingInputs = _inputSender.GetPendingInputs();

    foreach (var input in pendingInputs)
    {
        // Re-apply each unacknowledged input
        player.ReplayInput(
            input.Input.MoveInput,
            input.Input.IsSprinting,
            input.DeltaTime
        );
    }
}
```

## Data Flow

### Without Prediction (Phase 1)
```
User presses W
    ↓
Input sent to server (UDP)
    ↓
Wait 50-100ms for server response...
    ↓
Server update received
    ↓
Player moves
```
**Result**: 50-100ms delay = Feels laggy

### With Prediction (Phase 2)
```
User presses W
    ↓
Player moves IMMEDIATELY (prediction)
    ↓
Input sent to server (UDP, buffered)
    ↓
[Player continues moving instantly]
    ↓
Server update received (50-100ms later)
    ↓
Compare predicted vs actual position
    ↓
If error > threshold:
    ├─ Snap to server position
    └─ Replay pending inputs
```
**Result**: 0ms delay = Feels instant!

## Debug Information

### New Debug Stats

```
═══ Prediction ═══
GameState: 2 players | 148 updates | Last: 0.1s ago
Prediction Error: 0.52 (Max: 1.87) | Reconciliations: 0

Input: Seq=150 Ack=148 Pending=2 Loss=0.7%
```

**What it means**:
- **Prediction Error**: Current difference between prediction and server
- **Max**: Worst error we've encountered
- **Reconciliations**: How many times we had to correct (lower is better)
- **Pending**: Inputs waiting for server confirmation
- **Loss**: Percentage of packets lost (< 1% is good)

### Visual Indicators

**Yellow Flash**: When you see a yellow flash on your player:
- Reconciliation just occurred
- Your prediction was off by > 2.0 units
- Client corrected and replayed inputs

**If you see this often**:
- High latency to server
- Packet loss
- Server under load

**If you never see this**:
- Perfect predictions! 🎉
- Low latency
- Good connection

## Configuration

### Tuning Prediction (in Player.cs)

```csharp
[Export] public float ReconciliationThreshold { get; set} = 2.0f;
```

**Lower threshold (e.g., 0.5)**:
- More frequent corrections
- More accurate to server
- Might feel jittery

**Higher threshold (e.g., 5.0)**:
- Less frequent corrections
- Smoother feel
- Less accurate to server

### Enabling/Disabling Features (in GameStateManager)

```csharp
[Export] public bool EnablePrediction { get; set; } = true;
[Export] public bool EnableReconciliation { get; set; } = true;
```

**Test without prediction**:
- Set `EnablePrediction = false`
- Movement will wait for server (Phase 1 behavior)
- Compare the lag difference!

## Testing

### Test 1: Zero-Latency Feel

1. Run server locally
2. Connect client
3. Press WASD rapidly
4. **Expected**: Instant response, no delay

**✅ Pass**: Player moves immediately
**❌ Fail**: Player moves after delay → Check EnablePrediction

### Test 2: Reconciliation

1. Add artificial latency:
   ```bash
   # Linux/Mac
   sudo tc qdisc add dev lo root netem delay 100ms
   ```

2. Move around quickly
3. Watch for yellow flashes
4. Check reconciliation count

**✅ Pass**: Occasional yellow flashes, low reconciliation count
**❌ Fail**: Constant flashes → Server might be dropping packets

### Test 3: Packet Loss Handling

1. Check debug panel: `Loss=X%`
2. Should be < 1% on localhost
3. On remote server, < 5% is acceptable

**If loss > 5%**:
- Network issues
- Server under load
- Firewall dropping UDP packets

### Test 4: Prediction Accuracy

1. Move in straight lines
2. Check "Prediction Error" in debug panel
3. Should be < 2.0 units most of the time

**High error (> 5.0)**:
- Server simulation differs from client
- Physics mismatch
- High latency spikes

## Performance Metrics

### Target Metrics

| Metric | Target | Notes |
|--------|--------|-------|
| Prediction Error | < 2.0 units | Average |
| Reconciliations/min | < 10 | On good connection |
| Packet Loss | < 1% | Localhost |
| Packet Loss | < 5% | Remote server |
| Input Latency | 0ms | Feels instant |
| Pending Inputs | 2-5 | Normal buffer |

### Monitoring

Watch these in the debug panel:

1. **Pending Inputs**:
   - Normal: 2-5 inputs
   - High (> 10): Server lag or packet loss
   - Zero: Server responding instantly (localhost)

2. **Prediction Error**:
   - Normal: 0.0-2.0 units
   - High (> 5.0): Mispredictions occurring
   - Increasing: Connection degrading

3. **Reconciliations**:
   - Normal: 0-10 per minute
   - High (> 30/min): Poor predictions
   - None: Perfect predictions or no movement

## Comparison: Phase 1 vs Phase 2

### Phase 1 (Without Prediction)
- ❌ Movement waits for server (50-100ms delay)
- ❌ Feels laggy on remote servers
- ✅ Always matches server exactly
- ✅ Simple implementation

### Phase 2 (With Prediction)
- ✅ Movement is instant (0ms delay)
- ✅ Feels responsive even with lag
- ✅ Automatically corrects mispredictions
- ✅ Smooth gameplay experience
- ⚠️ Occasional corrections visible (yellow flash)
- ⚠️ More complex implementation

## Troubleshooting

### Problem: Player teleports constantly

**Symptoms**: Yellow flashes every frame, position jumping

**Causes**:
1. Physics mismatch between client and server
2. Very high latency (> 500ms)
3. ReconciliationThreshold too low

**Solutions**:
- Increase `ReconciliationThreshold` to 5.0
- Check server physics tick rate
- Verify MoveSpeed matches server

### Problem: No reconciliation, but positions drift

**Symptoms**: Prediction Error grows, no corrections

**Cause**: ReconciliationThreshold too high

**Solution**:
- Lower threshold to 1.0-2.0 units
- Check logs for reconciliation messages

### Problem: High packet loss (> 10%)

**Symptoms**: Loss stat in debug panel high

**Causes**:
1. Network congestion
2. Firewall blocking UDP
3. Server dropping packets

**Solutions**:
- Check firewall allows UDP port 5001
- Test on localhost (should be 0% loss)
- Check server logs for errors

### Problem: Input feels delayed

**Symptoms**: Movement not instant

**Causes**:
1. EnablePrediction = false
2. Frame rate issues (< 60 FPS)
3. Server not acknowledging inputs

**Solutions**:
- Verify `EnablePrediction = true`
- Check FPS in Godot debugger
- Check server is running and responsive

## Code Example: Complete Flow

```csharp
// 1. User presses W
Input.IsActionPressed("move_up") // = true

// 2. GameStateManager._Process() reads input
var moveInput = new Vector2(0, -1); // Up

// 3. Apply prediction immediately
_localPlayer.ApplyLocalMovement(moveInput, false, delta);
// Player position changes INSTANTLY

// 4. InputSender._PhysicsProcess() sends to server
var inputMessage = new PlayerInputMessage
{
    SequenceNumber = 42,           // Unique ID
    MoveInput = new Vector2(0, -1),
    // ... other fields
};
_udpClient.SendPlayerInput(inputMessage);
_inputBuffer.Enqueue(predictedInput); // Store for later

// 5. [50ms later] Server update arrives
OnGameStateUpdate(worldUpdate);

// 6. Check acknowledgment
update.AcknowledgedInputs["player_123"] = 40; // Server processed up to #40
_inputSender.OnServerUpdate(40); // Remove inputs #1-40 from buffer

// 7. Reconcile position
var serverPos = new Vector2(1234, 567);
var clientPos = player.Position; // Vector2(1235, 568)
var error = (clientPos - serverPos).Length(); // = 1.41 units

if (error > 2.0f) // Threshold
{
    // Misprediction! Correct it
    player.Position = serverPos; // Snap to server

    // Replay inputs #41 and #42 (still pending)
    ReplayPendingInputs(player);
}
// Error is 1.41 < 2.0, so no correction needed
```

## Best Practices

### For Developers

1. **Always test without prediction first**:
   - Set `EnablePrediction = false`
   - Verify server sync works
   - Then enable prediction

2. **Monitor reconciliation count**:
   - Should be rare (< 10/min)
   - Frequent = physics mismatch

3. **Tune threshold per game**:
   - Fast games: lower threshold (1.0)
   - Slower games: higher threshold (5.0)

4. **Log mispredictions**:
   - Already done in `Player.ReconcileWithServer()`
   - Review logs to find patterns

### For Players

1. **Watch for yellow flashes**:
   - Rare = good connection
   - Frequent = network issues

2. **Check packet loss**:
   - Should be < 1% on good connection
   - > 5% = investigate network

3. **Report persistent issues**:
   - Include debug panel screenshot
   - Note when reconciliations occur (moving fast, changing direction, etc.)

## Next Steps (Phase 3)

Phase 3 will add:
- State buffering for smoother remote player movement
- Combat system with prediction
- Projectile prediction
- Ability cooldown prediction
- Inventory UI

## References

- [Gabriel Gambetta - Client-Side Prediction](https://www.gabrielgambetta.com/client-side-prediction-server-reconciliation.html)
- [Valve - Source Multiplayer Networking](https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking)
- [Gaffer On Games - Networked Physics](https://gafferongames.com/post/networked_physics_2004/)

---

**Status**: Phase 2 Complete ✅

**What Works**:
- ✅ Instant movement (0ms latency)
- ✅ Server reconciliation
- ✅ Input replay
- ✅ Prediction error tracking
- ✅ Packet loss detection

**Try it now**:
1. Start server: `dotnet run`
2. Run client: F5 in Godot
3. Move with WASD
4. Feel the instant response!
5. Watch debug panel for prediction stats

**Last Updated**: 2025-11-19
