using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json;

string? lastPong = null;
object consoleLock = new();
int playerCount = 0;

void DrawPongBar()
{
    lock (consoleLock)
    {
        int origRow = Console.CursorTop;
        int origCol = Console.CursorLeft;
        Console.SetCursorPosition(0, 0);
        Console.Write($"[Last Pong: {lastPong ?? "(none)"}]");
        Console.Write(new string(' ', ( Console.WindowWidth == 0 ? 40 : Console.WindowWidth ) - (lastPong?.Length ?? 6) - 12)); // Clear rest of line
        Console.SetCursorPosition(origCol, origRow);
    }
}

// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");

Console.Clear();
DrawPongBar();
Console.SetCursorPosition(0, 2);
Console.WriteLine("Connecting to QuizHub...");

var connection = new HubConnectionBuilder()
    .WithUrl("http://localhost:5000/quizhub") // Change port if your API runs on a different port
    .Build();

// Inputt user name
connection.On("RequestName", () =>
{
    // Run input in a background task so DrawPongBar can still update
    _ = Task.Run(async () =>
    {
        lock (consoleLock)
        {
            int origRow = Console.CursorTop + 8; // Move down one line to avoid overwriting Pong bar
            int origCol = Console.CursorLeft;
            var enterNameMessage = "Please enter your name to join the quiz:";
            Console.SetCursorPosition(0, origRow); // Place input below player list
            Console.Write(enterNameMessage);
            Console.SetCursorPosition(enterNameMessage.Length + 1, origRow);
        }
        string? name = Console.ReadLine();
        await connection.InvokeAsync("SubmitName", name);
    });
});

connection.On<string>("ReceiveQuestion", async (question) =>
{
    _ = Task.Run(() =>
    {
        lock (consoleLock)
        {
            var enterNameMessage = $"Question: {question}";
            int origRow = Console.CursorTop;
            int origCol = Console.CursorLeft;
            Console.SetCursorPosition(0, origRow);
            Console.Write(enterNameMessage);
            Console.SetCursorPosition(enterNameMessage.Length + 1, origRow);
        }
    });
    var answer = Console.ReadLine();
    await connection.InvokeAsync("SubmitAnswer", answer);
});

connection.On<string>("GameOver", (winnerId) =>
{
    _ = Task.Run(() =>
    {
        lock (consoleLock)
        {
            int origRow = Console.CursorTop;
            int origCol = Console.CursorLeft;
            Console.SetCursorPosition(0, origRow);
            Console.WriteLine("Game Over!");
            Console.WriteLine($"Winner: {winnerId}");
            Console.WriteLine("Press any key to exit...");
        }
        Console.ReadKey();
    });
});

connection.On<IEnumerable<PlayerState>>("ReadyForGame", async (ready) =>
{
    _ = Task.Run(() =>
    {
        lock (consoleLock)
        {
            var enterNameMessage = $"Ready for game (y/n):";
            int origRow = Console.CursorTop;
            int origCol = Console.CursorLeft;
            Console.SetCursorPosition(0, origRow);
            Console.Write(enterNameMessage);
            Console.SetCursorPosition(enterNameMessage.Length + 1, origRow);
        }
    });
    var answer = Console.ReadLine();
    await connection.InvokeAsync("ReadyForGame", string.Equals("y", answer, StringComparison.OrdinalIgnoreCase) ? true : false  );
});

connection.On<IEnumerable<PlayerState>>("PlayersStatus", (players) =>
{
    lock (consoleLock)
    {
        int origRow = Console.CursorTop;
        int origCol = Console.CursorLeft;
        Console.SetCursorPosition(0, 4);
        Console.WriteLine("--- Player Status ---");
        int line = 5;
        playerCount = players.Count();
        foreach (var player in players)
        {
            Console.SetCursorPosition(0, line++);
            Console.WriteLine($"Player: {player.Name ?? "(unnamed)"}, Status: {player.Status}   ".PadRight(Console.WindowWidth - 1));
        }
        // Clear any extra lines from previous output
        // for (; line < playerCount + 5; line++)
        // {
        //     Console.SetCursorPosition(0, line);
        //     Console.Write(new string(' ', Console.WindowWidth));
        // }
        Console.SetCursorPosition(origCol, origRow);
    }
});

connection.On<string>("GameStarted", async (question) =>
{
    _ = Task.Run(() =>
    {
        lock (consoleLock)
        {
            var enterNameMessage = $"Question: {question}";
            int origRow = Console.CursorTop;
            int origCol = Console.CursorLeft;
            Console.SetCursorPosition(0, origRow);
            Console.Write(enterNameMessage);
            Console.SetCursorPosition(enterNameMessage.Length + 1, origRow);
        }
    });
    var answer = Console.ReadLine();
    await connection.InvokeAsync("SubmitAnswer", answer);
});

connection.On<DateTime>("Pong", (serverTime) =>
{
    lastPong = serverTime.ToString("O");
    DrawPongBar();
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

await Task.Delay(-1); // Keep the app running
