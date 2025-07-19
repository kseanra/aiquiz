using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

public static class DefaultGameRoomTask
{
    // Default Game Room
    // This method runs the specified number of connections to the quiz hub
    // It automatically joins the quiz, submits a name, and marks itself as ready for the game
   public static async Task RunDefaultGameRoomTaskAsync(string? value)
    {
        int connectionCount = int.TryParse(value, out int connections) ? connections : 20; // Change this to the number of connections you want
        List<Task> connectionTasks = new();

        for (int i = 0; i < connectionCount; i++)
        {
            var connection = ConnectionManager.CreateHubConnection();
            
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
                        await connection.InvokeAsync("ReadyForGame", true);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in connection: {ex.Message}");
                    }
                }));
        }

        await Task.WhenAll(connectionTasks);
        connectionTasks.Clear();
    }
}