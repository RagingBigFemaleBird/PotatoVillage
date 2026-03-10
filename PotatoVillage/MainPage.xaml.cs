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

        // Preferences key for storing nickname
        private const string NicknamePreferenceKey = "user_nickname";

        // Track selected roles
        private HashSet<string> selectedLangRen = new();
        private bool selectedJiaMian = false;
        private bool selectedNvWu = false;
        private bool selectedYuYanJia = false;
        private bool selectedTongLingShi = false;
        private bool selectedWuZhe = false;
        private bool selectedLieRen = false;
        private bool selectedLangQiang = false;
        private bool selectedDaMao = false;
        private bool selectedLaoShu = false;
        private bool selectedBaiChi = false;
        private bool selectedSheMengRen = false;
        private bool selectedXiong = false;
        private bool selectedShenLangGongWu1 = false;
        private bool selectedThief = false;
        private bool selectedMengMianRen = false;
        private bool selectedShouWei = false;
        private bool selectedYingZi = false;
        private bool selectedFuChouZhe = false;
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

            // Load stored nickname if the entry is empty
            if (string.IsNullOrEmpty(NicknameEntry.Text))
            {
                NicknameEntry.Text = Preferences.Get(NicknamePreferenceKey, "");
            }

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
                                   selectedTongLingShi ||
                                   selectedWuZhe || 
                                   selectedLieRen ||
                                   selectedLangQiang ||
                                   selectedDaMao ||
                                   selectedLaoShu ||
                                   selectedBaiChi ||
                                   selectedSheMengRen ||
                                   selectedXiong ||
                                   selectedShenLangGongWu1 ||
                                   selectedThief ||
                                   selectedMengMianRen ||
                                   selectedShouWei ||
                                   selectedYingZi ||
                                   selectedFuChouZhe ||
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

            // Save nickname to preferences
            Preferences.Set(NicknamePreferenceKey, nickname);

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

            if (selectedTongLingShi)
                roleDict["TongLingShi"] = 1;

            if (selectedWuZhe)
                roleDict["WuZhe"] = 1;

            if (selectedLieRen)
                roleDict["LieRen"] = 1;

            if (selectedLangQiang)
                roleDict["LangQiang"] = 1;

            if (selectedDaMao)
                roleDict["DaMao"] = 1;

            if (selectedLaoShu)
                roleDict["LaoShu"] = 1;

            if (selectedBaiChi)
                roleDict["BaiChi"] = 1;

            if (selectedSheMengRen)
                roleDict["SheMengRen"] = 1;

            if (selectedXiong)
                roleDict["Xiong"] = 1;

            if (selectedShenLangGongWu1)
                roleDict["ShenLangGongWu1"] = 1;

            if (selectedThief)
                roleDict["Thief"] = 1;

            if (selectedMengMianRen)
                roleDict["MengMianRen"] = 1;

            if (selectedShouWei)
                roleDict["ShouWei"] = 1;

            if (selectedYingZi)
                roleDict["YingZi"] = 1;

            if (selectedFuChouZhe)
                roleDict["FuChouZhe"] = 1;

            if (selectedPingMin.Count > 0)
                roleDict["PingMin"] = selectedPingMin.Count;

            // Always include LangRenSha
            roleDict["LangRenSha"] = 1;

            // Calculate total players
            int totalPlayers = roleDict.Values.Sum() - 1;
            if (roleDict.ContainsKey("ShenLangGongWu1"))
                totalPlayers -= 1;
            // Thief requires 3 extra roles, so actual player count is 3 less
            if (roleDict.ContainsKey("Thief"))
                totalPlayers -= 3;
            if (totalPlayers <= 0)
            {
                await DisplayAlert("Error", "Please select at least one role", "OK");
                ConnectBtn.IsEnabled = true;
                return;
            }

            // Parse duration settings
            int speechDuration = int.TryParse(SpeechDurationEntry.Text, out var sd) ? sd : 120;
            int werewolfDuration = int.TryParse(WerewolfDurationEntry.Text, out var wd) ? wd : 60;
            int godDuration = int.TryParse(GodDurationEntry.Text, out var gd) ? gd : 30;
            int roundTableMode = RoundTableModeSwitch.IsToggled ? 1 : 0;
            int ownerControlEnabled = OwnerControlSwitch.IsToggled ? 1 : 0;
            int seatCounterClockwise = SeatCounterClockwiseSwitch.IsToggled ? 1 : 0;

            isGameOwner = true;  // Creator is the owner
            if (!await connectionManager.CreateRoomAsync2(totalPlayers, roleDict, speechDuration, werewolfDuration, godDuration, roundTableMode, ownerControlEnabled, seatCounterClockwise))
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

            // Save nickname to preferences
            Preferences.Set(NicknamePreferenceKey, nickname);

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

        private void OnTongLingShiClicked(object? sender, EventArgs e)
        {
            selectedTongLingShi = !selectedTongLingShi;
            if (sender is Button button)
            {
                button.BackgroundColor = selectedTongLingShi ? Colors.Green : Colors.LightGray;
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

        private void OnLangQiangClicked(object? sender, EventArgs e)
        {
            selectedLangQiang = !selectedLangQiang;
            if (sender is Button button)
            {
                button.BackgroundColor = selectedLangQiang ? Colors.Green : Colors.LightGray;
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

        private void OnSheMengRenClicked(object? sender, EventArgs e)
        {
            selectedSheMengRen = !selectedSheMengRen;
            if (sender is Button button)
            {
                button.BackgroundColor = selectedSheMengRen ? Colors.Green : Colors.LightGray;
            }
            UpdateConnectButtonState();
        }

        private void OnXiongClicked(object? sender, EventArgs e)
        {
            selectedXiong = !selectedXiong;
            if (sender is Button button)
            {
                button.BackgroundColor = selectedXiong ? Colors.Green : Colors.LightGray;
            }
            UpdateConnectButtonState();
        }

        private void OnShenLangGongWu1Clicked(object? sender, EventArgs e)
        {
            selectedShenLangGongWu1 = !selectedShenLangGongWu1;
            if (sender is Button button)
            {
                button.BackgroundColor = selectedShenLangGongWu1 ? Colors.Green : Colors.LightGray;
            }
            UpdateConnectButtonState();
        }

        private void OnThiefClicked(object? sender, EventArgs e)
        {
            selectedThief = !selectedThief;
            if (sender is Button button)
            {
                button.BackgroundColor = selectedThief ? Colors.Green : Colors.LightGray;
            }
            UpdateConnectButtonState();
        }

        private void OnMengMianRenClicked(object? sender, EventArgs e)
        {
            selectedMengMianRen = !selectedMengMianRen;
            if (sender is Button button)
            {
                button.BackgroundColor = selectedMengMianRen ? Colors.Green : Colors.LightGray;
            }
            UpdateConnectButtonState();
        }

        private void OnShouWeiClicked(object? sender, EventArgs e)
        {
            selectedShouWei = !selectedShouWei;
            if (sender is Button button)
            {
                button.BackgroundColor = selectedShouWei ? Colors.Green : Colors.LightGray;
            }
            UpdateConnectButtonState();
        }

        private void OnYingZiClicked(object? sender, EventArgs e)
        {
            selectedYingZi = !selectedYingZi;
            if (sender is Button button)
            {
                button.BackgroundColor = selectedYingZi ? Colors.Green : Colors.LightGray;
            }
            UpdateConnectButtonState();
        }

        private void OnFuChouZheClicked(object? sender, EventArgs e)
        {
            selectedFuChouZhe = !selectedFuChouZhe;
            if (sender is Button button)
            {
                button.BackgroundColor = selectedFuChouZhe ? Colors.Green : Colors.LightGray;
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

