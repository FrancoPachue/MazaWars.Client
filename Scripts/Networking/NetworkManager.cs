using Godot;
using System;

namespace MazeWars.Client.Scripts.Networking;

/// <summary>
/// DEPRECATED: NetworkManager stub for backwards compatibility
/// The client now uses UDP-only architecture via UdpNetworkClient
/// This class is kept only to avoid breaking existing references
/// </summary>
public partial class NetworkManager : Node
{
	[Export] public string ServerUrl { get; set; } = "http://localhost:5000";

	[Signal] public delegate void ConnectedEventHandler();
	[Signal] public delegate void DisconnectedEventHandler();
	[Signal] public delegate void ConnectionErrorEventHandler(string error);

	// C# Events for complex types (Variant doesn't work well with DTOs)
	public event Action<string, object>? MessageReceived;

	private UdpNetworkClient _udpClient;

	// Proxy properties from UdpNetworkClient
	public bool IsConnected => _udpClient?.IsAuthenticated ?? false;
	public string PlayerId => _udpClient?.PlayerId ?? string.Empty;

	public override void _Ready()
	{
		GD.PrintErr("[NetworkManager] DEPRECATED: This class is no longer used.");
		GD.PrintErr("[NetworkManager] All networking is handled by UdpNetworkClient.");

		// Get UdpNetworkClient reference
		try
		{
			_udpClient = GetNode<UdpNetworkClient>("/root/UdpClient");
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[NetworkManager] Failed to get UdpNetworkClient: {ex.Message}");
		}
	}

	// Stub method for backwards compatibility
	public void DisconnectAsync()
	{
		GD.Print("[NetworkManager] DisconnectAsync() called - forwarding to UdpNetworkClient");
		_udpClient?.Disconnect();
	}
}
