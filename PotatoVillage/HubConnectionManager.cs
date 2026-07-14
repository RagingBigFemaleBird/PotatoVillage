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
        // When true, sequence-mismatch checking is suspended because we are
        // waiting for a fresh RoomCreated snapshot to re-establish the
        // authoritative sequence number (e.g. just after auto-reconnect).
        private volatile bool awaitingSnapshot = false;
        // Serializes resync attempts so a Window.Resumed event, a page
        // OnAppearing and the Closed self-heal loop can't restart/rejoin the
        // same connection concurrently.
        private readonly SemaphoreSlim resyncLock = new(1, 1);

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

                // Retry connection with exponential backoff. Each attempt
                // first tries WebSockets-only; if that fails, the same attempt
                // is repeated allowing LongPolling as a fallback. We prefer
                // WebSockets because LongPolling on iOS over cellular/VPN can
                // stall during message bursts (e.g. role distribution at game
                // start), causing perceived "frozen" games.
                int maxRetries = 3;
                int retryDelay = 500;
                Exception? lastException = null;

                for (int attempt = 0; attempt < maxRetries; attempt++)
                {
                    foreach (var transports in new[]
                    {
                        Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets,
                        Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets |
                            Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling,
                    })
                    {
                        try
                        {
                            connection = new HubConnectionBuilder()
                                .WithUrl(hubUrl, options =>
                                {
                                    options.Transports = transports;
                                    // Skip negotiation can help with some CORS issues
                                    options.SkipNegotiation = false;
                                })
                                .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
                                .Build();

                            // Use timeouts close to SignalR defaults. Long
                            // KeepAlive/ServerTimeout values cause iOS to silently
                            // drop the underlying socket (carrier NAT / Low Power
                            // Mode / VPNs close idle TCP after 30s-2min) without
                            // SignalR noticing for many minutes; the server then
                            // fire-and-forgets game-state updates into the void.
                            connection.ServerTimeout = TimeSpan.FromSeconds(30);
                            connection.KeepAliveInterval = TimeSpan.FromSeconds(15);

                            SetupConnectionHandlers();
                            await connection.StartAsync();

                            bool wsOnly = transports == Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets;
                            System.Diagnostics.Debug.WriteLine(
                                $"SignalR connected (attempt {attempt + 1}, transport={(wsOnly ? "WebSockets" : "WebSockets+LongPolling fallback")})");
                            return true;
                        }
                        catch (Exception ex)
                        {
                            lastException = ex;
                            bool wsOnly = transports == Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets;
                            System.Diagnostics.Debug.WriteLine(
                                $"Connection attempt {attempt + 1} ({(wsOnly ? "WS-only" : "WS+LongPolling")}) failed: {ex.Message}");

                            if (connection != null)
                            {
                                try { await connection.DisposeAsync(); } catch { }
                                connection = null;
                            }
                        }
                    }

                    if (attempt < maxRetries - 1)
                    {
                        await Task.Delay(retryDelay);
                        retryDelay *= 2; // Exponential backoff
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
            // Capture the instance the handlers belong to: the `connection`
            // field can be swapped/nulled (ConnectAsync, Disconnect) while a
            // handler is still running.
            var conn = connection;
            if (conn == null) return;

            conn.Closed += async (error) =>
            {
                // Closed only fires after WithAutomaticReconnect has given up
                // (all retries span ~17s) or after an intentional Stop. If the
                // app was frozen in the background (Android app freezer, iOS
                // suspension) the retries can all burn before the network is
                // back, leaving the connection permanently dead with nothing
                // to restart it - the UI then silently stops updating. Keep
                // trying to self-heal while we are still the active connection
                // for an ongoing game.
                System.Diagnostics.Debug.WriteLine($"Connection closed: {error?.Message}");

                if (registeredGameId <= 0)
                {
                    return;
                }

                foreach (var delaySeconds in new[] { 1, 2, 5, 10, 15, 15, 15, 15 })
                {
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));

                    // Stop if this connection was replaced or intentionally
                    // disconnected, or if something else already healed it.
                    if (!ReferenceEquals(connection, conn))
                    {
                        return;
                    }
                    if (conn.State == HubConnectionState.Connected)
                    {
                        return;
                    }

                    if (await EnsureConnectionAsync())
                    {
                        return;
                    }
                }
            };

            conn.Reconnected += async (connectionId) =>
            {
                // Re-join the game after reconnection. The server will reply
                // with a fresh RoomCreated snapshot whose `sequence` becomes
                // the new authoritative baseline. Until that snapshot arrives,
                // any GameStateUpdate that races in would falsely trigger the
                // sequence-mismatch guard (we are mid-game, so the next valid
                // sequence is NOT 1), so suspend the check here.
                if (registeredGameId > 0)
                {
                    awaitingSnapshot = true;
                    // SendAsync (not InvokeAsync): no need to wait for the hub
                    // method, the RoomCreated snapshot arrives as its own message.
                    await conn.SendAsync("JoinGame", clientId, nickname, registeredGameId, registeredPlayerId);
                }
            };

            conn.On<int, int, string, string>("RoomCreated", (gameId, playerId, gameStateJson, roomStateJson) =>
            {
                // Only announce a *new* registration. RoomCreated is also the
                // resync snapshot sent on every rejoin (auto-reconnect, resume
                // from background); re-firing Registered then would make
                // MainPage push a duplicate GameView/RoomView page.
                bool isInitialRegistration = registeredGameId != gameId;

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

                // Snapshot received - resume sequence-mismatch checking.
                awaitingSnapshot = false;

                // The snapshot is the complete authoritative state: REPLACE the
                // local dictionary instead of merging into it. Merging keeps
                // keys the server deleted while we were disconnected (deletions
                // travel as null values in diffs we never received), which
                // makes the UI show stale moves/buttons after a resume.
                gameDict = initialState;
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

                if (isInitialRegistration)
                {
                    Registered?.Invoke(gameId, playerId, gameStarted);
                }
                GameStateUpdated?.Invoke();
                RoomStateUpdated?.Invoke();
            });

            conn.On<string>("JoinFailed", (errorMessage) =>
            {
                // Translate known server error messages
                var translatedMessage = TranslateServerError(errorMessage);
                // Complete join operation with failure
                joinCompletionSource?.TrySetResult((false, translatedMessage));
            });

            conn.On<string>("GameStateUpdate", async (stateDiffJson) =>
            {
                var diff = JsonSerializer.Deserialize<Dictionary<string, object>>(stateDiffJson) ?? new();

                // Validate sequence number
                if (diff.TryGetValue("sequence", out var seqObj))
                {
                    var receivedSequence = GetInt32FromObject(seqObj) ?? 0;

                    // Skip the check while waiting for a post-reconnect snapshot:
                    // the authoritative sequence will be re-established by the
                    // upcoming RoomCreated message. Just adopt whatever arrives
                    // so once the snapshot lands we continue from the right spot.
                    if (awaitingSnapshot)
                    {
                        expectedSequenceNumber = receivedSequence;
                    }
                    else
                    {
                        // Check if sequence is what we expect (current + 1) or a valid reset (0 or 1)
                        if (receivedSequence != expectedSequenceNumber + 1 && receivedSequence > 1)
                        {
                            System.Diagnostics.Debug.WriteLine($"Sequence mismatch! Expected {expectedSequenceNumber + 1}, received {receivedSequence}");

                            // A gap means we missed one or more diffs (message
                            // lost around a background/foreground transition or
                            // a reconnect). A fresh snapshot fully repairs the
                            // state, so request one in place before resorting
                            // to kicking the player back to the lobby.
                            bool repairRequested = false;
                            if (registeredGameId > 0 && conn.State == HubConnectionState.Connected)
                            {
                                try
                                {
                                    awaitingSnapshot = true;
                                    // Must be SendAsync, not InvokeAsync: this runs on
                                    // the client's message-dispatch loop, and awaiting
                                    // an invocation completion here would deadlock it.
                                    await conn.SendAsync("JoinGame", clientId, nickname, registeredGameId, registeredPlayerId);
                                    repairRequested = true;
                                    System.Diagnostics.Debug.WriteLine("Sequence mismatch: requested fresh snapshot via rejoin");
                                }
                                catch (Exception ex)
                                {
                                    awaitingSnapshot = false;
                                    System.Diagnostics.Debug.WriteLine($"Sequence mismatch: rejoin failed: {ex.Message}");
                                }
                            }

                            if (!repairRequested)
                            {
                                // Disconnect and notify
                                SequenceMismatch?.Invoke($"State sync error: expected sequence {expectedSequenceNumber + 1}, got {receivedSequence}. Please rejoin the game.");
                                await Disconnect();
                            }
                            return;
                        }

                        expectedSequenceNumber = receivedSequence;
                    }
                }

                MergeGameDict(diff);
                GameStateUpdated?.Invoke();
            });

            conn.On<string>("RoomStateUpdate", (roomStateJson) =>
            {
                roomState = JsonSerializer.Deserialize<Dictionary<string, object>>(roomStateJson) ?? new();
                RoomStateUpdated?.Invoke();
            });

            conn.On("GameStarted", () =>
            {
                GameStarted?.Invoke();
            });

            conn.On<string>("GameEnded", (message) =>
            {
                // Translate known server messages
                var translatedMessage = TranslateServerError(message);
                GameEnded?.Invoke(translatedMessage);
            });

            conn.On<int>("SeatSwitched", (newPlayerId) =>
            {
                registeredPlayerId = newPlayerId;
                switchSeatCompletionSource?.TrySetResult((true, ""));
            });

            conn.On<string>("SwitchSeatFailed", (errorMessage) =>
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

                int appVersion = 18;
                int appVersionSys = int.MaxValue;

                if (int.TryParse(AppInfo.Current.BuildString, out appVersionSys))
                {
                    appVersion = Math.Min(appVersion, appVersionSys);
                }

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
            // Atomically take ownership of the connection so concurrent callers
            // (e.g. GameStateUpdate sequence-mismatch handler + OnLeaveRoomClicked)
            // don't both try to stop/dispose the same instance.
            var conn = System.Threading.Interlocked.Exchange(ref connection, null);
            if (conn == null)
            {
                return;
            }

            try
            {
                await conn.StopAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Disconnect: StopAsync failed: {ex.Message}");
            }

            try
            {
                await conn.DisposeAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Disconnect: DisposeAsync failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if the connection is active. Returns true if connected.
        /// </summary>
        public bool IsConnected => connection?.State == HubConnectionState.Connected;

        /// <summary>
        /// Ensures the connection is alive AND the local game state is in sync
        /// with the server. Called when the app returns from the background on
        /// iOS/Android (and on page re-appearance).
        ///
        /// Important: after an OS suspension, HubConnection.State can still
        /// report Connected while the underlying socket is dead (the server or
        /// a NAT dropped it while the app was frozen, and the client hasn't
        /// noticed yet). We therefore never trust State alone - when we are
        /// registered in a game we always perform a verified round-trip rejoin,
        /// which (a) proves the transport is alive, (b) restores the SignalR
        /// group membership if the server had dropped us, and (c) yields a
        /// fresh authoritative snapshot that repairs any missed updates.
        /// </summary>
        public async Task<bool> EnsureConnectionAsync()
        {
            var conn = connection;
            if (conn == null)
            {
                System.Diagnostics.Debug.WriteLine("EnsureConnectionAsync: No connection object");
                return false;
            }

            await resyncLock.WaitAsync();
            try
            {
                if (!ReferenceEquals(connection, conn))
                {
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"EnsureConnectionAsync: Current state = {conn.State}");

                // Give an in-flight automatic reconnect a chance to finish.
                // The retry schedule spans ~17s (0/2/5/10), so wait slightly
                // past the final attempt before treating it as failed.
                int waitedMs = 0;
                while ((conn.State == HubConnectionState.Connecting ||
                        conn.State == HubConnectionState.Reconnecting) &&
                       waitedMs < 15000)
                {
                    await Task.Delay(500);
                    waitedMs += 500;
                    if (!ReferenceEquals(connection, conn))
                    {
                        return false;
                    }
                }

                if (conn.State == HubConnectionState.Connected)
                {
                    // Not in a game: nothing to resync, being connected is enough.
                    if (registeredGameId <= 0)
                    {
                        return true;
                    }

                    // Verify liveness + resync with a real round trip.
                    if (await TryRejoinAsync(conn))
                    {
                        return true;
                    }

                    // Round trip failed => zombie connection. Tear it down and
                    // fall through to a manual restart.
                    System.Diagnostics.Debug.WriteLine("EnsureConnectionAsync: Connection is a zombie, restarting...");
                    try { await conn.StopAsync(); } catch { /* already dead */ }
                }

                // Connection is (now) disconnected: restart manually. The
                // network can take a few seconds to come back after a resume,
                // so retry a couple of times.
                bool started = false;
                for (int attempt = 0; attempt < 3 && !started; attempt++)
                {
                    if (!ReferenceEquals(connection, conn))
                    {
                        return false;
                    }

                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"EnsureConnectionAsync: StartAsync attempt {attempt + 1}...");
                        await conn.StartAsync();
                        started = conn.State == HubConnectionState.Connected;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"EnsureConnectionAsync: StartAsync failed: {ex.Message}");
                        if (attempt < 2)
                        {
                            await Task.Delay(1000 * (attempt + 1));
                        }
                    }
                }

                if (!started)
                {
                    return false;
                }

                if (registeredGameId <= 0)
                {
                    return true;
                }

                return await TryRejoinAsync(conn);
            }
            finally
            {
                resyncLock.Release();
            }
        }

        /// <summary>
        /// Performs a round-trip rejoin on an (apparently) connected hub and
        /// waits until the fresh RoomCreated snapshot has actually been
        /// received and applied. Returns false if the server does not answer
        /// in time (dead transport) or the rejoin is rejected (e.g. the game
        /// ended while the app was in the background).
        /// </summary>
        private async Task<bool> TryRejoinAsync(HubConnection conn)
        {
            var tcs = new TaskCompletionSource<(bool, string)>(TaskCreationOptions.RunContinuationsAsynchronously);
            joinCompletionSource = tcs;
            awaitingSnapshot = true;

            try
            {
                System.Diagnostics.Debug.WriteLine($"TryRejoinAsync: Rejoining game {registeredGameId} as player {registeredPlayerId}");

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await conn.InvokeAsync("JoinGame", clientId, nickname, registeredGameId, registeredPlayerId, cts.Token);

                // The invoke completing only proves the request went out; wait
                // for the RoomCreated snapshot (or JoinFailed) to come back.
                var completed = await Task.WhenAny(tcs.Task, Task.Delay(5000));
                if (completed != tcs.Task)
                {
                    System.Diagnostics.Debug.WriteLine("TryRejoinAsync: Timed out waiting for snapshot");
                    return false;
                }

                var (success, error) = await tcs.Task;
                if (!success)
                {
                    System.Diagnostics.Debug.WriteLine($"TryRejoinAsync: Rejoin rejected: {error}");
                }
                return success;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TryRejoinAsync: Failed: {ex.Message}");
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
