using Godot;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace MazeWars.Client.Scripts.Game;

/// <summary>
/// Represents a player in the game world
/// Supports client-side prediction and server reconciliation
/// Remote players use state buffering for ultra-smooth interpolation
/// </summary>
public partial class Player : CharacterBody2D
{
	[Export] public float MoveSpeed { get; set; } = 300f;
	[Export] public float SprintMultiplier { get; set; } = 1.5f;
	[Export] public float ReconciliationThreshold { get; set; } = 2.0f; // Units of error before correction
	[Export] public float InterpolationDelay { get; set; } = 0.1f; // 100ms delay for remote players (state buffering)

	/// <summary>
	/// Snapshot of player state at a specific time
	/// </summary>
	public struct StateSnapshot
	{
		public Godot.Vector2 Position;
		public Godot.Vector2 Velocity;
		public float ServerTime;
		public float Health;
	}

	// Player data
	public string PlayerId { get; set; }
	public string PlayerName { get; set; }
	public string PlayerClass { get; set; }
	public bool IsLocalPlayer { get; set; }

	// Visual components
	private ColorRect _sprite;
	private Label _nameLabel;
	private ProgressBar _healthBar;

	// Server state (for reconciliation and interpolation)
	private Godot.Vector2 _serverPosition;
	private Godot.Vector2 _serverVelocity;

	// Client prediction state
	private Godot.Vector2 _predictedPosition;
	private Godot.Vector2 _predictedVelocity;

	// State buffering for remote players (smooth interpolation)
	private Queue<StateSnapshot> _stateBuffer = new Queue<StateSnapshot>();
	private const int MAX_BUFFER_SIZE = 30; // ~0.5 seconds at 60 updates/sec
	private float _clientStartTime;

	// Reconciliation stats
	private int _reconciliationsPerformed = 0;
	private float _maxPredictionError = 0f;
	private float _lastPredictionError = 0f;

	// Health
	private float _currentHealth = 100f;
	private float _maxHealth = 100f;

	public override void _Ready()
	{
		_clientStartTime = (float)Time.GetTicksMsec() / 1000f;

		// Get or create visual components
		_sprite = GetNodeOrNull<ColorRect>("Sprite");
		_nameLabel = GetNodeOrNull<Label>("NameLabel");
		_healthBar = GetNodeOrNull<ProgressBar>("HealthBar");

		if (_sprite == null)
		{
			_sprite = new ColorRect
			{
				Size = new Godot.Vector2(32, 32),
				Position = new Godot.Vector2(-16, -16),
				Color = GetClassColor(PlayerClass)
			};
			AddChild(_sprite);
		}

		if (_nameLabel == null)
		{
			_nameLabel = new Label
			{
				Position = new Godot.Vector2(-50, -40),
				Size = new Godot.Vector2(100, 20),
				HorizontalAlignment = HorizontalAlignment.Center,
				Modulate = Colors.White
			};
			AddChild(_nameLabel);
		}

		if (_healthBar == null)
		{
			_healthBar = new ProgressBar
			{
				Position = new Godot.Vector2(-25, 20),
				Size = new Godot.Vector2(50, 5),
				MaxValue = 100,
				Value = 100,
				ShowPercentage = false
			};
			AddChild(_healthBar);
		}

		_nameLabel.Text = PlayerName;
		_sprite.Color = GetClassColor(PlayerClass);

		// Set collision layer
		CollisionLayer = 2; // Players layer
		CollisionMask = 1 | 2; // World and Players

		// Initialize prediction state
		_predictedPosition = Position;
		_predictedVelocity = Godot.Vector2.Zero;

		GD.Print($"[Player] Created player {PlayerName} ({PlayerId}) - Class: {PlayerClass}, IsLocal: {IsLocalPlayer}");
	}

