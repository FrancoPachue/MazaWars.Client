using Godot;
using System;
using System.Numerics;

namespace MazeWars.Client.Scripts.Game;

/// <summary>
/// Represents a player in the game world
/// </summary>
public partial class Player : CharacterBody2D
{
	[Export] public float MoveSpeed { get; set; } = 300f;
	[Export] public float SprintMultiplier { get; set; } = 1.5f;

	// Player data
	public string PlayerId { get; set; }
	public string PlayerName { get; set; }
	public string PlayerClass { get; set; }
	public bool IsLocalPlayer { get; set; }

	// Visual components
	private ColorRect _sprite;
	private Label _nameLabel;
	private ProgressBar _healthBar;

	// Server state (for remote players)
	private Godot.Vector2 _serverPosition;
	private Godot.Vector2 _serverVelocity;
	private float _interpolationSpeed = 15f;

	// Health
	private float _currentHealth = 100f;
	private float _maxHealth = 100f;

	public override void _Ready()
	{
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
			// Local player: use velocity set by input system
			// Movement is handled by InputSender and applied here
		}
		else
		{
			// Remote players: interpolate to server position
			InterpolateToServerPosition(delta);
		}
	}

	public void SetServerPosition(Godot.Vector2 position, Godot.Vector2 velocity)
	{
		_serverPosition = position;
		_serverVelocity = velocity;
	}

	private void InterpolateToServerPosition(double delta)
	{
		// Smooth interpolation to server position
		Position = Position.Lerp(_serverPosition, _interpolationSpeed * (float)delta);

		// Update sprite direction based on velocity
		if (_serverVelocity.LengthSquared() > 0.01f)
		{
			// Visual feedback: scale sprite slightly when moving
			_sprite.Scale = Godot.Vector2.One * 1.1f;
		}
		else
		{
			_sprite.Scale = Godot.Vector2.One;
		}
	}

	public void ApplyLocalMovement(Godot.Vector2 moveInput, bool isSprinting, float delta)
	{
		if (!IsLocalPlayer)
			return;

		var speed = MoveSpeed;
		if (isSprinting)
			speed *= SprintMultiplier;

		Velocity = moveInput * speed;
		MoveAndSlide();

		// Visual feedback
		if (Velocity.LengthSquared() > 0.01f)
		{
			_sprite.Scale = Godot.Vector2.One * 1.1f;

			// Rotate towards movement direction
			if (Velocity.X != 0)
			{
				_sprite.Rotation = Mathf.Lerp(_sprite.Rotation, Velocity.X > 0 ? 0.1f : -0.1f, 10f * delta);
			}
		}
		else
		{
			_sprite.Scale = Godot.Vector2.One;
			_sprite.Rotation = Mathf.Lerp(_sprite.Rotation, 0, 10f * delta);
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

	public void UpdateFromServerState(System.Numerics.Vector2 position, System.Numerics.Vector2 velocity, float health, float maxHealth)
	{
		SetServerPosition(new Godot.Vector2(position.X, position.Y), new Godot.Vector2(velocity.X, velocity.Y));
		UpdateHealth(health, maxHealth);
	}

	public Godot.Vector2 GetPosition()
	{
		return Position;
	}

	public void SetPosition(Godot.Vector2 position)
	{
		Position = position;
	}
}
