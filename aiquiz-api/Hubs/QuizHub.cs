using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using aiquiz_api.Models;
using Microsoft.Extensions.Logging;
using aiquiz_api.Services;

namespace aiquiz_api.Hubs
{
    public class QuizHub : Hub
    {
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
            await JoinRoom();
            await SendMessage("RequestName");   
            await base.OnConnectedAsync();
        }

        public async Task SubmitName(string name)
        {
            var gameRoom = await _roomManager.SetPlayerNameAsync(Context.ConnectionId, name);
            await SendMessage("ReadyForGame", gameRoom?.Players.Values);
            await NotifyAllPlayer("PlayersStatus");
        }

        public async Task ReadyForGame(bool isReady)
        {
            var gameRoom = await _roomManager.SetPlayerReadyAsync(Context.ConnectionId);
            // If this is the first player to mark ready, ask them to set the topic
            if (isReady && gameRoom?.Players.Values.Count(p => p.Status == PlayerStatus.ReadyForGame) == 1)
            {
                await SendMessage("RequestSetTopic");
            }
            else
            {
                await StartGame(gameRoom);
            }

            await NotifyAllPlayer("PlayersStatus");
        }

        public async Task SetQuizTopic(string topic, int? numQuestions)
        {
            if (!string.IsNullOrWhiteSpace(topic))
            {
                CurrentTopic = topic;
                var quizs = await _quizManager.GenerateQuizAsync(CurrentTopic, Math.Min(numQuestions ?? 4, 20));
                var gameRoom = await _roomManager.SetQuizAsync(Context.ConnectionId, quizs);
                // Send start countdown event to all players in the room (e.g., 60 seconds)
                if (gameRoom != null)
                {
                    await Clients.Group(gameRoom.RoomId).SendAsync("StartCountdown", 60);
                }
                _logger.LogInformation("Quiz topic set to: {Topic}, Number of questions: {NumQuestions}", topic, numQuestions);
                // Do not start the game immediately; game will start after countdown on client
            }
        }

        public async Task SubmitAnswer(string answer)
        {

            var (isCorrect, gameRoom, quiz) = await _roomManager.MarkAnswer(Context.ConnectionId, answer);

            if (isCorrect)
            {
                // Send the next question if not last question
                if (string.IsNullOrEmpty(gameRoom?.GameWinner))
                {
                    await SendMessage("ReceiveQuestion", quiz);
                }
                else
                {
                    await NotifyAllPlayer("GameOver");
                }
            }
            else
            {
                // Optionally, you can notify the player or let them retry
                await SendMessage("IncorrectAnswer", 0);
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
            _logger.LogDebug("On Disconnected {connectionid}", Context.ConnectionId);
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
                await Clients.Caller.SendAsync(message, data);
                _logger.LogInformation("Send Message {message} to {connectionId} with data {data}", message, Context.ConnectionId, data );
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

        private async Task NotifyAllPlayer(string message)
        {
            var room = await _roomManager.GetRoomByConnectionAsync(Context.ConnectionId);
            if (room != null)
            {
                await Clients.Group(room.RoomId).SendAsync(message, room.Players.Values);
            }
        }

        private async Task StartGame(GameRoom? gameRoom)
        {
            if (gameRoom?.ReadyForGame == true)
            {
                _logger.LogInformation("All Players are ready for the game");
                if (gameRoom.Questions.Count == 0)
                {
                    _logger.LogWarning("No question available to start the game.");
                    return;
                }
                await SendGroupMessage("ReceiveQuestion", gameRoom.Questions[0]);
                _logger.LogInformation("Send Question : {Index} to All Players.", 0);
            }
        }
    }
}
