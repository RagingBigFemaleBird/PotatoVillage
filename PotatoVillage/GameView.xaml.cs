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

        // User action dictionary keys
        private const string DictUserAction = "user_action";
        private const string DictUserActionUsers = "user_users";
        private const string DictUserActionTargets = "user_targets";
        private const string DictUserActionTargetsCount = "user_targets_count";
        private const string DictUserActionTargetsHint = "user_targets_hint";
        private const string DictUserActionInfo = "user_info";
        private const string DictUserActionSelects = "user_selects";

        // Hints dictionary - empty, to be filled in manually
        private static readonly Dictionary<int, string> TargetHints = new Dictionary<int, string>()
        {
            { 1, "LangRen kill" },
            { 2, "Yuyanjia Chayan" },
            { 3, "NvWu act" },
            { 4, "WuZhe act" },
            { 6, "JiaMian chayan" },
            { 7, "Yuyanjia Chanyan result" },
            { 101, "Vote sherriff" },
            { 102, "Round table" },
            { 100, "Volunteer sheriff" },
            { 103, "Vote sheriff" },
            { 110, "Sheriff recommend vote" },
            { 111, "Voteout" },
        };

        // Special targets dictionary - nested by hint, then by target ID
        // First level: indexed by target hints
        // Second level: indexed by special target values (>= 0)
        private static readonly Dictionary<int, Dictionary<int, string>> SpecialTargets = new Dictionary<int, Dictionary<int, string>>()
        {
            { 3, new Dictionary<int, string> { { 0, "JiuRen" }, } },
            { 100, new Dictionary<int, string> { { -1, "Volenteer" }, { -2, "Abstain" } } },
            { 102, new Dictionary<int, string> { { -1, "Done speaking" } } },

        };

        public GameView(HubConnectionManager connectionManager, int gameId, int playerId, bool isOwner = false)
        {
            InitializeComponent();
            this.connectionManager = connectionManager;
            this.gameId = gameId;
            this.playerId = playerId;
            this.isOwner = isOwner;
            PlayerIdTopLabel.Text = playerId.ToString();
            PlayerIdBottomLabel.Text = playerId.ToString();
            StartGameBtn.IsVisible = isOwner;
            StartGameBtn.Text = LocalizationManager.Instance.GetString("start_game");
            ConfirmButton.Text = LocalizationManager.Instance.GetString("confirm");

            // Set dynamic font sizes based on screen size
            UpdatePlayerIdFontSizes();
            UpdateGameStatusFontSize();
            this.SizeChanged += OnPageSizeChanged;
            
            // Subscribe to game state updates
            if (connectionManager != null)
            {
                connectionManager.GameStateUpdated += UpdateGameStatus;
                UpdateGameStatus();
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            
            // Clean up event subscriptions
            if (connectionManager != null)
            {
                connectionManager.GameStateUpdated -= UpdateGameStatus;
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
            // Calculate font size based on available height in the top 1/3 of right column
            double availableHeight = this.Height / 3; // Top 1/3 of screen
            double fontSize = availableHeight * 0.3; // Use 0.3 of available height
            fontSize = Math.Max(12, Math.Min(fontSize, 300)); // Clamp

            MainThread.BeginInvokeOnMainThread(() =>
            {
                GameStatusLabel.FontSize = fontSize;
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
                100 => "volunteer_sheriff",
                101 => "vote_sheriff",
                102 => "round_table",
                103 => "vote_sheriff_vote",
                110 => "sheriff_recommend_vote",
                111 => "voteout",
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

                // Get user action related data
                var userAction = GetInt32Value(gameDict.TryGetValue(DictUserAction, out var uaObj) ? uaObj : null) ?? 0;
                var userUsers = GetInt32List(gameDict.TryGetValue(DictUserActionUsers, out var uuObj) ? uuObj : null);
                var userTargets = GetInt32List(gameDict.TryGetValue(DictUserActionTargets, out var utObj) ? utObj : null);
                var userTargetsCount = GetInt32Value(gameDict.TryGetValue(DictUserActionTargetsCount, out var utcObj) ? utcObj : null) ?? 0;
                var userTargetsHint = GetInt32Value(gameDict.TryGetValue(DictUserActionTargetsHint, out var uthObj) ? uthObj : null) ?? -1;
                var userInfo = GetStringValue(gameDict.TryGetValue(DictUserActionInfo, out var uiObj) ? uiObj : null) ?? "";

                // Check if self can act
                bool isSelfActable = userAction != 0 && userUsers.Contains(playerId);

                if (isSelfActable)
                {
                    DisplayTargetSelection(userAction, userTargets, userTargetsCount, userTargetsHint, userInfo);
                }
                else
                {
                    HideTargetSelection();
                }

                if (userAction != 0 && userUsers.Count > 0 && phaseValue == 0)
                {
                    DisplayCurrentlyActing(userAction, userUsers);
                }
                else
                {
                    HideCurrentlyActing();
                }

            });
        }

        private void DisplayCurrentlyActing(int deadline, List<int> actingPlayerIds)
        {
            countdownCts?.Cancel();
            countdownCts = new CancellationTokenSource();

            // Get the roles of acting players
            var actingRoles = new HashSet<string>();
            foreach (var playerId in actingPlayerIds)
            {
                var role = GetPlayerRole(playerId);
                if (!string.IsNullOrEmpty(role))
                {
                    var rs = LocalizationManager.Instance.GetString(role);
                    actingRoles.Add(rs);
                }
            }

            // Display the acting roles
            string actingText = actingRoles.Count > 0 
                ? string.Join(", ", actingRoles) : "";
            
            GameStatusLabel.Text = actingText;

            // Start countdown timer
            StartCountdown(deadline, countdownCts.Token);
        }

        private void HideCurrentlyActing()
        {
            countdownCts?.Cancel();
            countdownCts = null;
        }

        private void DisplayTargetSelection(int userActionDeadline, List<int> availableTargets, int maxTargetCount, int hintIndex, string userInfo = "")
        {
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

            // Append user info if present
            if (!string.IsNullOrEmpty(userInfo))
            {
                TargetInstructionLabel.Text += $"\n{userInfo}";
            }

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

                    var button = new Button
                    {
                        Text = label,
                        CornerRadius = 5,
                        Padding = new Thickness(10, 5),
                        Margin = new Thickness(5),
                        BackgroundColor = Colors.LightGray,
                        TextColor = Colors.Black,
                        IsEnabled = true,
                        Opacity = 1.0
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

                var button = new Button
                {
                    Text = targetId.ToString(),
                    CornerRadius = 5,
                    Padding = new Thickness(10, 5),
                    Margin = new Thickness(5),
                    BackgroundColor = Colors.LightGray,
                    TextColor = Colors.Black,
                    IsEnabled = true,
                    Opacity = 1.0,
                };

                int capturedTargetId = targetId;
                button.Clicked += (s, e) => OnTargetSelected(button, capturedTargetId, maxTargetCount);

                TargetButtonsContainer.Add(button);
            }

            // Show target selection container and confirm button
            TargetSelectionContainer.IsVisible = true;
            ConfirmButton.IsVisible = true;

            // Start countdown timer
            StartCountdown(userActionDeadline, countdownCts.Token);
        }

        private void OnTargetSelected(Button button, int targetId, int maxCount)
        {
            if (selectedTargets.Contains(targetId))
            {
                selectedTargets.Remove(targetId);
                button.BackgroundColor = Colors.LightGray;
                button.TextColor = Colors.Black;
            }
            else if (maxCount == -1 || selectedTargets.Count < maxCount)
            {
                selectedTargets.Add(targetId);
                button.BackgroundColor = Colors.Green;
                button.TextColor = Colors.White;
            }
        }

        private void StartCountdown(int deadline, CancellationToken ct)
        {
            _ = Task.Run(async () =>
            {
                while (!ct.IsCancellationRequested)
                {
                    int now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    int timeRemaining = deadline - now;

                    if (timeRemaining <= 0)
                    {
                        MainThread.BeginInvokeOnMainThread(() => HideTargetSelection());
                        break;
                    }

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        CountdownLabel.Text = $"{timeRemaining}";
                    });

                    await Task.Delay(1000, ct);
                }
            }, ct);
        }

        private void HideTargetSelection()
        {
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
                // Send the selected targets to the server via SignalR
                await connectionManager.SendTargetSelectionAsync(gameId, playerId, selectedTargets.ToList());
                HideTargetSelection();
            }
            catch (Exception ex)
            {
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
                    await connectionManager.Disconnect();
                }
                await Navigation.PopAsync();
            }
        }

        private async void OnStartGameClicked(object? sender, EventArgs e)
        {
            var localization = LocalizationManager.Instance;
            if (connectionManager == null || !isOwner)
            {
                await DisplayAlert(
                    localization.GetString("error"),
                    localization.GetString("only_owner"),
                    localization.GetString("yes"));
                return;
            }

            if (!await connectionManager.StartGameAsync(gameId))
            {
                await DisplayAlert(
                    localization.GetString("error"),
                    localization.GetString("failed_start_game"),
                    localization.GetString("yes"));
                return;
            }

            // Hide the button after successfully starting the game
            MainThread.BeginInvokeOnMainThread(() =>
            {
                StartGameBtn.IsVisible = false;
            });
        }
    }

    public class PlayerDisplay
    {
        public string PlayerInfo { get; set; } = "";
        public string Details { get; set; } = "";
    }
}
