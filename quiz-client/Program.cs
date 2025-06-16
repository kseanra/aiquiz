using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json;

// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");

Console.WriteLine("Connecting to QuizHub...");

var connection = new HubConnectionBuilder()
    .WithUrl("http://localhost:5000/quizhub") // Change port if your API runs on a different port
    .Build();


// connection.On("RequestName", async () =>
// {
//     Console.Write("Enter your name: ");
//     var name = Console.ReadLine();
//     await connection.InvokeAsync("SubmitName", name);
// });

// connection.On<string>("ReceiveQuestion", async (question) =>
// {
//     Console.WriteLine($"Question: {question}");
//     Console.Write("Your answer: ");
//     var answer = Console.ReadLine();
//     await connection.InvokeAsync("SubmitAnswer", answer);
// });

// connection.On<string>("GameOver", (winnerId) =>
// {
//     Console.WriteLine($"Game over! Winner: {winnerId}");
//     Environment.Exit(0);
// });

connection.On<IEnumerable<PlayerState>>("PlayersStatus", (players) =>
{
    foreach (var player in players)
    {
        if (player.Disconnected)
        {
            Console.WriteLine($"Player {player.Name ?? "(unnamed)"} has disconnected.");
            continue;
        }
        else if (player.JustJoined)
        {
            Console.WriteLine($"Player {player.Name ?? "(unnamed)"} has just joined.");
            continue;
        }
        Console.WriteLine($"Player: {player.Name ?? "(unnamed)"}, Question #: {player.CurrentQuestionIndex + 1}");
    }
});

connection.On<DateTime>("Pong", (serverTime) =>
{
    //Console.WriteLine($"Pong received from server at {serverTime:O}");
});

// Example: send a ping every 10 seconds in the background
_ = Task.Run(async () =>
{
    while (true)
    {
        await Task.Delay(TimeSpan.FromSeconds(2));
        await connection.InvokeAsync("Ping");
    }
});

await connection.StartAsync();
Console.WriteLine("Connected! Waiting for questions...");

await Task.Delay(-1); // Keep the app running

public class PlayerState
{
    public string? ConnectionId { get; set; }
    public string? Name { get; set; }
    public int CurrentQuestionIndex { get; set; }
    public bool Disconnected { get; set; } = false; // Indicates if the player is disconnected
    public bool JustJoined { get; set; } = false; // Indicates if the player just joine
}
