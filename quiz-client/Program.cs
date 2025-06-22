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
        userInput = "";
        bool inputComplete = false;
        while (!inputComplete)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter)
                {
                    inputComplete = true;
                }
                else if (key.Key == ConsoleKey.Backspace && userInput.Length > 0)
                {
                    userInput = userInput.Substring(0, userInput.Length - 1);
                    lock (consoleLock)
                    {
                        Console.SetCursorPosition(0, Console.CursorTop);
                        Console.Write(new string(' ', Console.WindowWidth));
                        Console.SetCursorPosition(0, Console.CursorTop);
                        Console.Write(prompt + " " + userInput);
                        Console.SetCursorPosition(prompt.Length + 1 + userInput.Length, Console.CursorTop);
                    }
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    userInput += key.KeyChar;
                    lock (consoleLock)
                    {
                        Console.Write(key.KeyChar);
                    }
                }
            }
            Thread.Sleep(50); // Reduce CPU usage
        }
    });

    return userInput;
}

connection.On("RequestName", () =>
{
    _ = Task.Run(async () =>
    {
        string? name = await ReadUserInputAsync("Please enter your name to join the quiz:", Console.CursorTop + 8);
        await connection.InvokeAsync("SubmitName", name);
    });
});

connection.On<IEnumerable<PlayerState>>("ReadyForGame", async (ready) =>
{
    _ = Task.Run(async () =>
    {
        string answer = await ReadUserInputAsync("Ready for game (y/n):", Console.CursorTop + 1);
        await connection.InvokeAsync("ReadyForGame", string.Equals("y", answer, StringComparison.OrdinalIgnoreCase));
    });
});


connection.On<string?>("ReceiveQuestion", async (question) =>
{
    _ = Task.Run(async () =>
    {
        string answer = await ReadUserInputAsync($"Question: {question}", Console.CursorTop + 1);
        await connection.InvokeAsync("SubmitAnswer", answer);
    });
});

connection.On<string>("GameStarted", async (question) =>
{
    _ = Task.Run(async () =>
    {
        string answer = await ReadUserInputAsync($"Question: {question}", Console.CursorTop + 1);
        await connection.InvokeAsync("SubmitAnswer", answer);

    });
});

connection.On<IEnumerable<PlayerState>>("GameOver", async (players) =>
{
    _ = Task.Run(async () =>
    {
        DisplayPlayerStatusAsync(players);
        await connection.StopAsync();
        Thread.Sleep(2000); // Give time to read the game over message
        lock (consoleLock)
        {
            Console.Clear();
            Console.WriteLine("Game Over! Press any key for next game...");
        }
        await connection.StartAsync();
    });
});

connection.On<IEnumerable<PlayerState>>("PlayersStatus", (players) =>
{
    _ = Task.Run(async () =>
    {
        DisplayPlayerStatusAsync(players);
    });
});

connection.On<string>("GameStarted", async (question) =>
{
    _ = Task.Run(async () =>
    {
        string answer = await ReadUserInputAsync($"Question: {question}");
        await connection.InvokeAsync("SubmitAnswer", answer);
    });
});

connection.On<DateTime>("Pong", (serverTime) =>
{
    _ = Task.Run(async () =>
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
