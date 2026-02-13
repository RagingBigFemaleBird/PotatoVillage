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
        private int registeredGameId;
        private int registeredPlayerId;
        private string clientId;
        
        public event Action? GameStateUpdated;
        public event Action<string>? ConnectionFailed;
        public event Action<int, int>? Registered; // Fired when actually registered with actual gameId and playerId

        public int RegisteredGameId => registeredGameId;
        public int RegisteredPlayerId => registeredPlayerId;

        public HubConnectionManager(string nickname = "")
        {
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
                    await connection.InvokeAsync("JoinGame", clientId, registeredGameId, registeredPlayerId);
                };

                connection.On<int, int, string>("RoomCreated", (gameId, playerId, gameStateJson) =>
                {
                    registeredGameId = gameId;
                    registeredPlayerId = playerId;
                    MergeGameDict(JsonSerializer.Deserialize<Dictionary<string, object>>(gameStateJson) ?? new());
                    Registered?.Invoke(gameId, playerId);
                    GameStateUpdated?.Invoke();
                });

                connection.On<string>("GameStateUpdate", (stateDiffJson) =>
                {
                    var diff = JsonSerializer.Deserialize<Dictionary<string, object>>(stateDiffJson) ?? new();
                    MergeGameDict(diff);
                    GameStateUpdated?.Invoke();
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

        public async Task<bool> CreateRoomAsync(int numberOfPlayers, Dictionary<string, int> roleDict)
        {
            try
            {
                if (connection == null)
                {
                    ConnectionFailed?.Invoke("Not connected");
                    return false;
                }

                await connection.InvokeAsync("CreateRoom", clientId, numberOfPlayers, roleDict);
                return true;
            }
            catch (Exception ex)
            {
                ConnectionFailed?.Invoke($"Create room failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> JoinGameAsync(int gameId, int playerId)
        {
            try
            {
                if (connection == null)
                {
                    ConnectionFailed?.Invoke("Not connected");
                    return false;
                }

                await connection.InvokeAsync("JoinGame", clientId, gameId, playerId);
                return true;
            }
            catch (Exception ex)
            {
                ConnectionFailed?.Invoke($"Join game failed: {ex.Message}");
                return false;
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

        public async Task Disconnect()
        {
            if (connection != null)
            {
                await connection.StopAsync();
                await connection.DisposeAsync();
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
