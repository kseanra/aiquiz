using Microsoft.AspNetCore.SignalR.Client;

// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");

Console.WriteLine("Connecting to QuizHub...");

var connection = new HubConnectionBuilder()
    .WithUrl("http://localhost:5084/quizhub") // Change port if your API runs on a different port
    .Build();


connection.On("RequestName", async () =>
{
    Console.Write("Enter your name: ");
    var name = Console.ReadLine();
    await connection.InvokeAsync("SubmitName", name);
});

connection.On<string>("ReceiveQuestion", async (question) =>
{
    Console.WriteLine($"Question: {question}");
    Console.Write("Your answer: ");
    var answer = Console.ReadLine();
    await connection.InvokeAsync("SubmitAnswer", answer);
});

connection.On<string>("GameOver", (winnerId) =>
{
    Console.WriteLine($"Game over! Winner: {winnerId}");
    Environment.Exit(0);
});

await connection.StartAsync();
Console.WriteLine("Connected! Waiting for questions...");

await Task.Delay(-1); // Keep the app running
