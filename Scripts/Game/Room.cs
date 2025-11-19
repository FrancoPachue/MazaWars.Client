using Godot;
using System;

namespace MazeWars.Client.Scripts.Game;

/// <summary>
/// Represents a room in the 4×4 grid
/// </summary>
public partial class Room : Node2D
{
	[Export] public int RoomSize { get; set; } = 800; // 50 units × 16 pixels
	[Export] public Color FloorColor { get; set; } = new Color(0.15f, 0.15f, 0.2f);
	[Export] public Color WallColor { get; set; } = new Color(0.3f, 0.3f, 0.35f);

	public string RoomId { get; set; }
	public Godot.Vector2 GridPosition { get; set; } // Grid position (0-3, 0-3)

	private ColorRect _floor;
	private Label _debugLabel;
	private Node2D _walls;

	public override void _Ready()
	{
		// Create floor
		_floor = new ColorRect
		{
			Size = new Godot.Vector2(RoomSize, RoomSize),
			Color = FloorColor
		};
		AddChild(_floor);

		// Create walls container
		_walls = new Node2D { Name = "Walls" };
		AddChild(_walls);

		// Create debug label
		_debugLabel = new Label
		{
			Position = new Godot.Vector2(10, 10),
			Text = RoomId
		};
		AddChild(_debugLabel);

		// Generate walls
		GenerateWalls();
	}

	public void Setup(string roomId, int gridX, int gridY)
	{
		RoomId = roomId;
		GridPosition = new Godot.Vector2(gridX, gridY);
		Position = new Godot.Vector2(gridX * RoomSize, gridY * RoomSize);

		if (_debugLabel != null)
			_debugLabel.Text = $"{RoomId}\n({gridX}, {gridY})";
	}

	private void GenerateWalls()
	{
		// Border walls
		int wallThickness = 8;

		// Top wall
		var topWall = new ColorRect
		{
			Size = new Godot.Vector2(RoomSize, wallThickness),
			Position = Godot.Vector2.Zero,
			Color = WallColor
		};
		_walls.AddChild(topWall);

		// Bottom wall
		var bottomWall = new ColorRect
		{
			Size = new Godot.Vector2(RoomSize, wallThickness),
			Position = new Godot.Vector2(0, RoomSize - wallThickness),
			Color = WallColor
		};
		_walls.AddChild(bottomWall);

		// Left wall
		var leftWall = new ColorRect
		{
			Size = new Godot.Vector2(wallThickness, RoomSize),
			Position = Godot.Vector2.Zero,
			Color = WallColor
		};
		_walls.AddChild(leftWall);

		// Right wall
		var rightWall = new ColorRect
		{
			Size = new Godot.Vector2(wallThickness, RoomSize),
			Position = new Godot.Vector2(RoomSize - wallThickness, 0),
			Color = WallColor
		};
		_walls.AddChild(rightWall);

		// Add some random obstacles
		var random = new Random(RoomId.GetHashCode());
		for (int i = 0; i < 3; i++)
		{
			var obstacle = new ColorRect
			{
				Size = new Godot.Vector2(random.Next(40, 100), random.Next(40, 100)),
				Position = new Godot.Vector2(
					random.Next(50, RoomSize - 100),
					random.Next(50, RoomSize - 100)
				),
				Color = WallColor
			};
			_walls.AddChild(obstacle);
		}
	}

	public Godot.Vector2 GetRandomSpawnPosition()
	{
		var random = new Random();
		return GlobalPosition + new Godot.Vector2(
			random.Next(100, RoomSize - 100),
			random.Next(100, RoomSize - 100)
		);
	}
}
