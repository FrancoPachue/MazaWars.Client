using Godot;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using MazeWars.Client.Shared.NetworkModels;

namespace MazeWars.Client.Scripts.Networking;

/// <summary>
/// Manages UDP connection for low-latency player input and world updates
/// </summary>
public partial class UdpNetworkClient : Node
{
	[Export] public string ServerAddress { get; set; } = "127.0.0.1";
	[Export] public int ServerPort { get; set; } = 7001; // Must match server's UdpPort in appsettings.json

	[Signal] public delegate void UdpMessageReceivedEventHandler(byte[] data);
	[Signal] public delegate void ConnectionErrorEventHandler(string error);

	// C# Events for complex types (Godot signals don't support custom classes)
	public event Action<ConnectResponseData>? ConnectionResponse;
	public event Action<WorldUpdateMessage>? WorldUpdateReceived;
	public event Action<ChatReceivedData>? ChatMessageReceived;
	public event Action<CombatEvent>? CombatEventReceived;

	private UdpClient _udpClient;
	private IPEndPoint _serverEndpoint;
	private CancellationTokenSource _cancellationToken;
	private bool _isRunning;
	private Task _receiveTask;

	// Thread-safe queues for messages from background thread
	private ConcurrentQueue<ConnectResponseData> _connectionQueue = new ConcurrentQueue<ConnectResponseData>();
	private ConcurrentQueue<WorldUpdateMessage> _updateQueue = new ConcurrentQueue<WorldUpdateMessage>();
	private ConcurrentQueue<ChatReceivedData> _chatQueue = new ConcurrentQueue<ChatReceivedData>();
	private ConcurrentQueue<CombatEvent> _combatQueue = new ConcurrentQueue<CombatEvent>();

	// Connection state
	private bool _isAuthenticated = false;
	public string PlayerId { get; private set; } = string.Empty;
	public string SessionToken { get; private set; } = string.Empty;

	// Statistics
	private int _packetsSent = 0;
	private int _packetsReceived = 0;
	private DateTime _lastPacketTime;

	public bool IsConnected => _isRunning && _udpClient != null;
	public bool IsAuthenticated => _isAuthenticated;
	public int PacketsSent => _packetsSent;
	public int PacketsReceived => _packetsReceived;

	public override void _Ready()
	{
		GD.Print($"[UdpClient] Initialized for {ServerAddress}:{ServerPort}");
		_serverEndpoint = new IPEndPoint(IPAddress.Parse(ServerAddress), ServerPort);
	}

	/// <summary>
	/// Connects to the server and sends authentication data
	/// </summary>
	public void ConnectToServer(string playerName, string playerClass, string teamId = "team_red")
	{
		if (_isRunning)
		{
			GD.Print("[UdpClient] Already connected");
			return;
		}

		try
		{
			// Open UDP connection
			_udpClient = new UdpClient();
			_udpClient.Client.ReceiveTimeout = 5000; // 5 second timeout
			_udpClient.Connect(_serverEndpoint);

			_cancellationToken = new CancellationTokenSource();
			_isRunning = true;

			// Start listening thread
			_receiveTask = Task.Run(() => ReceiveLoop(_cancellationToken.Token));

			GD.Print($"[UdpClient] Connected to {ServerAddress}:{ServerPort}");

			// Send connection request - MessagePack handles object serialization automatically
			var connectData = new ClientConnectData
			{
				PlayerName = playerName,
				PlayerClass = playerClass,
				TeamId = teamId,
				AuthToken = string.Empty // No auth for now
			};

			// Wrap in NetworkMessage - pass object directly, MessagePack serializes recursively
			var message = new NetworkMessage
			{
				Type = "connect",
				Data = connectData,  // Pass object directly
				Timestamp = DateTime.UtcNow
			};

			// Serialize the entire NetworkMessage
			var bytes = MessagePackSerializer.Serialize(message);
			_udpClient.Send(bytes, bytes.Length);
			_packetsSent++;

			GD.Print($"[UdpClient] Sent connection request for player '{playerName}'");
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[UdpClient] Failed to connect: {ex.Message}");
			CallDeferred(MethodName.EmitSignal, SignalName.ConnectionError, ex.Message);
		}
	}

