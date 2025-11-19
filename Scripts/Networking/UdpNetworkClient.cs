using Godot;
using System;
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
	[Export] public int ServerPort { get; set; } = 5001;

	[Signal] public delegate void UdpMessageReceivedEventHandler(byte[] data);
	[Signal] public delegate void WorldUpdateReceivedEventHandler(WorldUpdateMessage update);
	[Signal] public delegate void ConnectionErrorEventHandler(string error);

	private UdpClient _udpClient;
	private IPEndPoint _serverEndpoint;
	private CancellationTokenSource _cancellationToken;
	private bool _isRunning;
	private Task _receiveTask;

	// Statistics
	private int _packetsSent = 0;
	private int _packetsReceived = 0;
	private DateTime _lastPacketTime;

	public bool IsConnected => _isRunning && _udpClient != null;
	public int PacketsSent => _packetsSent;
	public int PacketsReceived => _packetsReceived;

	public override void _Ready()
	{
		GD.Print($"[UdpClient] Initialized for {ServerAddress}:{ServerPort}");
		_serverEndpoint = new IPEndPoint(IPAddress.Parse(ServerAddress), ServerPort);
	}

	public void Connect()
	{
		if (_isRunning)
		{
			GD.Print("[UdpClient] Already connected");
			return;
		}

		try
		{
			_udpClient = new UdpClient();
			_udpClient.Client.ReceiveTimeout = 5000; // 5 second timeout
			_udpClient.Connect(_serverEndpoint);

			_cancellationToken = new CancellationTokenSource();
			_isRunning = true;

			// Start listening thread
			_receiveTask = Task.Run(() => ReceiveLoop(_cancellationToken.Token));

			GD.Print($"[UdpClient] Connected to {ServerAddress}:{ServerPort}");
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
		SendMessage(new NetworkMessage
		{
			Type = "PlayerInput",
			PlayerId = input.PlayerId,
			Data = input,
			Timestamp = DateTime.UtcNow
		});
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

			// Try to deserialize as WorldUpdateMessage
			var message = MessagePackSerializer.Deserialize<NetworkMessage>(data);

			if (message.Type == "WorldUpdate")
			{
				var update = MessagePackSerializer.Deserialize<WorldUpdateMessage>(
					MessagePackSerializer.Serialize(message.Data)
				);
				EmitSignal(SignalName.WorldUpdateReceived, update);
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[UdpClient] Failed to process received data: {ex.Message}");
		}
	}

	public override void _Process(double delta)
	{
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
