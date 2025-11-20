using Godot;
using System;
using MazeWars.Client.Shared.NetworkModels;

namespace MazeWars.Client.Scripts.Networking;

/// <summary>
/// Central hub for processing network messages and routing them to appropriate systems
/// </summary>
public partial class MessageHandler : Node
{
	// C# Events for complex types (Godot signals don't support custom classes)
	public event Action<WorldUpdateMessage>? GameStateUpdate;
	public event Action<ChatReceivedData>? ChatMessageReceived;
	public event Action<string, string>? PlayerJoined;
	public event Action<string>? PlayerLeft;
	public event Action<InventoryUpdate>? InventoryUpdated;
	public event Action<CombatEvent>? CombatEventOccurred;

	private NetworkManager? _networkManager; // Optional - for SignalR (deprecated)
	private UdpNetworkClient _udpClient;

	// Statistics
	private int _messagesProcessed = 0;
	private DateTime _lastMessageTime;

	public override void _Ready()
	{
		GD.Print("[MessageHandler] Initializing...");
	}

	public void Initialize(NetworkManager? networkManager, UdpNetworkClient udpClient)
	{
		_networkManager = networkManager;
		_udpClient = udpClient;

		// Subscribe to SignalR messages (deprecated - only for backwards compatibility)
		if (_networkManager != null)
		{
			_networkManager.MessageReceived += OnSignalRMessage;
			GD.Print("[MessageHandler] Subscribed to SignalR messages (deprecated)");
		}

		// Subscribe to UDP messages (primary communication method)
		_udpClient.WorldUpdateReceived += OnWorldUpdate;
		_udpClient.ChatMessageReceived += OnChatMessage;
		_udpClient.CombatEventReceived += OnCombatEvent;

		GD.Print("[MessageHandler] Initialized and subscribed to UDP events");
	}

	private void OnSignalRMessage(string messageType, object data)
	{
		_messagesProcessed++;
		_lastMessageTime = DateTime.UtcNow;

		GD.Print($"[MessageHandler] Processing SignalR message: {messageType}");

		try
		{
			switch (messageType)
			{
				case "WorldUpdate":
					HandleWorldUpdate(data);
					break;

				case "ChatMessage":
					HandleChatMessage(data);
					break;

				case "PlayerJoined":
					HandlePlayerJoined(data);
					break;

				case "PlayerLeft":
					HandlePlayerLeft(data);
					break;

				case "InventoryUpdate":
					HandleInventoryUpdate(data);
					break;

				case "CombatEvent":
					HandleCombatEvent(data);
					break;

				default:
					GD.Print($"[MessageHandler] Unknown message type: {messageType}");
					break;
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[MessageHandler] Error processing message {messageType}: {ex.Message}");
		}
	}

	private void OnWorldUpdate(WorldUpdateMessage update)
	{
		_messagesProcessed++;
		_lastMessageTime = DateTime.UtcNow;

		// Invoke C# event for game state manager
		GameStateUpdate?.Invoke(update);
	}

	private void OnChatMessage(ChatReceivedData chatData)
	{
		_messagesProcessed++;
		_lastMessageTime = DateTime.UtcNow;

		GD.Print($"[MessageHandler] Chat from {chatData.PlayerName} [{chatData.ChatType}]: {chatData.Message}");
		ChatMessageReceived?.Invoke(chatData);
	}

	private void OnCombatEvent(CombatEvent combatEvent)
	{
		_messagesProcessed++;
		_lastMessageTime = DateTime.UtcNow;

		GD.Print($"[MessageHandler] Combat event: {combatEvent.EventType}");
		CombatEventOccurred?.Invoke(combatEvent);
	}

	// ===== DEPRECATED SignalR Handlers (kept for backwards compatibility) =====

	private void HandleWorldUpdate(object data)
	{
		try
		{
			var update = data as WorldUpdateMessage;
			if (update != null)
			{
				GameStateUpdate?.Invoke(update);
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[MessageHandler] Failed to parse WorldUpdate: {ex.Message}");
		}
	}

	private void HandleChatMessage(object data)
	{
		try
		{
			var chatData = data as ChatReceivedData;
			if (chatData != null)
			{
				GD.Print($"[MessageHandler] Chat from {chatData.PlayerName} [{chatData.ChatType}]: {chatData.Message}");
				ChatMessageReceived?.Invoke(chatData);
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[MessageHandler] Failed to parse ChatMessage: {ex.Message}");
		}
	}

	private void HandlePlayerJoined(object data)
	{
		try
		{
			var playerData = data as string;
			if (!string.IsNullOrEmpty(playerData))
			{
				GD.Print($"[MessageHandler] Player joined: {playerData}");
				PlayerJoined?.Invoke(playerData, "Unknown");
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[MessageHandler] Failed to parse PlayerJoined: {ex.Message}");
		}
	}

	private void HandlePlayerLeft(object data)
	{
		try
		{
			var playerId = data as string;
			if (!string.IsNullOrEmpty(playerId))
			{
				GD.Print($"[MessageHandler] Player left: {playerId}");
				PlayerLeft?.Invoke(playerId);
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[MessageHandler] Failed to parse PlayerLeft: {ex.Message}");
		}
	}

	private void HandleInventoryUpdate(object data)
	{
		try
		{
			var inventory = data as InventoryUpdate;
			if (inventory != null)
			{
				GD.Print($"[MessageHandler] Inventory updated for {inventory.PlayerId}");
				InventoryUpdated?.Invoke(inventory);
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[MessageHandler] Failed to parse InventoryUpdate: {ex.Message}");
		}
	}

	private void HandleCombatEvent(object data)
	{
		try
		{
			var combatEvent = data as CombatEvent;
			if (combatEvent != null)
			{
				GD.Print($"[MessageHandler] Combat event: {combatEvent.EventType}");
				CombatEventOccurred?.Invoke(combatEvent);
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[MessageHandler] Failed to parse CombatEvent: {ex.Message}");
		}
	}

	public string GetDebugInfo()
	{
		if (_messagesProcessed == 0)
			return "Messages: None";

		var timeSinceLast = (DateTime.UtcNow - _lastMessageTime).TotalSeconds;
		return $"Messages: {_messagesProcessed} | Last: {timeSinceLast:F1}s ago";
	}
}
