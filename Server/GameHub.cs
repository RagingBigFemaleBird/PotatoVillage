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
        private static readonly ConcurrentDictionary<string, string> ConnectionIdToClientId = new();
        private static readonly ConcurrentDictionary<string, int> ClientIdToGameId = new();
        private static readonly ConcurrentDictionary<int, Game> games = new();
        private static readonly ConcurrentDictionary<int, string> gameOwners = new();
        private static readonly ConcurrentDictionary<Game, Thread> gameThreads = new();
        private static int nextGameId = 1;

        // Public static methods to get game statistics
        public static int GetTotalGamesCount() => games.Count;
        public static int GetActiveGamesCount() => games.Values.Count(g => g.GameStarted);
        public static int GetWaitingGamesCount() => games.Values.Count(g => !g.GameStarted);

        // Simple DTO returned to clients when asking for targets
        public class TargetsDto
        {
            public bool DoInput { get; set; }
            public List<int> Targets { get; set; } = new();
            public int TargetsCount { get; set; }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // Get the clientId associated with this connection
            if (ConnectionIdToClientId.TryRemove(Context.ConnectionId, out var clientId))
            {
                await RemoveClientFromGame(clientId);
            }
            await base.OnDisconnectedAsync(exception);
        }

        private async Task RemoveClientFromGame(string clientId)
        {
            // Remove client from game
            if (ClientIdToGame.TryRemove(clientId, out var game) && 
                ClientIdToGameId.TryRemove(clientId, out var gameId))
            {
                // Only clean up if game hasn't started yet
                if (!game.GameStarted)
                {
                    if (game.PlayerToId.TryRemove(clientId, out var playerId))
                    {
                        game.IdToPlayer.TryRemove(playerId, out _);
                        game.PlayerNicknames.TryRemove(playerId, out _);

                        Console.WriteLine($"Client {clientId} (Player {playerId}) removed from Game {gameId}");

                        // Notify remaining clients about the room state update
                        string roomState = GetRoomStateJson(gameId);
                        IHubContext<GameHub> hubContext = Server.Controllers.HomeController.GetGameHubContext();
                        await hubContext.Clients.Group($"game-{gameId}").SendAsync("RoomStateUpdate", roomState);
                    }
                }
            }
        }

        public async Task LeaveGame(string clientId, int gameId)
        {
            Console.WriteLine($"Client {clientId} leaving Game {gameId}");

            // Remove connection mapping
            foreach (var kvp in ConnectionIdToClientId.Where(x => x.Value == clientId).ToList())
            {
                ConnectionIdToClientId.TryRemove(kvp.Key, out _);
            }

            await RemoveClientFromGame(clientId);

            // Remove from SignalR group
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"game-{gameId}");
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
        public Task CreateRoom(string clientId, string nickname, int numberOfPlayers, Dictionary<string, int> roleDict, Dictionary<string, int>? gameOptions = null)
        {
            int gameId = GetNextAvailableGameId();
            var optionsStr = gameOptions != null ? string.Join(", ", gameOptions.Select(kv => $"{kv.Key}={kv.Value}")) : "none";
            Console.WriteLine($"Client {clientId} ({nickname}) creating room with Game ID {gameId}, Players: {numberOfPlayers}, Roles: {string.Join(", ", roleDict)}, Options: {optionsStr}");

            // Track connection to client mapping
            ConnectionIdToClientId[Context.ConnectionId] = clientId;

            var game = new Game(GameActionCallback);

            // Store the role configuration in the game
            game.RoleConfiguration = roleDict;

            // Apply game options if provided
            if (gameOptions != null)
            {
                var optionsDict = new Dictionary<string, object>();
                foreach (var item in gameOptions)
                {
                    optionsDict[item.Key] = item.Value;
                }
                game.StateUpdate(optionsDict);
            }

            // Add role actions based on roleDict
            var roleActionMap = new Dictionary<string, GameAction>
            {
                { "LangRenSha", new LangRenSha() },
                { "LangRen", new LangRen() },
                { "YuYanJia", new YuYanJia() },
                { "NvWu", new NvWu() },
                { "WuZhe", new WuZhe() },
                { "JiaMian", new JiaMian() },
                { "LieRen", new LieRen() },
                { "BaiChi", new BaiChi() },
                { "LaoShu", new LaoShu() },
                { "DaMao", new DaMao() },
            };

            if (!roleDict.ContainsKey("LangRen"))
            {
                game.Actions.Add(new LangRen());
            }
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
            ClientIdToGameId[clientId] = gameId;
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
                        g.PlayerNicknames[i] = nickname;
                        playerId = i;
                        break;
                    }
                }
            }

            var dict = games[gameId].GetGameDictionary();
            string dictString = JsonSerializer.Serialize(dict);
            string roomState = GetRoomStateJson(gameId);
            return Clients.Caller.SendAsync("RoomCreated", gameId, playerId, dictString, roomState);
        }

        public async Task JoinGame(string clientId, string nickname, int gameId, int playerId)
        {
            Console.WriteLine($"Client {clientId} ({nickname}) joining Game {gameId} as Player {playerId}");

            // Track connection to client mapping
            ConnectionIdToClientId[Context.ConnectionId] = clientId;

            if (!games.ContainsKey(gameId))
            {
                Console.WriteLine($"Game {gameId} does not exist");
                await Clients.Caller.SendAsync("JoinFailed", "Game does not exist");
                return;
            }

            var game = games[gameId];

            // Check if this client is already in the game (reconnection case)
            bool isReconnection = game.PlayerToId.ContainsKey(clientId);

            // If game has started, only allow reconnection for existing players
            if (game.GameStarted)
            {
                if (!isReconnection)
                {
                    Console.WriteLine($"Game {gameId} has already started and client is not a member");
                    await Clients.Caller.SendAsync("JoinFailed", "Game has already started");
                    return;
                }

                // Reconnection - get the player's actual seat
                playerId = game.PlayerToId[clientId];
                Console.WriteLine($"Client {clientId} reconnecting to Game {gameId} as Player {playerId}");
            }
            else
            {
                // Game not started - normal join logic

                // Check if player ID is already taken by someone else
                if (game.IdToPlayer.ContainsKey(playerId) && game.IdToPlayer[playerId] != clientId)
                {
                    Console.WriteLine($"Player {playerId} is already taken in Game {gameId}");
                    await Clients.Caller.SendAsync("JoinFailed", "Seat already taken");
                    return;
                }

                // Check if player ID is valid
                if (playerId < 1 || playerId > game.TotalPlayers)
                {
                    Console.WriteLine($"Invalid player ID {playerId} for Game {gameId}");
                    await Clients.Caller.SendAsync("JoinFailed", "Invalid seat number");
                    return;
                }

                // Assign player to this client
                game.PlayerToId[clientId] = playerId;
                game.IdToPlayer[playerId] = clientId;
                game.PlayerNicknames[playerId] = nickname;
            }

            // Update tracking
            ClientIdToGame[clientId] = game;
            ClientIdToGameId[clientId] = gameId;
            await Groups.AddToGroupAsync(Context.ConnectionId, $"game-{gameId}");

            var dict = games[gameId].GetGameDictionary();
            string dictString = JsonSerializer.Serialize(dict);
            string roomState = GetRoomStateJson(gameId);

            // Notify the joining client
            await Clients.Caller.SendAsync("RoomCreated", gameId, playerId, dictString, roomState);

            // Notify all other clients in the room about the room state update (only if game hasn't started)
            if (!game.GameStarted)
            {
                await Clients.Group($"game-{gameId}").SendAsync("RoomStateUpdate", roomState);
            }
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

        public async Task StartGame(string clientId, int gameId)
        {
            Console.WriteLine($"Client {clientId} requesting to start Game {gameId}");

            if (!games.ContainsKey(gameId))
            {
                Console.WriteLine($"Game {gameId} does not exist");
                await Clients.Caller.SendAsync("StartGameFailed", "Game does not exist");
                return;
            }

            if (!gameOwners.TryGetValue(gameId, out var ownerId) || ownerId != clientId)
            {
                Console.WriteLine($"Client {clientId} is not the owner of Game {gameId}");
                await Clients.Caller.SendAsync("StartGameFailed", "Only the room owner can start the game");
                return;
            }

            var game = games[gameId];

            // Check if all players have joined
            if (game.IdToPlayer.Count < game.TotalPlayers)
            {
                Console.WriteLine($"Game {gameId} cannot start: only {game.IdToPlayer.Count}/{game.TotalPlayers} players have joined");
                await Clients.Caller.SendAsync("StartGameFailed", $"Not all players have joined ({game.IdToPlayer.Count}/{game.TotalPlayers})");
                return;
            }

            if (gameThreads.ContainsKey(game))
            {
                Console.WriteLine($"Game {gameId} is already running.");
                await Clients.Caller.SendAsync("StartGameFailed", "Game is already running");
                return;
            }

            game.GameStarted = true;

            // Notify all clients that game has started
            await Clients.Group($"game-{gameId}").SendAsync("GameStarted");

            var gameThread = new Thread(() => 
            {
                try
                {
                    game.ActionLoop();
                }
                finally
                {
                    // Clean up when game thread exits (either normally or due to exception)
                    CleanupGame(gameId);
                }
            });
            gameThread.Start();
            gameThreads[game] = gameThread;
            Console.WriteLine($"Game {gameId} started successfully");
        }

        private static void CleanupGame(int gameId)
        {
            Console.WriteLine($"Game {gameId} thread exiting, cleaning up...");

            if (!games.TryRemove(gameId, out var game))
            {
                Console.WriteLine($"Game {gameId} not found during cleanup");
                return;
            }

            // Remove game thread tracking
            gameThreads.TryRemove(game, out _);

            // Remove game owner
            gameOwners.TryRemove(gameId, out _);

            // Get all clients in this game and clean them up
            var clientsToRemove = new List<string>();
            foreach (var kvp in ClientIdToGameId)
            {
                if (kvp.Value == gameId)
                {
                    clientsToRemove.Add(kvp.Key);
                }
            }

            foreach (var clientId in clientsToRemove)
            {
                ClientIdToGame.TryRemove(clientId, out _);
                ClientIdToGameId.TryRemove(clientId, out _);

                // Remove connection mappings
                foreach (var connKvp in ConnectionIdToClientId.Where(x => x.Value == clientId).ToList())
                {
                    ConnectionIdToClientId.TryRemove(connKvp.Key, out _);
                }
            }

            // Notify all clients in the game that it has ended
            IHubContext<GameHub> hubContext = Server.Controllers.HomeController.GetGameHubContext();
            hubContext.Clients.Group($"game-{gameId}").SendAsync("GameEnded", "Game has ended");

            Console.WriteLine($"Game {gameId} cleanup complete. Removed {clientsToRemove.Count} clients.");
        }

        public async Task SwitchSeat(string clientId, int gameId, int newPlayerId)
        {
            Console.WriteLine($"Client {clientId} requesting to switch to seat {newPlayerId} in Game {gameId}");

            if (!games.ContainsKey(gameId))
            {
                await Clients.Caller.SendAsync("SwitchSeatFailed", "Game does not exist");
                return;
            }

            var game = games[gameId];

            // Check if game has already started
            if (game.GameStarted)
            {
                await Clients.Caller.SendAsync("SwitchSeatFailed", "Cannot switch seats after game has started");
                return;
            }

            // Check if new seat is valid
            if (newPlayerId < 1 || newPlayerId > game.TotalPlayers)
            {
                await Clients.Caller.SendAsync("SwitchSeatFailed", "Invalid seat number");
                return;
            }

            // Check if new seat is already taken
            if (game.IdToPlayer.ContainsKey(newPlayerId))
            {
                await Clients.Caller.SendAsync("SwitchSeatFailed", "Seat already taken");
                return;
            }

            // Get current seat
            if (!game.PlayerToId.TryGetValue(clientId, out int oldPlayerId))
            {
                await Clients.Caller.SendAsync("SwitchSeatFailed", "You are not in this game");
                return;
            }

            // Get nickname
            string nickname = game.PlayerNicknames.TryGetValue(oldPlayerId, out var n) ? n : "";

            // Remove from old seat
            game.IdToPlayer.TryRemove(oldPlayerId, out _);
            game.PlayerNicknames.TryRemove(oldPlayerId, out _);

            // Assign to new seat
            game.PlayerToId[clientId] = newPlayerId;
            game.IdToPlayer[newPlayerId] = clientId;
            game.PlayerNicknames[newPlayerId] = nickname;

            string roomState = GetRoomStateJson(gameId);

            // Notify the switching client of their new seat
            await Clients.Caller.SendAsync("SeatSwitched", newPlayerId);

            // Notify all clients in the room about the room state update
            await Clients.Group($"game-{gameId}").SendAsync("RoomStateUpdate", roomState);
        }

        private string GetRoomStateJson(int gameId)
        {
            if (!games.ContainsKey(gameId))
                return "{}";

            var game = games[gameId];
            var roomState = new Dictionary<string, object>
            {
                { "totalPlayers", game.TotalPlayers },
                { "gameStarted", game.GameStarted },
                { "players", game.PlayerNicknames.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value) }
            };

            // Add owner info
            if (gameOwners.TryGetValue(gameId, out var ownerId) && game.PlayerToId.TryGetValue(ownerId, out var ownerPlayerId))
            {
                roomState["ownerId"] = ownerPlayerId;
            }

            return JsonSerializer.Serialize(roomState);
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