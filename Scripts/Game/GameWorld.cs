using Godot;
using System;
using System.Collections.Generic;
using MazeWars.Client.Scripts.Networking;

namespace MazeWars.Client.Scripts.Game;

/// <summary>
/// Main game world scene - manages game state and rendering
/// </summary>
public partial class GameWorld : Node2D
{
	[Export] public PackedScene RoomScene { get; set; }
	[Export] public PackedScene PlayerScene { get; set; }

	private NetworkManager _networkManager;
	private UdpNetworkClient _udpClient;
	private MessageHandler _messageHandler;
	private InputSender _inputSender;
	private GameStateManager _gameStateManager;

	// World structure
	private Node2D _roomsContainer;
	private Node2D _playersContainer;
	private Dictionary<string, Room> _rooms = new Dictionary<string, Room>();

	// Camera
	private Camera2D _camera;

	// Debug info
	private CanvasLayer _debugLayer;
	private Label _debugLabel;

	public override void _Ready()
	{
		GD.Print("[GameWorld] Initializing...");

		// Load scenes if not set
		if (RoomScene == null)
			RoomScene = GD.Load<PackedScene>("res://Scenes/Game/Room.tscn");
		if (PlayerScene == null)
			PlayerScene = GD.Load<PackedScene>("res://Scenes/Game/Player.tscn");

		// Get singletons
		_networkManager = GetNode<NetworkManager>("/root/NetworkManager");
		_udpClient = GetNode<UdpNetworkClient>("/root/UdpClient");
		_messageHandler = GetNode<MessageHandler>("/root/MessageHandler");
		_inputSender = GetNode<InputSender>("/root/InputSender");

		// Create world structure
		CreateWorldStructure();

		// Create game state manager
		_gameStateManager = new GameStateManager
		{
			PlayerScene = PlayerScene
		};
		AddChild(_gameStateManager);
		_gameStateManager.SetPlayersContainer(_playersContainer);

		// Setup camera
		SetupCamera();

		// Create debug UI
		CreateDebugUI();

		GD.Print($"[GameWorld] Connected as Player ID: {_networkManager.PlayerId}");
		GD.Print("[GameWorld] World ready, waiting for game data...");
	}

	private void CreateWorldStructure()
	{
		// Create rooms container
		_roomsContainer = new Node2D { Name = "Rooms" };
		AddChild(_roomsContainer);

		// Create players container
		_playersContainer = new Node2D { Name = "Players", ZIndex = 10 };
		AddChild(_playersContainer);

		// Generate 4×4 grid of rooms
		GenerateWorld();
	}

	private void GenerateWorld()
	{
		GD.Print("[GameWorld] Generating 4×4 room grid...");

		for (int y = 0; y < 4; y++)
		{
			for (int x = 0; x < 4; x++)
			{
				var room = RoomScene.Instantiate<Room>();
				room.Setup($"room_{x}_{y}", x, y);
				_roomsContainer.AddChild(room);
				_rooms[$"room_{x}_{y}"] = room;
			}
		}

		GD.Print($"[GameWorld] Generated {_rooms.Count} rooms");
	}

	private void SetupCamera()
	{
		_camera = new Camera2D
		{
			Position = new Vector2(1600, 1600), // Center of 4×4 grid (800*4/2)
			Enabled = true
		};
		AddChild(_camera);

		GD.Print("[GameWorld] Camera created");
	}

	private void CreateDebugUI()
	{
		_debugLayer = new CanvasLayer { Name = "DebugUI" };
		AddChild(_debugLayer);

		_debugLabel = new Label
		{
			Position = new Vector2(10, 10),
			Modulate = Colors.White
		};

		// Create background panel for better readability
		var panel = new Panel
		{
			Position = new Vector2(5, 5),
			Size = new Vector2(400, 200)
		};
		panel.Modulate = new Color(0, 0, 0, 0.7f);
		_debugLayer.AddChild(panel);
		_debugLayer.AddChild(_debugLabel);

		GD.Print("[GameWorld] Debug UI created");
	}

	public override void _Process(double delta)
	{
		// Update camera to follow local player
		if (_gameStateManager?.LocalPlayer != null)
		{
			var targetPos = _gameStateManager.LocalPlayer.GlobalPosition;
			_camera.GlobalPosition = _camera.GlobalPosition.Lerp(targetPos, 10f * (float)delta);
		}

		// Update debug info
		UpdateDebugInfo();
	}

	private void UpdateDebugInfo()
	{
		if (_debugLabel == null)
			return;

		var localPlayerPos = _gameStateManager?.LocalPlayer?.GlobalPosition ?? Vector2.Zero;
		var currentRoom = GetRoomAtPosition(localPlayerPos);

		_debugLabel.Text = $"═══ MazeWars Client v0.1.0-alpha (Phase 1) ═══\n" +
		                   $"Player ID: {_networkManager.PlayerId}\n" +
		                   $"Position: ({localPlayerPos.X:F0}, {localPlayerPos.Y:F0})\n" +
		                   $"Current Room: {currentRoom}\n" +
		                   $"\n" +
		                   $"═══ Network Status ═══\n" +
		                   $"SignalR: {(_networkManager.IsConnected ? "✓ Connected" : "✗ Disconnected")}\n" +
		                   $"{_udpClient.GetDebugInfo()}\n" +
		                   $"{_inputSender.GetDebugInfo()}\n" +
		                   $"{_messageHandler.GetDebugInfo()}\n" +
		                   $"{_gameStateManager.GetDebugInfo()}\n" +
		                   $"\n" +
		                   $"═══ Controls ═══\n" +
		                   $"WASD: Move | Shift: Sprint | ESC: Disconnect";
	}

	private string GetRoomAtPosition(Vector2 position)
	{
		int gridX = Mathf.FloorToInt(position.X / 800);
		int gridY = Mathf.FloorToInt(position.Y / 800);

		if (gridX >= 0 && gridX < 4 && gridY >= 0 && gridY < 4)
			return $"room_{gridX}_{gridY}";

		return "Out of bounds";
	}

	public override void _Input(InputEvent @event)
	{
		// Handle disconnect
		if (@event.IsActionPressed("ui_cancel")) // ESC key
		{
			GD.Print("[GameWorld] Disconnecting...");
			_networkManager.DisconnectAsync();
			_udpClient.Disconnect();
			GetTree().ChangeSceneToFile("res://Scenes/Main.tscn");
		}
	}
}
