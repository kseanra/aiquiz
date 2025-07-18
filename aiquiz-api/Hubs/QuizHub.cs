using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using aiquiz_api.Models;
using Prometheus;

namespace aiquiz_api.Hubs
{
    public class QuizHub : Hub
    {
        private readonly IHubContext<QuizHub> _hubContext;
        private static ConcurrentDictionary<string, PlayerState> _lobby = new();
        private readonly ILogger<QuizHub> _logger;
        private readonly IQuizManager _quizManager;
        private readonly IRoomManager _roomManager;
        private readonly double GameStarIn = 5000;

        public QuizHub(ILogger<QuizHub> logger, IQuizManager quizManager, IRoomManager roomManager, IHubContext<QuizHub> hubContext)
        {
            _logger = logger;
            _quizManager = quizManager;
            _roomManager = roomManager;
            _hubContext = hubContext;
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogDebug("Client connected: {ConnectionId}", Context.ConnectionId);
            var room = await _roomManager.GetRoomByConnectionAsync(Context.ConnectionId);
            await SendMessage(room, "RequestName");
            await base.OnConnectedAsync();
        }

        public async Task SubmitName(string name)
        {
            _logger.LogDebug("Client connected: {ConnectionId} set Name: {name}", Context.ConnectionId, name);
            await Task.Run(() =>
            {
                var player = new PlayerState() { ConnectionId = Context.ConnectionId, Name = name };
                _lobby.AddOrUpdate(Context.ConnectionId, player, (key, oldValue) => player);
                LobbyCounter.Set(_lobby.Count);
            });
        }

        public async Task ReadyForGame(bool isReady)
        {
            if (!isReady) return;
            _logger.LogDebug("Client connected: {ConnectionId} is ready for games ", Context.ConnectionId);
            var gameRoom = await JoinRoom(_lobby[Context.ConnectionId]);
            if (gameRoom != null)
            {
                _ = _lobby.Remove<string, PlayerState>(Context.ConnectionId, out PlayerState? player);
                LobbyCounter.Set(_lobby.Count);
                await SetTopic(gameRoom);
                await NotifyAllPlayer(gameRoom, "PlayersStatus");
            }
        }

        public async Task SetQuizTopic(string topic, int? numQuestions)
        {
            if (!string.IsNullOrWhiteSpace(topic))
            {
                _logger.LogDebug("Client connected: {ConnectionId} set game topic: {topic}", Context.ConnectionId, topic);
                var gameRoom = await _roomManager.GetRoomByConnectionAsync(Context.ConnectionId);
                if (gameRoom?.Status == RoomStatus.Ready)
                {
                    StartGameAfterCountdown(gameRoom, topic, numQuestions);
                }
            }
        }

        public async Task SubmitAnswer(string answer)
        {
            var (isCorrect, gameRoom, quiz) = await _roomManager.MarkAnswer(Context.ConnectionId, answer);
            _logger.LogDebug("Client {Connection} Submit Answer {answer}", Context.ConnectionId, answer);
            if (isCorrect)
            {
                // Send the next question if not last question
                if (string.IsNullOrEmpty(gameRoom?.GameWinner))
                {
                    await SendMessage(gameRoom, "ReceiveQuestion", quiz);
                }
                else
                {
                    if (!_roomManager.GameRoomClosed(gameRoom.RoomId))
                    {
                        await NotifyAllPlayer(gameRoom, "GameOver");
                        _roomManager.SetGameRoomStatus(gameRoom.RoomId, RoomStatus.Close);
                    }
                }
            }
            else
            {
                // Optionally, you can notify the player or let them retry
                await SendMessage(gameRoom, "IncorrectAnswer", 0);
            }

            // Notify all players about everyone's status
            if (gameRoom != null)
                await NotifyAllPlayer(gameRoom, "PlayersStatus");
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

        private async Task<GameRoom?> JoinRoom(PlayerState player)
        {
            var room = _roomManager.JoinRoomAsync(Context.ConnectionId, player);
            if (room != null)
            {
                _logger.LogDebug("Player {Name}: {ConnectionId} joined room {RoomId}", player.Name, Context.ConnectionId, room.RoomId);
                await Groups.AddToGroupAsync(Context.ConnectionId, room.RoomId);
            }
            else
            {
                _logger.LogWarning("Failed to join room for Player {Name}: {ConnectionId}", player.Name, Context.ConnectionId);
            }

            return room;
        }

        private async Task SetTopic(GameRoom? gameRoom)
        {
            // when room is full can start game by asking user to set the topic
            if (gameRoom?.Status == RoomStatus.Ready)
            {
                var player = gameRoom.GetRandomPlayer();
                _logger.LogDebug("Send set topic to {Name}", player?.Name);
                if (player != null && !string.IsNullOrEmpty(player.ConnectionId))
                {
                    await Clients.Client(player.ConnectionId).SendAsync("RequestSetTopic");
                }
                else
                {
                    _logger.LogWarning("Cannot send RequestSetTopic: player or ConnectionId is null");
                }
            }
        }

        private async Task SendMessage(GameRoom? room, string message, object? data = null)
        {
            if (room != null)
            {
                await Clients.Caller.SendAsync(message, data);
                _logger.LogDebug("Send Message {message} to {connectionId} with data {data}", message, Context.ConnectionId, data);
            }
        }

        private async Task NotifyAllPlayer(GameRoom room, string message)
        {
            if (room != null)
            {
                await Clients.Group(room.RoomId).SendAsync(message, room.Players.Values);
            }
        }

        private void StartGameAfterCountdown(GameRoom? gameRoom, string category, int? numQuestions = 4)
        {
            _logger.LogDebug("Send countdown to client");
            if (gameRoom == null) return;
            var roomId = gameRoom.RoomId;
            var questionsCopy = gameRoom.Questions.ToList();
            var logger = _logger;
            var roomManager = _roomManager;
            var hubContext = _hubContext;
            var quizManager = _quizManager;
            var numberOfQuestion = numQuestions ?? 4;
            _ = Task.Run(async () =>
            {
                try
                {
                    var questionGeneratedStarted = DateTime.Now;
                    _logger.LogDebug($"Generating question started: {questionGeneratedStarted}");
                    await hubContext.Clients.Group(roomId).SendAsync("StartCountdown", GameStarIn / 1000);
                    var room = roomManager.GetGameRoomById(roomId);
                    room.Questions = await _quizManager.GenerateQuizForCategoryAsync(category, numberOfQuestion); ;
                    var questionGeneratedCompleted = DateTime.Now;
                    _logger.LogDebug($"Generating questions completed: {questionGeneratedCompleted}");
                    if (room != null && room.Questions.Count > 0 && room.Status != RoomStatus.GameStarted)
                    {
                        var diff = (questionGeneratedCompleted - questionGeneratedStarted).TotalMilliseconds;
                        logger.LogDebug($"time diff is {diff}");
                        var delaySeconds = GameStarIn - diff;
                        logger.LogDebug($"Delay at {delaySeconds}");
                        await Task.Delay((int)delaySeconds);
                        logger.LogInformation("Send first question to room {id} users", roomId);
                        roomManager.SetGameRoomStatus(room.RoomId, RoomStatus.GameStarted);
                        // Now send question to the group (only ready players remain)
                        await hubContext.Clients.Group(roomId).SendAsync("ReceiveQuestion", room.Questions[0]);
                    }
                    else
                    {
                        logger.LogDebug("room not found");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error sending question after countdown");
                }
            });
        }
        private static readonly Gauge LobbyCounter = Metrics.CreateGauge(
            "lobby_total", 
            "number of players in the lobby"
        );
    }
}
