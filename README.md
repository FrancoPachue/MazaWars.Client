# MazeWars Client

Official client for the MazeWars multiplayer game built with Godot 4.3 and C#/.NET.

## Overview

MazeWars is a 2D top-down multiplayer action game featuring:
- 24 concurrent players (4 teams × 6 players)
- Team-based PvP with extraction mechanics
- Real-time networking (UDP + WebSocket)
- Client-side prediction and server reconciliation
- 6 playable classes with unique abilities
- Dynamic loot and mob systems

## Technology Stack

- **Engine**: Godot 4.3 (C#/.NET 8.0)
- **Networking**:
  - SignalR (WebSocket) for reliable messages
  - UDP for low-latency player input
- **Serialization**: MessagePack binary format
- **Target**: 60 FPS with 24+ players

## Prerequisites

- [Godot 4.3 Mono (.NET)](https://godotengine.org/download)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 or JetBrains Rider (optional but recommended)

## Getting Started

### 1. Clone the Repository

```bash
git clone https://github.com/FrancoPachue/MazaWars.Client.git
cd MazaWars.Client
```

### 2. Restore NuGet Packages

```bash
dotnet restore
```

### 3. Open in Godot

1. Open Godot 4.3 (Mono version)
2. Click "Import"
3. Navigate to the project folder and select `project.godot`
4. Click "Import & Edit"

### 4. Build the Project

In Godot:
- Press `Ctrl+B` to build the C# project
- Verify no compilation errors

### 5. Configure Server Connection

Edit connection settings in `Scripts/Networking/NetworkManager.cs`:

```csharp
[Export] public string ServerUrl { get; set; } = "http://localhost:5000";
[Export] public string UdpServerAddress { get; set; } = "127.0.0.1";
[Export] public int UdpServerPort { get; set; } = 5001;
```

### 6. Run the Client

Press `F5` in Godot to run the client.

## Project Structure

```
MazeWars.Client/
├── project.godot           # Godot project configuration
├── MazeWars.Client.csproj  # C# project file
├── Scenes/                 # Godot scenes
│   ├── Main.tscn          # Entry point
│   ├── Game/              # Game scenes
│   └── UI/                # UI scenes
├── Scripts/               # C# scripts
│   ├── Networking/        # Network layer
│   ├── Game/              # Game logic
│   └── UI/                # UI controllers
├── Assets/                # Game assets
│   ├── Sprites/
│   ├── Sounds/
│   └── Fonts/
└── Shared/                # Shared DTOs from server
```

## Development Roadmap

- [x] **Phase 0: Project Setup** ✅
  - Godot 4.3 C# project configured
  - NuGet packages (SignalR, MessagePack)
  - Networking layer (UDP + WebSocket)
  - Connection flow working

- [x] **Phase 1: Basic Rendering** ✅
  - 4×4 room grid with visual elements
  - Player rendering with class colors
  - Local player movement (WASD + Sprint)
  - Remote player synchronization
  - Camera follow system
  - See [PHASE1.md](PHASE1.md) for details

- [x] **Phase 2: Client Prediction** ✅ NEW!
  - ⚡ Instant movement (0ms input latency)
  - 🔄 Server reconciliation with error detection
  - 🎯 Input replay on misprediction
  - 📊 Prediction accuracy tracking
  - 🌐 Packet loss monitoring
  - See [PHASE2.md](PHASE2.md) for details

- [ ] **Phase 3: Game Systems**
  - Combat abilities
  - Inventory UI
  - Chat system
  - Mobs and loot

- [ ] **Phase 4: Polish & Optimization**

See [CLIENT_DEVELOPMENT_ROADMAP.md](../MazeWars.GameServer/CLIENT_DEVELOPMENT_ROADMAP.md) for detailed timeline.

## Network Protocol

The client implements the MazeWars network protocol with:

### Player Input (Client → Server via UDP)
- Sequence numbers for input ordering
- Client timestamps for latency compensation
- Acknowledgment numbers for reconciliation

### World Updates (Server → Client via UDP)
- Frame-based synchronization
- Acknowledged input tracking
- Server authoritative state

See [CLIENT_IMPLEMENTATION_GUIDE.md](../MazeWars.GameServer/Docs/CLIENT_IMPLEMENTATION_GUIDE.md) for protocol details.

## Key Features

### Client-Side Prediction
- Instant input response
- Smooth local player movement
- Minimal perceived latency

### Server Reconciliation
- Automatic position correction
- Replay of unacknowledged inputs
- Configurable error thresholds

### Entity Interpolation
- Smooth movement for remote players
- State buffering (100ms delay)
- Linear interpolation between snapshots

## Input Controls

| Action | Key |
|--------|-----|
| Move | W/A/S/D or Arrow Keys |
| Sprint | Left Shift |
| Attack | Left Mouse Button |
| Ability 1 | 1 |
| Ability 2 | 2 |
| Ability 3 | 3 |
| Inventory | I |
| Chat | Enter |

## Building for Release

### Windows
```bash
# In Godot: Project → Export → Windows Desktop
# Or via command line:
godot --headless --export-release "Windows Desktop" ./builds/MazeWars.exe
```

### Linux
```bash
godot --headless --export-release "Linux/X11" ./builds/MazeWars.x86_64
```

### macOS
```bash
godot --headless --export-release "macOS" ./builds/MazeWars.app
```

## Performance Targets

- **FPS**: 60 FPS constant
- **Network**: 20 tick/second updates
- **Latency**: < 50ms input response
- **Memory**: < 500MB RAM usage
- **Players**: 24+ concurrent

## Server Repository

The game server is available at:
https://github.com/FrancoPachue/MazeWars.GameServer

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Server: [MazeWars.GameServer](https://github.com/FrancoPachue/MazeWars.GameServer)
- Engine: [Godot Engine](https://godotengine.org/)
- Networking: [ASP.NET Core SignalR](https://docs.microsoft.com/aspnet/core/signalr/)

## Support

For issues and questions:
- GitHub Issues: [Report a bug](https://github.com/FrancoPachue/MazaWars.Client/issues)
- Server Documentation: See server repository docs

---

**Status**: ⚡ Phase 2 Complete - Instant Movement!

**Current Phase**: Phase 2 - Client-Side Prediction ✅

**Version**: 0.2.0-alpha (Phase 2)

**What Works**:
- ✅ Connect to server
- ✅ See game world (4×4 rooms)
- ⚡ **NEW: Instant player movement (0ms latency!)**
- 🔄 **NEW: Server reconciliation with error tracking**
- 🎯 **NEW: Input replay on misprediction**
- 📊 **NEW: Prediction accuracy metrics**
- ✅ See other players in real-time
- ✅ Camera follows player
- ✅ Network synchronization

**Notable Improvements**:
- Movement feels instant even with 100ms+ latency
- Smooth corrections when predictions differ from server
- Real-time packet loss monitoring
- Visual feedback (yellow flash) on reconciliation

**Try it now**: See [PHASE2.md](PHASE2.md) for testing and comparison with Phase 1!

**Last Updated**: 2025-11-19