	private Color GetClassColor(string playerClass)
	{
		return playerClass switch
		{
			"Tank" => new Color(0.3f, 0.3f, 0.8f), // Blue
			"Healer" => new Color(0.3f, 0.8f, 0.3f), // Green
			"Damage" => new Color(0.8f, 0.3f, 0.3f), // Red
			"Rogue" => new Color(0.6f, 0.3f, 0.6f), // Purple
			"Mage" => new Color(0.3f, 0.6f, 0.8f), // Cyan
			"Ranger" => new Color(0.8f, 0.6f, 0.3f), // Orange
			_ => Colors.Gray
		};
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsLocalPlayer)
		{
			// Local player: prediction is handled by GameStateManager
			// This just updates visuals
			UpdateVisuals(delta);
		}
		else
		{
			// Remote players: use state buffering for ultra-smooth interpolation
			InterpolateFromBuffer(delta);
		}
	}

	/// <summary>
	/// Applies movement input with prediction (for local player)
	/// </summary>
	public void ApplyLocalMovement(Godot.Vector2 moveInput, bool isSprinting, float delta)
	{
		if (!IsLocalPlayer)
			return;

		var speed = MoveSpeed;
		if (isSprinting)
			speed *= SprintMultiplier;

		Velocity = moveInput * speed;
		_predictedVelocity = Velocity;

		MoveAndSlide();

		// Update predicted position
		_predictedPosition = Position;
	}

	/// <summary>
	/// Sets the server's authoritative position and velocity
	/// For remote players, buffers the state for smooth interpolation
	/// For local player, used for reconciliation
	/// </summary>
	public void SetServerPosition(Godot.Vector2 position, Godot.Vector2 velocity)
	{
		_serverPosition = position;
		_serverVelocity = velocity;
	}

	/// <summary>
	/// Buffers a server state snapshot for remote player interpolation
	/// </summary>
	public void BufferServerState(Godot.Vector2 position, Godot.Vector2 velocity, float health, float serverTime)
	{
		if (IsLocalPlayer)
			return; // Local player doesn't use buffering

		var snapshot = new StateSnapshot
		{
			Position = position,
			Velocity = velocity,
			ServerTime = serverTime,
			Health = health
		};

		_stateBuffer.Enqueue(snapshot);

		// Maintain buffer size (keep last ~0.5 seconds)
		while (_stateBuffer.Count > MAX_BUFFER_SIZE)
		{
			_stateBuffer.Dequeue();
		}
	}

	/// <summary>
	/// Interpolates position from buffered snapshots
	/// Renders player "in the past" for ultra-smooth movement
	/// </summary>
	private void InterpolateFromBuffer(double delta)
	{
		// Need at least 2 snapshots to interpolate
		if (_stateBuffer.Count < 2)
		{
			// Fallback to simple interpolation if buffer not ready
			InterpolateToServerPosition(delta);
			return;
		}

		// Calculate render time (current time - interpolation delay)
		float currentTime = (float)Time.GetTicksMsec() / 1000f - _clientStartTime;
		float renderTime = currentTime - InterpolationDelay;

		// Find the two snapshots to interpolate between
		StateSnapshot? from = null;
		StateSnapshot? to = null;

		var snapshots = _stateBuffer.ToArray();
		for (int i = 0; i < snapshots.Length - 1; i++)
		{
			if (snapshots[i].ServerTime <= renderTime && snapshots[i + 1].ServerTime >= renderTime)
			{
				from = snapshots[i];
				to = snapshots[i + 1];
				break;
			}
		}

		// If we found valid snapshots, interpolate between them
		if (from.HasValue && to.HasValue)
		{
			// Calculate interpolation factor
			float duration = to.Value.ServerTime - from.Value.ServerTime;
			float t = duration > 0 ? (renderTime - from.Value.ServerTime) / duration : 0f;
			t = Mathf.Clamp(t, 0f, 1f);

			// Interpolate position
			Position = from.Value.Position.Lerp(to.Value.Position, t);

			// Update velocity for visual feedback
			_serverVelocity = from.Value.Velocity.Lerp(to.Value.Velocity, t);

			// Interpolate health
			float interpolatedHealth = Mathf.Lerp(from.Value.Health, to.Value.Health, t);
			UpdateHealth(interpolatedHealth, _maxHealth);

			// Update visuals based on velocity
			UpdateRemotePlayerVisuals(delta);
		}
		else
		{
			// No valid snapshots found, use latest snapshot or fallback
			if (_stateBuffer.Count > 0)
			{
				var latest = _stateBuffer.ToArray()[_stateBuffer.Count - 1];
				Position = Position.Lerp(latest.Position, 10f * (float)delta);
				_serverVelocity = latest.Velocity;
				UpdateRemotePlayerVisuals(delta);
			}
		}

		// Clean up old snapshots (older than render time - 1 second)
		while (_stateBuffer.Count > 0 && _stateBuffer.Peek().ServerTime < renderTime - 1.0f)
		{
			_stateBuffer.Dequeue();
		}
	}

	/// <summary>
	/// Reconciles predicted position with server position
	/// Returns true if reconciliation was performed (misprediction detected)
	/// </summary>
	public bool ReconcileWithServer(Godot.Vector2 serverPosition)
	{
		if (!IsLocalPlayer)
			return false;

		// Calculate prediction error
		var predictionError = (Position - serverPosition).Length();
		_lastPredictionError = predictionError;

		if (predictionError > _maxPredictionError)
			_maxPredictionError = predictionError;

		// Check if error exceeds threshold
		if (predictionError > ReconciliationThreshold)
		{
			_reconciliationsPerformed++;

			GD.Print($"[Player] Reconciliation! Error: {predictionError:F2} units. " +
			         $"Client: {Position}, Server: {serverPosition}");

			// Snap to server position
			Position = serverPosition;
			_predictedPosition = serverPosition;

			// Visual feedback for reconciliation (optional)
			_sprite.Modulate = new Color(1, 1, 0, 1); // Yellow flash

			return true;
		}

		return false;
	}

	/// <summary>
	/// Re-applies an input for reconciliation
	/// Used to replay unacknowledged inputs after server correction
	/// </summary>
	public void ReplayInput(System.Numerics.Vector2 moveInput, bool isSprinting, float deltaTime)
	{
		if (!IsLocalPlayer)
			return;

		var godotInput = new Godot.Vector2(moveInput.X, moveInput.Y);

		var speed = MoveSpeed;
		if (isSprinting)
			speed *= SprintMultiplier;

		Velocity = godotInput * speed;
		MoveAndSlide();

		_predictedPosition = Position;
	}

	/// <summary>
	/// Interpolates remote player to server position smoothly (fallback)
	/// </summary>
	private void InterpolateToServerPosition(double delta)
	{
		// Smooth interpolation to server position
		Position = Position.Lerp(_serverPosition, 15f * (float)delta);

		UpdateRemotePlayerVisuals(delta);
	}

	/// <summary>
	/// Updates visual feedback for remote players based on velocity
	/// </summary>
	private void UpdateRemotePlayerVisuals(double delta)
	{
		// Update sprite direction based on velocity
		if (_serverVelocity.LengthSquared() > 0.01f)
		{
			// Visual feedback: scale sprite slightly when moving
			_sprite.Scale = Godot.Vector2.One * 1.1f;

			// Smooth rotation towards movement direction
			if (_serverVelocity.X != 0)
			{
				float targetRotation = _serverVelocity.X > 0 ? 0.1f : -0.1f;
				_sprite.Rotation = Mathf.Lerp(_sprite.Rotation, targetRotation, 10f * (float)delta);
			}
		}
		else
		{
			_sprite.Scale = Godot.Vector2.One;
			_sprite.Rotation = Mathf.Lerp(_sprite.Rotation, 0, 10f * (float)delta);
		}
	}

	/// <summary>
	/// Updates visual feedback based on current state
	/// </summary>
	private void UpdateVisuals(double delta)
	{
		// Visual feedback for movement
		if (Velocity.LengthSquared() > 0.01f)
		{
			_sprite.Scale = Godot.Vector2.One * 1.1f;

			// Rotate towards movement direction
			if (Velocity.X != 0)
			{
				_sprite.Rotation = Mathf.Lerp(_sprite.Rotation, Velocity.X > 0 ? 0.1f : -0.1f, 10f * (float)delta);
			}
		}
		else
		{
			_sprite.Scale = Godot.Vector2.One;
			_sprite.Rotation = Mathf.Lerp(_sprite.Rotation, 0, 10f * (float)delta);
		}

		// Fade back yellow flash from reconciliation
		if (_sprite.Modulate != Colors.White)
		{
			_sprite.Modulate = _sprite.Modulate.Lerp(Colors.White, 10f * (float)delta);
		}
	}

	public void UpdateHealth(float current, float max)
	{
		_currentHealth = current;
		_maxHealth = max;

		if (_healthBar != null)
		{
			_healthBar.MaxValue = max;
			_healthBar.Value = current;

			// Color based on health percentage
			var percentage = current / max;
			if (percentage > 0.6f)
				_healthBar.Modulate = Colors.Green;
			else if (percentage > 0.3f)
				_healthBar.Modulate = Colors.Yellow;
			else
				_healthBar.Modulate = Colors.Red;
		}
	}

	public void UpdateFromServerState(System.Numerics.Vector2 position, System.Numerics.Vector2 velocity, float health, float maxHealth, float serverTime)
	{
		var godotPos = new Godot.Vector2(position.X, position.Y);
		var godotVel = new Godot.Vector2(velocity.X, velocity.Y);

		SetServerPosition(godotPos, godotVel);
		BufferServerState(godotPos, godotVel, health, serverTime);
		_maxHealth = maxHealth;
	}

	public Godot.Vector2 GetPosition()
	{
		return Position;
	}

	public Godot.Vector2 GetPredictedPosition()
	{
		return _predictedPosition;
	}

	public Godot.Vector2 GetPredictedVelocity()
	{
		return _predictedVelocity;
	}

	public void SetPosition(Godot.Vector2 position)
	{
		Position = position;
		_predictedPosition = position;
	}

	// Debug info
	public string GetPredictionDebugInfo()
	{
		if (!IsLocalPlayer)
			return $"Remote | Buffer: {_stateBuffer.Count}/{MAX_BUFFER_SIZE}";

		return $"Prediction Error: {_lastPredictionError:F2} (Max: {_maxPredictionError:F2}) | " +
		       $"Reconciliations: {_reconciliationsPerformed}";
	}
}
