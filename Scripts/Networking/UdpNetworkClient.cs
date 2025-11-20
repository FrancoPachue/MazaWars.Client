using Godot;
using System;
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

			// Send connection request using MessagePack (consistent with all other messages)
			var connectData = new ClientConnectData
			{
				PlayerName = playerName,
				PlayerClass = playerClass,
				TeamId = teamId,
				AuthToken = string.Empty // No auth for now
			};

			// Wrap in NetworkMessage
			var message = new NetworkMessage
			{
				Type = "connect",
				Data = connectData,
				Timestamp = DateTime.UtcNow
			};

			// Serialize as MessagePack (consistent serialization format)
			var bytes = MessagePackSerializer.Serialize(message);
			_udpClient.Send(bytes, bytes.Length);
			_packetsSent++;

			GD.Print($"[UdpClient] Sent connection request for player '{playerName}' ({playerClass}) using MessagePack");
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
		// Send input directly without wrapping in NetworkMessage
		// The server will identify the player from the UDP endpoint
		SendMessage(input);
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
			// This handles the case where server sends raw MessagePack objects

			if (!_isAuthenticated)
			{
				// During connection phase, try ConnectResponseData directly first
				try
				{
					var response = MessagePackSerializer.Deserialize<ConnectResponseData>(data);
					if (response != null && (!string.IsNullOrEmpty(response.PlayerId) || !string.IsNullOrEmpty(response.ErrorMessage)))
					{
						GD.Print($"[UdpClient] Received ConnectResponse (direct): Success={response.Success}");
						if (response.Success)
						{
							_isAuthenticated = true;
							PlayerId = response.PlayerId;
							SessionToken = response.SessionToken;
							GD.Print($"[UdpClient] Authenticated as {PlayerId}");
						}
						_connectionQueue.Enqueue(response);
						return;
					}
				}
				catch
				{
					// Not a direct ConnectResponseData, continue
				}
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
			catch
			{
				// Not a direct WorldUpdateMessage
			}

			// Strategy 2: Try deserializing as NetworkMessage wrapper
			// This handles the case where server wraps messages in NetworkMessage
			try
			{
				var networkMessage = MessagePackSerializer.Deserialize<NetworkMessage>(data);
				if (networkMessage != null && !string.IsNullOrEmpty(networkMessage.Type))
				{
					GD.Print($"[UdpClient] Received wrapped message type: {networkMessage.Type}");

					// Try to extract and deserialize the Data field based on Type
					// Note: The Data field is already deserialized by MessagePack into some type
					// We need to re-serialize it and then deserialize to the correct type
					if (networkMessage.Data != null)
					{
						switch (networkMessage.Type.ToLowerInvariant())
						{
							case "connect_response":
							case "connectresponse":
								try
								{
									var responseBytes = MessagePackSerializer.Serialize(networkMessage.Data);
									var response = MessagePackSerializer.Deserialize<ConnectResponseData>(responseBytes);

									if (response != null)
									{
										if (response.Success)
										{
											_isAuthenticated = true;
											PlayerId = response.PlayerId;
											SessionToken = response.SessionToken;
											GD.Print($"[UdpClient] Authenticated as {PlayerId}");
										}
										_connectionQueue.Enqueue(response);
									}
									return;
								}
								catch (Exception ex)
								{
									GD.PrintErr($"[UdpClient] Failed to extract ConnectResponse from wrapper: {ex.Message}");
								}
								break;

							case "world_update":
							case "worldupdate":
								try
								{
									var updateBytes = MessagePackSerializer.Serialize(networkMessage.Data);
									var update = MessagePackSerializer.Deserialize<WorldUpdateMessage>(updateBytes);

									if (update != null && update.Players != null)
									{
										_updateQueue.Enqueue(update);
									}
									return;
								}
								catch (Exception ex)
								{
									GD.PrintErr($"[UdpClient] Failed to extract WorldUpdate from wrapper: {ex.Message}");
								}
								break;

							case "player_states_batch":
								try
								{
									var batchBytes = MessagePackSerializer.Serialize(networkMessage.Data);

									// Try deserializing as PlayerStatesBatch first
									try
									{
										var batch = MessagePackSerializer.Deserialize<PlayerStatesBatch>(batchBytes);
										if (batch != null && batch.Players != null)
										{
											// Convert PlayerStatesBatch to WorldUpdateMessage for compatibility
											var update = new WorldUpdateMessage
											{
												Players = batch.Players,
												ServerTime = batch.ServerTime,
												FrameNumber = batch.FrameNumber,
												AcknowledgedInputs = new(),
												CombatEvents = new(),
												LootUpdates = new(),
												MobUpdates = new()
											};
											_updateQueue.Enqueue(update);
											return;
										}
									}
									catch
									{
										// Try as a simple list of PlayerStateUpdate
										var playersList = MessagePackSerializer.Deserialize<List<PlayerStateUpdate>>(batchBytes);
										if (playersList != null && playersList.Count > 0)
										{
											// Convert to WorldUpdateMessage for compatibility
											var update = new WorldUpdateMessage
											{
												Players = playersList,
												ServerTime = 0,
												FrameNumber = 0,
												AcknowledgedInputs = new(),
												CombatEvents = new(),
												LootUpdates = new(),
												MobUpdates = new()
											};
											_updateQueue.Enqueue(update);
											return;
										}
									}

									GD.PrintErr($"[UdpClient] Failed to deserialize player_states_batch data");
									return;
								}
								catch (Exception ex)
								{
									GD.PrintErr($"[UdpClient] Failed to extract PlayerStatesBatch from wrapper: {ex.Message}");
								}
								break;

							case "chat":
							case "chat_message":
								try
								{
									var chatBytes = MessagePackSerializer.Serialize(networkMessage.Data);
									var chat = MessagePackSerializer.Deserialize<ChatReceivedData>(chatBytes);

									if (chat != null && !string.IsNullOrEmpty(chat.Message))
									{
										_chatQueue.Enqueue(chat);
									}
									return;
								}
								catch (Exception ex)
								{
									GD.PrintErr($"[UdpClient] Failed to extract Chat from wrapper: {ex.Message}");
								}
								break;

							case "combat":
							case "combat_event":
								try
								{
									var combatBytes = MessagePackSerializer.Serialize(networkMessage.Data);
									var combat = MessagePackSerializer.Deserialize<CombatEvent>(combatBytes);

									if (combat != null)
									{
										_combatQueue.Enqueue(combat);
									}
									return;
								}
								catch (Exception ex)
								{
									GD.PrintErr($"[UdpClient] Failed to extract CombatEvent from wrapper: {ex.Message}");
								}
								break;

							default:
								GD.PrintErr($"[UdpClient] Unknown wrapped message type: {networkMessage.Type} ({data.Length} bytes)");
								return;
						}
					}
				}
			}
			catch
			{
				// Not a NetworkMessage wrapper either
			}

			// If we get here, we couldn't deserialize the message with any strategy
			GD.PrintErr($"[UdpClient] Unknown message format received ({data.Length} bytes)");

			// Debug: Print first few bytes to help diagnose the issue
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
