using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR.Client;

public static class PrivateGameRoomTask
{
    // Private Game Room
    // This method runs the specified number of private rooms (stub for now)
    public static async Task RunPrivateGameRoomTaskAsync(string? value, string? maxPlayers = "4")
    {
        int.TryParse(value, out int roomCount);
        int.TryParse(maxPlayers, out int maxPlayerCount);
        var rooms = new ConcurrentDictionary<string, GameRoom>();
        List<Task> connectionTasks = new();

        // Step 1: Create private rooms and store passwords
        for (int i = 0; i < roomCount; i++)
        {
            var connection = ConnectionManager.CreateHubConnection();

            string createdRoomId = string.Empty;
            string createdPassword = string.Empty;

            connection.On<GameRoom>("RoomCreated", (room) =>
            {
                rooms.AddOrUpdate(room.RoomId, room, (key, oldValue) => oldValue);
            });

            connectionTasks.Add(
                Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(new Random().Next(1000, 10000));
                        await connection.StartAsync();
                        await Task.Delay(500);
                        await connection.InvokeAsync("SubmitName", $"Bot_{Guid.NewGuid()}");
                        await Task.Delay(500);
                        await connection.InvokeAsync("CreatePrivateRoomAndReady", $"Room_{Guid.NewGuid()}", "NBA", maxPlayerCount);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in connection: {ex.Message}");
                    }
                }));
        }

        // Wait for all room creation tasks to complete
        await Task.WhenAll(connectionTasks);

        List<Task> joinPrivateRoomSubTasks = new();
        // Step 2: Join each private room with the stored password using a new connection
        foreach (var room in rooms)
        {
            for (int j = 0; j < room.Value.MaxPlayers - 1; j++) // Join each room with two connections
            {
                var connection = ConnectionManager.CreateHubConnection();

                joinPrivateRoomSubTasks.Add(
                Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(new Random().Next(1000, 10000));
                        await connection.StartAsync();
                        await Task.Delay(500);
                        await connection.InvokeAsync("SubmitName", $"Bot_{Guid.NewGuid()}");
                        await Task.Delay(500);
                        await connection.InvokeAsync("JoinGameByPassword", room.Value.RoomPassword);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in connection: {ex.Message}");
                    }
                }));
            }
        }
        await Task.WhenAll(joinPrivateRoomSubTasks);
        // Optionally, keep connections alive for a while
        await Task.Delay(2000);
    }
}
