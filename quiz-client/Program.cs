using Microsoft.AspNetCore.SignalR.Client;
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
            .WithUrl($"http://localhost:5000/quizhub?client={i}") // Optionally add a query param for identification
            .Build();

        // Event handlers for each connection
        connection.On("RequestName", () =>
        {
            _ = Task.Run(async () =>
            {
                // Automatically submit a name
                await Task.Delay(500);
                await connection.InvokeAsync("SubmitName", $"Bot_{i}");
            });
        });

        connection.On<IEnumerable<PlayerState>>("ReadyForGame", (ready) =>
        {
            _ = Task.Run(async () =>
            {
                // Automatically ready for game

            });
        });

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

        connection.On<DateTime>("Pong", (serverTime) =>
        {
            _ = Task.Run(() => { });
        });

        // Example: send a ping every 2 seconds in the background
        _ = Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
                await connection.InvokeAsync("Ping");
            }
        });

        connectionTasks.Add(
            Task.Run(async () =>
            {
                await Task.Delay(new Random().Next(1000, 2000));
                await connection.StartAsync();
                await Task.Delay(500);
                await connection.InvokeAsync("SubmitName", $"Bot_{Guid.NewGuid()}");
                await Task.Delay(500);
                await connection.InvokeAsync("ReadyForGame", true);
            }));
    }

    await Task.WhenAll(connectionTasks);
}

//await Task.Delay(-1);