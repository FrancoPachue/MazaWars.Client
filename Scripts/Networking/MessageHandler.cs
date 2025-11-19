using Godot;
using System;
using MazeWars.Client.Shared.NetworkModels;

namespace MazeWars.Client.Scripts.Networking;

/// <summary>
/// Central hub for processing network messages and routing them to appropriate systems
/// </summary>
public partial class MessageHandler : Node
{
	[Signal] public delegate void GameStateUpdateEventHandler(WorldUpdateMessage update);
	[Signal] public delegate void ChatMessageReceivedEventHandler(ChatReceivedData chatData);
	[Signal] public delegate void PlayerJoinedEventHandler(string playerId, string playerName);
	[Signal] public delegate void PlayerLeftEventHandler(string playerId);
	[Signal] public delegate void InventoryUpdatedEventHandler(InventoryUpdate inventory);
	[Signal] public delegate void CombatEventEventHandler(CombatEvent combatEvent);

	private NetworkManager _networkManager;
	private UdpNetworkClient _udpClient;

	// Statistics
	private int _messagesProcessed = 0;
	private DateTime _lastMessageTime;

	public override void _Ready()
	{
		GD.Print("[MessageHandler] Initializing...");
	}

	public void Initialize(NetworkManager networkManager, UdpNetworkClient udpClient)
	{
		_networkManager = networkManager;
		_udpClient = udpClient;

		// Subscribe to SignalR messages
		_networkManager.MessageReceived += OnSignalRMessage;

		// Subscribe to UDP messages
		_udpClient.WorldUpdateReceived += OnWorldUpdate;

		GD.Print("[MessageHandler] Initialized and subscribed to network events");
	}

	private void OnSignalRMessage(string messageType, Variant data)
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

		// Emit signal for game state manager
		EmitSignal(SignalName.GameStateUpdate, update);
	}

	private void HandleWorldUpdate(Variant data)
	{
		try
		{
			var update = data.As<WorldUpdateMessage>();
			if (update != null)
			{
				EmitSignal(SignalName.GameStateUpdate, update);
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[MessageHandler] Failed to parse WorldUpdate: {ex.Message}");
		}
	}

	private void HandleChatMessage(Variant data)
	{
		try
		{
			var chatData = data.As<ChatReceivedData>();
			if (chatData != null)
			{
				GD.Print($"[MessageHandler] Chat from {chatData.SenderName} [{chatData.Channel}]: {chatData.Message}");
				EmitSignal(SignalName.ChatMessageReceived, chatData);
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[MessageHandler] Failed to parse ChatMessage: {ex.Message}");
		}
	}

	private void HandlePlayerJoined(Variant data)
	{
		try
		{
			var playerData = data.AsString();
			if (!string.IsNullOrEmpty(playerData))
			{
				GD.Print($"[MessageHandler] Player joined: {playerData}");
				EmitSignal(SignalName.PlayerJoined, playerData, "Unknown");
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[MessageHandler] Failed to parse PlayerJoined: {ex.Message}");
		}
	}

	private void HandlePlayerLeft(Variant data)
	{
		try
		{
			var playerId = data.AsString();
			if (!string.IsNullOrEmpty(playerId))
			{
				GD.Print($"[MessageHandler] Player left: {playerId}");
				EmitSignal(SignalName.PlayerLeft, playerId);
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[MessageHandler] Failed to parse PlayerLeft: {ex.Message}");
		}
	}

	private void HandleInventoryUpdate(Variant data)
	{
		try
		{
			var inventory = data.As<InventoryUpdate>();
			if (inventory != null)
			{
				GD.Print($"[MessageHandler] Inventory updated for {inventory.PlayerId}");
				EmitSignal(SignalName.InventoryUpdated, inventory);
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[MessageHandler] Failed to parse InventoryUpdate: {ex.Message}");
		}
	}

	private void HandleCombatEvent(Variant data)
	{
		try
		{
			var combatEvent = data.As<CombatEvent>();
			if (combatEvent != null)
			{
				GD.Print($"[MessageHandler] Combat event: {combatEvent.EventType}");
				EmitSignal(SignalName.CombatEvent, combatEvent);
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
