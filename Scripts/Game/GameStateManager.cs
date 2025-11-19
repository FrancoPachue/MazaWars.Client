using Godot;
using System;
using System.Collections.Generic;
using MazeWars.Client.Scripts.Networking;
using MazeWars.Client.Shared.NetworkModels;

namespace MazeWars.Client.Scripts.Game;

/// <summary>
/// Manages game state synchronization - spawning, updating, and removing players
/// </summary>
public partial class GameStateManager : Node
{
	[Export] public PackedScene PlayerScene { get; set; }

	private Dictionary<string, Player> _players = new Dictionary<string, Player>();
	private Node2D _playersContainer;
	private string _localPlayerId;
	private Player _localPlayer;

	private MessageHandler _messageHandler;
	private InputSender _inputSender;
	private NetworkManager _networkManager;

	// Statistics
	private int _updatesReceived = 0;
	private DateTime _lastUpdateTime;

	public Player LocalPlayer => _localPlayer;
	public int PlayerCount => _players.Count;

	public override void _Ready()
	{
		GD.Print("[GameStateManager] Initializing...");

		// Get network components
		_messageHandler = GetNode<MessageHandler>("/root/MessageHandler");
		_inputSender = GetNode<InputSender>("/root/InputSender");
		_networkManager = GetNode<NetworkManager>("/root/NetworkManager");

		// Subscribe to world updates
		_messageHandler.GameStateUpdate += OnGameStateUpdate;

		_localPlayerId = _networkManager.PlayerId;
		GD.Print($"[GameStateManager] Local player ID: {_localPlayerId}");
	}

	public void SetPlayersContainer(Node2D container)
	{
		_playersContainer = container;
		GD.Print("[GameStateManager] Players container set");
	}

	private void OnGameStateUpdate(WorldUpdateMessage update)
	{
		_updatesReceived++;
		_lastUpdateTime = DateTime.UtcNow;

		// Update acknowledged inputs for reconciliation
		if (update.AcknowledgedInputs != null && update.AcknowledgedInputs.TryGetValue(_localPlayerId, out uint ackedSeq))
		{
			_inputSender?.OnServerUpdate(ackedSeq);
		}

		// Update all players
		if (update.Players != null)
		{
			foreach (var playerState in update.Players)
			{
				UpdateOrSpawnPlayer(playerState);
			}

			// Remove disconnected players
			RemoveDisconnectedPlayers(update.Players);
		}
	}

	private void UpdateOrSpawnPlayer(PlayerStateUpdate playerState)
	{
		if (string.IsNullOrEmpty(playerState.PlayerId))
			return;

		// Check if player already exists
		if (_players.TryGetValue(playerState.PlayerId, out var existingPlayer))
		{
			// Update existing player
			UpdatePlayer(existingPlayer, playerState);
		}
		else
		{
			// Spawn new player
			SpawnPlayer(playerState);
		}
	}

	private void SpawnPlayer(PlayerStateUpdate playerState)
	{
		if (_playersContainer == null || PlayerScene == null)
		{
			GD.PrintErr("[GameStateManager] Cannot spawn player - container or scene not set");
			return;
		}

		var player = PlayerScene.Instantiate<Player>();
		player.PlayerId = playerState.PlayerId;
		player.PlayerName = playerState.PlayerName ?? playerState.PlayerId;
		player.PlayerClass = playerState.PlayerClass ?? "Tank";
		player.IsLocalPlayer = playerState.PlayerId == _localPlayerId;

		// Set initial position
		player.Position = new Godot.Vector2(playerState.Position.X, playerState.Position.Y);

		_playersContainer.AddChild(player);
		_players[playerState.PlayerId] = player;

		// If this is the local player, store reference
		if (player.IsLocalPlayer)
		{
			_localPlayer = player;
			GD.Print($"[GameStateManager] Local player spawned at {player.Position}");
		}

		GD.Print($"[GameStateManager] Spawned player {player.PlayerName} ({playerState.PlayerId}) at {player.Position}");
	}

	private void UpdatePlayer(Player player, PlayerStateUpdate state)
	{
		if (player.IsLocalPlayer)
		{
			// Local player: use for reconciliation
			// For now, we'll just update health
			player.UpdateHealth(state.CurrentHealth, state.MaxHealth);
		}
		else
		{
			// Remote players: update position and state
			player.UpdateFromServerState(
				state.Position,
				state.Velocity,
				state.CurrentHealth,
				state.MaxHealth
			);
		}
	}

	private void RemoveDisconnectedPlayers(List<PlayerStateUpdate> currentPlayers)
	{
		var currentIds = new HashSet<string>();
		foreach (var p in currentPlayers)
		{
			if (!string.IsNullOrEmpty(p.PlayerId))
				currentIds.Add(p.PlayerId);
		}

		var toRemove = new List<string>();
		foreach (var playerId in _players.Keys)
		{
			if (!currentIds.Contains(playerId))
			{
				toRemove.Add(playerId);
			}
		}

		foreach (var playerId in toRemove)
		{
			if (_players.TryGetValue(playerId, out var player))
			{
				GD.Print($"[GameStateManager] Removing disconnected player {player.PlayerName} ({playerId})");
				player.QueueFree();
				_players.Remove(playerId);
			}
		}
	}

	public override void _Process(double delta)
	{
		// Apply local player movement from input
		if (_localPlayer != null && _inputSender != null)
		{
			var moveInput = ReadMovementInput();
			var isSprinting = Input.IsActionPressed("sprint");
			_localPlayer.ApplyLocalMovement(moveInput, isSprinting, (float)delta);
		}
	}

	private Godot.Vector2 ReadMovementInput()
	{
		var input = Godot.Vector2.Zero;

		if (Input.IsActionPressed("move_up"))
			input.Y -= 1;
		if (Input.IsActionPressed("move_down"))
			input.Y += 1;
		if (Input.IsActionPressed("move_left"))
			input.X -= 1;
		if (Input.IsActionPressed("move_right"))
			input.X += 1;

		if (input.Length() > 0)
			input = input.Normalized();

		return input;
	}

	public string GetDebugInfo()
	{
		if (_updatesReceived == 0)
			return "GameState: No updates";

		var timeSinceLast = (DateTime.UtcNow - _lastUpdateTime).TotalSeconds;
		return $"GameState: {_players.Count} players | {_updatesReceived} updates | Last: {timeSinceLast:F1}s ago";
	}

	public override void _ExitTree()
	{
		// Cleanup
		if (_messageHandler != null)
		{
			_messageHandler.GameStateUpdate -= OnGameStateUpdate;
		}
	}
}
