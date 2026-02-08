using Microsoft.AspNetCore.SignalR;
using ProcedureCore.Core;
using ProcedureCore.LangRenSha;
using System;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Server
{
    public class GameHub : Hub
    {
        private static readonly ConcurrentDictionary<string, Game> ConnectionPlayers = new();
        private static readonly ConcurrentDictionary<int, Game> games = new();
        private static readonly ConcurrentDictionary<Game, Thread> gameThreads = new();

        // Simple DTO returned to clients when asking for targets
        public class TargetsDto
        {
            public bool DoInput { get; set; }
            public List<int> Targets { get; set; } = new();
            public int TargetsCount { get; set; }
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            ConnectionPlayers.TryRemove(Context.ConnectionId, out _);
            return base.OnDisconnectedAsync(exception);
        }

        // Client calls to declare which player this connection controls
        public Task Register(int gameId, int playerId)
        {
            Console.WriteLine($"Connection {Context.ConnectionId} registered for Game {gameId}");
            if (!games.ContainsKey(gameId))
            {
                var game = new Game(GameActionCallback);
                game.Actions.Add(new LangRenSha());
                game.Actions.Add(new LangRen());
                game.Actions.Add(new YuYanJia());
                game.Actions.Add(new NvWu());
                game.Actions.Add(new WuZhe());
                game.Actions.Add(new JiaMian());
                game.TotalPlayers = 12;
                games[gameId] = game;
            }
            ConnectionPlayers[Context.ConnectionId] = games[gameId];
            Groups.AddToGroupAsync(Context.ConnectionId, $"game-{gameId}");
            var g = games[gameId];
            if (g.PlayerToId.ContainsKey(Context.ConnectionId))
            {
                playerId = g.PlayerToId[Context.ConnectionId];
            }
            else
            {
                for (int i = 1; i <= g.TotalPlayers; i++)
                {
                    if (!g.IdToPlayer.ContainsKey(i))
                    {
                        g.PlayerToId[Context.ConnectionId] = i;
                        g.IdToPlayer[i] = Context.ConnectionId;
                        playerId = i;
                        break;
                    }
                }
            }
            return Clients.Caller.SendAsync("Registered", gameId, playerId);
        }

        public void GameActionCallback(Game game, Dictionary<string, object> stateDiff)
        {
            IHubContext<GameHub> hubContext = Server.Controllers.HomeController.GetGameHubContext();
            string updateString = JsonSerializer.Serialize(stateDiff);
            Console.WriteLine("Game state update: " + updateString);
            var gameId = 0;
            foreach (var g in games)
            {
                if (g.Value == game)
                {
                    gameId = g.Key;
                }
            }
            hubContext.Clients.Group($"game-{gameId}").SendAsync("GameStateUpdate", updateString);
        }

        public Task StartGame(int gameId)
        {
            Console.WriteLine($"Starting Game {gameId}");
            if (games.ContainsKey(gameId))
            {
                var game = games[gameId];
                if (gameThreads.ContainsKey(game))
                {
                    Console.WriteLine($"Game {gameId} is already running.");
                    return Task.CompletedTask;
                }
                var gameThread = new Thread(() => game.ActionLoop());
                gameThread.Start();
                gameThreads[game] = gameThread;
            }
            return Task.CompletedTask;
        }

        public Task UserAction(int gameId, int playerId, List<int> targets)
        {
            Console.WriteLine($"Received user action for Game {gameId}, Player {playerId}, Targets: {string.Join(", ", targets)}");
            if (games.ContainsKey(gameId))
            {
                var game = games[gameId];
                if (game.PlayerToId.TryGetValue(Context.ConnectionId, out int registeredPlayerId) && registeredPlayerId == playerId)
                {
                    ProcedureCore.Core.UserAction.UserActionRespond(game, playerId, targets);
                }
                else
                {
                    Console.WriteLine($"Player {playerId} is not registered for Game {gameId}");
                }
            }
            return Task.CompletedTask;
        }
    }
}