using Godot;
using System;
using System.Collections.Generic;
using MazeWars.Client.Scripts.Networking;
using MazeWars.Client.Shared.NetworkModels;

namespace MazeWars.Client.Scripts.Game;

/// <summary>
/// Manages game state synchronization - spawning, updating, and removing players
/// Implements client-side prediction and server reconciliation
/// </summary>
public partial class GameStateManager : Node
{
	[Export] public PackedScene PlayerScene { get; set; }
	[Export] public bool EnablePrediction { get; set; } = true;
	[Export] public bool EnableReconciliation { get; set; } = true;

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
	private int _reconciliationsPerformed = 0;

	public Player LocalPlayer => _localPlayer;
	public int PlayerCount => _players.Count;

	public override void _Ready()
	{
		GD.Print("[GameStateManager] Initializing with Client-Side Prediction...");

		// Get network components
		_messageHandler = GetNode<MessageHandler>("/root/MessageHandler");
		_inputSender = GetNode<InputSender>("/root/InputSender");
		_networkManager = GetNode<NetworkManager>("/root/NetworkManager");

		// Subscribe to world updates
		_messageHandler.GameStateUpdate += OnGameStateUpdate;

		_localPlayerId = _networkManager.PlayerId;
		GD.Print($"[GameStateManager] Local player ID: {_localPlayerId}");
		GD.Print($"[GameStateManager] Prediction: {EnablePrediction}, Reconciliation: {EnableReconciliation}");
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

		// Get server time from update (for state buffering)
		float serverTime = update.ServerTime;

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
				UpdateOrSpawnPlayer(playerState, serverTime);
			}

			// Remove disconnected players
			RemoveDisconnectedPlayers(update.Players);
		}
	}

	private void UpdateOrSpawnPlayer(PlayerStateUpdate playerState, float serverTime)
	{
		if (string.IsNullOrEmpty(playerState.PlayerId))
			return;

		// Check if player already exists
		if (_players.TryGetValue(playerState.PlayerId, out var existingPlayer))
		{
			// Update existing player
			UpdatePlayer(existingPlayer, playerState, serverTime);
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

	private void UpdatePlayer(Player player, PlayerStateUpdate state, float serverTime)
	{
		if (player.IsLocalPlayer)
		{
			// Local player: perform reconciliation
			ReconcileLocalPlayer(player, state);
		}
		else
		{
			// Remote players: update position and state with buffering
			player.UpdateFromServerState(
				state.Position,
				state.Velocity,
				state.Health,
				state.MaxHealth,
				serverTime
			);
		}
	}

	/// <summary>
	/// Performs client-side prediction reconciliation with server state
	/// </summary>
	private void ReconcileLocalPlayer(Player player, PlayerStateUpdate serverState)
	{
		// Always update health (non-predicted state)
		player.UpdateHealth(serverState.Health, serverState.MaxHealth);

		if (!EnableReconciliation)
		{
			// No reconciliation: just snap to server position
			player.SetPosition(new Godot.Vector2(serverState.Position.X, serverState.Position.Y));
			return;
		}

		var serverPosition = new Godot.Vector2(serverState.Position.X, serverState.Position.Y);

		// Perform reconciliation
		bool mispredicted = player.ReconcileWithServer(serverPosition);

		if (mispredicted)
		{
			_reconciliationsPerformed++;

			// Replay unacknowledged inputs
			if (EnablePrediction)
			{
				ReplayPendingInputs(player);
			}
		}
	}

	/// <summary>
	/// Replays all pending (unacknowledged) inputs to maintain prediction accuracy
	/// </summary>
	private void ReplayPendingInputs(Player player)
	{
		if (_inputSender == null)
			return;

		var pendingInputs = _inputSender.GetPendingInputs();

		if (pendingInputs.Count > 0)
		{
			GD.Print($"[GameStateManager] Replaying {pendingInputs.Count} pending inputs...");

			foreach (var predictedInput in pendingInputs)
			{
				// Re-apply the input
				player.ReplayInput(
					predictedInput.Input.MoveInput,
					predictedInput.Input.IsSprinting,
					predictedInput.DeltaTime
				);
			}

			GD.Print($"[GameStateManager] Replay complete. New position: {player.Position}");
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
		// Apply local player movement with prediction
		if (_localPlayer != null && _inputSender != null && EnablePrediction)
		{
			var moveInput = ReadMovementInput();
			var isSprinting = Input.IsActionPressed("sprint");
			_localPlayer.ApplyLocalMovement(moveInput, isSprinting, (float)delta);

			// Update input sender with predicted position
			_inputSender.UpdateLastPredictedPosition(
				_localPlayer.GetPredictedPosition(),
				_localPlayer.GetPredictedVelocity()
			);
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
		var predictionInfo = _localPlayer != null ? _localPlayer.GetPredictionDebugInfo() : "";

		return $"GameState: {_players.Count} players | {_updatesReceived} updates | Last: {timeSinceLast:F1}s ago\n{predictionInfo}";
	}

	public string GetDetailedDebugInfo()
	{
		var sb = new System.Text.StringBuilder();
		sb.AppendLine("Game State Manager:");
		sb.AppendLine($"  Players: {_players.Count}");
		sb.AppendLine($"  Updates Received: {_updatesReceived}");
		sb.AppendLine($"  Reconciliations: {_reconciliationsPerformed}");
		sb.AppendLine($"  Prediction Enabled: {EnablePrediction}");
		sb.AppendLine($"  Reconciliation Enabled: {EnableReconciliation}");

		if (_localPlayer != null)
		{
			sb.AppendLine($"\nLocal Player:");
			sb.AppendLine($"  Position: {_localPlayer.Position}");
			sb.AppendLine($"  {_localPlayer.GetPredictionDebugInfo()}");
		}

		return sb.ToString();
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
