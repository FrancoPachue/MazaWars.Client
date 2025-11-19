using Godot;
using System;
using System.Collections.Generic;
using MazeWars.Client.Shared.NetworkModels;

namespace MazeWars.Client.Scripts.Networking;

/// <summary>
/// Handles player input collection and sending to server with sequence numbers
/// </summary>
public partial class InputSender : Node
{
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

	private struct PredictedInput
	{
		public uint SequenceNumber;
		public PlayerInputMessage Input;
		public Godot.Vector2 PredictedPosition;
		public float Timestamp;
	}

	public bool IsEnabled { get; set; } = false;

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

		// Read current input
		var moveInput = ReadMovementInput();
		var isSprinting = Input.IsActionPressed("sprint");
		var aimDirection = CalculateAimDirection();

		// Create input message with sequence numbers
		var inputMessage = CreateInputMessage(moveInput, isSprinting, aimDirection);

		// Send to server via UDP
		_udpClient.SendPlayerInput(inputMessage);

		// Buffer for prediction
		BufferInput(inputMessage, Godot.Vector2.Zero); // Position will be set by prediction system

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

	private void BufferInput(PlayerInputMessage input, Godot.Vector2 predictedPosition)
	{
		_inputBuffer.Enqueue(new PredictedInput
		{
			SequenceNumber = input.SequenceNumber,
			Input = input,
			PredictedPosition = predictedPosition,
			Timestamp = input.ClientTimestamp
		});

		// Limit buffer size to prevent memory leak
		while (_inputBuffer.Count > MAX_BUFFER_SIZE)
		{
			_inputBuffer.Dequeue();
		}
	}

	public void OnServerUpdate(uint acknowledgedSequence)
	{
		_lastServerUpdate = acknowledgedSequence;

		// Remove acknowledged inputs from buffer
		while (_inputBuffer.Count > 0 && _inputBuffer.Peek().SequenceNumber <= acknowledgedSequence)
		{
			_inputBuffer.Dequeue();
		}
	}

	public Queue<PredictedInput> GetPendingInputs()
	{
		return new Queue<PredictedInput>(_inputBuffer);
	}

	public void ClearInputBuffer()
	{
		_inputBuffer.Clear();
	}

	// Debug info
	public string GetDebugInfo()
	{
		return $"Input: Seq={_currentSequenceNumber} Ack={_lastServerUpdate} Buffered={_inputBuffer.Count}";
	}
}
