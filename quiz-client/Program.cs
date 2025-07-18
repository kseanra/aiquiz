using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using System.Text.Json;

while (true)
{
    Console.Write("Enter number of connections: ");
    var input = Console.ReadLine();

    if (string.Equals(input, "exit", StringComparison.OrdinalIgnoreCase))
        break;

    await RunTaskAsync(input);

    Console.Clear();
}
static async Task RunTaskAsync(string? value)
{
    int connectionCount = int.TryParse(value, out int connections) ? connections : 20; // Change this to the number of connections you want
    List<Task> connectionTasks = new();

    for (int i = 0; i < connectionCount; i++)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl($"http://localhost:5000/quizhub", Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets | Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling) // Optionally add a query param for identification
            .WithAutomaticReconnect(new[]
            {
                TimeSpan.FromSeconds(0),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30)
            }   ) // Retry intervals
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Debug);
            })
            .Build();

        connection.Closed += async (error) =>
        {
            //Console.WriteLine($"Connection closed: {error?.Message}");
            await Task.Delay(500);
        };
        connection.On("RequestSetTopic", () =>
        {
            _ = Task.Run(async () =>
            {
                // Automatically submit a categry
                await Task.Delay(500);
                await connection.InvokeAsync("SetQuizTopic", "NBA", 4);
            });
        });
    
        connection.On<Quiz>("ReceiveQuestion", (question) =>
        {
            _ = Task.Run(async () =>
            {
                // Automatically submit "correct" as the answer
                await Task.Delay(500);
                await connection.InvokeAsync("SubmitAnswer", question.Answer);
            });
        });

        connection.On<IEnumerable<PlayerState>>("GameOver", (players) =>
        {
            _ = Task.Run(async () =>
            {
                await connection.StopAsync();
            });
        });

        connection.On<IEnumerable<PlayerState>>("PlayersStatus", (players) =>
        {
            // User status display removed
        });



        connectionTasks.Add(
            Task.Run(async () =>
            {
                try {
                    await Task.Delay(new Random().Next(10000, 20000));
                    await connection.StartAsync();
                    await Task.Delay(500);
                    await connection.InvokeAsync("SubmitName", $"Bot_{Guid.NewGuid()}");
                    await Task.Delay(500);
                    await connection.InvokeAsync("ReadyForGame", true);
                } catch (Exception ex) {
                    Console.WriteLine($"Error in connection: {ex.Message}");
                }
            }));
    }

    await Task.WhenAll(connectionTasks);
    connectionTasks.Clear();
}

//await Task.Delay(-1);