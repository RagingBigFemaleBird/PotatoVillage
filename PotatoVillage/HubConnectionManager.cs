using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;

namespace PotatoVillage
{
    public class HubConnectionManager
    {
        private HubConnection? connection;
        private Dictionary<string, object> gameDict = new();
        private int registeredGameId;
        private int registeredPlayerId;
        
        public event Action? GameStateUpdated;
        public event Action<string>? ConnectionFailed;
        public event Action<int, int>? Registered; // Fired when actually registered with actual gameId and playerId

        public int RegisteredGameId => registeredGameId;
        public int RegisteredPlayerId => registeredPlayerId;

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
                    .WithAutomaticReconnect()
                    .Build();

                connection.On<int, int, string>("Registered", (gameId, playerId, gameStateJson) =>
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

        public async Task<bool> RegisterAsync(int gameId, int playerId)
        {
            try
            {
                if (connection == null)
                {
                    ConnectionFailed?.Invoke("Not connected");
                    return false;
                }

                await connection.InvokeAsync("Register", gameId, playerId);
                return true;
            }
            catch (Exception ex)
            {
                ConnectionFailed?.Invoke($"Register failed: {ex.Message}");
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

                await connection.InvokeAsync("StartGame", gameId);
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

                await connection.InvokeAsync("UserAction", gameId, playerId, selectedTargets);
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
