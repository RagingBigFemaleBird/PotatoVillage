using Microsoft.AspNetCore.SignalR.Client;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PotatoVillage
{
    public class HubConnectionManager
    {
        private HubConnection? connection;
        private Dictionary<string, object> gameDict = new();
        private Dictionary<string, object> roomState = new();
        private int registeredGameId;
        private int registeredPlayerId;
        private string clientId;
        private string nickname;
        private TaskCompletionSource<(bool, string)>? joinCompletionSource;
        private TaskCompletionSource<(bool, string)>? switchSeatCompletionSource;

        public event Action? GameStateUpdated;
        public event Action? RoomStateUpdated;
        public event Action? GameStarted;
        public event Action<string>? GameEnded;
        public event Action<string>? ConnectionFailed;
        public event Action<int, int, bool>? Registered; // Fired when actually registered with actual gameId, playerId, and gameStarted flag

        public int RegisteredGameId => registeredGameId;
        public int RegisteredPlayerId => registeredPlayerId;
        public Dictionary<string, object> RoomState => roomState;

        public HubConnectionManager(string nickname = "")
        {
            this.nickname = nickname;
            // Generate unique client ID based on machine, user, and nickname
            clientId = GenerateClientId(nickname);
        }

        private string GenerateClientId(string nickname)
        {
            var machineName = Environment.MachineName;
            var userName = Environment.UserName;
            var combined = $"{machineName}_{userName}";
            
            // Create hash of combined machine + user
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
                var hash = Convert.ToHexString(hashedBytes).Substring(0, 8);
                
                // Combine hash with nickname
                if (string.IsNullOrEmpty(nickname))
                {
                    return hash;
                }
                return $"{hash}_{nickname}";
            }
        }

        public async Task<bool> ConnectAsync(string hubUrl)
        {
            try
            {
                // Dispose existing connection if any
                if (connection != null)
                {
                    await connection.StopAsync();
                    await connection.DisposeAsync();
                }

                connection = new HubConnectionBuilder()
                    .WithUrl(hubUrl)
                    .Build();

                connection.Closed += async (error) =>
                {
                    await Task.Delay(100);
                    await connection.StartAsync();
                    await connection.InvokeAsync("JoinGame", clientId, nickname, registeredGameId, registeredPlayerId);
                };

                connection.On<int, int, string, string>("RoomCreated", (gameId, playerId, gameStateJson, roomStateJson) =>
                {
                    registeredGameId = gameId;
                    registeredPlayerId = playerId;
                    MergeGameDict(JsonSerializer.Deserialize<Dictionary<string, object>>(gameStateJson) ?? new());
                    roomState = JsonSerializer.Deserialize<Dictionary<string, object>>(roomStateJson) ?? new();

                    // Check if game has already started
                    bool gameStarted = false;
                    if (roomState.TryGetValue("gameStarted", out var gameStartedObj))
                    {
                        if (gameStartedObj is bool b) gameStarted = b;
                        else if (gameStartedObj is JsonElement je && je.ValueKind == JsonValueKind.True) gameStarted = true;
                    }

                    // Complete join operation successfully
                    joinCompletionSource?.TrySetResult((true, ""));

                    Registered?.Invoke(gameId, playerId, gameStarted);
                    GameStateUpdated?.Invoke();
                    RoomStateUpdated?.Invoke();
                });

                connection.On<string>("JoinFailed", (errorMessage) =>
                {
                    // Complete join operation with failure
                    joinCompletionSource?.TrySetResult((false, errorMessage));
                });

                connection.On<string>("GameStateUpdate", (stateDiffJson) =>
                {
                    var diff = JsonSerializer.Deserialize<Dictionary<string, object>>(stateDiffJson) ?? new();
                    MergeGameDict(diff);
                    GameStateUpdated?.Invoke();
                });

                connection.On<string>("RoomStateUpdate", (roomStateJson) =>
                {
                    roomState = JsonSerializer.Deserialize<Dictionary<string, object>>(roomStateJson) ?? new();
                    RoomStateUpdated?.Invoke();
                });

                connection.On("GameStarted", () =>
                {
                    GameStarted?.Invoke();
                });

                connection.On<string>("GameEnded", (message) =>
                {
                    GameEnded?.Invoke(message);
                });

                connection.On<int>("SeatSwitched", (newPlayerId) =>
                {
                    registeredPlayerId = newPlayerId;
                    switchSeatCompletionSource?.TrySetResult((true, ""));
                });

                connection.On<string>("SwitchSeatFailed", (errorMessage) =>
                {
                    switchSeatCompletionSource?.TrySetResult((false, errorMessage));
                });

                await connection.StartAsync();
                return true;
            }
            catch (Exception ex)
            {
                // Clean up the failed connection
                if (connection != null)
                {
                    await connection.DisposeAsync();
                    connection = null;
                }
                ConnectionFailed?.Invoke($"Connection failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CreateRoomAsync(int numberOfPlayers, Dictionary<string, int> roleDict, int speechDuration = 120, int werewolfDuration = 60, int godDuration = 30)
        {
            try
            {
                if (connection == null)
                {
                    ConnectionFailed?.Invoke("Not connected");
                    return false;
                }

                var gameOptions = new Dictionary<string, int>
                {
                    { "duration_speech", speechDuration },
                    { "duration_langren", werewolfDuration },
                    { "duration_player_react", godDuration }
                };

                await connection.InvokeAsync("CreateRoom", clientId, nickname, numberOfPlayers, roleDict, gameOptions);
                return true;
            }
            catch (Exception ex)
            {
                ConnectionFailed?.Invoke($"Create room failed: {ex.Message}");
                return false;
            }
        }

        public async Task<(bool success, string errorMessage)> JoinGameAsync(int gameId, int playerId)
        {
            try
            {
                if (connection == null)
                {
                    return (false, "Not connected");
                }

                // Create a completion source to wait for the result
                joinCompletionSource = new TaskCompletionSource<(bool, string)>();

                await connection.InvokeAsync("JoinGame", clientId, nickname, gameId, playerId);

                // Wait for either RoomCreated or JoinFailed with timeout
                var timeoutTask = Task.Delay(10000);
                var completedTask = await Task.WhenAny(joinCompletionSource.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    return (false, "Join request timed out");
                }

                return await joinCompletionSource.Task;
            }
            catch (Exception ex)
            {
                return (false, $"Join game failed: {ex.Message}");
            }
        }

        public async Task<bool> StartGameAsync(int gameId)
        {
            try
            {
                if (connection == null)
                {
                    ConnectionFailed?.Invoke("Not connected");
                    return false;
                }

                await connection.InvokeAsync("StartGame", clientId, gameId);
                return true;
            }
            catch (Exception ex)
            {
                ConnectionFailed?.Invoke($"Start game failed: {ex.Message}");
                return false;
            }
        }

        public async Task<(bool success, string errorMessage)> SwitchSeatAsync(int gameId, int newPlayerId)
        {
            try
            {
                if (connection == null)
                {
                    return (false, "Not connected");
                }

                // Create a completion source to wait for the result
                switchSeatCompletionSource = new TaskCompletionSource<(bool, string)>();

                await connection.InvokeAsync("SwitchSeat", clientId, gameId, newPlayerId);

                // Wait for either SeatSwitched or SwitchSeatFailed with timeout
                var timeoutTask = Task.Delay(10000);
                var completedTask = await Task.WhenAny(switchSeatCompletionSource.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    return (false, "Switch seat request timed out");
                }

                return await switchSeatCompletionSource.Task;
            }
            catch (Exception ex)
            {
                return (false, $"Switch seat failed: {ex.Message}");
            }
        }

        public async Task LeaveGameAsync(int gameId)
        {
            try
            {
                if (connection != null)
                {
                    await connection.InvokeAsync("LeaveGame", clientId, gameId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Leave game failed: {ex.Message}");
            }
        }

        public async Task Disconnect()
        {
            if (connection != null)
            {
                await connection.StopAsync();
                await connection.DisposeAsync();
                connection = null;
            }
        }

        public Dictionary<string, object> GetGameDictionary()
        {
            return gameDict;
        }

        public async Task SendTargetSelectionAsync(int gameId, int playerId, List<int> selectedTargets)
        {
            try
            {
                if (connection == null)
                {
                    throw new InvalidOperationException("Not connected");
                }

                await connection.InvokeAsync("UserAction", clientId, gameId, playerId, selectedTargets);
            }
            catch (Exception ex)
            {
                ConnectionFailed?.Invoke($"Send targets failed: {ex.Message}");
                throw;
            }
        }

        private void MergeGameDict(Dictionary<string, object> diff)
        {
            foreach (var kv in diff)
            {
                if (kv.Value == null)
                {
                    gameDict.Remove(kv.Key);
                }
                else
                {
                    gameDict[kv.Key] = kv.Value;
                }
            }
        }
    }
}
