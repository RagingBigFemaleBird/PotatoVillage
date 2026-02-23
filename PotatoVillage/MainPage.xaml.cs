using System.Collections.ObjectModel;
using System.Text.Json;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui;
using PotatoVillage.Services;

namespace PotatoVillage
{
    public partial class MainPage : ContentPage
    {
        private HubConnectionManager? connectionManager;
        private int gameId;
        private int playerId;
        private bool isGameOwner;
        private bool isDiscovering = false;

        // Track selected roles
        private HashSet<string> selectedLangRen = new();
        private bool selectedJiaMian = false;
        private bool selectedNvWu = false;
        private bool selectedYuYanJia = false;
        private bool selectedWuZhe = false;
        private bool selectedLieRen = false;
        private bool selectedDaMao = false;
        private bool selectedLaoShu = false;
        private bool selectedBaiChi = false;
        private HashSet<string> selectedPingMin = new();

        public MainPage()
        {
            InitializeComponent();
            UpdateConnectButtonState();

            // Auto-discover server on startup
            _ = DiscoverServerAsync();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            // Re-enable all inputs when returning to this page
            ResetUIState();

            // Re-discover server if URL is empty
            if (string.IsNullOrEmpty(HubUrlEntry.Text))
            {
                _ = DiscoverServerAsync();
            }
        }

        private async Task DiscoverServerAsync()
        {
            if (isDiscovering) return;
            isDiscovering = true;

            try
            {
                // Show discovering status
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    HubUrlEntry.Placeholder = "Discovering server...";
                });

                // Try to discover server
                var serverUrl = await ServerDiscoveryService.DiscoverServerAsync(2000);

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    HubUrlEntry.Text = serverUrl;
                    HubUrlEntry.Placeholder = "Hub URL";
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Server discovery failed: {ex.Message}");

                // Fall back to default URL
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    HubUrlEntry.Text = ServerDiscoveryService.DefaultServerUrl;
                    HubUrlEntry.Placeholder = "Hub URL";
                });
            }
            finally
            {
                isDiscovering = false;
            }
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
                                   selectedDaMao ||
                                   selectedLaoShu ||
                                   selectedBaiChi ||
                                   selectedPingMin.Count > 0;
            ConnectBtn.IsEnabled = hasRolesSelected;
        }

        private async void OnConnectClicked(object? sender, EventArgs e)
        {
            // Disable button immediately to prevent multiple clicks
            ConnectBtn.IsEnabled = false;

            var hubUrl = HubUrlEntry.Text?.Trim();
            if (string.IsNullOrEmpty(hubUrl))
            {
                await DisplayAlert("Error", "Hub URL is required", "OK");
                ConnectBtn.IsEnabled = true;
                return;
            }

            var nickname = NicknameEntry.Text?.Trim() ?? "";
            connectionManager = new HubConnectionManager(nickname);
            connectionManager.ConnectionFailed += async (msg) =>
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await DisplayAlert("Error", msg, "OK");
                    ConnectBtn.IsEnabled = true;
                });
            };
            connectionManager.Registered += OnRegistered;

            // Connect to hub
            if (!await connectionManager.ConnectAsync(hubUrl))
            {
                ConnectBtn.IsEnabled = true;
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

            if (selectedDaMao)
                roleDict["DaMao"] = 1;

            if (selectedLaoShu)
                roleDict["LaoShu"] = 1;

            if (selectedBaiChi)
                roleDict["BaiChi"] = 1;

            if (selectedPingMin.Count > 0)
                roleDict["PingMin"] = selectedPingMin.Count;

            // Always include LangRenSha
            roleDict["LangRenSha"] = 1;

            // Calculate total players
            int totalPlayers = roleDict.Values.Sum() - 1;
            if (totalPlayers == 0)
            {
                await DisplayAlert("Error", "Please select at least one role", "OK");
                ConnectBtn.IsEnabled = true;
                return;
            }

            // Parse duration settings
            int speechDuration = int.TryParse(SpeechDurationEntry.Text, out var sd) ? sd : 120;
            int werewolfDuration = int.TryParse(WerewolfDurationEntry.Text, out var wd) ? wd : 60;
            int godDuration = int.TryParse(GodDurationEntry.Text, out var gd) ? gd : 30;

            isGameOwner = true;  // Creator is the owner
            if (!await connectionManager.CreateRoomAsync(totalPlayers, roleDict, speechDuration, werewolfDuration, godDuration))
            {
                ConnectBtn.IsEnabled = true;
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
            // Disable button immediately to prevent multiple clicks
            JoinBtn.IsEnabled = false;

            if (string.IsNullOrEmpty(HubUrlEntry.Text?.Trim()))
            {
                await DisplayAlert("Error", "Hub URL is required", "OK");
                JoinBtn.IsEnabled = true;
                return;
            }

            if (!int.TryParse(RoomNumberEntry.Text, out int roomNumber) || roomNumber <= 0)
            {
                await DisplayAlert("Error", "Invalid room number", "OK");
                JoinBtn.IsEnabled = true;
                return;
            }

            if (!int.TryParse(SeatNumberEntry.Text, out int seatNumber) || seatNumber <= 0)
            {
                await DisplayAlert("Error", "Invalid seat number (must be a positive number)", "OK");
                JoinBtn.IsEnabled = true;
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
                    JoinBtn.IsEnabled = true;
                });
            };
            connectionManager.Registered += OnRegistered;

            // Connect to hub
            if (!await connectionManager.ConnectAsync(hubUrl))
            {
                JoinBtn.IsEnabled = true;
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
                JoinBtn.IsEnabled = true;
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

        private void OnDaMaoClicked(object? sender, EventArgs e)
        {
            selectedDaMao = !selectedDaMao;
            if (sender is Button button)
            {
                button.BackgroundColor = selectedDaMao ? Colors.Green : Colors.LightGray;
            }
            UpdateConnectButtonState();
        }

        private void OnLaoShuClicked(object? sender, EventArgs e)
        {
            selectedLaoShu = !selectedLaoShu;
            if (sender is Button button)
            {
                button.BackgroundColor = selectedLaoShu ? Colors.Green : Colors.LightGray;
            }
            UpdateConnectButtonState();
        }

        private void OnBaiChiClicked(object? sender, EventArgs e)
        {
            selectedBaiChi = !selectedBaiChi;
            if (sender is Button button)
            {
                button.BackgroundColor = selectedBaiChi ? Colors.Green : Colors.LightGray;
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

        private async void OnAboutClicked(object? sender, EventArgs e)
        {
            var localization = Services.LocalizationManager.Instance;
            var title = localization.GetString("about", "About");
            var message = localization.GetString("about_message", "Author: Bi Wu.\nRole design: Ke Ji.\nVoiceover: Tu dou.");
            var ok = localization.GetString("yes", "OK");

            await DisplayAlert(title, message, ok);
        }
    }
}

