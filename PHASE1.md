# Phase 1: Basic Rendering - Implementation Guide

## Overview

Phase 1 implements the core rendering system for MazeWars client. You can now see the game world, your player, and other players moving around in real-time.

## What's Implemented

### 🌍 World Rendering
- **4×4 Room Grid**: 16 rooms each 800×800 pixels (50 game units × 16px)
- **Visual Rooms**: Each room has:
  - Floor with distinct color
  - Border walls
  - Random obstacles for variety
  - Debug label showing room ID and coordinates

### 👤 Player System
- **Player Rendering**: Players are shown as colored squares
- **Class Colors**:
  - 🔵 Tank: Blue
  - 🟢 Healer: Green
  - 🔴 Damage: Red
  - 🟣 Rogue: Purple
  - 🔷 Mage: Cyan
  - 🟠 Ranger: Orange
- **Player Info**: Each player shows:
  - Name above sprite
  - Health bar below sprite
  - Visual feedback when moving (slight scale/rotation)

### 🎮 Local Player Movement
- **Direct Control**: WASD moves your player immediately
- **Sprint**: Hold Shift to move 1.5× faster
- **Smooth Movement**: CharacterBody2D with collision
- **Visual Feedback**: Player sprite scales and rotates when moving

### 👥 Remote Players
- **Interpolation**: Other players smoothly move to their server positions
- **Real-time Updates**: See other players as they move
- **Sync**: Position updates from server at ~20 tick/sec

### 📷 Camera System
- **Smooth Follow**: Camera smoothly follows your player
- **Centered**: Automatically centers on player position
- **Responsive**: Lerp-based movement for smooth tracking

### 📊 Debug UI
Enhanced debug panel showing:
- Player ID and position
- Current room
- Network status (SignalR + UDP)
- Input sequence numbers
- Game state updates
- Player count

## How to Test

### 1. Start the Server

First, make sure the MazeWars server is running:

```bash
cd ../MazeWars.GameServer
dotnet run
```

You should see:
```
info: MazeWars.GameServer[0]
      Now listening on: http://localhost:5000
```

### 2. Run the Client

In Godot:
1. Press **F5** (or click Play ▶)
2. Enter your player name (e.g., "Alice")
3. Select a class (try "Tank" for blue)
4. Verify server URL: `http://localhost:5000`
5. Click **CONNECT**

### 3. What You Should See

After connecting, you'll see:

1. **The World Grid**:
   - 16 rooms laid out in a 4×4 pattern
   - Each room has walls and obstacles
   - Your player spawned in one of the rooms

2. **Your Player**:
   - A colored square (based on your class)
   - Your name above it
   - Health bar below

3. **Debug Info** (top-left):
   ```
   ═══ MazeWars Client v0.1.0-alpha (Phase 1) ═══
   Player ID: abc123
   Position: (1234, 567)
   Current Room: room_1_0

   ═══ Network Status ═══
   SignalR: ✓ Connected
   UDP: ↑50 ↓48 | Last: 0.1s ago
   Input: Seq=50 Ack=48 Buffered=2
   Messages: 48 | Last: 0.1s ago
   GameState: 1 players | 48 updates | Last: 0.1s ago

   ═══ Controls ═══
   WASD: Move | Shift: Sprint | ESC: Disconnect
   ```

### 4. Test Movement

- **Press W/A/S/D**: Your player should move immediately
- **Hold Shift**: You should move faster
- **Camera**: Should smoothly follow your player
- **Obstacles**: You should collide with walls
- **Room Transitions**: Walk between rooms, watch the "Current Room" update

### 5. Test Multiplayer

Open a second client:
1. Run another instance of the client
2. Connect with a different name and class
3. In both clients, you should now see TWO players
4. Move in one client, watch the other client show your movement

## Controls

| Key | Action |
|-----|--------|
| W/↑ | Move Up |
| S/↓ | Move Down |
| A/← | Move Left |
| D/→ | Move Right |
| Shift | Sprint (1.5× speed) |
| ESC | Disconnect and return to menu |

## Architecture

### Component Hierarchy

```
GameWorld (Node2D)
├── Rooms (Node2D)
│   ├── room_0_0 (Room)
│   ├── room_0_1 (Room)
│   ├── ... (16 total)
│   └── room_3_3 (Room)
├── Players (Node2D, ZIndex=10)
│   ├── Player_abc123 (Player) - Local
│   ├── Player_def456 (Player) - Remote
│   └── ... (up to 24 players)
├── GameStateManager (Node)
├── Camera2D
└── DebugUI (CanvasLayer)
    ├── Panel (background)
    └── Label (debug text)
```

### Data Flow

