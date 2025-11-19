using Godot;
using System;
using MazeWars.Client.Scripts.Networking;

namespace MazeWars.Client.Scripts.Game;

/// <summary>
/// Main game world scene - manages game state and rendering
/// </summary>
public partial class GameWorld : Node2D
{
	private NetworkManager _networkManager;
	private UdpNetworkClient _udpClient;
	private MessageHandler _messageHandler;
	private InputSender _inputSender;

	// Debug info
	private Label _debugLabel;

	public override void _Ready()
	{
		GD.Print("[GameWorld] Initializing...");

		// Get singletons
		_networkManager = GetNode<NetworkManager>("/root/NetworkManager");
		_udpClient = GetNode<UdpNetworkClient>("/root/UdpClient");
		_messageHandler = GetNode<MessageHandler>("/root/MessageHandler");
		_inputSender = GetNode<InputSender>("/root/InputSender");

		// Create debug label
		_debugLabel = new Label
		{
			Position = new Vector2(10, 10),
			Theme = new Theme()
		};
		AddChild(_debugLabel);

		GD.Print($"[GameWorld] Connected as Player ID: {_networkManager.PlayerId}");
		GD.Print("[GameWorld] Waiting for game data...");
	}

	public override void _Process(double delta)
	{
		// Update debug info
		if (_debugLabel != null)
		{
			_debugLabel.Text = $"MazeWars Client v0.1.0-alpha\n" +
			                   $"Player ID: {_networkManager.PlayerId}\n" +
			                   $"SignalR: {(_networkManager.IsConnected ? "Connected" : "Disconnected")}\n" +
			                   $"{_udpClient.GetDebugInfo()}\n" +
			                   $"{_inputSender.GetDebugInfo()}\n" +
			                   $"{_messageHandler.GetDebugInfo()}\n" +
			                   $"\nPress ESC to disconnect";
		}
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
