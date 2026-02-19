using System.Text.Json;
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

                // Update player list
                PlayerListContainer.Clear();
                
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

                    var frame = new Frame
                    {
                        Padding = new Thickness(12, 8),
                        CornerRadius = 8,
                        BackgroundColor = isJoined ? Colors.LightGreen : Colors.LightGray,
                        BorderColor = i == playerId ? Colors.Blue : Colors.Transparent,
                    };

                    var stack = new HorizontalStackLayout { Spacing = 12 };
                    
                    var seatLabel = new Label
                    {
                        Text = $"#{i}",
                        FontAttributes = FontAttributes.Bold,
                        FontSize = 16,
                        VerticalOptions = LayoutOptions.Center,
                        WidthRequest = 40
                    };
                    stack.Add(seatLabel);

                    var nicknameLabel = new Label
                    {
                        Text = isJoined ? (string.IsNullOrEmpty(nickname) ? "(No nickname)" : nickname) : LocalizationManager.Instance.GetString("empty_seat"),
                        FontSize = 14,
                        VerticalOptions = LayoutOptions.Center,
                        TextColor = isJoined ? Colors.Black : Colors.Gray
                    };
                    stack.Add(nicknameLabel);

                    if (i == playerId)
                    {
                        var youLabel = new Label
                        {
                            Text = LocalizationManager.Instance.GetString("you"),
                            FontSize = 12,
                            TextColor = Colors.Blue,
                            VerticalOptions = LayoutOptions.Center
                        };
                        stack.Add(youLabel);
                    }

                    // Check if this is the owner
                    int? ownerId = GetInt32Value(roomState, "ownerId");
                    if (ownerId.HasValue && ownerId.Value == i)
                    {
                        var ownerLabel = new Label
                        {
                            Text = LocalizationManager.Instance.GetString("owner"),
                            FontSize = 12,
                            TextColor = Colors.Orange,
                            VerticalOptions = LayoutOptions.Center
                        };
                        stack.Add(ownerLabel);
                    }

                    frame.Content = stack;
                    PlayerListContainer.Add(frame);
                }
            });
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
                await DisplayAlert(
                    localization.GetString("error"),
                    localization.GetString("failed_start_game"),
                    localization.GetString("yes"));
            }
            // If successful, the GameStarted event will handle navigation
        }

        private async void OnSwitchSeatClicked(object? sender, EventArgs e)
        {
            if (connectionManager == null) return;

            var localization = LocalizationManager.Instance;

            if (!int.TryParse(NewSeatEntry.Text, out int newSeat) || newSeat <= 0)
            {
                await DisplayAlert(
                    localization.GetString("error"),
                    localization.GetString("invalid_seat_number"),
                    localization.GetString("yes"));
                return;
            }

            var (success, errorMessage) = await connectionManager.SwitchSeatAsync(gameId, newSeat);
            if (!success)
            {
                await DisplayAlert(
                    localization.GetString("error"),
                    errorMessage,
                    localization.GetString("yes"));
            }
            else
            {
                NewSeatEntry.Text = "";
            }
        }

        private async void OnLeaveRoomClicked(object? sender, EventArgs e)
        {
            var localization = LocalizationManager.Instance;

            bool confirm = await DisplayAlert(
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