	public void Disconnect()
	{
		if (!_isRunning)
			return;

		GD.Print("[UdpClient] Disconnecting...");
		_isRunning = false;
		_cancellationToken?.Cancel();

		try
		{
			_udpClient?.Close();
			_udpClient?.Dispose();
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[UdpClient] Error during disconnect: {ex.Message}");
		}

		try
		{
			_receiveTask?.Wait(TimeSpan.FromSeconds(2));
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[UdpClient] Error waiting for receive task: {ex.Message}");
		}

		GD.Print("[UdpClient] Disconnected");
	}

	public void SendMessage<T>(T message)
	{
		if (!_isRunning)
		{
			GD.PrintErr("[UdpClient] Cannot send: not connected");
			return;
		}

		try
		{
			var data = MessagePackSerializer.Serialize(message);
			_udpClient.Send(data, data.Length);
			_packetsSent++;
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[UdpClient] Failed to send message: {ex.Message}");
		}
	}

	public void SendPlayerInput(PlayerInputMessage input)
	{
		if (string.IsNullOrEmpty(PlayerId))
		{
			GD.PrintErr("[UdpClient] Cannot send input: PlayerId not set");
			return;
		}

		// Wrap PlayerInputMessage in NetworkMessage (same as connect)
		var message = new NetworkMessage
		{
			Type = "player_input",
			PlayerId = PlayerId,
			Data = input,
			Timestamp = DateTime.UtcNow
		};

		SendMessage(message);
	}

	private async Task ReceiveLoop(CancellationToken token)
	{
		GD.Print("[UdpClient] Receive loop started");

		while (_isRunning && !token.IsCancellationRequested)
		{
			try
			{
				var result = await _udpClient.ReceiveAsync(token);
				_packetsReceived++;
				_lastPacketTime = DateTime.UtcNow;

				// Process on main thread
				CallDeferred(MethodName.ProcessReceivedData, result.Buffer);
			}
			catch (OperationCanceledException)
			{
				// Normal cancellation
				break;
			}
			catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
			{
				// Timeout is normal, continue
				continue;
			}
			catch (Exception ex)
			{
				if (_isRunning)
				{
					GD.PrintErr($"[UdpClient] Receive error: {ex.Message}");
					CallDeferred(MethodName.EmitSignal, SignalName.ConnectionError, ex.Message);
				}
			}
		}

		GD.Print("[UdpClient] Receive loop ended");
	}

