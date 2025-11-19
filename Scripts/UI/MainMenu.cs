using Godot;
using System;
using System.Threading.Tasks;
using MazeWars.Client.Scripts.Networking;

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
	private NetworkManager _networkManager;
	private UdpNetworkClient _udpClient;
	private MessageHandler _messageHandler;
	private InputSender _inputSender;

	private bool _isConnecting = false;

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
		_serverUrlInput.Text = "http://localhost:5000";
		_loadingPanel.Visible = false;

		// Connect button signal
		_connectButton.Pressed += OnConnectPressed;

		// Get autoload singletons (will be created in project.godot)
		_networkManager = GetNode<NetworkManager>("/root/NetworkManager");
		_udpClient = GetNode<UdpNetworkClient>("/root/UdpClient");
		_messageHandler = GetNode<MessageHandler>("/root/MessageHandler");
		_inputSender = GetNode<InputSender>("/root/InputSender");

		// Subscribe to connection events
		_networkManager.Connected += OnConnected;
		_networkManager.Disconnected += OnDisconnected;
		_networkManager.ConnectionError += OnConnectionError;

		UpdateStatus("Ready to connect");
	}

	private async void OnConnectPressed()
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
			UpdateStatus("Please enter a server URL", true);
			return;
		}

		_isConnecting = true;
		_connectButton.Disabled = true;
		_loadingPanel.Visible = true;
		UpdateStatus("Connecting to server...");

		try
		{
			// Configure server URL
			_networkManager.ServerUrl = serverUrl;

			// Extract UDP address from HTTP URL
			var uri = new Uri(serverUrl);
			_udpClient.ServerAddress = uri.Host;
			_udpClient.ServerPort = 5001; // Default UDP port

			// Connect to SignalR
			var connected = await _networkManager.ConnectAsync(playerName, playerClass);

			if (connected)
			{
				// SignalR connected, now connect UDP
				_udpClient.Connect();

				// Initialize message handler
				_messageHandler.Initialize(_networkManager, _udpClient);

				// Initialize input sender
				_inputSender.Initialize(_udpClient, _networkManager.PlayerId);

				UpdateStatus("Connected! Loading game...");

				// Wait a moment then load game scene
				await Task.Delay(1000);
				LoadGameScene();
			}
			else
			{
				UpdateStatus("Failed to connect to server", true);
				_isConnecting = false;
				_connectButton.Disabled = false;
				_loadingPanel.Visible = false;
			}
		}
		catch (Exception ex)
		{
			UpdateStatus($"Connection error: {ex.Message}", true);
			_isConnecting = false;
			_connectButton.Disabled = false;
			_loadingPanel.Visible = false;
		}
	}

	private void OnConnected()
	{
		CallDeferred(nameof(UpdateStatus), "Connected successfully!");
	}

	private void OnDisconnected()
	{
		CallDeferred(nameof(UpdateStatus), "Disconnected from server", true);
		CallDeferred(nameof(EnableConnectButton));
	}

	private void OnConnectionError(string error)
	{
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
		if (_networkManager != null)
		{
			_networkManager.Connected -= OnConnected;
			_networkManager.Disconnected -= OnDisconnected;
			_networkManager.ConnectionError -= OnConnectionError;
		}
	}
}
