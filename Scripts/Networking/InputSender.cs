using Godot;
using System;
using System.Collections.Generic;
using MazeWars.Client.Shared.NetworkModels;

namespace MazeWars.Client.Scripts.Networking;

/// <summary>
/// Handles player input collection and sending to server with sequence numbers
/// Supports client-side prediction by tracking pending inputs
/// </summary>
public partial class InputSender : Node
{
	/// <summary>
	/// Represents a predicted input with its associated data
	/// </summary>
	public struct PredictedInput
	{
		public uint SequenceNumber;
		public PlayerInputMessage Input;
		public Godot.Vector2 PredictedPosition;
		public Godot.Vector2 PredictedVelocity;
		public float Timestamp;
		public float DeltaTime;
	}

	private UdpNetworkClient _udpClient;
	private string _playerId;

	// Sequence tracking (critical for client prediction)
	private uint _currentSequenceNumber = 0;
	private uint _lastServerUpdate = 0;

	// Input buffering for client-side prediction
	private Queue<PredictedInput> _inputBuffer = new Queue<PredictedInput>();
	private const int MAX_BUFFER_SIZE = 100; // Prevent memory leak

	// Input state
	private Godot.Vector2 _lastMoveInput = Godot.Vector2.Zero;
	private bool _lastSprinting = false;
	private float _lastAimDirection = 0f;

	// Timing
	private float _clientStartTime;
	private float _lastInputTime;

	// Statistics
	private int _inputsSent = 0;
	private int _inputsAcknowledged = 0;

	public bool IsEnabled { get; set; } = false;
	public uint CurrentSequence => _currentSequenceNumber;
	public uint LastAcknowledged => _lastServerUpdate;
	public int PendingInputCount => _inputBuffer.Count;

	public override void _Ready()
	{
		_clientStartTime = (float)Time.GetTicksMsec() / 1000f;
		GD.Print("[InputSender] Initialized");
	}

	public void Initialize(UdpNetworkClient udpClient, string playerId)
	{
		_udpClient = udpClient;
		_playerId = playerId;
		IsEnabled = true;
		GD.Print($"[InputSender] Configured for player {playerId}");
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!IsEnabled || string.IsNullOrEmpty(_playerId) || _udpClient == null || !_udpClient.IsConnected)
			return;

		_lastInputTime = (float)delta;

		// Read current input
		var moveInput = ReadMovementInput();
		var isSprinting = Input.IsActionPressed("sprint");
		var aimDirection = CalculateAimDirection();

		// Create input message with sequence numbers
		var inputMessage = CreateInputMessage(moveInput, isSprinting, aimDirection);

		// Send to server via UDP
		_udpClient.SendPlayerInput(inputMessage);
		_inputsSent++;

		// Store for prediction (position will be set by GameStateManager)
		// This is just a placeholder - actual prediction happens in Player/GameStateManager
		BufferInputForPrediction(inputMessage, (float)delta);

		// Update last input state
		_lastMoveInput = moveInput;
		_lastSprinting = isSprinting;
		_lastAimDirection = aimDirection;
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

		// Normalize diagonal movement
		if (input.Length() > 0)
			input = input.Normalized();

