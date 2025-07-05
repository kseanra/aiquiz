using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using aiquiz_api.Models;
using Microsoft.Extensions.Logging;
using aiquiz_api.Services;

namespace aiquiz_api.Hubs
{
    public class QuizHub : Hub
    {
        private static int TotlaParticipants = 3;
        private static ConcurrentDictionary<string, PlayerState> Players = new();
        private static List<Quiz> Questions = new(); // Use Quiz objects
        private static string CurrentTopic = "";
        private static int TotalQuestions => Questions.Count;
        private readonly ILogger<QuizHub> _logger;
        private readonly IQuizManager _quizManager;
        private readonly IRoomManager _roomManager;

        public QuizHub(ILogger<QuizHub> logger, IQuizManager quizManager, IRoomManager roomManager)
        {
            _logger = logger;
            _quizManager = quizManager;
            _roomManager = roomManager;
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
            Players[Context.ConnectionId] = new PlayerState() { ConnectionId = Context.ConnectionId, Status = PlayerStatus.JustJoined };
            await JoinRoom();
            await SendMessage("RequestName");
            await base.OnConnectedAsync();
        }

        public async Task SubmitName(string name)
        {
            var player = Players[Context.ConnectionId];
            player.Name = name;
            player.Status = PlayerStatus.JustJoined;
            _logger.LogInformation("Player {ConnectionId} {Name} is {Status}", player.ConnectionId, player.Name, player.Status);
            await SendMessage("ReadyForGame", GetPlayerStates());
        }

        public async Task ReadyForGame(bool isReady)
        {
            var player = Players[Context.ConnectionId];
            player.Status = isReady ? PlayerStatus.ReadyForGame : PlayerStatus.WaitingForGame;
            _logger.LogInformation("Player {ConnectionId} {Name} is {Status}", player.ConnectionId, player.Name, player.Status);

            // If this is the first player to mark ready, ask them to set the topic
            if (isReady && Players.Values.Count(p => p.Status == PlayerStatus.ReadyForGame) == 1)
            {
                await SendMessage("RequestSetTopic");
                // Do not proceed to start the game until topic is set
                return;
            }

            // If all players are ReadyForGame, generate questions and send event to all
            await StartGame();

            await NotifyAllPlayer("PlayersStatus");
        }

        public async Task SetQuizTopic(string topic, int numQuestions = 4)
        {
            if (!string.IsNullOrWhiteSpace(topic))
            {
                CurrentTopic = topic;
                if (numQuestions < 1) numQuestions = 4;
                Questions = await _quizManager.GenerateQuizAsync(CurrentTopic, numQuestions);
                // check if all players are ready to start the game, if yes send first question
                _logger.LogInformation("Quiz topic set to: {Topic}, Number of questions: {NumQuestions}", topic, numQuestions);
                await StartGame();
            }
        }

        public async Task SubmitAnswer(string answer)
        {
            var player = Players[Context.ConnectionId];

            _logger.LogInformation("Player {ConnectionId} {Name} submitted answer: {Answer} for question index: {Index}", player.ConnectionId, player.Name, answer, player.CurrentQuestionIndex);
            // Check if answer is correct for the current question
            bool isCorrect = false;
            if (player.CurrentQuestionIndex < Questions.Count)
            {
                var currentQuestion = Questions[player.CurrentQuestionIndex];
                isCorrect = string.Equals(answer?.Trim(), currentQuestion.Answer?.Trim(), StringComparison.OrdinalIgnoreCase);
            }

            if (isCorrect)
            {
                _logger.LogInformation("Player {ConnectionId} answered correctly.", player.ConnectionId);
                // Send the next question if not last question
                if (player.CurrentQuestionIndex < TotalQuestions - 1)
                {
                    player.CurrentQuestionIndex++;
                    _logger.LogInformation("Send new question to Player {ConnectionId} {Name} : {Index}", player.ConnectionId, player.Name, player.CurrentQuestionIndex);
                    await SendQuestionToPlayer("ReceiveQuestion", Questions[player.CurrentQuestionIndex]);
                }
                else if (!HaveGameWinner())
                {
                    SetCurrentPlayerAsGameWinner();
                    _logger.LogInformation("Game Over, we have a winner: {Name}", player.Name);
                    await NotifyAllPlayer("GameOver");
                }
            }
            else
            {
                _logger.LogInformation("Player {ConnectionId} answered incorrectly.", player.ConnectionId);
                // Optionally, you can notify the player or let them retry
                await SendMessage("IncorrectAnswer", player.CurrentQuestionIndex);
            }

            // Notify all players about everyone's status
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
            await _roomManager.LeaveRoomAsync(Context.ConnectionId);
        }

        private async Task JoinRoom()
        {
            var room = await _roomManager.JoinRoomAsync(Context.ConnectionId);
            if (room != null)
            {
                _logger.LogInformation("Player {ConnectionId} joined room {RoomId}", Context.ConnectionId, room.RoomId);
                await Groups.AddToGroupAsync(Context.ConnectionId, room.RoomId);            }
            else
            {
                _logger.LogWarning("Failed to join room for Player {ConnectionId}", Context.ConnectionId);
            }
        }

        private async Task SendMessage(string message,  object? data = null)
        {
            var room = await _roomManager.GetRoomByConnectionAsync(Context.ConnectionId);
            if (room != null)
            {
                await Clients.Group(room.RoomId).SendAsync(message, Context.ConnectionId, data);
            }
        }

        private async Task SendGroupMessage(string message, object? data = null)
        {
            var room = await _roomManager.GetRoomByConnectionAsync(Context.ConnectionId);
            if (room != null)
            {
                await Clients.Group(room.RoomId).SendAsync(message, data);
            }
        }

        private async Task SetReady(bool isReady)
        {
            var allReady = await _roomManager.SetPlayerReadyAsync(Context.ConnectionId, isReady);
            var room = await _roomManager.GetRoomByConnectionAsync(Context.ConnectionId);
            if (room != null && allReady)
            {
                room.IsGameStarted = true;
                await Clients.Group(room.RoomId).SendAsync("GameStarted");
            }
        }

        // Returns true if any player's status is GameOver
        private bool HaveGameWinner()
        {
            var result = Players.Values.Any(p => p.Status == PlayerStatus.GameWinner);
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
                await SendGroupMessage(method ?? "PlayersStatus", playerStates);
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
                await SendMessage(method, question);
                _logger.LogInformation("Send Question to Player {Name} : {Index}", player.Name, question);
            }
        }

        private async Task StartGame()
        {
            if (AllPlayersReady())
            {
                var question = Questions.Count > 0 ? Questions[0] : null;
                _logger.LogInformation("All Players are ready for the game");
                if (question == null)
                {
                    _logger.LogWarning("No question available to start the game.");
                    return;
                }
                await SendGroupMessage("ReceiveQuestion", question);
                _logger.LogInformation("Send Question : {Index} to All Players.", question);
            }
        }
    }
}
