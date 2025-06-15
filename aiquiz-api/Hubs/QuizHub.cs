using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace aiquiz_api.Hubs
{
    public class QuizHub : Hub
    {
        private static ConcurrentDictionary<string, PlayerState> Players = new();
        private static readonly List<string> Questions = new() { "Q1", "Q2", "Q3" };
        private const int TotalQuestions = 3;

        public override Task OnConnectedAsync()
        {
            Players[Context.ConnectionId] = new PlayerState();
            Clients.Caller.SendAsync("ReceiveQuestion", Questions[0]);
            return base.OnConnectedAsync();
        }

        public async Task SubmitAnswer(string answer)
        {
            var player = Players[Context.ConnectionId];
            player.CurrentQuestionIndex++;

            if (player.CurrentQuestionIndex >= TotalQuestions)
            {
                await Clients.All.SendAsync("GameOver", Context.ConnectionId);
            }
            else
            {
                await Clients.Caller.SendAsync("ReceiveQuestion", Questions[player.CurrentQuestionIndex]);
            }
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            Players.TryRemove(Context.ConnectionId, out _);
            return base.OnDisconnectedAsync(exception);
        }

        private class PlayerState
        {
            public int CurrentQuestionIndex { get; set; } = 0;
        }
    }
}
