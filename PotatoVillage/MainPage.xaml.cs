using System.Collections.ObjectModel;
using System.Text.Json;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui;

namespace PotatoVillage
{
    public partial class MainPage : ContentPage
    {
        private HubConnectionManager? connectionManager;
        private int gameId;
        private int playerId;
        private bool isGameOwner;

        // Track selected roles
        private HashSet<string> selectedLangRen = new();
        private bool selectedJiaMian = false;
        private bool selectedNvWu = false;
        private bool selectedYuYanJia = false;
        private bool selectedWuZhe = false;
        private bool selectedLieRen = false;
        private HashSet<string> selectedPingMin = new();

        public MainPage()
        {
            InitializeComponent();
            UpdateConnectButtonState();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            // Re-enable all inputs when returning to this page
            ResetUIState();
        }

        private void ResetUIState()
        {
            NicknameEntry.IsEnabled = true;
            HubUrlEntry.IsEnabled = true;
            RoomNumberEntry.IsEnabled = true;
            SeatNumberEntry.IsEnabled = true;
            JoinBtn.IsEnabled = true;

            // Reset connection manager
            connectionManager = null;

            // Update connect button based on role selection
            UpdateConnectButtonState();
        }

        private void UpdateConnectButtonState()
        {
            bool hasRolesSelected = selectedLangRen.Count > 0 || 
                                   selectedJiaMian || 
                                   selectedNvWu || 
                                   selectedYuYanJia || 
                                   selectedWuZhe || 
                                   selectedLieRen ||
                                   selectedPingMin.Count > 0;
            ConnectBtn.IsEnabled = hasRolesSelected;
        }

        private async void OnConnectClicked(object? sender, EventArgs e)
        {
            var hubUrl = HubUrlEntry.Text?.Trim();
            if (string.IsNullOrEmpty(hubUrl))
            {
                await DisplayAlert("Error", "Hub URL is required", "OK");
                return;
            }

            var nickname = NicknameEntry.Text?.Trim() ?? "";
            connectionManager = new HubConnectionManager(nickname);
            connectionManager.ConnectionFailed += async (msg) =>
            {
                await DisplayAlert("Error", msg, "OK");
            };
            connectionManager.Registered += OnRegistered;

            // Connect to hub
            if (!await connectionManager.ConnectAsync(hubUrl))
            {
                return;
            }

            // Build roleDict from selected roles
            var roleDict = new Dictionary<string, int>();

            if (selectedLangRen.Count > 0)
                roleDict["LangRen"] = selectedLangRen.Count;

            if (selectedJiaMian)
                roleDict["JiaMian"] = 1;

            if (selectedNvWu)
                roleDict["NvWu"] = 1;

            if (selectedYuYanJia)
                roleDict["YuYanJia"] = 1;

            if (selectedWuZhe)
                roleDict["WuZhe"] = 1;

            if (selectedLieRen)
                roleDict["LieRen"] = 1;

            if (selectedPingMin.Count > 0)
                roleDict["PingMin"] = selectedPingMin.Count;

            // Always include LangRenSha
            roleDict["LangRenSha"] = 1;

            // Calculate total players
            int totalPlayers = roleDict.Values.Sum() - 1;
            if (totalPlayers == 0)
            {
                await DisplayAlert("Error", "Please select at least one role", "OK");
                return;
            }

            isGameOwner = true;  // Creator is the owner
            if (!await connectionManager.CreateRoomAsync(totalPlayers, roleDict))
            {
                return;
            }
        }

