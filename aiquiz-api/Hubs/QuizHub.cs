using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using aiquiz_api.Models;
using Microsoft.Extensions.Logging;
using aiquiz_api.Services;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;

namespace aiquiz_api.Hubs
{
    public class QuizHub : Hub
    {
        private readonly IHubContext<QuizHub> _hubContext;
        private static ConcurrentDictionary<string, PlayerState> _lobby = new();
        private readonly ILogger<QuizHub> _logger;
        private readonly IQuizManager _quizManager;
        private readonly IRoomManager _roomManager;

        public QuizHub(ILogger<QuizHub> logger, IQuizManager quizManager, IRoomManager roomManager, IHubContext<QuizHub> hubContext)
        {
            _logger = logger;
            _quizManager = quizManager;
            _roomManager = roomManager;
            _hubContext = hubContext;
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
            await SendMessage("RequestName");
            await base.OnConnectedAsync();
        }

        public async Task SubmitName(string name)
        {
            await Task.Run(() =>
                _lobby.AddOrUpdate(Context.ConnectionId, new PlayerState() { ConnectionId = Context.ConnectionId, Name = name }, (key, oldValue) => oldValue)
            );
        }

        public async Task ReadyForGame(bool isReady)
        {
            if (!isReady) return;
            var gameRoom = await JoinRoom(_lobby[Context.ConnectionId]);
            await SetTopic(gameRoom);
            await StartGame(gameRoom);
            await NotifyAllPlayer("PlayersStatus");
        }

        public async Task SetQuizTopic(string topic, int? numQuestions)
        {
            if (!string.IsNullOrWhiteSpace(topic))
            {
                //var quizs = await _quizManager.GenerateQuizAsync(topic, Math.Min(numQuestions ?? 4, 20));
                //var gameRoom = await _roomManager.SetQuizAsync(Context.ConnectionId, quizs);
                var gameRoom = await _roomManager.GetRoomByConnectionAsync(Context.ConnectionId);
                // Send start countdown event to all players in the room (e.g., 60 seconds)
                if (gameRoom?.ReadyForGame == true)
                {
                    //await StartGame(gameRoom);
                    await StartGameAfterCountdown(gameRoom, topic); 
                }
                // else
                // {
                //     await StartGameAfterCountdown(gameRoom, topic);
                // }
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

        private async Task<GameRoom?> JoinRoom(PlayerState player)
        {
            var room = await _roomManager.JoinRoomAsync(Context.ConnectionId, player);
            if (room != null)
            {
                _logger.LogInformation("Player {ConnectionId} joined room {RoomId}", Context.ConnectionId, room.RoomId);
                await Groups.AddToGroupAsync(Context.ConnectionId, room.RoomId);
            }
            else
            {
                _logger.LogWarning("Failed to join room for Player {ConnectionId}", Context.ConnectionId);
            }

            return room;
        }

        private async Task SetTopic(GameRoom? gameRoom)
        {
            // If this is the first player to mark ready, ask them to set the topic
            if (gameRoom?.ReadyForGame == false  && gameRoom?.PlayerOneReady == true)
            {
                await SendMessage("RequestSetTopic");
            }
        }

        private async Task SendMessage(string message, object? data = null)
        {
            var room = await _roomManager.GetRoomByConnectionAsync(Context.ConnectionId);
            if (room != null)
            {
                await Clients.Caller.SendAsync(message, data);
                _logger.LogInformation("Send Message {message} to {connectionId} with data {data}", message, Context.ConnectionId, data);
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
            if (gameRoom?.ReadyForGame == true && gameRoom?.IsGameStarted == false && gameRoom.Questions.Count > 0)
            {
                _logger.LogInformation("All Players are ready for the game");
                await SendGroupMessage("ReceiveQuestion", gameRoom.Questions[0]);
                _logger.LogInformation("Send Question : {Index} to All Players.", 0);
            }
        }

        private async Task StartGameAfterCountdown(GameRoom? gameRoom, string topic, int? numQuestions = 4)
        {
            _logger.LogDebug("Send countdown to client");
            // Schedule game start after 60 seconds
            var roomId = gameRoom.RoomId;
            var questionsCopy = gameRoom.Questions.ToList();
            var logger = _logger;
            var roomManager = _roomManager;
            var hubContext = _hubContext;
            var quizManager = _quizManager;
            _ = Task.Run(async () =>
            {
                try
                {
                    await hubContext.Clients.Group(gameRoom?.RoomId).SendAsync("StartCountdown", 10);
                    var quizs = await quizManager.GenerateQuizAsync(topic, Math.Min(numQuestions ?? 4, 20));
                    var room = roomManager.GetGameRoomById(roomId);
                    room.Questions = quizs;
                    if (room != null && room.RoomId == roomId && quizs.Count > 0 && !room.IsGameStarted)
                    {
                        logger.LogDebug("send first question to user");
                        room.IsGameStarted = true;
                        room.ReadyForGame = true;
                        // Now send question to the group (only ready players remain)
                        await hubContext.Clients.Group(roomId).SendAsync("ReceiveQuestion", room.Questions[0]);
                        logger.LogDebug("Removed not-ready players and sent first question to group {RoomId} after countdown", roomId);
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
    }
}