```
Server WorldUpdate
        ↓
MessageHandler.GameStateUpdate signal
        ↓
GameStateManager.OnGameStateUpdate()
        ↓
    ┌───────────────┴───────────────┐
    ↓                               ↓
UpdatePlayer()              SpawnPlayer()
    ↓                               ↓
Player.UpdateFromServerState()   Instantiate Player
    ↓                               ↓
SetServerPosition()             Add to scene
    ↓
InterpolateToServerPosition()
    ↓
Visual Update (60 FPS)
```

### Local Player Movement

```
User Input (WASD)
        ↓
GameStateManager._Process()
        ↓
Player.ApplyLocalMovement()
        ↓
    ┌───────────┴──────────┐
    ↓                      ↓
Velocity = input      MoveAndSlide()
    ↓                      ↓
Visual feedback       Collision handling
```

### Parallel: Input Sending

```
User Input (WASD)
        ↓
InputSender._PhysicsProcess() [60 FPS]
        ↓
CreateInputMessage()
        ↓
UdpClient.SendPlayerInput()
        ↓
Server processes
        ↓
WorldUpdate sent back
```

## File Structure

### New Files in Phase 1

```
Scripts/Game/
├── Player.cs              # Player entity with movement
├── Room.cs                # Room rendering
└── GameStateManager.cs    # Player synchronization

Scenes/Game/
├── Player.tscn           # Player scene template
└── Room.tscn             # Room scene template
```

### Updated Files

```
Scripts/Game/
└── GameWorld.cs          # Now has full rendering system

Scenes/Game/
└── GameWorld.tscn        # Simplified scene
```

## Known Limitations

### Current Phase
- ✅ Basic rendering working
- ✅ Player movement working
- ✅ Multiplayer synchronization working
- ✅ Camera follow working

### Not Yet Implemented (Future Phases)
- ❌ Client-side prediction (Phase 2)
- ❌ Server reconciliation (Phase 2)
- ❌ Combat system (Phase 3)
- ❌ Inventory UI (Phase 3)
- ❌ Chat system (Phase 3)
- ❌ Mobs (Phase 3)
- ❌ Loot (Phase 3)
- ❌ Abilities (Phase 3)

## Troubleshooting

### Problem: Can't see my player

**Check**:
1. Is the server sending WorldUpdate messages?
2. Check debug info: Does "GameState" show your player?
3. Check Godot output: Any errors during player spawn?
4. Check player position: Are you off-screen?

**Solution**:
- Check server logs for player spawn
- Verify UDP connection is receiving packets
- Try zooming out the camera (Ctrl+Mouse Wheel in editor)

### Problem: Movement is laggy

**Check**:
- Debug panel: Is "UDP: Last" showing < 0.5s?
- Are you on localhost or remote server?
- Check server performance

**Solution**:
- This is normal for remote players (interpolation delay)
- Local player should be instant
- If local player is laggy, check server tick rate

### Problem: Other players are jittery

**Reason**: This is expected in Phase 1!

**Explanation**:
- Remote players interpolate to server positions
- Without buffering, movement can appear choppy
- Phase 2 will add proper interpolation with state buffering

**Workaround**: None yet - this is a known limitation

### Problem: Players don't spawn

**Check**:
1. Server logs: Is player authenticated?
2. Debug panel: Is SignalR connected?
3. Debug panel: Are WorldUpdates being received?

**Solution**:
- Verify server is running
- Check connection in MainMenu
- Look for errors in Godot Output panel

## Performance Metrics

Target performance for Phase 1:

| Metric | Target | Notes |
|--------|--------|-------|
| FPS | 60 | Local rendering |
| World Updates | 20/sec | From server |
| Input Sends | 60/sec | To server |
| Players Supported | 24 | Full lobby |
| Memory Usage | < 200MB | Basic rendering only |

## Next Steps (Phase 2)

Phase 2 will add:
1. **Client-Side Prediction**:
   - Predict local player movement
   - Don't wait for server confirmation

2. **Server Reconciliation**:
   - Compare predictions with server
   - Correct position if mismatch

3. **Better Interpolation**:
   - State buffering for remote players
   - Smoother movement
   - Less jitter

4. **Input Replay**:
   - Replay unacknowledged inputs
   - Maintain prediction accuracy

## Testing Checklist

- [ ] Server starts successfully
- [ ] Client connects to server
- [ ] Player spawns in world
- [ ] WASD movement works
- [ ] Sprint (Shift) works
- [ ] Camera follows player
- [ ] Can move between rooms
- [ ] Debug panel shows correct info
- [ ] Second client can connect
- [ ] Can see other players
- [ ] Other players move when they move
- [ ] ESC disconnects properly

## Support

For issues:
- Check server console for errors
- Check Godot Output panel for errors
- Verify both SignalR and UDP are connected
- Check debug panel for network stats

---

**Status**: Phase 1 Complete ✅

**Ready for**: Testing with multiple clients

**Next**: Phase 2 - Client-Side Prediction & Reconciliation
