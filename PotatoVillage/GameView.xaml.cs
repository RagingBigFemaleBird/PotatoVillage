using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text;
using System.Linq;
using Microsoft.Maui.Controls;
using PotatoVillage.Services;

namespace PotatoVillage
{
    public partial class GameView : ContentPage
    {
        private HubConnectionManager? connectionManager;
        private int playerId;
        private int gameId;
        private bool isOwner;
        private HashSet<int> selectedTargets = new();
        private CancellationTokenSource? countdownCts;
        private bool announcerEnabled = false; // Client-only setting, default off
        private bool warningBeepPlayed = false; // Track if 15-second warning beep was played
        private int serverTimeOffset = 0; // Offset between server and client clocks (server_time - client_time)
        private int lastServerTime = 0;

        // Track currently displayed target selection to avoid flickering rebuilds
        private int currentDisplayedDeadline = 0;
        private int currentDisplayedHint = -1;

        // User action dictionary keys
        private const string DictUserAction = "user_action";
        private const string DictUserActionUsers = "user_users";
        private const string DictUserActionTargets = "user_targets";
        private const string DictUserActionTargetsCount = "user_targets_count";
        private const string DictUserActionTargetsHint = "user_targets_hint";
        private const string DictUserActionInfo = "user_info";
        private const string DictUserActionResponse = "user_response";
        private const string DictUserActionPauseStart = "user_pause_start";
        private const string DictServerTime = "server_time";
        private const string DictSpeaker = "speaker";

        // Property to check if announcer sounds should play
        public bool IsAnnouncerEnabled => announcerEnabled;

        // Special targets dictionary - nested by hint, then by target ID
        // First level: indexed by target hints
        // Second level: indexed by special target values (<= 0)
        private static readonly Dictionary<int, Dictionary<int, string>> SpecialTargets = new Dictionary<int, Dictionary<int, string>>()
        {
            { 3, new Dictionary<int, string> { { 0, "JiuRen" }, { -100, "DoNotUse"} } },
            { 100, new Dictionary<int, string> { { -1, "Volunteer" }, { 0, "Abstain" } } },
            { 102, new Dictionary<int, string> { { -1, "Done speaking" }, { 0, "Pause game" } } },
            { 104, new Dictionary<int, string> { { -1, "Done speaking" }, { 0, "Pause game" } } },
            { 105, new Dictionary<int, string> { { -1, "Done speaking" }, { 0, "Pause game" } } },
            { 151, new Dictionary<int, string> { { -100, "DoNotUse"} } },
            { 153, new Dictionary<int, string> { { -2, "Left" }, { -1, "Right"} } },
        };

        // Handlers for user actions (DisplayTargetSelection path)
        private static readonly Dictionary<int, Func<string, string>> UserInfoHints = new()
        {
            { 3, NvWuInfoHandler },
            { 5, LangRenSuccessionHandler },
            { 12, LangRenSuccessionHandler },
            { 62, LangRenSuccessionHandler },
            { 6, JiaMianInfoHandler },
            { 7, YuYanJiaInfoHandler },
            { 76, GiftedPoisonHandler },
            { 104, SheriffSpeechHandler },
            { 105, SheriffPKHandler },
            { 151, LieRenInfoHandler },
            { 154, VoteResultInfoHandler },
            { 1000, CheckRoleInfoHandler },
            { 1003, GameWinnerHandler },
        };

        // Handlers for announcements (DisplayCurrentlyActing path, user == -1)
        private static readonly Dictionary<int, Func<string, string>> AnnouncementInfoHandlers = new()
        {
            { 152, DeathAnnouncementHandler },
            { 1003, GameWinnerHandler },
        };
        private static string LieRenInfoHandler(string userInfo)
        {
            if (userInfo == "1")
            {
                return LocalizationManager.Instance.GetString("lieren_can_shoot", "Can shoot if dead.");
            }
            else
            {
                return LocalizationManager.Instance.GetString("lieren_cannot_shoot", "Shooting disabled.");
            }
        }

        private static string GiftedPoisonHandler(string userInfo)
        {
            if (userInfo != "gifted")
            {
                return LocalizationManager.Instance.GetString("not_gifted_yet", "Not gifted yet");
            }
            return LocalizationManager.Instance.GetString("gifted_use_trap", "Gifted, use cat trap:");
        }

        private static string GameWinnerHandler(string userInfo)
        {
            // userInfo is "Good" or "Evil"
            var winnerName = LocalizationManager.Instance.GetString(userInfo, userInfo);
            var txt = LocalizationManager.Instance.GetString("game_winner", "{0} wins!");
            return txt.Replace("{0}", winnerName);
        }

        private static string LangRenSuccessionHandler(string userInfo)
        {
            if (string.IsNullOrEmpty(userInfo))
                return LocalizationManager.Instance.GetString("langren_succession_no", "Cannot yet attack");
            return LocalizationManager.Instance.GetString("langren_succession_yes", "Can attack");
        }

