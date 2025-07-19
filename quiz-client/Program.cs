using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using System.Text.Json;

while (true)
{
    Console.WriteLine("Select an option:");
    Console.WriteLine("1. Default Game Room");
    Console.WriteLine("2. Private Game Room");
    Console.WriteLine("Type 'exit' to quit.");
    Console.Write("Enter your choice: ");
    var selection = Console.ReadLine();

    if (string.Equals(selection, "exit", StringComparison.OrdinalIgnoreCase))
        break;

    if (selection == "1")
    {
        Console.Write("Enter number of connections: ");
        var input = Console.ReadLine();
        await DefaultGameRoomTask.RunDefaultGameRoomTaskAsync(input);
    }
    else if (selection == "2")
    {
        Console.Write("Enter number of private rooms: ");
        var input = Console.ReadLine();
        await PrivateGameRoomTask.RunPrivateGameRoomTaskAsync(input);
    }
    else
    {
        Console.WriteLine("Invalid selection. Please try again.\n");
        continue;
    }

    Console.Clear();
}   


//await Task.Delay(-1);