	private void ProcessReceivedData(byte[] data)
	{
		try
		{
			// Emit raw data signal
			EmitSignal(SignalName.UdpMessageReceived, data);

			// Strategy 1: Try deserializing directly as specific message types (no wrapper)
			if (!_isAuthenticated)
			{
				try
				{
					var response = MessagePackSerializer.Deserialize<ConnectResponseData>(data);
					if (response != null && !string.IsNullOrEmpty(response.PlayerId))
					{
						GD.Print($"[UdpClient] Received ConnectResponse (direct): PlayerId={response.PlayerId}");
						_isAuthenticated = true;
						PlayerId = response.PlayerId;
						SessionToken = response.SessionToken;
						GD.Print($"[UdpClient] Authenticated as {PlayerId}");
						_connectionQueue.Enqueue(response);
						return;
					}
				}
				catch { }
			}

			// Try WorldUpdateMessage (most common) - direct deserialization
			try
			{
				var update = MessagePackSerializer.Deserialize<WorldUpdateMessage>(data);
				if (update != null && update.Players != null && update.Players.Count > 0)
				{
					_updateQueue.Enqueue(update);
					return;
				}
			}
			catch { }

			// Strategy 2: Manually extract Data field from NetworkMessage
			// This avoids the object deserialization issue
			try
			{
				var sequence = new ReadOnlySequence<byte>(data);
				var reader = new MessagePackReader(sequence);

				// Read array header (NetworkMessage has 4 fields)
				var arrayLength = reader.ReadArrayHeader();
				if (arrayLength < 3)
				{
					return;
				}

				// Read Type (Key 0)
				var messageType = reader.ReadString();

				// Read PlayerId (Key 1)
				var playerId = reader.ReadString();

				// Read Data (Key 2) - extract raw bytes without deserializing
				var dataStartPos = reader.Consumed;
				reader.Skip(); // Skip over the Data field to find its end
				var dataEndPos = reader.Consumed;

				// Extract the Data field as raw bytes
				var dataLength = (int)(dataEndPos - dataStartPos);
				var dataBytes = new byte[dataLength];
				Array.Copy(data, (int)dataStartPos, dataBytes, 0, dataLength);

				GD.Print($"[UdpClient] Received wrapped message type: {messageType}");

				switch (messageType.ToLowerInvariant())
				{
					case "connected":
						// Server now sends complete ConnectResponseData typed model
						try
						{
							var response = MessagePackSerializer.Deserialize<ConnectResponseData>(dataBytes);
							if (response != null && !string.IsNullOrEmpty(response.PlayerId))
							{
								_isAuthenticated = true;
								PlayerId = response.PlayerId;
								SessionToken = response.SessionToken;
								GD.Print($"[UdpClient] Connected as {PlayerId} (World: {response.WorldId}, Lobby: {response.IsLobby})");
								_connectionQueue.Enqueue(response);
								return;
							}
						}
						catch (Exception ex)
						{
							GD.PrintErr($"[UdpClient] Failed to deserialize connected message: {ex.Message}");
						}
						break;

					case "world_update":
					case "worldupdate":
						try
						{
							var update = MessagePackSerializer.Deserialize<WorldUpdateMessage>(dataBytes);
							if (update != null && update.Players != null)
							{
								_updateQueue.Enqueue(update);
							}
							return;
						}
						catch (Exception ex)
						{
							GD.PrintErr($"[UdpClient] Failed to deserialize WorldUpdate: {ex.Message}");
						}
						break;

					case "player_states_batch":
						try
						{
							// Server now sends complete PlayerStatesBatchData typed model
							var batch = MessagePackSerializer.Deserialize<PlayerStatesBatchData>(dataBytes);
							if (batch != null && batch.Players != null && batch.Players.Count > 0)
							{
								// DEBUG: Check if acknowledgments are being received
								var ackCount = batch.AcknowledgedInputs?.Count ?? 0;
								if (ackCount > 0)
								{
									var ackStr = string.Join(", ", batch.AcknowledgedInputs.Select(kvp => $"{kvp.Key.Substring(0, 8)}={kvp.Value}"));
									GD.Print($"[UdpClient] Received player_states_batch: {batch.Players.Count} players (batch {batch.BatchIndex + 1}/{batch.TotalBatches}) - ACKS: {ackStr}");
								}
								else
								{
									GD.Print($"[UdpClient] Received player_states_batch: {batch.Players.Count} players (batch {batch.BatchIndex + 1}/{batch.TotalBatches}) - NO ACKS!");
								}

								// Convert PlayerUpdateData to PlayerStateUpdate for WorldUpdateMessage
								var players = batch.Players.Select(p => new PlayerStateUpdate
								{
									PlayerId = p.PlayerId,
									Position = p.Position,
									Velocity = p.Velocity,
									Direction = p.Direction,
									Health = p.Health,
									MaxHealth = p.MaxHealth,
									IsAlive = p.IsAlive,
									IsMoving = p.IsMoving,
									IsCasting = p.IsCasting,
									PlayerName = string.Empty,
									PlayerClass = string.Empty
								}).ToList();

								var update = new WorldUpdateMessage
								{
									Players = players,
									ServerTime = 0,
									FrameNumber = 0,
									AcknowledgedInputs = batch.AcknowledgedInputs ?? new(),
									CombatEvents = new(),
									LootUpdates = new(),
									MobUpdates = new()
								};
								_updateQueue.Enqueue(update);
								return;
							}
						}
						catch (Exception ex)
						{
							GD.PrintErr($"[UdpClient] Failed to deserialize player_states_batch: {ex.Message}");
						}
						break;

					case "chat":
					case "chat_message":
						try
						{
							var chat = MessagePackSerializer.Deserialize<ChatReceivedData>(dataBytes);
							if (chat != null && !string.IsNullOrEmpty(chat.Message))
							{
								_chatQueue.Enqueue(chat);
							}
							return;
						}
						catch (Exception ex)
						{
							GD.PrintErr($"[UdpClient] Failed to deserialize Chat: {ex.Message}");
						}
						break;

					case "combat":
					case "combat_event":
						try
						{
							var combat = MessagePackSerializer.Deserialize<CombatEvent>(dataBytes);
							if (combat != null)
							{
								_combatQueue.Enqueue(combat);
							}
							return;
						}
						catch (Exception ex)
						{
							GD.PrintErr($"[UdpClient] Failed to deserialize CombatEvent: {ex.Message}");
						}
						break;

					case "lobby_update":
						try
						{
							var lobbyUpdate = MessagePackSerializer.Deserialize<LobbyUpdateData>(dataBytes);
							if (lobbyUpdate != null)
							{
								GD.Print($"[UdpClient] Lobby update: {lobbyUpdate.CurrentPlayers}/{lobbyUpdate.MaxPlayers} players, Status: {lobbyUpdate.Status}");
								// TODO: Emit signal or event for lobby UI to update
							}
							return;
						}
						catch (Exception ex)
						{
							GD.PrintErr($"[UdpClient] Failed to deserialize lobby_update: {ex.Message}");
						}
						break;

					case "error":
						try
						{
							var errorData = MessagePackSerializer.Deserialize<ErrorData>(dataBytes);
							if (errorData != null && !string.IsNullOrEmpty(errorData.Message))
							{
								GD.PrintErr($"[UdpClient] Server error: {errorData.Message}");
								// TODO: Emit signal for UI to show error notification
							}
							return;
						}
						catch (Exception ex)
						{
							GD.PrintErr($"[UdpClient] Failed to deserialize error message: {ex.Message}");
						}
						break;

					default:
						GD.PrintErr($"[UdpClient] Unknown wrapped message type: {messageType} ({data.Length} bytes)");
						return;
				}
			}
			catch (Exception ex)
			{
				GD.Print($"[UdpClient] Not a wrapped message: {ex.Message}");
			}

			// If we get here, we couldn't deserialize the message
			GD.PrintErr($"[UdpClient] Unknown message format received ({data.Length} bytes)");
			var preview = string.Join(" ", data.Take(Math.Min(20, data.Length)).Select(b => b.ToString("X2")));
			GD.Print($"[UdpClient] Message preview (hex): {preview}...");
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[UdpClient] Failed to process received data: {ex.Message}");
		}
	}

