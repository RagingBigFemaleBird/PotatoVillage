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
        private static readonly ConcurrentDictionary<string, Game> ClientIdToGame = new();
        private static readonly ConcurrentDictionary<int, Game> games = new();
        private static readonly ConcurrentDictionary<int, string> gameOwners = new();
        private static readonly ConcurrentDictionary<Game, Thread> gameThreads = new();
        private static int nextGameId = 1;

        // Simple DTO returned to clients when asking for targets
        public class TargetsDto
        {
            public bool DoInput { get; set; }
            public List<int> Targets { get; set; } = new();
            public int TargetsCount { get; set; }
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            ClientIdToGame.TryRemove(Context.ConnectionId, out _);
            return base.OnDisconnectedAsync(exception);
        }

        private int GetNextAvailableGameId()
        {
            lock (games)
            {
                while (games.ContainsKey(nextGameId))
                {
                    nextGameId++;
                }
                return nextGameId++;
            }
        }

        // Client calls to create a room with specified player count and roles
        public Task CreateRoom(string clientId, int numberOfPlayers, Dictionary<string, int> roleDict)
        {
            int gameId = GetNextAvailableGameId();
            Console.WriteLine($"Client {clientId} creating room with Game ID {gameId}, Players: {numberOfPlayers}, Roles: {string.Join(", ", roleDict)}");
            
            var game = new Game(GameActionCallback);
            
            // Store the role configuration in the game
            game.RoleConfiguration = roleDict;
            
            // Add role actions based on roleDict
            var roleActionMap = new Dictionary<string, GameAction>
            {
                { "LangRenSha", new LangRenSha() },
                { "LangRen", new LangRen() },
                { "YuYanJia", new YuYanJia() },
                { "NvWu", new NvWu() },
                { "WuZhe", new WuZhe() },
                { "JiaMian", new JiaMian() }
            };
            
            foreach (var role in roleDict.Keys)
            {
                if (roleActionMap.TryGetValue(role, out var action))
                {
                    game.Actions.Add(action);
                }
            }
            
            game.TotalPlayers = numberOfPlayers;
            games[gameId] = game;
            gameOwners[gameId] = clientId;
            
            ClientIdToGame[clientId] = game;
            Groups.AddToGroupAsync(Context.ConnectionId, $"game-{gameId}");
            
            var g = games[gameId];
            
            // Assign player ID to this client
            int playerId = 0;
            if (g.PlayerToId.ContainsKey(clientId))
            {
                playerId = g.PlayerToId[clientId];
            }
            else
            {
                for (int i = 1; i <= g.TotalPlayers; i++)
                {
                    if (!g.IdToPlayer.ContainsKey(i))
                    {
                        g.PlayerToId[clientId] = i;
                        g.IdToPlayer[i] = clientId;
                        playerId = i;
                        break;
                    }
                }
            }
            
            var dict = games[gameId].GetGameDictionary();
            string dictString = JsonSerializer.Serialize(dict);
            return Clients.Caller.SendAsync("RoomCreated", gameId, playerId, dictString);
        }

        public Task JoinGame(string clientId, int gameId, int playerId)
        {
            Console.WriteLine($"Client {clientId} joining Game {gameId} as Player {playerId}");
            
            if (!games.ContainsKey(gameId))
            {
                Console.WriteLine($"Game {gameId} does not exist");
                return Clients.Caller.SendAsync("JoinFailed", "Game does not exist");
            }

            var game = games[gameId];
            
            // Check if player ID is already taken by someone else
            if (game.IdToPlayer.ContainsKey(playerId) && game.IdToPlayer[playerId] != clientId)
            {
                Console.WriteLine($"Player {playerId} is already taken in Game {gameId}");
                return Clients.Caller.SendAsync("JoinFailed", "Seat already taken");
            }

            // Check if player ID is valid
            if (playerId < 1 || playerId > game.TotalPlayers)
            {
                Console.WriteLine($"Invalid player ID {playerId} for Game {gameId}");
                return Clients.Caller.SendAsync("JoinFailed", "Invalid seat number");
            }

            // Assign player to this client
            ClientIdToGame[clientId] = game;
            Groups.AddToGroupAsync(Context.ConnectionId, $"game-{gameId}");
            game.PlayerToId[clientId] = playerId;
            game.IdToPlayer[playerId] = clientId;

            var dict = games[gameId].GetGameDictionary();
            string dictString = JsonSerializer.Serialize(dict);
            return Clients.Caller.SendAsync("RoomCreated", gameId, playerId, dictString);
        }

        public void GameActionCallback(Game game, Dictionary<string, object> stateDiff)
        {
            IHubContext<GameHub> hubContext = Server.Controllers.HomeController.GetGameHubContext();
            string updateString = JsonSerializer.Serialize(stateDiff);
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

        public Task StartGame(string clientId, int gameId)
        {
            Console.WriteLine($"Client {clientId} requesting to start Game {gameId}");
            
            if (!games.ContainsKey(gameId))
            {
                Console.WriteLine($"Game {gameId} does not exist");
                return Clients.Caller.SendAsync("StartGameFailed", "Game does not exist");
            }

            if (!gameOwners.TryGetValue(gameId, out var ownerId) || ownerId != clientId)
            {
                Console.WriteLine($"Client {clientId} is not the owner of Game {gameId}");
                return Clients.Caller.SendAsync("StartGameFailed", "Only the room owner can start the game");
            }

            var game = games[gameId];
            if (gameThreads.ContainsKey(game))
            {
                Console.WriteLine($"Game {gameId} is already running.");
                return Clients.Caller.SendAsync("StartGameFailed", "Game is already running");
            }

            var gameThread = new Thread(() => game.ActionLoop());
            gameThread.Start();
            gameThreads[game] = gameThread;
            Console.WriteLine($"Game {gameId} started successfully");
            return Task.CompletedTask;
        }

        public Task UserAction(string clientId, int gameId, int playerId, List<int> targets)
        {
            Console.WriteLine($"Received user action for Game {gameId}, Player {playerId}, Targets: {string.Join(", ", targets)}");
            if (games.ContainsKey(gameId))
            {
                var game = games[gameId];
                if (game.PlayerToId.TryGetValue(clientId, out int registeredPlayerId) && registeredPlayerId == playerId)
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