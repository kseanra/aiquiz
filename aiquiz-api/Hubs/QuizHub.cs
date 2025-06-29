using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using aiquiz_api.Models;
using Microsoft.Extensions.Logging;
using aiquiz_api.Services;

namespace aiquiz_api.Hubs
{
    public class QuizHub : Hub
    {
        private static int TotlaParticipants = 1;
        private static ConcurrentDictionary<string, PlayerState> Players = new();
        private static List<Quiz> Questions = new(); // Use Quiz objects
        private static string CurrentTopic = "general knowledge";
        private static int TotalQuestions => Questions.Count;
        private readonly ILogger<QuizHub> _logger;
        private readonly QuizManager _quizManager;

        public QuizHub(ILogger<QuizHub> logger, QuizManager quizManager)
        {
            _logger = logger;
            _quizManager = quizManager;
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
            Players[Context.ConnectionId] = new PlayerState() { ConnectionId = Context.ConnectionId, Status = PlayerStatus.JustJoined };

            await Clients.Caller.SendAsync("RequestName");
            await base.OnConnectedAsync();
        }

        public async Task SubmitName(string name)
        {
            var player = Players[Context.ConnectionId];
            player.Name = name;
            player.Status = PlayerStatus.JustJoined;
            _logger.LogInformation("Player {ConnectionId} {Name} is {Status}", player.ConnectionId, player.Name, player.Status);
            await Clients.Caller.SendAsync("ReadyForGame", GetPlayerStates());
        }

        public async Task ReadyForGame(bool isReady)
        {
            var player = Players[Context.ConnectionId];
            player.Status = isReady ? PlayerStatus.ReadyForGame : PlayerStatus.WaitingForGame;
            _logger.LogInformation("Player {ConnectionId} {Name} is {Status}", player.ConnectionId, player.Name, player.Status);

            // If all players are ReadyForGame, generate questions and send event to all
            if (AllPlayersReady())
            {
                if (Questions.Count == 0) // Only generate once per game
                {
                    Questions = await _quizManager.GenerateQuizAsync(CurrentTopic);
                }
                var question = Questions.Count > 0 ? Questions[0] : null;
                _logger.LogInformation("All Players are ready for the game");
                await StartGame(question);
                _logger.LogInformation("Send Question : {Index} to All Players.", question);
            }

            await NotifyAllPlayer("PlayersStatus");
        }

        public async Task SubmitAnswer(string answer)
        {
            var player = Players[Context.ConnectionId];

            _logger.LogInformation("Player {ConnectionId} {Name} submitted answer: {Answer} for question index: {Index}", player.ConnectionId, player.Name, answer, player.CurrentQuestionIndex);
            /// TODO: Notify all players about everyone's status

            if (IsLastQuestion(player) && !HaveGameWinner())
            {
                SetCurrentPlayerAsGameWinner();
                _logger.LogInformation("Game Over, we have a winner: {Name}", player.Name);
                // Notify all players about everyone's status "GameOver"
                await NotifyAllPlayer("GameOver");
            }
            else
            {
                // Send the next question to the player
                if (player.CurrentQuestionIndex < TotalQuestions - 1)
                {
                    player.CurrentQuestionIndex++; 
                    _logger.LogInformation("Send new question to Player {ConnectionId} {Name} : {Index}", player.ConnectionId, player.Name, player.CurrentQuestionIndex);
                    // Send the next question to the player
                    await SendQuestionToPlayer("ReceiveQuestion", Questions[player.CurrentQuestionIndex]);
                }
            }
            /// Notify all players about everyone's status
            await NotifyAllPlayer("PlayersStatus");
        }

        public async Task Ping()
        {
            await Clients.Caller.SendAsync("Pong", DateTime.UtcNow);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var player = Players[Context.ConnectionId];
            _logger.LogInformation("Client disconnected: {ConnectionId}, Player: {Name}", Context.ConnectionId, player.Name);
            Players.TryRemove(Context.ConnectionId, out _);
            await base.OnDisconnectedAsync(exception);
        }

        // Returns true if any player's status is GameOver
        private bool HaveGameWinner()
        {
            var result = Players.Values.Any(p => p.Status == PlayerStatus.GameWinner);
            return result;
        }

        private bool IsLastQuestion(PlayerState player)
        {
            var result = player.CurrentQuestionIndex >= TotalQuestions - 1;
            return result;
        }

        // Set the current player as the game winner
        private void SetCurrentPlayerAsGameWinner()
        {
            if (Players.TryGetValue(Context.ConnectionId, out var player))
            {
                player.Status = PlayerStatus.GameWinner;
            }
        }

        // Helper function to return a list of PlayerState from Players
        private List<PlayerState> GetPlayerStates()
        {
            var players = Players?.Values.ToList() ?? new List<PlayerState>();

            foreach (var player in players)
            {
                _logger.LogInformation("Current Players Status: {ConnectionId} {Name} : {PlayersStatus}", player.ConnectionId, player.Name, player.Status);
            }

            return players;
        }

        private bool AllPlayersReady()
        {
            var playersNotReady = Players.Values.Any(p => p.Status != PlayerStatus.ReadyForGame);
            return Players.Count == TotlaParticipants && playersNotReady == false;
        }

        private async Task NotifyAllPlayer(string method)
        {
            var playerStates = GetPlayerStates();
            if (Players != null)
            {
                await Clients.All.SendAsync(method ?? "PlayersStatus", playerStates);
            }
        }

        private async Task SendQuestionToPlayer(string method, Quiz? question)
        {
            if(question == null)
            {
                _logger.LogWarning("Question is null, cannot send to player.");
                return;
            }
            if (Players.TryGetValue(Context.ConnectionId, out var player))
            {
                await Clients.Caller.SendAsync(method, question);
                _logger.LogInformation("Send Question to Player {Name} : {Index}", player.Name, question);
            }
        }

        private async Task StartGame(Quiz question)
        {
           await SendNextQuestion(question);
        }

        private async Task SendNextQuestion(Quiz question)
        {
            if (question == null)
            {
                _logger.LogWarning("No question available to start the game.");
                return;
            }

            if (Players.TryGetValue(Context.ConnectionId, out var player))
            {
                await Clients.All.SendAsync("ReceiveQuestion", question);
                _logger.LogInformation("Send Question to Player {Name} : {Index}", player.Name, question);
            }
        }
    }
}
