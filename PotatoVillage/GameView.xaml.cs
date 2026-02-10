using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text;
using System.Linq;
using Microsoft.Maui.Controls;

namespace PotatoVillage
{
    public partial class GameView : ContentPage
    {
        private HubConnectionManager? connectionManager;
        private int playerId;
        private int gameId;
        private HashSet<int> selectedTargets = new();
        private CancellationTokenSource? countdownCts;

        // User action dictionary keys
        private const string DictUserAction = "user_action";
        private const string DictUserActionUsers = "user_users";
        private const string DictUserActionTargets = "user_targets";
        private const string DictUserActionTargetsCount = "user_targets_count";
        private const string DictUserActionTargetsHint = "user_targets_hint";
        private const string DictUserActionSelects = "user_selects";

        // Hints dictionary - empty, to be filled in manually
        private static readonly Dictionary<int, string> TargetHints = new Dictionary<int, string>()
        {
            { 1, "LangRen kill" },
            { 2, "Yuyanjia Chayan" },
            { 3, "NvWu act" },
            { 4, "WuZhe act" },
            { 6, "JiaMian chayan" },
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

        public GameView(HubConnectionManager connectionManager, int gameId, int playerId)
        {
            InitializeComponent();
            this.connectionManager = connectionManager;
            this.gameId = gameId;
            this.playerId = playerId;
            PlayerIdLabel.Text = playerId.ToString();
            
            // Subscribe to game state updates
            if (connectionManager != null)
            {
                connectionManager.GameStateUpdated += UpdateGameStatus;
                UpdateGameStatus();
            }
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
            if (TargetHints.TryGetValue(hintIndex, out var hint))
            {
                return hint;
            }
            return string.Empty;
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
                
                // Update day counter
                if (gameDict.TryGetValue("day", out var dayObj))
                {
                    var day = GetInt32Value(dayObj);
                    if (day.HasValue)
                        DayLabel.Text = day.ToString();
                }

                // Update game status (phase, etc.)
                var phaseValue = GetInt32Value(gameDict.TryGetValue("phase", out var phaseObj) ? phaseObj : null);
                var phaseStr = phaseValue == 0 ? "Night" : phaseValue == 1 ? "Day" : "Unknown";
                var dayNum = gameDict.TryGetValue("day", out var dayNum2) ? GetInt32Value(dayNum2)?.ToString() ?? "?" : "?";
                GameStatusLabel.Text = $"Phase: {phaseStr}\nDay: {dayNum}";

                // Get user action related data
                var userAction = GetInt32Value(gameDict.TryGetValue(DictUserAction, out var uaObj) ? uaObj : null) ?? 0;
                var userUsers = GetInt32List(gameDict.TryGetValue(DictUserActionUsers, out var uuObj) ? uuObj : null);
                var userTargets = GetInt32List(gameDict.TryGetValue(DictUserActionTargets, out var utObj) ? utObj : null);
                var userTargetsCount = GetInt32Value(gameDict.TryGetValue(DictUserActionTargetsCount, out var utcObj) ? utcObj : null) ?? 0;
                var userTargetsHint = GetInt32Value(gameDict.TryGetValue(DictUserActionTargetsHint, out var uthObj) ? uthObj : null) ?? -1;

                // Check if self can act
                bool isSelfActable = userAction != 0 && userUsers.Contains(playerId);

                if (userAction != 0 && userUsers.Count > 0 && phaseValue == 0)
                {
                    DisplayCurrentlyActing(userAction, userUsers);
                }
                else
                {
                    HideCurrentlyActing();
                }

                if (isSelfActable)
                {
                    DisplayTargetSelection(userAction, userTargets, userTargetsCount, userTargetsHint);
                }
                else
                {
                    HideTargetSelection();
                }

                // Display self player status
                DisplaySelfPlayerStatus();
            });
        }

        private void DisplaySelfPlayerStatus()
        {
            SelfPlayerStatusLabel.Text = $"Player {playerId}";
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
                    actingRoles.Add(role);
                }
            }

            // Display the acting roles
            string actingText = actingRoles.Count > 0 
                ? string.Join(", ", actingRoles) + " is acting" : "";
            
            GameStatusLabel.Text = $"Currently: {actingText}";

            // Start countdown timer
            StartCountdown(deadline, countdownCts.Token);
        }

        private void HideCurrentlyActing()
        {
            countdownCts?.Cancel();
            countdownCts = null;
        }

        private void DisplayTargetSelection(int userActionDeadline, List<int> availableTargets, int maxTargetCount, int hintIndex)
        {
            // Cancel previous countdown if any
            countdownCts?.Cancel();
            countdownCts = new CancellationTokenSource();
            
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
                    ? "Select any number of targets:" 
                    : $"Select up to {maxTargetCount} target(s):";
                TargetInstructionLabel.Text = instructionText;
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
            foreach (var targetId in allPlayerIds)
            {
                bool isAvailable = availableTargets.Contains(targetId);
                
                var button = new Button
                {
                    Text = targetId.ToString(),
                    CornerRadius = 5,
                    Padding = new Thickness(10, 5),
                    Margin = new Thickness(5),
                    BackgroundColor = Colors.LightGray,
                    TextColor = isAvailable ? Colors.Black : Colors.Gray,
                    IsEnabled = isAvailable,
                    Opacity = isAvailable ? 1.0 : 0.5
                };

                int capturedTargetId = targetId;
                if (isAvailable)
                {
                    button.Clicked += (s, e) => OnTargetSelected(button, capturedTargetId, maxTargetCount);
                }

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
                        CountdownLabel.Text = $"Time: {timeRemaining}s";
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

            try
            {
                // Send the selected targets to the server via SignalR
                await connectionManager.SendTargetSelectionAsync(gameId, playerId, selectedTargets.ToList());
                HideTargetSelection();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to send selection: {ex.Message}", "OK");
            }
        }

        private async void OnDisconnectClicked(object? sender, EventArgs e)
        {
            bool confirm = await DisplayAlert("Disconnect", "Are you sure you want to disconnect?", "Yes", "No");
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
    }

    public class PlayerDisplay
    {
        public string PlayerInfo { get; set; } = "";
        public string Details { get; set; } = "";
    }
}