        private async void OnRegistered(int registeredGameId, int registeredPlayerId, bool gameStarted)
        {
            gameId = registeredGameId;
            playerId = registeredPlayerId;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                ConnectBtn.IsEnabled = false;
                NicknameEntry.IsEnabled = false;
                HubUrlEntry.IsEnabled = false;
                RoomNumberEntry.IsEnabled = false;
                SeatNumberEntry.IsEnabled = false;
                JoinBtn.IsEnabled = false;

                if (gameStarted)
                {
                    // Game already started - go directly to game view (reconnection case)
                    await Navigation.PushAsync(new GameView(connectionManager!, gameId, playerId, isGameOwner));
                }
                else
                {
                    // Game not started - go to room view
                    await Navigation.PushAsync(new RoomView(connectionManager!, gameId, playerId, isGameOwner));
                }
            });
        }

        private async void OnJoinClicked(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(HubUrlEntry.Text?.Trim()))
            {
                await DisplayAlert("Error", "Hub URL is required", "OK");
                return;
            }

            if (!int.TryParse(RoomNumberEntry.Text, out int roomNumber) || roomNumber <= 0)
            {
                await DisplayAlert("Error", "Invalid room number", "OK");
                return;
            }

            if (!int.TryParse(SeatNumberEntry.Text, out int seatNumber) || seatNumber <= 0)
            {
                await DisplayAlert("Error", "Invalid seat number (must be a positive number)", "OK");
                return;
            }

            var hubUrl = HubUrlEntry.Text?.Trim();
            var nickname = NicknameEntry.Text?.Trim() ?? "";
            connectionManager = new HubConnectionManager(nickname);
            connectionManager.ConnectionFailed += async (msg) =>
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await DisplayAlert("Error", msg, "OK");
                });
            };
            connectionManager.Registered += OnRegistered;

            // Connect to hub
            if (!await connectionManager.ConnectAsync(hubUrl))
            {
                return;
            }

            isGameOwner = false;  // Joiner is not the owner

            // Join the existing game and check result
            var (success, errorMessage) = await connectionManager.JoinGameAsync(roomNumber, seatNumber);
            if (!success)
            {
                await DisplayAlert("Error", errorMessage, "OK");
                await connectionManager.Disconnect();
                connectionManager = null;
                return;
            }
        }

        // Role selection button handlers
        private void OnLangRenClicked(object? sender, EventArgs e)
        {
            if (sender is Button button)
            {
                // Use button name as unique identifier instead of text
                string role = button.StyleId ?? button.AutomationId ?? "LangRen";
                if (selectedLangRen.Contains(role))
                {
                    selectedLangRen.Remove(role);
                    button.BackgroundColor = Colors.LightGray;
                }
                else
                {
                    selectedLangRen.Add(role);
                    button.BackgroundColor = Colors.Green;
                }
                UpdateConnectButtonState();
            }
        }

        private void OnJiaMianClicked(object? sender, EventArgs e)
        {
            selectedJiaMian = !selectedJiaMian;
            if (sender is Button button)
            {
                button.BackgroundColor = selectedJiaMian ? Colors.Green : Colors.LightGray;
            }
            UpdateConnectButtonState();
        }

        private void OnNvWuClicked(object? sender, EventArgs e)
        {
            selectedNvWu = !selectedNvWu;
            if (sender is Button button)
            {
                button.BackgroundColor = selectedNvWu ? Colors.Green : Colors.LightGray;
            }
            UpdateConnectButtonState();
        }

        private void OnYuYanJiaClicked(object? sender, EventArgs e)
        {
            selectedYuYanJia = !selectedYuYanJia;
            if (sender is Button button)
            {
                button.BackgroundColor = selectedYuYanJia ? Colors.Green : Colors.LightGray;
            }
            UpdateConnectButtonState();
        }

        private void OnWuZheClicked(object? sender, EventArgs e)
        {
            selectedWuZhe = !selectedWuZhe;
            if (sender is Button button)
            {
                button.BackgroundColor = selectedWuZhe ? Colors.Green : Colors.LightGray;
            }
            UpdateConnectButtonState();
        }

        private void OnLieRenClicked(object? sender, EventArgs e)
        {
            selectedLieRen = !selectedLieRen;
            if (sender is Button button)
            {
                button.BackgroundColor = selectedLieRen ? Colors.Green : Colors.LightGray;
            }
            UpdateConnectButtonState();
        }

        private void OnPingMinClicked(object? sender, EventArgs e)
        {
            if (sender is Button button)
            {
                // Use StyleId as unique identifier instead of text
                string role = button.StyleId ?? button.AutomationId ?? "PingMin";
                if (selectedPingMin.Contains(role))
                {
                    selectedPingMin.Remove(role);
                    button.BackgroundColor = Colors.LightGray;
                }
                else
                {
                    selectedPingMin.Add(role);
                    button.BackgroundColor = Colors.Green;
                }
                UpdateConnectButtonState();
            }
        }
    }
}