	public override void _Process(double delta)
	{
		// Process all queued messages on main thread (Godot thread-safe)

		// Connection responses
		while (_connectionQueue.TryDequeue(out var response))
		{
			ConnectionResponse?.Invoke(response);
		}

		// World updates (most frequent)
		while (_updateQueue.TryDequeue(out var update))
		{
			WorldUpdateReceived?.Invoke(update);
		}

		// Chat messages
		while (_chatQueue.TryDequeue(out var chat))
		{
			ChatMessageReceived?.Invoke(chat);
		}

		// Combat events
		while (_combatQueue.TryDequeue(out var combat))
		{
			CombatEventReceived?.Invoke(combat);
		}

		// Check for connection timeout (no packets for 10 seconds)
		if (_isRunning && _packetsReceived > 0)
		{
			var timeSinceLastPacket = DateTime.UtcNow - _lastPacketTime;
			if (timeSinceLastPacket.TotalSeconds > 10)
			{
				GD.PrintErr("[UdpClient] Connection timeout - no packets received for 10 seconds");
				Disconnect();
			}
		}
	}

	public override void _ExitTree()
	{
		Disconnect();
	}

	// Debug info
	public string GetDebugInfo()
	{
		if (!_isRunning)
			return "UDP: Disconnected";

		var timeSinceLast = _packetsReceived > 0
			? (DateTime.UtcNow - _lastPacketTime).TotalSeconds
			: 0;

		return $"UDP: ↑{_packetsSent} ↓{_packetsReceived} | Last: {timeSinceLast:F1}s ago";
	}
}
