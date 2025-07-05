using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json;

string? lastPong = null;
object consoleLock = new();
int playerCount = 0;
int defaultQuestionStartRow = 15;
int currentQuestionStartRow = 15;

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


void DisplayPlayerStatusAsync(IEnumerable<PlayerState> players)
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
            if (line >= Console.WindowHeight - 1) // Avoid overflow
            {
                Console.SetCursorPosition(0, Console.WindowHeight - 1);
                Console.WriteLine("... (more players)");
                break;
            }
            if (player.CurrentQuestionIndex > 0)
            {
                Console.SetCursorPosition(0, line++);
                Console.WriteLine($"Player: {player.Name ?? "(unnamed)"}, Status: {player.Status}, on Question Index: {player.CurrentQuestionIndex}   ".PadRight(Console.WindowWidth - 1));
            }
            else
            {
                Console.SetCursorPosition(0, line++);
                Console.WriteLine($"Player: {player.Name ?? "(unnamed)"}, Status: {player.Status}   ".PadRight(Console.WindowWidth - 1));
            }
        }
        Console.SetCursorPosition(origCol, origRow);
    }
 }   

// Reusable non-blocking input method
async Task<string> ReadUserInputAsync(string prompt, int? defaultOrigRow = default, int? defaultOrigCol = default)
{
    var userInput = "";
    //bool inputComplete = false;

    // Print prompt
    lock (consoleLock)
    {
        int origRow = defaultOrigRow ?? Console.CursorTop;
        int origCol = defaultOrigCol ?? Console.CursorLeft;
        Console.SetCursorPosition(0, origRow);
        Console.Write(prompt);
        Console.SetCursorPosition(prompt.Length + 1, origRow);
    }

    await Task.Run(() =>
    {
        userInput = Console.ReadLine() ?? string.Empty;
    });

    return userInput;
}

connection.On("RequestName", () =>
{
    _ = Task.Run(async () =>
    {
        string? name = await ReadUserInputAsync("Please enter your name to join the quiz:", 11);
        await connection.InvokeAsync("SubmitName", name);
    });
});

connection.On<IEnumerable<PlayerState>>("ReadyForGame", (ready) =>
{
    _ = Task.Run(async () =>
    {
        string answer = await ReadUserInputAsync("Ready for game (y/n):", 12);
        await connection.InvokeAsync("ReadyForGame", string.Equals("y", answer, StringComparison.OrdinalIgnoreCase));
    });
});


connection.On<string?>("ReceiveQuestion", (question) =>
{
    _ = Task.Run(async () =>
    {
        string answer = await ReadUserInputAsync($"Question: {question}", currentQuestionStartRow++);
        await connection.InvokeAsync("SubmitAnswer", answer);
    });
});

connection.On<string>("GameStarted", (question) =>
{
    _ = Task.Run(async () =>
    {
        string answer = await ReadUserInputAsync($"Question: {question}", defaultQuestionStartRow);
        await connection.InvokeAsync("SubmitAnswer", answer);

    });
});

connection.On<IEnumerable<PlayerState>>("GameOver", (players) =>
{
    _ = Task.Run(async () =>
    {
        DisplayPlayerStatusAsync(players);
        await connection.StopAsync();
        Thread.Sleep(2000); // Give time to read the game over message
        currentQuestionStartRow = 15;
        lock (consoleLock)
        {
            Console.Clear();
            Console.SetCursorPosition(0, 0);
        }

        System.Diagnostics.Process.Start(Environment.ProcessPath!);
        Environment.Exit(0);
    
    });
});

connection.On<IEnumerable<PlayerState>>("PlayersStatus", (players) =>
{
    _ = Task.Run(() =>
    {
        DisplayPlayerStatusAsync(players);
    });
});


connection.On<DateTime>("Pong", (serverTime) =>
{
    _ = Task.Run(() =>
    {
        lastPong = serverTime.ToString("O");
        DrawPongBar();
    });
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
