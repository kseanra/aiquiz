using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using aiquiz_api.Models;
namespace aiquiz_api.Hubs
{
    public class QuizHub : Hub
    {
        private static ConcurrentDictionary<string, PlayerState> Players = new();
        private static readonly List<string> Questions = new() { "Q1", "Q2", "Q3" };
        private const int TotalQuestions = 3;

        public override async Task OnConnectedAsync()
        {
            Players[Context.ConnectionId] = new PlayerState();
        
            await Clients.Caller.SendAsync("RequestName");
             var playerStates = Players.Select(p => new PlayerState() { Name = p.Key, ConnectionId = p.Key, JustJoined = true }).ToList();
            await Clients.All.SendAsync("PlayersStatus", playerStates);
            await base.OnConnectedAsync();
        }

        public async Task SubmitName(string name)
        {
            var player = Players[Context.ConnectionId];
            player.Name = name;
            await Clients.Caller.SendAsync("ReceiveQuestion", Questions[0]);
            // Notify all players about the new player and everyone's status
            var playerStates = Players.Select(p => new PlayerState() { Name = p.Value.Name, CurrentQuestionIndex = 0, ConnectionId = p.Key }).ToList();
            await Clients.All.SendAsync("PlayersStatus", playerStates);
        }

        public async Task SubmitAnswer(string answer)
        {
            var player = Players[Context.ConnectionId];
            player.CurrentQuestionIndex++;

            // Notify all players about everyone's status after answer
            var playerStates = Players.Select(p => new PlayerState() { Name = p.Value.Name, CurrentQuestionIndex = p.Value.CurrentQuestionIndex, ConnectionId = p.Key }).ToList();
            await Clients.All.SendAsync("PlayersStatus", playerStates);

            if (player.CurrentQuestionIndex >= TotalQuestions)
            {
                await Clients.All.SendAsync("GameOver", Context.ConnectionId);
            }
            else
            {
                await Clients.Caller.SendAsync("ReceiveQuestion", Questions[player.CurrentQuestionIndex]);
            }
        }

        public async Task Ping()
        {
            await Clients.Caller.SendAsync("Pong", DateTime.UtcNow);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            Players.TryRemove(Context.ConnectionId, out _);
            // Notify all players about updated status
            var playerStates = Players.Select(p => new PlayerState(){ Name = p.Value.Name, CurrentQuestionIndex = p.Value.CurrentQuestionIndex, ConnectionId = p.Key, Disconnected = true }).ToList();
            await Clients.All.SendAsync("PlayersStatus", playerStates);
            await base.OnDisconnectedAsync(exception);
        }
    }
}
