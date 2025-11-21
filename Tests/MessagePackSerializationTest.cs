using MessagePack;
using MazeWars.Client.Shared.NetworkModels;
using System;

namespace MazeWars.Client.Tests;

/// <summary>
/// Test to verify MessagePack serialization/deserialization matches server expectations
/// </summary>
public static class MessagePackSerializationTest
{
    public static void TestConnectMessage()
    {
        Console.WriteLine("=== Testing Connect Message Serialization ===");

        // Create the same message the client sends
        var connectData = new ClientConnectData
        {
            PlayerName = "TestPlayer",
            PlayerClass = "Tank",
            TeamId = "team_red",
            AuthToken = string.Empty
        };

        var message = new NetworkMessage
        {
            Type = "connect",
            PlayerId = string.Empty,
            Data = connectData,
            Timestamp = DateTime.UtcNow
        };

        // Serialize
        var bytes = MessagePackSerializer.Serialize(message);
        Console.WriteLine($"Serialized size: {bytes.Length} bytes");
        Console.WriteLine($"Hex: {BitConverter.ToString(bytes).Replace("-", " ")}");

        // Deserialize back
        var deserialized = MessagePackSerializer.Deserialize<NetworkMessage>(bytes);
        Console.WriteLine($"Type: {deserialized.Type}");
        Console.WriteLine($"PlayerId: {deserialized.PlayerId}");
        Console.WriteLine($"Data type: {deserialized.Data?.GetType().Name}");

        // Check if Data is object[]
        if (deserialized.Data is object[] dataArray)
        {
            Console.WriteLine($"✅ Data is object[] with {dataArray.Length} elements");
            for (int i = 0; i < dataArray.Length; i++)
            {
                var element = dataArray[i];
                Console.WriteLine($"  [{i}] {element?.GetType().Name ?? "null"}: {element}");
            }
        }
        else
        {
            Console.WriteLine($"❌ Data is {deserialized.Data?.GetType().FullName}, not object[]");
        }
    }

    public static void TestPlayerInputMessage()
    {
        Console.WriteLine("\n=== Testing PlayerInput Message Serialization ===");

        var inputData = new PlayerInputMessage
        {
            SequenceNumber = 1,
            AckSequenceNumber = 0,
            ClientTimestamp = 1.5f,
            MoveInput = new Vector2(1.0f, 0.0f),
            IsSprinting = false,
            AimDirection = 0.0f,
            IsAttacking = false,
            AbilityType = string.Empty,
            AbilityTarget = new Vector2()
        };

        var message = new NetworkMessage
        {
            Type = "player_input",
            PlayerId = "test-player-id",
            Data = inputData,
            Timestamp = DateTime.UtcNow
        };

        // Serialize
        var bytes = MessagePackSerializer.Serialize(message);
        Console.WriteLine($"Serialized size: {bytes.Length} bytes");

        // Deserialize back
        var deserialized = MessagePackSerializer.Deserialize<NetworkMessage>(bytes);
        Console.WriteLine($"Type: {deserialized.Type}");
        Console.WriteLine($"Data type: {deserialized.Data?.GetType().Name}");

        // Check if Data is object[]
        if (deserialized.Data is object[] dataArray)
        {
            Console.WriteLine($"✅ Data is object[] with {dataArray.Length} elements");
            for (int i = 0; i < Math.Min(dataArray.Length, 5); i++)
            {
                var element = dataArray[i];
                Console.WriteLine($"  [{i}] {element?.GetType().Name ?? "null"}: {element}");
            }
        }
        else
        {
            Console.WriteLine($"❌ Data is {deserialized.Data?.GetType().FullName}, not object[]");
        }
    }

    public static void RunAllTests()
    {
        try
        {
            TestConnectMessage();
            TestPlayerInputMessage();
            Console.WriteLine("\n=== All tests completed ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Test failed: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}