		return input;
	}

	private float CalculateAimDirection()
	{
		// Get mouse position and calculate angle to center of screen
		var viewport = GetViewport();
		if (viewport == null)
			return _lastAimDirection;

		var mousePos = viewport.GetMousePosition();
		var screenCenter = viewport.GetVisibleRect().Size / 2;
		var direction = mousePos - screenCenter;

		if (direction.Length() < 0.1f)
			return _lastAimDirection;

		return Mathf.RadToDeg(Mathf.Atan2(direction.Y, direction.X));
	}

	private PlayerInputMessage CreateInputMessage(Godot.Vector2 moveInput, bool isSprinting, float aimDirection)
	{
		// Increment sequence number
		_currentSequenceNumber++;

		// Get client time (seconds since client start)
		float clientTime = (float)Time.GetTicksMsec() / 1000f - _clientStartTime;

		// Convert Godot Vector2 to System.Numerics.Vector2 (server uses this)
		var systemMoveInput = new System.Numerics.Vector2(moveInput.X, moveInput.Y);

		return new PlayerInputMessage
		{
			// ⭐ CRITICAL: Synchronization fields (per CLIENT_IMPLEMENTATION_GUIDE.md)
			SequenceNumber = _currentSequenceNumber,
			AckSequenceNumber = _lastServerUpdate,
			ClientTimestamp = clientTime,

			// Player ID
			PlayerId = _playerId,

			// Input data
			MoveInput = systemMoveInput,
			IsSprinting = isSprinting,
			AimDirection = aimDirection,
			IsAttacking = Input.IsActionPressed("attack"),

			// Abilities (TODO: implement ability targeting)
			AbilityType = GetPressedAbility(),
			AbilityTarget = System.Numerics.Vector2.Zero
		};
	}

	private string GetPressedAbility()
	{
		if (Input.IsActionJustPressed("ability_1"))
			return "Ability1";
		if (Input.IsActionJustPressed("ability_2"))
			return "Ability2";
		if (Input.IsActionJustPressed("ability_3"))
			return "Ability3";

		return string.Empty;
	}

	private void BufferInputForPrediction(PlayerInputMessage input, float deltaTime)
	{
		var predictedInput = new PredictedInput
		{
			SequenceNumber = input.SequenceNumber,
			Input = input,
			PredictedPosition = Godot.Vector2.Zero, // Will be set after prediction is applied
			PredictedVelocity = Godot.Vector2.Zero,
			Timestamp = input.ClientTimestamp,
			DeltaTime = deltaTime
		};

		_inputBuffer.Enqueue(predictedInput);

		// Limit buffer size to prevent memory leak
		while (_inputBuffer.Count > MAX_BUFFER_SIZE)
		{
			_inputBuffer.Dequeue();
			GD.PrintErr("[InputSender] Input buffer overflow! Oldest input discarded.");
		}
	}

	/// <summary>
	/// Updates the predicted position for the most recent input
	/// Called after prediction is applied in GameStateManager
	/// </summary>
	public void UpdateLastPredictedPosition(Godot.Vector2 position, Godot.Vector2 velocity)
	{
		if (_inputBuffer.Count == 0)
			return;

		// Get the last input and update it
		var inputs = _inputBuffer.ToArray();
		var lastInput = inputs[inputs.Length - 1];
		lastInput.PredictedPosition = position;
		lastInput.PredictedVelocity = velocity;

		// Rebuild queue with updated input
		_inputBuffer.Clear();
		foreach (var input in inputs)
		{
			if (input.SequenceNumber == lastInput.SequenceNumber)
				_inputBuffer.Enqueue(lastInput);
			else
				_inputBuffer.Enqueue(input);
		}
	}

	/// <summary>
	/// Called when server update is received
	/// Removes acknowledged inputs from buffer
	/// </summary>
	public void OnServerUpdate(uint acknowledgedSequence)
	{
		var previousAck = _lastServerUpdate;
		_lastServerUpdate = acknowledgedSequence;

		// Remove acknowledged inputs from buffer
		int removed = 0;
		while (_inputBuffer.Count > 0 && _inputBuffer.Peek().SequenceNumber <= acknowledgedSequence)
		{
			_inputBuffer.Dequeue();
			removed++;
			_inputsAcknowledged++;
		}

		if (removed > 0)
		{
			GD.Print($"[InputSender] Acknowledged sequence {acknowledgedSequence}, removed {removed} inputs. Pending: {_inputBuffer.Count}");
		}
	}

	/// <summary>
	/// Gets all pending (unacknowledged) inputs
	/// Used for reconciliation and input replay
	/// </summary>
	public List<PredictedInput> GetPendingInputs()
	{
		return new List<PredictedInput>(_inputBuffer);
	}

	/// <summary>
	/// Clears the entire input buffer
	/// Use when player is teleported or state is reset
	/// </summary>
	public void ClearInputBuffer()
	{
		var count = _inputBuffer.Count;
		_inputBuffer.Clear();
		if (count > 0)
			GD.Print($"[InputSender] Cleared {count} pending inputs");
	}

	/// <summary>
	/// Gets the packet loss rate
	/// </summary>
	public float GetPacketLossRate()
	{
		if (_inputsSent == 0)
			return 0f;

		var lost = _inputsSent - _inputsAcknowledged;
		return (float)lost / _inputsSent;
	}

	// Debug info
	public string GetDebugInfo()
	{
		var lossRate = GetPacketLossRate();
		return $"Input: Seq={_currentSequenceNumber} Ack={_lastServerUpdate} Pending={_inputBuffer.Count} Loss={lossRate:P1}";
	}

	public string GetDetailedDebugInfo()
	{
		return $"Input Sender:\n" +
		       $"  Sequence: {_currentSequenceNumber}\n" +
		       $"  Last Ack: {_lastServerUpdate}\n" +
		       $"  Pending: {_inputBuffer.Count}/{MAX_BUFFER_SIZE}\n" +
		       $"  Sent: {_inputsSent}\n" +
		       $"  Acknowledged: {_inputsAcknowledged}\n" +
		       $"  Loss Rate: {GetPacketLossRate():P2}";
	}
}
