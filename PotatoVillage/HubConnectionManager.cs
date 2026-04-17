using Microsoft.AspNetCore.SignalR.Client;
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
        private int expectedSequenceNumber = 0;

        public event Action? GameStateUpdated;
        public event Action? RoomStateUpdated;
        public event Action? GameStarted;
        public event Action<string>? GameEnded;
        public event Action<string>? ConnectionFailed;
        public event Action<string>? SequenceMismatch; // Fired when sequence number is incorrect
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
            // Use a persistent device ID stored in preferences
            // This ensures uniqueness across all devices
            const string DeviceIdKey = "PotatoVillage_DeviceId";

            string? deviceId = Preferences.Get(DeviceIdKey, null);

            if (string.IsNullOrEmpty(deviceId))
            {
                // Generate a new unique device ID (GUID) and store it
                deviceId = Guid.NewGuid().ToString("N").Substring(0, 12);
                Preferences.Set(DeviceIdKey, deviceId);
            }

            // Combine device ID with nickname
            if (string.IsNullOrEmpty(nickname))
            {
                return deviceId;
            }
            return $"{deviceId}_{nickname}";
        }

        /// <summary>
        /// Translates known server error messages to localized strings.
        /// </summary>
        private static string TranslateServerError(string serverMessage)
        {
            var localization = Services.LocalizationManager.Instance;

            // Map known server error messages to localization keys
            return serverMessage switch
            {
                "Game does not exist" => localization.GetString("error_game_not_exist"),
                "Game has already started" => localization.GetString("error_game_already_started"),
                "Seat already taken" => localization.GetString("error_seat_taken"),
                "Invalid seat number" => localization.GetString("error_invalid_seat"),
                "Game has ended" => localization.GetString("error_game_has_ended"),
                "No available seats" => localization.GetString("error_no_available_seats"),
                _ => serverMessage // Return original message if no translation found
            };
        }

        public async Task<bool> ConnectAsync(string hubUrl)
        {
            try
            {
                // Dispose existing connection if any
                if (connection != null)
                {
                    try
                    {
                        await connection.StopAsync();
                        await connection.DisposeAsync();
                    }
                    catch { /* Ignore disposal errors */ }
                    connection = null;

                    // Wait for the server to fully process the disconnect
                    await Task.Delay(500);
                }

                // Clear previous game state
                gameDict.Clear();
                roomState.Clear();
                registeredGameId = 0;
                registeredPlayerId = 0;
                expectedSequenceNumber = 0;

                // Retry connection with exponential backoff
                int maxRetries = 3;
                int retryDelay = 500;
                Exception? lastException = null;

                for (int attempt = 0; attempt < maxRetries; attempt++)
                {
                    try
                    {
                        connection = new HubConnectionBuilder()
                            .WithUrl(hubUrl, options =>
                            {
                                // Configure transports - prefer WebSockets, fallback to LongPolling
                                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets |
                                                    Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
                                // Skip negotiation can help with some CORS issues
                                options.SkipNegotiation = false;
                            })
                            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
                            .Build();

                        // Increase timeouts to 10 minutes to prevent disconnects during inactivity
                        connection.ServerTimeout = TimeSpan.FromMinutes(10);
                        connection.KeepAliveInterval = TimeSpan.FromMinutes(5);

                        SetupConnectionHandlers();
                        await connection.StartAsync();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        System.Diagnostics.Debug.WriteLine($"Connection attempt {attempt + 1} failed: {ex.Message}");

                        if (connection != null)
                        {
                            try { await connection.DisposeAsync(); } catch { }
                            connection = null;
                        }

                        if (attempt < maxRetries - 1)
                        {
                            await Task.Delay(retryDelay);
                            retryDelay *= 2; // Exponential backoff
                        }
                    }
                }

                ConnectionFailed?.Invoke($"Connection failed after {maxRetries} attempts: {lastException?.Message}");
                return false;
            }
            catch (Exception ex)
            {
                // Clean up the failed connection
                if (connection != null)
                {
                    try { await connection.DisposeAsync(); } catch { }
                    connection = null;
                }
                ConnectionFailed?.Invoke($"Connection failed: {ex.Message}");
                return false;
            }
        }

        private void SetupConnectionHandlers()
        {
            if (connection == null) return;

            connection.Closed += async (error) =>
            {
                // Connection will auto-reconnect due to WithAutomaticReconnect
                System.Diagnostics.Debug.WriteLine($"Connection closed: {error?.Message}");
            };

            connection.Reconnected += async (connectionId) =>
            {
                // Re-join the game after reconnection
                if (registeredGameId > 0)
                {
                    await connection.InvokeAsync("JoinGame", clientId, nickname, registeredGameId, registeredPlayerId);
                }
            };

            connection.On<int, int, string, string>("RoomCreated", (gameId, playerId, gameStateJson, roomStateJson) =>
            {
                registeredGameId = gameId;
                registeredPlayerId = playerId;
                var initialState = JsonSerializer.Deserialize<Dictionary<string, object>>(gameStateJson) ?? new();

                // Initialize expected sequence number from initial state
                if (initialState.TryGetValue("sequence", out var seqObj))
                {
                    expectedSequenceNumber = GetInt32FromObject(seqObj) ?? 0;
                }
                else
                {
                    expectedSequenceNumber = 0;
                }

                MergeGameDict(initialState);
                roomState = JsonSerializer.Deserialize<Dictionary<string, object>>(roomStateJson) ?? new();

                // Check minimum version required
                if (roomState.TryGetValue("minVersionRequired", out var minVersionObj))
                {
                    int minVersionRequired = GetInt32FromObject(minVersionObj) ?? 0;
                    int currentVersion = 0;
                    if (int.TryParse(AppInfo.Current.BuildString, out var buildVersion))
                    {
                        currentVersion = buildVersion;
                    }

                    if (minVersionRequired > currentVersion)
                    {
                        // Version too old, fail the join with a special message
                        joinCompletionSource?.TrySetResult((false, $"VERSION_TOO_OLD:{minVersionRequired}"));
                        return;
                    }
                }

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
                // Translate known server error messages
                var translatedMessage = TranslateServerError(errorMessage);
                // Complete join operation with failure
                joinCompletionSource?.TrySetResult((false, translatedMessage));
            });

            connection.On<string>("GameStateUpdate", async (stateDiffJson) =>
            {
                var diff = JsonSerializer.Deserialize<Dictionary<string, object>>(stateDiffJson) ?? new();

                // Validate sequence number
                if (diff.TryGetValue("sequence", out var seqObj))
                {
                    var receivedSequence = GetInt32FromObject(seqObj) ?? 0;

                    // Check if sequence is what we expect (current + 1) or a valid reset (0 or 1)
                    if (receivedSequence != expectedSequenceNumber + 1 && receivedSequence > 1)
                    {
                        System.Diagnostics.Debug.WriteLine($"Sequence mismatch! Expected {expectedSequenceNumber + 1}, received {receivedSequence}");

                        // Disconnect and notify
                        SequenceMismatch?.Invoke($"State sync error: expected sequence {expectedSequenceNumber + 1}, got {receivedSequence}. Please rejoin the game.");
                        await Disconnect();
                        return;
                    }

                    expectedSequenceNumber = receivedSequence;
                }

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
                // Translate known server messages
                var translatedMessage = TranslateServerError(message);
                GameEnded?.Invoke(translatedMessage);
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

        public async Task<bool> CreateRoomAsync2(int numberOfPlayers, Dictionary<string, int> roleDict, int speechDuration = 120, int werewolfDuration = 60, int godDuration = 30, int roundTableMode = 0, int ownerControlEnabled = 0, int seatCounterClockwise = 0, int sheriffExtraTime = 0, int viewRoleInTurn = 0, int roleViewingGroupSize = 3)
        {
            try
            {
                if (connection == null)
                {
                    ConnectionFailed?.Invoke("Not connected");
                    return false;
                }

                int appVersion = 16;

                var gameOptions = new Dictionary<string, int>
                {
                    { "duration_speech", speechDuration },
                    { "duration_langren", werewolfDuration },
                    { "duration_player_react", godDuration },
                    { "duration_sheriff_extra_time", sheriffExtraTime },
                    { "round_table_mode", roundTableMode },
                    { "owner_control_enabled", ownerControlEnabled },
                    { "seat_counter_clockwise", seatCounterClockwise },
                    { "view_role_in_turn", viewRoleInTurn },
                    { "role_viewing_group_size", roleViewingGroupSize },
                    { "minVersionRequired", appVersion }
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

        // DTO for pending games (matches server DTO)
        public class PendingGameInfo
        {
            public int GameId { get; set; }
            public string OwnerName { get; set; } = "";
            public int TotalPlayers { get; set; }
            public int JoinedPlayers { get; set; }
        }

        public async Task<List<PendingGameInfo>> GetPendingGamesAsync()
        {
            try
            {
                if (connection == null)
                {
                    return new List<PendingGameInfo>();
                }

                var result = await connection.InvokeAsync<List<PendingGameInfo>>("GetPendingGames");
                return result ?? new List<PendingGameInfo>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetPendingGames failed: {ex.Message}");
                return new List<PendingGameInfo>();
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

        /// <summary>
        /// Checks if the connection is active. Returns true if connected.
        /// </summary>
        public bool IsConnected => connection?.State == HubConnectionState.Connected;

        /// <summary>
        /// Ensures the connection is active. If disconnected, attempts to reconnect and rejoin the game.
        /// This is useful for handling iOS background/foreground transitions.
        /// </summary>
        public async Task<bool> EnsureConnectionAsync()
        {
            if (connection == null)
            {
                System.Diagnostics.Debug.WriteLine("EnsureConnectionAsync: No connection object");
                return false;
            }

            var state = connection.State;
            System.Diagnostics.Debug.WriteLine($"EnsureConnectionAsync: Current state = {state}");

            if (state == HubConnectionState.Connected)
            {
                return true;
            }

            if (state == HubConnectionState.Connecting || state == HubConnectionState.Reconnecting)
            {
                // Wait for connection to complete
                int waitCount = 0;
                while ((connection.State == HubConnectionState.Connecting || 
                        connection.State == HubConnectionState.Reconnecting) && 
                       waitCount < 10)
                {
                    await Task.Delay(500);
                    waitCount++;
                }
                return connection.State == HubConnectionState.Connected;
            }

            // Connection is disconnected, try to reconnect
            try
            {
                System.Diagnostics.Debug.WriteLine("EnsureConnectionAsync: Attempting to reconnect...");
                await connection.StartAsync();

                // Re-join the game after reconnection
                if (registeredGameId > 0 && connection.State == HubConnectionState.Connected)
                {
                    System.Diagnostics.Debug.WriteLine($"EnsureConnectionAsync: Rejoining game {registeredGameId} as player {registeredPlayerId}");
                    await connection.InvokeAsync("JoinGame", clientId, nickname, registeredGameId, registeredPlayerId);
                }

                return connection.State == HubConnectionState.Connected;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EnsureConnectionAsync: Reconnection failed: {ex.Message}");
                return false;
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

        private int? GetInt32FromObject(object? obj)
        {
            if (obj == null) return null;
            if (obj is int intValue) return intValue;
            if (obj is long longValue) return (int)longValue;
            if (obj is JsonElement je)
            {
                if (je.ValueKind == JsonValueKind.Number) return je.GetInt32();
            }
            if (int.TryParse(obj.ToString(), out var parsed)) return parsed;
            return null;
        }
    }
}
