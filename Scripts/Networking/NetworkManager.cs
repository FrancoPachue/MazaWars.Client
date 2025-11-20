using Godot;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Threading.Tasks;
using MazeWars.Client.Shared.NetworkModels;

namespace MazeWars.Client.Scripts.Networking;

/// <summary>
/// Manages SignalR (WebSocket) connection to game server for reliable messaging
/// </summary>
public partial class NetworkManager : Node
{
	[Export] public string ServerUrl { get; set; } = "http://localhost:5000";

	[Signal] public delegate void ConnectedEventHandler();
	[Signal] public delegate void DisconnectedEventHandler();
	[Signal] public delegate void ConnectionErrorEventHandler(string error);
	[Signal] public delegate void MessageReceivedEventHandler(string messageType, Variant data);

	private HubConnection _hubConnection;
	private bool _isConnecting;

	public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;
	public string PlayerId { get; private set; }

	public override void _Ready()
	{
		GD.Print($"[NetworkManager] Initializing with server URL: {ServerUrl}");
		SetupSignalR();
	}

	private void SetupSignalR()
	{
		try
		{
			_hubConnection = new HubConnectionBuilder()
				.WithUrl($"{ServerUrl}/gamehub")
				.WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
				.Build();

			// Register message handlers
			_hubConnection.On<string, object>("ReceiveMessage", OnMessageReceived);
			_hubConnection.On<string>("PlayerConnected", OnPlayerConnected);
			_hubConnection.On<string>("PlayerDisconnected", OnPlayerDisconnected);
			_hubConnection.On<WorldUpdateMessage>("WorldUpdate", OnWorldUpdate);
			_hubConnection.On<ChatReceivedData>("ChatMessage", OnChatMessage);

			_hubConnection.Closed += async (error) =>
			{
				CallDeferred(MethodName.EmitSignal, SignalName.Disconnected);

				if (error != null)
				{
					GD.PrintErr($"[NetworkManager] Connection closed: {error.Message}");
					CallDeferred(MethodName.EmitSignal, SignalName.ConnectionError, error.Message);
				}
				else
				{
					GD.Print("[NetworkManager] Connection closed gracefully");
				}
			};

			_hubConnection.Reconnecting += (error) =>
			{
				GD.Print($"[NetworkManager] Reconnecting... {error?.Message ?? "Unknown reason"}");
				return Task.CompletedTask;
			};

			_hubConnection.Reconnected += (connectionId) =>
			{
				GD.Print($"[NetworkManager] Reconnected! Connection ID: {connectionId}");
				CallDeferred(MethodName.EmitSignal, SignalName.Connected);
				return Task.CompletedTask;
			};

			GD.Print("[NetworkManager] SignalR hub configured successfully");
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[NetworkManager] Failed to setup SignalR: {ex.Message}");
			CallDeferred(MethodName.EmitSignal, SignalName.ConnectionError, ex.Message);
		}
	}

	public async Task<bool> ConnectAsync(string playerName, string playerClass)
	{
		if (_isConnecting)
		{
			GD.PrintErr("[NetworkManager] Already attempting to connect");
			return false;
		}

		if (IsConnected)
		{
			GD.Print("[NetworkManager] Already connected");
			return true;
		}

		try
		{
			_isConnecting = true;
			GD.Print($"[NetworkManager] Connecting to {ServerUrl}...");

			await _hubConnection.StartAsync();

			GD.Print("[NetworkManager] SignalR connected, sending authentication...");

			// Send authentication/connection data
			var connectData = new ClientConnectData
			{
				PlayerName = playerName,
				PlayerClass = playerClass
			};

			var response = await _hubConnection.InvokeAsync<ConnectResponseData>("Connect", connectData);

			if (response.Success)
			{
				PlayerId = response.PlayerId;
				GD.Print($"[NetworkManager] Successfully connected! Player ID: {PlayerId}");
				CallDeferred(MethodName.EmitSignal, SignalName.Connected);
				return true;
			}
			else
			{
				GD.PrintErr($"[NetworkManager] Connection rejected: {response.ErrorMessage}");
				CallDeferred(MethodName.EmitSignal, SignalName.ConnectionError, response.ErrorMessage);
				await _hubConnection.StopAsync();
				return false;
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[NetworkManager] Failed to connect: {ex.Message}");
			CallDeferred(MethodName.EmitSignal, SignalName.ConnectionError, ex.Message);
			return false;
		}
		finally
		{
			_isConnecting = false;
		}
	}

	public async Task DisconnectAsync()
	{
		if (_hubConnection != null && IsConnected)
		{
			try
			{
				GD.Print("[NetworkManager] Disconnecting...");
				await _hubConnection.StopAsync();
				await _hubConnection.DisposeAsync();
				GD.Print("[NetworkManager] Disconnected successfully");
			}
			catch (Exception ex)
			{
				GD.PrintErr($"[NetworkManager] Error during disconnect: {ex.Message}");
			}
		}
	}

	private void OnMessageReceived(string messageType, object data)
	{
		GD.Print($"[NetworkManager] Received message: {messageType}");
		CallDeferred(MethodName.EmitSignal, SignalName.MessageReceived, messageType, Variant.From(data));
	}

	private void OnPlayerConnected(string playerId)
	{
		GD.Print($"[NetworkManager] Player connected: {playerId}");
	}

	private void OnPlayerDisconnected(string playerId)
	{
		GD.Print($"[NetworkManager] Player disconnected: {playerId}");
	}

	private void OnWorldUpdate(WorldUpdateMessage update)
	{
		// Forward to message handler via signal
		CallDeferred(MethodName.EmitSignal, SignalName.MessageReceived, "WorldUpdate", Variant.From(update));
	}

	private void OnChatMessage(ChatReceivedData chatData)
	{
		GD.Print($"[NetworkManager] Chat message from {chatData.PlayerName}: {chatData.Message}");
		CallDeferred(MethodName.EmitSignal, SignalName.MessageReceived, "ChatMessage", Variant.From(chatData));
	}

	public async Task SendMessageAsync(string messageType, object data)
	{
		if (!IsConnected)
		{
			GD.PrintErr("[NetworkManager] Cannot send message: not connected");
			return;
		}

		try
		{
			await _hubConnection.InvokeAsync("SendMessage", messageType, data);
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[NetworkManager] Failed to send message: {ex.Message}");
		}
	}

	public async Task SendChatMessageAsync(string message, string channel = "Global")
	{
		if (!IsConnected)
		{
			GD.PrintErr("[NetworkManager] Cannot send chat: not connected");
			return;
		}

		try
		{
			var chatMessage = new ChatMessage
			{
				Message = message,
				ChatType = channel
			};

			await _hubConnection.InvokeAsync("SendChatMessage", chatMessage);
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[NetworkManager] Failed to send chat message: {ex.Message}");
		}
	}

	public override void _ExitTree()
	{
		// Cleanup on scene exit
		if (_hubConnection != null)
		{
			Task.Run(async () => await DisconnectAsync()).Wait(TimeSpan.FromSeconds(2));
		}
	}
}
