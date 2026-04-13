using System.Text.Json;
using Microsoft.Maui.Controls.Shapes;
using PotatoVillage.Services;

namespace PotatoVillage
{
    public partial class RoomView : ContentPage
    {
        private HubConnectionManager? connectionManager;
        private int gameId;
        private int playerId;
        private bool isOwner;

        public RoomView(HubConnectionManager connectionManager, int gameId, int playerId, bool isOwner)
        {
            InitializeComponent();
            this.connectionManager = connectionManager;
            this.gameId = gameId;
            this.playerId = playerId;
            this.isOwner = isOwner;

            RoomNumberLabel.Text = gameId.ToString();
            YourSeatLabel.Text = playerId.ToString();
            StartGameBtn.IsVisible = isOwner;

            // Subscribe to room state updates
            if (connectionManager != null)
            {
                connectionManager.RoomStateUpdated += UpdateRoomState;
                connectionManager.GameStarted += OnGameStarted;
                UpdateRoomState();
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            if (connectionManager != null)
            {
                connectionManager.RoomStateUpdated -= UpdateRoomState;
                connectionManager.GameStarted -= OnGameStarted;
            }
        }

        private void UpdateRoomState()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (connectionManager == null) return;

                var roomState = connectionManager.RoomState;

                // Update player ID in case it was switched
                playerId = connectionManager.RegisteredPlayerId;
                YourSeatLabel.Text = playerId.ToString();

                int totalPlayers = GetInt32Value(roomState, "totalPlayers") ?? 0;
                var players = GetDictionaryValue(roomState, "players");
                int joinedCount = players.Count;

                PlayersCountLabel.Text = $"{joinedCount}/{totalPlayers}";

                // Enable start button only if all players have joined
                if (isOwner)
                {
                    StartGameBtn.IsEnabled = joinedCount >= totalPlayers;
                }

                // Get owner ID for badge display
                int? ownerId = GetInt32Value(roomState, "ownerId");

                // Update player list with box layout (4 per row)
                PlayerListContainer.Clear();

                // Calculate box width for 4 columns with margins
                // FlexLayout will handle wrapping automatically
                double boxWidth = 80;
                double boxHeight = 90;
                double margin = 4;

                for (int i = 1; i <= totalPlayers; i++)
                {
                    var playerKey = i.ToString();
                    string nickname = "";
                    bool isJoined = false;

                    if (players.TryGetValue(playerKey, out var nicknameObj))
                    {
                        nickname = GetStringFromObject(nicknameObj) ?? "";
                        isJoined = true;
                    }

                    // Create a box border for each player
                    var border = new Border
                    {
                        Padding = new Thickness(6),
                        StrokeShape = new RoundRectangle { CornerRadius = 8 },
                        BackgroundColor = isJoined ? Colors.LightGreen : Colors.LightGray,
                        Stroke = i == playerId ? Colors.Blue : Colors.Transparent,
                        WidthRequest = boxWidth,
                        HeightRequest = boxHeight,
                        Margin = new Thickness(margin),
                    };

                    var stack = new VerticalStackLayout 
                    { 
                        Spacing = 4,
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center
                    };

                    // Seat number (large, centered)
                    var seatLabel = new Label
                    {
                        Text = $"#{i}",
                        FontAttributes = FontAttributes.Bold,
                        FontSize = 20,
                        HorizontalOptions = LayoutOptions.Center,
                        HorizontalTextAlignment = TextAlignment.Center
                    };
                    stack.Add(seatLabel);

                    // Nickname (smaller, centered, truncated)
                    var displayName = isJoined 
                        ? (string.IsNullOrEmpty(nickname) ? "-" : (nickname.Length > 6 ? nickname.Substring(0, 6) + ".." : nickname))
                        : LocalizationManager.Instance.GetString("empty_seat");

                    var nicknameLabel = new Label
                    {
                        Text = displayName,
                        FontSize = 11,
                        HorizontalOptions = LayoutOptions.Center,
                        HorizontalTextAlignment = TextAlignment.Center,
                        TextColor = isJoined ? Colors.Black : Colors.Gray,
                        LineBreakMode = LineBreakMode.TailTruncation,
                        MaxLines = 1
                    };
                    stack.Add(nicknameLabel);

                    // Badge row for "You" and "Owner" indicators
                    var badgeStack = new HorizontalStackLayout
                    {
                        HorizontalOptions = LayoutOptions.Center,
                        Spacing = 2
                    };

                    if (i == playerId)
                    {
                        var youLabel = new Label
                        {
                            Text = LocalizationManager.Instance.GetString("you"),
                            FontSize = 10,
                            TextColor = Colors.White,
                            BackgroundColor = Colors.Blue,
                            Padding = new Thickness(4, 1),
                        };
                        badgeStack.Add(youLabel);
                    }

                    if (ownerId.HasValue && ownerId.Value == i)
                    {
                        var ownerLabel = new Label
                        {
                            Text = LocalizationManager.Instance.GetString("owner"),
                            FontSize = 10,
                            TextColor = Colors.White,
                            BackgroundColor = Colors.Orange,
                            Padding = new Thickness(4, 1),
                        };
                        badgeStack.Add(ownerLabel);
                    }

                    if (badgeStack.Children.Count > 0)
                    {
                        stack.Add(badgeStack);
                    }

                    border.Content = stack;

                    // Add tap gesture to empty seats for switching
                    if (!isJoined)
                    {
                        var seatNumber = i; // Capture for closure
                        var tapGesture = new TapGestureRecognizer();
                        tapGesture.Tapped += async (s, e) => await OnEmptySeatTapped(seatNumber);
                        border.GestureRecognizers.Add(tapGesture);
                    }

                    PlayerListContainer.Add(border);
                }

                // Update roles display
                UpdateRolesDisplay(roomState);
            });
        }

        private void UpdateRolesDisplay(Dictionary<string, object> roomState)
        {
            RolesListContainer.Clear();

            var roles = GetDictionaryValue(roomState, "roles");
            if (roles.Count == 0)
            {
                RolesContainer.IsVisible = false;
                return;
            }

            RolesContainer.IsVisible = true;
            var localization = LocalizationManager.Instance;

            foreach (var role in roles)
            {
                var roleName = role.Key;

                // Filter out LangRenSha as it's the game itself, not a role
                if (roleName == "LangRenSha")
                    continue;

                var count = GetInt32FromObject(role.Value) ?? 1;

                // Get localized role name
                var displayName = localization.GetString(roleName, roleName);

                // Create a badge for each role
                var border = new Border
                {
                    Padding = new Thickness(8, 4),
                    StrokeShape = new RoundRectangle { CornerRadius = 12 },
                    BackgroundColor = GetRoleColor(roleName),
                    Stroke = Colors.Transparent,
                    Margin = new Thickness(2),
                };

                // Always show the count
                var label = new Label
                {
                    Text = $"{displayName} x{count}",
                    FontSize = 12,
                    TextColor = Colors.White,
                    VerticalOptions = LayoutOptions.Center,
                };

                border.Content = label;
                RolesListContainer.Add(border);
            }
        }

        private Color GetRoleColor(string roleName)
        {
            // Wolves - red shades
            if (roleName.StartsWith("LangRen") || roleName == "JiaMian" || roleName == "LangQiang" || 
                roleName == "DaMao" || roleName == "JiXieLang" || roleName == "LangMeiRen" ||
                roleName == "HongTaiLang" || roleName == "TuFu" || roleName == "AwkShiXiangGui" ||
                roleName == "ShiXiangGui" || roleName == "XueYue")
            {
                return Color.FromArgb("#8B0000");
            }
            // Gods - blue shades
            if (roleName == "YuYanJia" || roleName == "TongLingShi" || roleName == "NvWu" || 
                roleName == "WuZhe" || roleName == "LieRen" || roleName == "BaiChi" ||
                roleName == "LaoShu" || roleName == "SheMengRen" || roleName == "Xiong" ||
                roleName == "Thief" || roleName == "MengMianRen" || roleName == "ShouWei" ||
                roleName == "MeiYangYang" || roleName == "LieMoRen" || roleName == "AwkSheMengRen" ||
                roleName == "ShouMuRen")
            {
                return Color.FromArgb("#1E3A8A");
            }
            // Third party - purple
            if (roleName == "YingZi" || roleName == "FuChouZhe" || roleName == "HunZi" || roleName == "GhostBride")
            {
                return Color.FromArgb("#6B21A8");
            }
            // Villagers - green
            if (roleName.StartsWith("PingMin"))
            {
                return Color.FromArgb("#166534");
            }
            // Game mode / special - orange
            if (roleName == "ShenLangGongWu1")
            {
                return Color.FromArgb("#C2410C");
            }
            // Default - gray
            return Color.FromArgb("#4B5563");
        }

        private int? GetInt32FromObject(object? obj)
        {
            if (obj == null) return null;
            if (obj is int intValue) return intValue;
            if (obj is JsonElement je && je.ValueKind == JsonValueKind.Number) return je.GetInt32();
            if (int.TryParse(obj.ToString(), out var parsed)) return parsed;
            return null;
        }

        private void OnGameStarted()
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (connectionManager != null)
                {
                    // Unsubscribe before navigating
                    connectionManager.RoomStateUpdated -= UpdateRoomState;
                    connectionManager.GameStarted -= OnGameStarted;
                }

                // Navigate to game view
                await Navigation.PushAsync(new GameView(connectionManager!, gameId, playerId, isOwner));
                
                // Remove this page from the navigation stack
                Navigation.RemovePage(this);
            });
        }

        private async void OnStartGameClicked(object? sender, EventArgs e)
        {
            if (connectionManager == null || !isOwner) return;

            var localization = LocalizationManager.Instance;
            
            if (!await connectionManager.StartGameAsync(gameId))
            {
                await DisplayAlertAsync(
                    localization.GetString("error"),
                    localization.GetString("failed_start_game"),
                    localization.GetString("yes"));
            }
            // If successful, the GameStarted event will handle navigation
        }

        private async Task OnEmptySeatTapped(int newSeat)
        {
            if (connectionManager == null) return;

            var localization = LocalizationManager.Instance;

            var (success, errorMessage) = await connectionManager.SwitchSeatAsync(gameId, newSeat);
            if (!success)
            {
                await DisplayAlertAsync(
                    localization.GetString("error"),
                    errorMessage,
                    localization.GetString("yes"));
            }
        }

        private async void OnLeaveRoomClicked(object? sender, EventArgs e)
        {
            var localization = LocalizationManager.Instance;

            bool confirm = await DisplayAlertAsync(
                localization.GetString("leave_room"),
                localization.GetString("leave_room_confirm"),
                localization.GetString("yes"),
                localization.GetString("no"));

            if (confirm)
            {
                if (connectionManager != null)
                {
                    connectionManager.RoomStateUpdated -= UpdateRoomState;
                    connectionManager.GameStarted -= OnGameStarted;

                    // Notify server that we're leaving
                    await connectionManager.LeaveGameAsync(gameId);
                    await connectionManager.Disconnect();
                }
                await Navigation.PopAsync();
            }
        }

        private int? GetInt32Value(Dictionary<string, object> dict, string key)
        {
            if (!dict.TryGetValue(key, out var obj)) return null;
            
            if (obj is int intValue) return intValue;
            if (obj is JsonElement je && je.ValueKind == JsonValueKind.Number) return je.GetInt32();
            
            return null;
        }

        private string? GetStringFromObject(object? obj)
        {
            if (obj == null) return null;
            if (obj is string str) return str;
            if (obj is JsonElement je && je.ValueKind == JsonValueKind.String) return je.GetString();
            return obj.ToString();
        }

        private Dictionary<string, object> GetDictionaryValue(Dictionary<string, object> dict, string key)
        {
            if (!dict.TryGetValue(key, out var obj)) return new();
            
            if (obj is Dictionary<string, object> d) return d;
            
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
    }
}
