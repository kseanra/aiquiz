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
            var playerStates = Players.Select(p => new PlayerState() { Name = p.Key, ConnectionId = p.Key, Status = PlayerStatus.JustJoined }).ToList();
            //await Clients.All.SendAsync("PlayersStatus", playerStates);
            await base.OnConnectedAsync();
        }

        public async Task SubmitName(string name)
        {
            var player = Players[Context.ConnectionId];
            player.Name = name;
            player.Status = PlayerStatus.JustJoined;
            var playerStates = Players.Select(p => new PlayerState() { Name = p.Value.Name, CurrentQuestionIndex = 0, ConnectionId = p.Key }).ToList();
            await Clients.All.SendAsync("PlayersStatus", playerStates);
            await Clients.All.SendAsync("ReadyForGame", playerStates);
        }
        
        public async Task ReadyForGame(bool isReady)
        {
            var player = Players[Context.ConnectionId];
            player.CurrentQuestionIndex = 0;
            player.Status = isReady? PlayerStatus.ReadyForGame : PlayerStatus.WaitingForGame;

            // Notify all players about everyone's status
            var playerStates = Players.Select(p => new PlayerState() { Name = p.Value.Name, CurrentQuestionIndex = p.Value.CurrentQuestionIndex, ConnectionId = p.Key, Status = p.Value.Status }).ToList();
            await Clients.All.SendAsync("PlayersStatus", playerStates);

            // If all players are ReadyForGame, send event to all
            if (Players.Count > 0 && Players.All(p => p.Value.Status == PlayerStatus.ReadyForGame))
            {
                await Clients.All.SendAsync("GameReady");
            }

            // Send the first question to the player
            await Clients.All.SendAsync("GameStarted", Questions[player.CurrentQuestionIndex]);
        }

        public async Task SubmitAnswer(string answer)
        {
            var player = Players[Context.ConnectionId];
            player.CurrentQuestionIndex++;

            // Notify all players about everyone's status after answer
            var playerStates = Players.Select(p => new PlayerState() { Name = p.Value.Name, CurrentQuestionIndex = p.Value.CurrentQuestionIndex, ConnectionId = p.Key }).ToList();
            await Clients.All.SendAsync("PlayersStatus", playerStates);
            
            if (player.CurrentQuestionIndex >= TotalQuestions && HaveGameWinner())
            {
                SetCurrentPlayerAsGameWinner();
                await Clients.All.SendAsync("GameOver", playerStates);
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
            var playerStates = Players.Select(p => new PlayerState(){ Name = p.Value.Name, CurrentQuestionIndex = p.Value.CurrentQuestionIndex, ConnectionId = p.Key, Status = PlayerStatus.Disconnected }).ToList();
            await Clients.All.SendAsync("PlayersStatus", playerStates);
            await base.OnDisconnectedAsync(exception);
        }

        // Returns true if any player's status is GameOver
        private bool HaveGameWinner()
        {
            return Players.Values.Any(p => p.Status == PlayerStatus.GameWinner);
        }

        // Set the current player as the game winner
        private void SetCurrentPlayerAsGameWinner()
        {
            if (Players.TryGetValue(Context.ConnectionId, out var player))
            {
                player.Status = PlayerStatus.GameWinner;
            }
        }
    }
}