        private static string SheriffSpeechHandler(string userInfo)
        {
            return LocalizationManager.Instance.GetString("sheriff_speech_info", "{0} volunteered.").Replace("{0}", userInfo);
        }
        private static string SheriffPKHandler(string userInfo)
        {
            return LocalizationManager.Instance.GetString("sheriff_pk_info", "{0} PK.").Replace("{0}", userInfo);
        }

        private static string VoteResultInfoHandler(string userInfo)
        {
            var txt = LocalizationManager.Instance.GetString("vote_result", "Vote result: {0}");
            return txt.Replace("{0}", userInfo);
        }

        private static string DeathAnnouncementHandler(string userInfo)
        {
            var localization = LocalizationManager.Instance;

            // Parse the userInfo: "deadPlayers;xiongBark" or just "deadPlayers"
            var parts = userInfo.Split(';');
            var deadPlayersStr = parts.Length > 0 ? parts[0] : "";
            var xiongBarkStr = parts.Length > 1 ? parts[1] : "";

            string result;
            // Format death announcement
            if (string.IsNullOrEmpty(deadPlayersStr))
            {
                result = localization.GetString("death_announcement_none", "Last night no deaths.");
            }
            else
            {
                var txt = localization.GetString("death_announcement", "Last night death: {0}");
                result = txt.Replace("{0}", deadPlayersStr);
            }

            // Add Xiong bark info if provided
            if (!string.IsNullOrEmpty(xiongBarkStr))
            {
                if (xiongBarkStr == "1")
                {
                    // Xiong barked
                    result += "\n" + localization.GetString("xiong_barked", "Bear barked!");
                }
                else if (xiongBarkStr == "2")
                {
                    // Xiong did not bark
                    result += "\n" + localization.GetString("xiong_not_barked", "Bear did not bark.");
                }
            }

            return result;
        }

        private static string CheckRoleInfoHandler(string userInfo)
        {
            var localization = LocalizationManager.Instance;

            // Parse comma-separated string: role,allegiance
            var parts = userInfo.Split(',');
            var role = parts.Length > 0 ? parts[0] : "";
            var allegiance = parts.Length > 1 ? parts[1] : "1";

            // Translate role name
            if (!string.IsNullOrEmpty(role))
            {
                role = localization.GetString(role);
            }

            // Translate allegiance (1 = good, 2 = evil)
            var allegianceText = allegiance == "2" 
                ? localization.GetString("evil") 
                : localization.GetString("good");

            var txt = localization.GetString("check_role_info", "Your role is {0}.");
            return txt.Replace("{0}", $"{role} ({allegianceText})");
        }

        private static string NvWuInfoHandler(string userInfo)
        {
            if (string.IsNullOrEmpty(userInfo) || userInfo == "0")
                return LocalizationManager.Instance.GetString("nvwu_no_save", "Cannot view attack info.");
            var txt = LocalizationManager.Instance.GetString("nvwu_save", "Last night {0} was attacked.");
            return txt.Replace("{0}", userInfo);
        }

        private static string YuYanJiaInfoHandler(string userInfo)
        {
            int.TryParse(userInfo, out var result);
            var txt = LocalizationManager.Instance.GetString("yuyanjia_chayan_result", "Yuyanjia's Chayan result: {0}");
            if (result != 0)
            {
                var allegience = LocalizationManager.Instance.GetString(result == 1 ? "good" : "evil");
                return txt.Replace("{0}", allegience);
            }
            else
            {
                return txt.Replace("{0}", LocalizationManager.Instance.GetString(userInfo));
            }
        }

        private static string JiaMianInfoHandler(string userInfo)
        {
            int.TryParse(userInfo, out var result);
            var txt = LocalizationManager.Instance.GetString("jiamian_info", "Your check result: {0}. Select target to flip");
            return txt.Replace("{0}", LocalizationManager.Instance.GetString("jiamian_info" + userInfo));
        }

        public GameView(HubConnectionManager connectionManager, int gameId, int playerId, bool isOwner = false)
        {
            InitializeComponent();
            this.connectionManager = connectionManager;
            this.gameId = gameId;
            this.playerId = playerId;
            this.isOwner = isOwner;
            PlayerIdTopLabel.Text = playerId.ToString();
            PlayerIdBottomLabel.Text = playerId.ToString();
            RevealBtn.Text = LocalizationManager.Instance.GetString("reveal");
            ConfirmButton.Text = LocalizationManager.Instance.GetString("confirm");

            // Set announcer to ON by default for game owner
            if (isOwner)
            {
                announcerEnabled = true;
                AnnouncerBtn.Text = "🔊";
                AnnouncerBtn.BackgroundColor = Colors.Green;
                VoiceoverService.Instance.IsEnabled = true;
            }

            // Set dynamic font sizes based on screen size
            UpdatePlayerIdFontSizes();
            UpdateGameStatusFontSize();
            this.SizeChanged += OnPageSizeChanged;

            // Subscribe to game state updates
            if (connectionManager != null)
            {
                connectionManager.GameStateUpdated += UpdateGameStatus;
                connectionManager.GameEnded += OnGameEnded;
                UpdateGameStatus();
            }
        }

