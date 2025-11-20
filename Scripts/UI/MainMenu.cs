using Godot;
using System;
using System.Threading.Tasks;
using MazeWars.Client.Scripts.Networking;
using MazeWars.Client.Shared.NetworkModels;

namespace MazeWars.Client.Scripts.UI;

/// <summary>
/// Main menu and connection screen
/// </summary>
public partial class MainMenu : Control
{
	// UI Elements (will be assigned in scene)
	private LineEdit _playerNameInput;
	private OptionButton _classSelector;
	private LineEdit _serverUrlInput;
	private Button _connectButton;
	private Label _statusLabel;
	private Panel _loadingPanel;

	// Network references (autoload singletons)
	private UdpNetworkClient _udpClient;
	private MessageHandler _messageHandler;
	private InputSender _inputSender;

	private bool _isConnecting = false;
	private string _pendingPlayerName = string.Empty;
	private string _pendingPlayerClass = string.Empty;

	public override void _Ready()
	{
		GD.Print("[MainMenu] Initializing...");

		// Get UI elements
		_playerNameInput = GetNode<LineEdit>("VBoxContainer/PlayerNameInput");
		_classSelector = GetNode<OptionButton>("VBoxContainer/ClassSelector");
		_serverUrlInput = GetNode<LineEdit>("VBoxContainer/ServerUrlInput");
		_connectButton = GetNode<Button>("VBoxContainer/ConnectButton");
		_statusLabel = GetNode<Label>("VBoxContainer/StatusLabel");
		_loadingPanel = GetNode<Panel>("LoadingPanel");

		// Setup class options
		_classSelector.AddItem("Tank");
		_classSelector.AddItem("Healer");
		_classSelector.AddItem("Damage");
		_classSelector.AddItem("Rogue");
		_classSelector.AddItem("Mage");
		_classSelector.AddItem("Ranger");

		// Set defaults
		_playerNameInput.Text = $"Player{GD.Randi() % 1000}";
		_serverUrlInput.Text = "127.0.0.1:7001"; // UDP server address (must match server's UdpPort in appsettings.json)
		_loadingPanel.Visible = false;

		// Connect button signal
		_connectButton.Pressed += OnConnectPressed;

		// Get autoload singletons (will be created in project.godot)
		_udpClient = GetNode<UdpNetworkClient>("/root/UdpClient");
		_messageHandler = GetNode<MessageHandler>("/root/MessageHandler");
		_inputSender = GetNode<InputSender>("/root/InputSender");

		// Subscribe to UDP connection events
		_udpClient.ConnectionResponse += OnConnectionResponse;
		_udpClient.ConnectionError += OnConnectionError;

		UpdateStatus("Ready to connect");
	}

	private void OnConnectPressed()
	{
		if (_isConnecting)
			return;

		var playerName = _playerNameInput.Text.Trim();
		var playerClass = _classSelector.GetItemText(_classSelector.Selected);
		var serverUrl = _serverUrlInput.Text.Trim();

		// Validation
		if (string.IsNullOrEmpty(playerName))
		{
			UpdateStatus("Please enter a player name", true);
			return;
		}

		if (string.IsNullOrEmpty(serverUrl))
		{
			UpdateStatus("Please enter a server address", true);
			return;
		}

		// Parse server address (format: "host:port" or just "host")
		var parts = serverUrl.Split(':');
		var serverAddress = parts[0];
		var serverPort = parts.Length > 1 && int.TryParse(parts[1], out var port) ? port : 7001; // Default to server's UDP port

		_isConnecting = true;
		_connectButton.Disabled = true;
		_loadingPanel.Visible = true;
		_pendingPlayerName = playerName;
		_pendingPlayerClass = playerClass;

		UpdateStatus($"Connecting to {serverAddress}:{serverPort}...");

		try
		{
			// Configure UDP client
			_udpClient.ServerAddress = serverAddress;
			_udpClient.ServerPort = serverPort;

			// Connect via UDP (sends ClientConnectData automatically)
			_udpClient.ConnectToServer(playerName, playerClass);

			GD.Print($"[MainMenu] Connection request sent for {playerName} ({playerClass})");
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[MainMenu] Failed to connect to server");
			GD.PrintErr($"[MainMenu] Error: {ex.Message}");
			UpdateStatus($"Connection error: {ex.Message}", true);
			_isConnecting = false;
			_connectButton.Disabled = false;
			_loadingPanel.Visible = false;
		}
	}

	private async void OnConnectionResponse(ConnectResponseData response)
	{
		if (response.Success)
		{
			GD.Print($"[MainMenu] Connection successful! Player ID: {response.PlayerId}");
			UpdateStatus("Connected! Loading game...");

			// Initialize message handler (without NetworkManager)
			_messageHandler.Initialize(null, _udpClient);

			// Initialize input sender with UDP client and player ID
			_inputSender.Initialize(_udpClient, response.PlayerId);

			// Wait a moment then load game scene
			await Task.Delay(1000);
			LoadGameScene();
		}
		else
		{
			GD.PrintErr($"[MainMenu] Connection rejected: {response.ErrorMessage}");
			UpdateStatus($"Connection failed: {response.ErrorMessage}", true);
			EnableConnectButton();
		}
	}

	private void OnConnectionError(string error)
	{
		GD.PrintErr($"[MainMenu] Connection error: {error}");
		CallDeferred(nameof(UpdateStatus), $"Error: {error}", true);
		CallDeferred(nameof(EnableConnectButton));
	}

	private void EnableConnectButton()
	{
		_connectButton.Disabled = false;
		_loadingPanel.Visible = false;
		_isConnecting = false;
	}

	private void UpdateStatus(string message, bool isError = false)
	{
		_statusLabel.Text = message;
		_statusLabel.Modulate = isError ? Colors.Red : Colors.White;
		GD.Print($"[MainMenu] {message}");
	}

	private void LoadGameScene()
	{
		GD.Print("[MainMenu] Loading game scene...");
		GetTree().ChangeSceneToFile("res://Scenes/Game/GameWorld.tscn");
	}

	public override void _ExitTree()
	{
		// Cleanup subscriptions
		if (_udpClient != null)
		{
			_udpClient.ConnectionResponse -= OnConnectionResponse;
			_udpClient.ConnectionError -= OnConnectionError;
		}
	}
}
