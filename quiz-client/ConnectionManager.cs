using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

public static class ConnectionManager
{
    // This method runs the specified number of connections to the quiz hub
    // It automatically joins the quiz, submits a name, and marks itself as ready for the game
    public static HubConnection CreateHubConnection()
    {
        var connection = new HubConnectionBuilder()
                .WithUrl($"http://localhost:5000/quizhub", Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets | Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling) // Optionally add a query param for identification
                .WithAutomaticReconnect(new[]
                {
                    TimeSpan.FromSeconds(0),
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(10),
                    TimeSpan.FromSeconds(30)
                }) // Retry intervals
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
            
        return connection;
    }
}