        private async void OnGameEnded(string message)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await DisplayAlert("Game Ended", message, "OK");
                await Navigation.PopToRootAsync();
            });
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            // Lock to landscape when entering game view
            OrientationService.LockLandscape();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            // Return to portrait when leaving game view
            OrientationService.LockPortrait();

            // Clean up event subscriptions
            if (connectionManager != null)
            {
                connectionManager.GameStateUpdated -= UpdateGameStatus;
                connectionManager.GameEnded -= OnGameEnded;
            }

            this.SizeChanged -= OnPageSizeChanged;

            // Cancel any running countdown
            countdownCts?.Cancel();
            countdownCts = null;
        }

        private void OnPageSizeChanged(object? sender, EventArgs e)
        {
            UpdatePlayerIdFontSizes();
            UpdateGameStatusFontSize();
        }

        private void UpdatePlayerIdFontSizes()
        {
            // Calculate font size based on available width (2/3 of screen for player IDs)
            double availableWidth = this.Width * 2 / 3;
            double availableHeight = this.Height / 2; // Each ID takes half the height
            
            // Use the smaller dimension to determine font size
            double fontSize = Math.Min(availableWidth, availableHeight) * 0.4;
            fontSize = Math.Max(80, Math.Min(fontSize, 300)); // Clamp

            MainThread.BeginInvokeOnMainThread(() =>
            {
                PlayerIdTopLabel.FontSize = fontSize;
                PlayerIdBottomLabel.FontSize = fontSize;
            });
        }

        private void UpdateGameStatusFontSize()
        {
            // Calculate font size based on text content to prevent wrapping
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var text = GameStatusLabel.Text ?? "";

                // Get the container's width (Frame) minus padding (10 on each side)
                double availableWidth = GameStatusFrame.Width - 20;
                double availableHeight = GameStatusFrame.Height - 20;

                if (availableWidth <= 0)
                {
                    // Fallback: calculate from screen dimensions
                    // Right column is 5/6 of 2/3 of screen width
                    var displayInfo = DeviceDisplay.MainDisplayInfo;
                    double screenWidth = displayInfo.Width / displayInfo.Density;
                    availableWidth = (screenWidth * 2 / 3) * 5 / 6 - 20;
                }

                if (availableHeight <= 0)
                {
                    // Fallback: calculate from screen height
                    var displayInfo = DeviceDisplay.MainDisplayInfo;
                    double screenHeight = displayInfo.Height / displayInfo.Density;
                    availableHeight = (screenHeight * 1 / 5) - 20;
                }

                if (availableWidth <= 0)
                {
                    // Final fallback default
                    availableWidth = 200;
                }

                if (availableHeight <= 0)
                {
                    availableHeight = 100;
                }

                // Start with a base font size
                double maxFontSize = 40;

                if (string.IsNullOrEmpty(text))
                {
                    GameStatusLabel.FontSize = maxFontSize;
                    return;
                }

                // Estimate characters that fit at current font size
                double avgCharWidth = 2.2 * maxFontSize;
                int maxCharsPerLine = (int)(availableWidth / avgCharWidth);

                // Get the longest line in the text
                var lines = text.Split('\n');
                int maxLineLength = lines.Length > 0 ? lines.Max(l => l.Length) : 0;

                // If text is too long, reduce font size proportionally
                if (maxLineLength > maxCharsPerLine && maxCharsPerLine > 0)
                {
                    double ratio = (double)maxCharsPerLine / maxLineLength;
                    maxFontSize = Math.Max(10, maxFontSize * ratio);
                }

                double avgCharHeight = 0.6 * maxFontSize;
                int maxLines = (int)(availableHeight / avgCharHeight);

                if (lines.Count() > maxLines && maxLines > 0)
                {
                    double ratio = (double)maxLines / lines.Count();
                    maxFontSize = Math.Max(10, maxFontSize * ratio);
                }

                GameStatusLabel.FontSize = maxFontSize;
            });
        }

        private int? GetInt32Value(object? obj)
        {
            if (obj == null) return null;
            
            if (obj is int intValue)
                return intValue;
            
            if (obj is JsonElement je)
            {
                return je.ValueKind == JsonValueKind.Number ? je.GetInt32() : null;
            }
            
            return null;
        }

        private string? GetStringValue(object? obj)
        {
            if (obj == null) return null;
            
            if (obj is string str)
                return str;
            
            if (obj is JsonElement je && je.ValueKind == JsonValueKind.String)
                return je.GetString();
            
            return null;
        }

        private List<int> GetInt32List(object? obj)
        {
            if (obj == null) return new();
            
            if (obj is List<int> intList)
                return intList;
            
            if (obj is JsonElement je && je.ValueKind == JsonValueKind.Array)
            {
                return je.EnumerateArray().Select(e => e.GetInt32()).ToList();
            }
            
            return new();
        }

        private string GetTargetHint(int hintIndex)
        {
            var localization = LocalizationManager.Instance;
            var hintKey = hintIndex switch
            {
                1 => "langren_kill",
                2 => "yuyanjia_chayan",
                3 => "nvwu_act",
                4 => "wuzhe_act",
                6 => "jiamian_chayan",
                7 => "yuyanjia_result",
                8 => "shemengren_act",
                9 => "xiong_act",
                11 => "langren_kill_target",
                12 => "converted_langren_succession",
                50 => "open_eyes",
                51 => "close_eyes",
                52 => "lucky_one_open_eyes",
                53 => "lucky_one_close_eyes",
                54 => "converted_open_eyes",
                55 => "converted_close_eyes",
                75 => "check_mice",
                100 => "volunteer_sheriff",
                101 => "vote_sheriff",
                102 => "round_table",
                103 => "vote_sheriff_vote",
                104 => "sheriff_speech",
                105 => "sheriff_pk",
                110 => "sheriff_recommend_vote",
                111 => "voteout",
                150 => "sheriff_handover",
                151 => "hunter_kill",
                152 => "death_announcement",
                153 => "sheriff_choose_direction",
                154 => "vote_result",
                1000 => "check_private",
                1001 => "night_time",
                1002 => "day_time",
                1003 => "game_over",
                1020 => "put_down_device",
                _ => null
            };

            return hintKey != null ? localization.GetString(hintKey) : string.Empty;
        }

        private string GetSpecialTargetLabel(int hintIndex, int targetId)
        {
            if (SpecialTargets.TryGetValue(hintIndex, out var targetDict))
            {
                if (targetDict.TryGetValue(targetId, out var label))
                {
                    return label;
                }
            }
            return string.Empty;
        }

        private string GetPlayerRole(int playerId)
        {
            var gameDict = connectionManager?.GetGameDictionary() ?? new();
            var playersDict = GetDictionaryValue(gameDict.TryGetValue("players", out var playersObj) ? playersObj : null);

            if (playersDict == null || playersDict.Count == 0)
                return string.Empty;

            var playerKey = playerId.ToString();
            if (!playersDict.ContainsKey(playerKey))
                return string.Empty;

            var playerDict = GetDictionaryValue(playersDict[playerKey]);
            return GetStringValue(playerDict.TryGetValue("role", out var roleObj) ? roleObj : null) ?? string.Empty;
        }

        private int GetPlayerAllegiance(int playerId)
        {
            var gameDict = connectionManager?.GetGameDictionary() ?? new();
            var playersDict = GetDictionaryValue(gameDict.TryGetValue("players", out var playersObj) ? playersObj : null);

            if (playersDict == null || playersDict.Count == 0)
                return 1; // Default to good

            var playerKey = playerId.ToString();
            if (!playersDict.ContainsKey(playerKey))
                return 1; // Default to good

            var playerDict = GetDictionaryValue(playersDict[playerKey]);
            return GetInt32Value(playerDict.TryGetValue("alliance", out var allianceObj) ? allianceObj : null) ?? 1;
        }

        private bool IsPlayerDead(int playerId)
        {
            var gameDict = connectionManager?.GetGameDictionary() ?? new();
            var playersDict = GetDictionaryValue(gameDict.TryGetValue("players", out var playersObj) ? playersObj : null);

            if (playersDict == null || playersDict.Count == 0)
                return false;

            var playerKey = playerId.ToString();
            if (!playersDict.ContainsKey(playerKey))
                return false;

            var playerDict = GetDictionaryValue(playersDict[playerKey]);
            var aliveValue = GetInt32Value(playerDict.TryGetValue("alive", out var aliveObj) ? aliveObj : null);

            // Player is dead if alive == 0
            return aliveValue == 0;
        }

        private Dictionary<string, object> GetDictionaryValue(object? obj)
        {
            if (obj == null) return new();

            if (obj is Dictionary<string, object> dict)
                return dict;

            if (obj is JsonElement je && je.ValueKind == JsonValueKind.Object)
            {
                var result = new Dictionary<string, object>();
                foreach (var property in je.EnumerateObject())
                {
                    result[property.Name] = property.Value;
                }
                return result;
            }
            
            return new();
        }

        private void UpdateGameStatus()
        {
            if (connectionManager == null) return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                var gameDict = connectionManager.GetGameDictionary();
                var localization = LocalizationManager.Instance;

                // Update game status (phase, etc.)
                var phaseValue = GetInt32Value(gameDict.TryGetValue("phase", out var phaseObj) ? phaseObj : null);
                var phaseStr = phaseValue == 0 ? localization.GetString("phase_night") : 
                              phaseValue == 1 ? localization.GetString("phase_day") : 
                              localization.GetString("phase_unknown");
                var dayNum = gameDict.TryGetValue("day", out var dayNum2) ? GetInt32Value(dayNum2)?.ToString() ?? "?" : "?";
                var dayString = localization.GetString("day").Replace("{0}", dayNum);
                GameStatusLabel.Text = $"{dayString} {phaseStr}\n";
                UpdateGameStatusFontSize();

                // Show/hide Reveal button based on day time (phaseValue == 1)
                if (phaseValue == 1)
                {
                    // Add Reveal button if not already in toolbar
                    if (!ToolbarItems.Contains(RevealBtn))
                    {
                        ToolbarItems.Insert(0, RevealBtn);
                    }
                }
                else
                {
                    // Remove Reveal button if it's in toolbar
                    if (ToolbarItems.Contains(RevealBtn))
                    {
                        ToolbarItems.Remove(RevealBtn);
                    }
                }

                // Check if current player is dead and update player ID color
                var isPlayerDead = IsPlayerDead(playerId);
                var playerIdColor = isPlayerDead ? Colors.Red : Colors.Blue;
                PlayerIdTopLabel.TextColor = playerIdColor;
                PlayerIdBottomLabel.TextColor = playerIdColor;

                var userAction = GetInt32Value(gameDict.TryGetValue(DictUserAction, out var uaObj) ? uaObj : null) ?? 0;
                var userUsers = GetInt32List(gameDict.TryGetValue(DictUserActionUsers, out var uuObj) ? uuObj : null);
                var userTargets = GetInt32List(gameDict.TryGetValue(DictUserActionTargets, out var utObj) ? utObj : null);
                var userTargetsCount = GetInt32Value(gameDict.TryGetValue(DictUserActionTargetsCount, out var utcObj) ? utcObj : null) ?? 0;
                var userTargetsHint = GetInt32Value(gameDict.TryGetValue(DictUserActionTargetsHint, out var uthObj) ? uthObj : null) ?? -1;
                var userInfo = GetStringValue(gameDict.TryGetValue(DictUserActionInfo, out var uiObj) ? uiObj : null) ?? "";
                var userResponse = GetDictionaryValue(gameDict.TryGetValue(DictUserActionResponse, out var urObj) ? urObj : null);

                // Calculate server-client clock offset for accurate countdown
                var serverTime = GetInt32Value(gameDict.TryGetValue(DictServerTime, out var stObj) ? stObj : null) ?? 0;
                if (serverTime != lastServerTime)
                {
                    int clientNow = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    serverTimeOffset = serverTime - clientNow;
                    lastServerTime = serverTime;
                }

                var speaking = GetInt32List(gameDict.TryGetValue(DictSpeaker, out var speakingObj) ? speakingObj : null);
                var speaker = speaking.Count > 0 ? speaking[speaking.Count - 1] : 0;

                // Check if self can act
                bool isSelfActable = userAction != 0 && userUsers.Contains(playerId);

                if (isSelfActable)
                {
                    DisplayTargetSelection(userAction, userTargets, userTargetsCount, userTargetsHint, userInfo, phaseValue == 0 ? userResponse : null);
                }
                else
                {
                    HideTargetSelection();
                }

                if (userAction != 0 && userUsers.Count > 0)
                {
                    // Pass isSelfActable to avoid double-managing the countdown
                    // When isSelfActable is true, DisplayTargetSelection already handles the countdown
                    DisplayCurrentlyActing(userAction, userUsers, userTargetsHint, userInfo, speaker, phaseValue ?? 0, !isSelfActable);
                }
                else
                {
                    HideCurrentlyActing();
                }

            });
        }

        private void DisplayCurrentlyActing(int deadline, List<int> actingPlayerIds, int userTargetsHint, string userInfo, int speaker = 0, int phaseValue = 0, bool manageCountdown = true)
        {
            // Only manage countdown if we're not showing target selection (which has its own countdown)
            if (manageCountdown)
            {
                countdownCts?.Cancel();
                countdownCts = new CancellationTokenSource();
            }

            if (actingPlayerIds.Contains(-1) && actingPlayerIds.Count == 1)
            {
                // This is an announcement (user == -1)
                // Check if there's a special handler for this hint
                if (AnnouncementInfoHandlers.TryGetValue(userTargetsHint, out var handler))
                {
                    GameStatusLabel.Text = handler(userInfo);
                }
                else
                {
                    // Default handling: get hint text and replace {0} with userInfo
                    var hintText = GetTargetHint(userTargetsHint);
                    userInfo = LocalizationManager.Instance.GetString(userInfo);
                    hintText = hintText.Replace("{0}", userInfo);
                    GameStatusLabel.Text = hintText;
                }
                UpdateGameStatusFontSize();

                // Play voiceover
                if (IsAnnouncerEnabled)
                {
                    _ = PlayVoiceoverAsync(GameStatusLabel.Text);
                }
            }
            else if (phaseValue == 1)
            {
                // Day phase - show who should be speaking
                var localization = LocalizationManager.Instance;

                // Display speaking indicator with roles
                var speakingText = localization.GetString("speaking", "Speaking: {0}");
                if (speaker != 0)
                    GameStatusLabel.Text = speakingText.Replace("{0}", speaker.ToString());
                else
                    GameStatusLabel.Text = "";
                UpdateGameStatusFontSize();
            }
            else
            {
                // Night phase - get the roles of acting players
                var actingRoles = new HashSet<string>();
                if (userTargetsHint == 76 || userTargetsHint == 52 || userTargetsHint == 53)
                {
                    actingRoles.Add(LocalizationManager.Instance.GetString("lucky_one"));
                }
                else
                    if (userTargetsHint == 1 || userTargetsHint == 11 || userTargetsHint == 12)
                    {
                        actingRoles.Add(LocalizationManager.Instance.GetString("LangRen"));
                    }
                    else
                    {
                        foreach (var playerId in actingPlayerIds)
                        {
                            var role = GetPlayerRole(playerId);
                            if (!string.IsNullOrEmpty(role))
                            {
                                var rs = LocalizationManager.Instance.GetString(role);
                                actingRoles.Add(rs);
                            }
                        }
                    }

                // Display the acting roles
                string actingText = actingRoles.Count > 0
                    ? string.Join(", ", actingRoles) : "";

                GameStatusLabel.Text = actingText;
                UpdateGameStatusFontSize();
            }

            // Start countdown timer only if we're managing it
            if (manageCountdown)
            {
                StartCountdown(deadline, countdownCts.Token);
            }
        }

        private void HideCurrentlyActing()
        {
            countdownCts?.Cancel();
            countdownCts = null;
        }

        /// <summary>
        /// Plays voiceover for the given text using the VoiceoverService.
        /// Falls back to TextToSpeech if custom audio is not available.
        /// Only plays if announcer is enabled.
        /// </summary>
        private async Task PlayVoiceoverAsync(string text)
        {
            if (!announcerEnabled || string.IsNullOrEmpty(text))
                return;

            try
            {
                // Try to use custom voice clips first
                var segments = VoiceoverService.Instance.ParseText(text);

                if (segments.Count > 0)
                {
                    // Play using custom voice clips
                    await VoiceoverService.Instance.PlayAsync(text);
                }
                else
                {
                    // Fall back to TextToSpeech
                    await TextToSpeech.Default.SpeakAsync(text);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Voiceover failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Plays a warning beep sound when countdown is running low.
        /// Uses audio beep and visual feedback.
        /// </summary>
        private void PlayWarningBeep()
        {
            try
            {
                // Play audio beep
                _ = BeepService.PlayWarningBeepAsync();

                // Also trigger haptic feedback (vibration) for tactile warning on mobile
                try
                {
                    HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);
                }
                catch { /* Haptic not available on all platforms */ }

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Warning beep failed: {ex.Message}");
            }
        }

        private void DisplayTargetSelection(int userActionDeadline, List<int> availableTargets, int maxTargetCount, int hintIndex, string userInfo = "", Dictionary<string, object>? userResponse = null)
        {
            // Check if we're already displaying the same target selection
            // Only compare deadline and hint - don't rely on UI visibility state which can have race conditions
            if (currentDisplayedDeadline == userActionDeadline && 
                currentDisplayedHint == hintIndex)
            {
                // Already displaying this state, no need to rebuild
                return;
            }

            // Track the current displayed state
            currentDisplayedDeadline = userActionDeadline;
            currentDisplayedHint = hintIndex;

            // Cancel previous countdown if any
            countdownCts?.Cancel();
            countdownCts = new CancellationTokenSource();

            var localization = LocalizationManager.Instance;
            selectedTargets.Clear();
            TargetButtonsContainer.Clear();

            // Update hint label
            string hintText = GetTargetHint(hintIndex);
            if (!string.IsNullOrEmpty(hintText))
            {
                TargetInstructionLabel.Text = hintText;
            }
            else
            {
                // Fallback to instruction if no hint available
                string instructionText = maxTargetCount == -1 
                    ? localization.GetString("select_any_targets")
                    : localization.GetString("select_up_to_targets").Replace("{0}", maxTargetCount.ToString());
                TargetInstructionLabel.Text = instructionText;
            }

            if (hintIndex == 1000)
            {
                var role = GetPlayerRole(playerId);
                var allegiance = GetPlayerAllegiance(playerId);
                userInfo = $"{role},{allegiance}";
            }
            if (hintIndex == 75)
            {
                if (userInfo.Contains(","))
                {
                    var split = userInfo.Split(',');
                    if (split[0] == playerId.ToString())
                    {
                        userInfo = localization.GetString("check_mice_info");
                        userInfo = userInfo.Replace("{0}", split.Length > 1 ? split[1] : "");
                    }
                    else
                    {
                        userInfo = localization.GetString("no");
                    }
                }
                else
                {
                    userInfo = userInfo == playerId.ToString() ? localization.GetString("yes") : localization.GetString("no");
                }
            }
            var ui = UserInfoHints.TryGetValue(hintIndex, out var handler) ? handler(userInfo) : userInfo;
            TargetInstructionLabel.Text += $"\n{ui}";

            // Get all players from game dictionary
            var gameDict = connectionManager?.GetGameDictionary() ?? new();
            var playersDict = GetDictionaryValue(gameDict.TryGetValue("players", out var playersObj) ? playersObj : null);
            var allPlayerIds = new List<int>();
            
            if (playersDict != null && playersDict.Count > 0)
            {
                foreach (var key in playersDict.Keys)
                {
                    if (int.TryParse(key, out var id))
                    {
                        allPlayerIds.Add(id);
                    }
                }
                allPlayerIds.Sort();
            }

            // Parse user responses to show who chose what
            var responsesByTarget = new Dictionary<int, List<int>>();
            var ownChoices = new HashSet<int>();

            // Create special target buttons first (if they are in availableTargets)
            if (SpecialTargets.TryGetValue(hintIndex, out var specialTargetsForHint))
            {
                foreach (var specialTarget in specialTargetsForHint.Keys.OrderBy(x => x))
                {
                    if (!availableTargets.Contains(specialTarget))
                        continue;

                    var label = GetSpecialTargetLabel(hintIndex, specialTarget);
                    if (string.IsNullOrEmpty(label))
                        label = specialTarget.ToString();
                    else
                        label = localization.GetString(label);

                    // Add response count if available
                    if (responsesByTarget.TryGetValue(specialTarget, out var respondents))
                    {
                        label += $" ({respondents.Count})";
                    }

                    var button = new Button
                    {
                        Text = label,
                        CornerRadius = 5,
                        Padding = new Thickness(10, 5),
                        Margin = new Thickness(5),
                        BackgroundColor = ownChoices.Contains(specialTarget) ? Colors.Orange : Colors.LightGray,
                        TextColor = Colors.Black,
                        IsEnabled = true,
                        Opacity = 1.0,
                        MinimumWidthRequest = 60,
                        MinimumHeightRequest = 40,
                        HeightRequest = 45,
                        FontSize = 16
                    };

                    int capturedTargetId = specialTarget;
                    button.Clicked += (s, e) => OnTargetSelected(button, capturedTargetId, maxTargetCount);

                    TargetButtonsContainer.Add(button);
                }
            }

            // Create target buttons for all players
            foreach (var targetId in availableTargets)
            {
                if (targetId <= 0) 
                    continue; // Skip special targets, already handled

                var buttonText = targetId.ToString();

                // Add response count and indicator if available
                if (responsesByTarget.TryGetValue(targetId, out var respondents))
                {
                    buttonText += $" ({respondents.Count})";
                }

                var button = new Button
                {
                    Text = buttonText,
                    CornerRadius = 5,
                    Padding = new Thickness(10, 5),
                    Margin = new Thickness(5),
                    BackgroundColor = ownChoices.Contains(targetId) ? Colors.Orange : Colors.LightGray,
                    TextColor = Colors.Black,
                    IsEnabled = true,
                    Opacity = 1.0,
                    MinimumWidthRequest = 50,
                    MinimumHeightRequest = 40,
                    HeightRequest = 45,
                    FontSize = 18
                };

                int capturedTargetId = targetId;
                button.Clicked += (s, e) => OnTargetSelected(button, capturedTargetId, maxTargetCount);

                TargetButtonsContainer.Add(button);
            }

            // Show target selection container and confirm button
            TargetSelectionContainer.IsVisible = true;
            ConfirmButton.IsVisible = true;
            ConfirmButton.IsEnabled = true;
            ConfirmButton.Text = LocalizationManager.Instance.GetString("confirm");

            // Force layout update on Android to ensure buttons are properly rendered
            TargetButtonsContainer.InvalidateMeasure();

            // Start countdown timer
            StartCountdown(userActionDeadline, countdownCts.Token);
        }

        private void OnTargetSelected(Button button, int targetId, int maxCount)
        {
            if (selectedTargets.Contains(targetId))
            {
                // Deselect the target
                selectedTargets.Remove(targetId);
                button.BackgroundColor = Colors.LightGray;
                button.TextColor = Colors.Black;
            }
            else if (maxCount == -1 || selectedTargets.Count < maxCount)
            {
                // Select the target
                selectedTargets.Add(targetId);
                button.BackgroundColor = Colors.Green;
                button.TextColor = Colors.White;
            }
            else if (maxCount == 1 && selectedTargets.Count == 1)
            {
                // Special case: when maxCount is 1, auto-deselect the previous target
                // Reset all buttons to deselected state
                foreach (var child in TargetButtonsContainer.Children)
                {
                    if (child is Button btn)
                    {
                        btn.BackgroundColor = Colors.LightGray;
                        btn.TextColor = Colors.Black;
                    }
                }

                // Clear previous selection and select the new target
                selectedTargets.Clear();
                selectedTargets.Add(targetId);
                button.BackgroundColor = Colors.Green;
                button.TextColor = Colors.White;
            }
        }

        private void StartCountdown(int deadline, CancellationToken ct)
        {
            warningBeepPlayed = false; // Reset warning flag for new countdown

            _ = Task.Run(async () =>
            {
                while (!ct.IsCancellationRequested)
                {
                    int clientNow = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    // Adjust client time using server offset to account for clock skew
                    int adjustedNow = clientNow + serverTimeOffset;

                    // Check if game is paused
                    var gameDict = connectionManager?.GetGameDictionary() ?? new();
                    var pauseStart = GetInt32Value(gameDict.TryGetValue(DictUserActionPauseStart, out var psObj) ? psObj : null) ?? 0;
                    bool isPaused = pauseStart != 0;

                    // Calculate effective deadline accounting for current pause
                    int effectiveDeadline = deadline;
                    if (isPaused)
                    {
                        effectiveDeadline = deadline + (adjustedNow - pauseStart);
                    }

                    int timeRemaining = effectiveDeadline - adjustedNow;

                    if (!isPaused && timeRemaining <= 0)
                    {
                        // Time expired locally - hide UI but don't reset tracking
                        // The server will send an update that will reset tracking properly
                        MainThread.BeginInvokeOnMainThread(() => HideTargetSelection(resetTracking: false));
                        break;
                    }

                    // Play warning beep when countdown reaches 15 seconds
                    if (!isPaused && timeRemaining == 15 && !warningBeepPlayed && IsAnnouncerEnabled)
                    {
                        warningBeepPlayed = true;
                        MainThread.BeginInvokeOnMainThread(() => PlayWarningBeep());
                    }

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (isPaused)
                        {
                            CountdownLabel.Text = LocalizationManager.Instance.GetString("game_paused", "⏸ Paused");
                        }
                        else
                        {
                            CountdownLabel.Text = $"{timeRemaining}";
                        }
                    });

                    await Task.Delay(1000, ct);
                }
            }, ct);
        }

        private void HideTargetSelection(bool resetTracking = true)
        {
            // Only reset tracking variables when the server indicates the action is complete
            // Don't reset when hiding due to local countdown expiry (server might still have same deadline)
            if (resetTracking)
            {
                currentDisplayedDeadline = 0;
                currentDisplayedHint = -1;
            }

            countdownCts?.Cancel();
            countdownCts = null;
            selectedTargets.Clear();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                TargetSelectionContainer.IsVisible = false;
                ConfirmButton.IsVisible = false;
                CountdownLabel.Text = "";
                TargetButtonsContainer.Clear();
            });
        }

        private async void OnConfirmClicked(object? sender, EventArgs e)
        {
            if (connectionManager == null || selectedTargets.Count == 0) return;

            var localization = LocalizationManager.Instance;
            try
            {
                // Disable confirm button to prevent double-clicks and show feedback
                ConfirmButton.IsEnabled = false;
                ConfirmButton.Text = "✓";

                // Send the selected targets to the server via SignalR
                // Don't hide the selection - let server state drive the UI
                // This prevents countdown timer from resetting when server sends update
                await connectionManager.SendTargetSelectionAsync(gameId, playerId, selectedTargets.ToList());
            }
            catch (Exception ex)
            {
                // Re-enable on error
                ConfirmButton.IsEnabled = true;
                ConfirmButton.Text = localization.GetString("confirm");

                await DisplayAlert(
                    localization.GetString("error"),
                    localization.GetString("failed_send_selection") + ": " + ex.Message,
                    localization.GetString("yes"));
            }
        }

        private async void OnDisconnectClicked(object? sender, EventArgs e)
        {
            var localization = LocalizationManager.Instance;
            bool confirm = await DisplayAlert(
                localization.GetString("disconnect"),
                localization.GetString("disconnect_confirm"),
                localization.GetString("yes"),
                localization.GetString("no"));
            if (confirm)
            {
                if (connectionManager != null)
                {
                    connectionManager.GameStateUpdated -= UpdateGameStatus;

                    // Notify server that we're leaving
                    await connectionManager.LeaveGameAsync(gameId);
                    await connectionManager.Disconnect();
                }
                await Navigation.PopAsync();
            }
        }

        private async void OnRevealClicked(object? sender, EventArgs e)
        {
            if (connectionManager == null)
                return;

            try
            {
                // Send action -10 to the server
                await connectionManager.SendTargetSelectionAsync(gameId, playerId, new List<int> { -10 });
            }
            catch (Exception ex)
            {
                var localization = LocalizationManager.Instance;
                await DisplayAlert(
                    localization.GetString("error"),
                    localization.GetString("failed_send_selection") + ": " + ex.Message,
                    localization.GetString("yes"));
            }
        }

        private void OnAnnouncerToggleClicked(object? sender, EventArgs e)
        {
            announcerEnabled = !announcerEnabled;

            // Update button appearance based on state
            if (announcerEnabled)
            {
                AnnouncerBtn.Text = "🔊";
                AnnouncerBtn.BackgroundColor = Colors.Green;
                VoiceoverService.Instance.IsEnabled = true;
            }
            else
            {
                AnnouncerBtn.Text = "🔇";
                AnnouncerBtn.BackgroundColor = Colors.LightGray;
                VoiceoverService.Instance.IsEnabled = false;
            }
        }
    }

    public class PlayerDisplay
    {
        public string PlayerInfo { get; set; } = "";
        public string Details { get; set; } = "";
    }
}
