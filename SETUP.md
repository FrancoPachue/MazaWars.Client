# MazeWars Client - Setup Guide

## Quick Start

This guide will help you get the MazeWars client up and running.

## Prerequisites

Before you begin, ensure you have:

1. **Godot 4.3 Mono (C#/.NET version)**
   - Download from: https://godotengine.org/download
   - ⚠️ **IMPORTANT**: Download the "Mono" version, not the standard version
   - The Mono version includes C# support which is required for this project

2. **.NET 8.0 SDK**
   - Download from: https://dotnet.microsoft.com/download/dotnet/8.0
   - Verify installation: `dotnet --version` (should show 8.0.x)

3. **MazeWars Game Server**
   - The server must be running before you can connect
   - Server repository: https://github.com/FrancoPachue/MazeWars.GameServer
   - Default server URL: `http://localhost:5000`
   - Default UDP port: `5001`

## Installation Steps

### 1. Clone the Repository

```bash
git clone https://github.com/FrancoPachue/MazaWars.Client.git
cd MazaWars.Client
```

### 2. Restore NuGet Packages

The project uses the following NuGet packages:
- Microsoft.AspNetCore.SignalR.Client (v8.0.0)
- MessagePack (v2.5.140)
- MessagePackAnalyzer (v2.5.140)
- System.Numerics.Vectors (v4.5.0)

Restore them with:

```bash
dotnet restore
```

### 3. Open in Godot

1. Launch Godot 4.3 (Mono version)
2. Click **"Import"**
3. Click **"Browse"** and navigate to the `MazaWars.Client` folder
4. Select the `project.godot` file
5. Click **"Import & Edit"**

### 4. Build the C# Project

Once the project is open in Godot:

1. Wait for Godot to finish importing assets
2. Press **Ctrl+B** (or **Cmd+B** on Mac) to build the C# project
3. Check the **Build** panel at the bottom for any errors
4. If successful, you should see "Build succeeded"

**Common Build Issues:**

- **"Could not find .NET SDK"**: Make sure .NET 8.0 SDK is installed
- **"NuGet packages not found"**: Run `dotnet restore` again
- **"MessagePack not found"**: Check that NuGet packages are restored

### 5. Configure Server Connection (Optional)

By default, the client connects to `http://localhost:5000`.

To change this:
1. Run the game (F5)
2. On the main menu, modify the "Server URL" field
3. Or edit `Scripts/Networking/NetworkManager.cs` and change the default `ServerUrl`

## Running the Client

### Start the Server First

Before running the client, make sure the MazeWars server is running:

```bash
# In the server repository
cd MazeWars.GameServer
dotnet run
```

You should see output indicating the server is running on `http://localhost:5000`.

### Start the Client

1. In Godot, press **F5** (or click the Play button ▶)
2. The main menu will appear
3. Enter your player name
4. Select a class (Tank, Healer, Damage, Rogue, Mage, or Ranger)
5. Verify the server URL is correct
6. Click **"CONNECT"**

### Connection Process

When you click Connect:

1. **SignalR Connection**: Establishes WebSocket connection for reliable messaging
2. **Authentication**: Sends player name and class to server
3. **UDP Connection**: Establishes UDP connection for low-latency input
4. **Game Load**: If successful, loads the game world scene

### Troubleshooting Connection Issues

**"Failed to connect to server"**
- Check that the server is running
- Verify the server URL is correct
- Check firewall settings
- Look at server logs for errors

**"Connection timeout"**
- Server might be overloaded
- Check network connectivity
- Verify server is listening on the correct port

**"UDP packets not being received"**
- Check that UDP port 5001 is open
- Verify firewall allows UDP traffic
- Check that server's UDP listener is running

## Development Workflow

### Recommended IDE Setup

For the best C# development experience:

**Option 1: JetBrains Rider** (Recommended)
- Best Godot C# integration
- Excellent debugging support
- Code completion and refactoring

**Option 2: Visual Studio 2022**
- Full .NET debugging
- Good C# tooling
- Install "Godot Support" extension

**Option 3: VS Code**
- Lightweight
- Install C# extension (ms-dotnettools.csharp)
- Install Godot Tools extension

### Project Structure

```
MazaWars.Client/
├── project.godot           # Godot configuration
├── MazeWars.Client.csproj  # C# project file
├── Scenes/                 # Godot scenes (.tscn files)
│   ├── Main.tscn          # Main menu
│   └── Game/
│       └── GameWorld.tscn # Game world
├── Scripts/               # C# scripts
│   ├── Networking/        # Network layer (autoloads)
│   │   ├── NetworkManager.cs      # SignalR (WebSocket)
│   │   ├── UdpNetworkClient.cs    # UDP client
│   │   ├── MessageHandler.cs      # Message routing
│   │   └── InputSender.cs         # Input handling
│   ├── Game/              # Game logic
│   │   └── GameWorld.cs
│   └── UI/                # UI controllers
│       └── MainMenu.cs
├── Shared/                # Shared DTOs from server
│   └── NetworkModels/     # Network message definitions
└── Assets/                # Game assets
    ├── Sprites/
    ├── Sounds/
    └── Fonts/
```

### Autoload Singletons

The following scripts are configured as autoload singletons (accessible globally):

- **NetworkManager** (`/root/NetworkManager`) - SignalR connection
- **UdpClient** (`/root/UdpClient`) - UDP connection
- **MessageHandler** (`/root/MessageHandler`) - Message processing
- **InputSender** (`/root/InputSender`) - Player input

Access them from any script:
```csharp
var networkManager = GetNode<NetworkManager>("/root/NetworkManager");
```

## Testing

### Manual Testing

1. **Connection Test**:
   - Run server
   - Run client
   - Enter credentials and connect
   - Verify connection success message
   - Check server logs for player connection

2. **Input Test**:
   - After connecting, the GameWorld scene loads
   - Check debug info in top-left corner
   - Verify "UDP: ↑X ↓Y" shows increasing packet counts
   - Press WASD keys and verify input sequence numbers increment

3. **Disconnect Test**:
   - Press ESC in game world
   - Should return to main menu
   - Server should log player disconnect

### Debug Information

In the GameWorld scene, debug info is displayed showing:
- Player ID
- SignalR connection status
- UDP packet statistics (sent/received)
- Input sequence numbers
- Last packet received time

### Logs

Check the Godot console (Output panel) for detailed logs:
- `[NetworkManager]` - SignalR events
- `[UdpClient]` - UDP events
- `[MessageHandler]` - Message processing
- `[InputSender]` - Input events
- `[GameWorld]` - Game events

## Network Protocol

The client implements the MazeWars network protocol:

### SignalR (WebSocket) - Reliable Messages
- Player authentication
- Chat messages
- Inventory updates
- Combat events
- Lobby state

### UDP - Low-Latency Input
- Player movement input
- Ability usage
- World state updates

### Sequence Numbers

All player inputs include:
- `SequenceNumber`: Incremental counter (client)
- `AckSequenceNumber`: Last acknowledged server update
- `ClientTimestamp`: Client time for latency calculation

See `Docs/CLIENT_IMPLEMENTATION_GUIDE.md` in the server repository for protocol details.

## Next Steps

After successful connection, you can:

1. **Implement Player Rendering**:
   - Create Player.tscn scene
   - Add sprite and animations
   - Implement client-side prediction

2. **Implement World Rendering**:
   - Create 4×4 room grid
   - Add tilemap for walls
   - Implement camera following

3. **Implement Game State Sync**:
   - Parse WorldUpdateMessage
   - Interpolate remote players
   - Reconcile local player position

4. **Implement UI**:
   - HUD (health, abilities)
   - Inventory system
   - Chat system
   - Minimap

## Support

For issues:
- Check server logs: `MazeWars.GameServer/logs/`
- Check client logs: Godot Output panel
- Check network: Use Wireshark to inspect packets
- Report bugs: GitHub Issues

## License

MIT License - See LICENSE file

---

**Status**: Phase 0 Complete ✅
- [x] Project structure
- [x] Networking layer
- [x] Connection flow
- [ ] Game rendering (Phase 1)
- [ ] Game state sync (Phase 2)
- [ ] UI systems (Phase 3)
