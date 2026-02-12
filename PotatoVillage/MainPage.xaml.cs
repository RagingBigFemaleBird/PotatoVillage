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
        private HashSet<string> selectedPingMin = new();

        public MainPage()
        {
            InitializeComponent();
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
            
            if (selectedPingMin.Count > 0)
                roleDict["PingMin"] = selectedPingMin.Count;

            // Always include LangRenSha
            roleDict["LangRenSha"] = 1;

            // Calculate total players
            int totalPlayers = roleDict.Values.Sum();
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

        private async void OnRegistered(int registeredGameId, int registeredPlayerId)
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

                // Navigate to game view with owner flag
                await Navigation.PushAsync(new GameView(connectionManager, gameId, playerId, isGameOwner));
            });
        }

        private async void OnJoinClicked(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(HubUrlEntry.Text?.Trim()))
            {
                await DisplayAlert("Error", "Hub URL is required", "OK");
                return;
            }

            if (!int.TryParse(RoomNumberEntry.Text, out int roomNumber) || !int.TryParse(SeatNumberEntry.Text, out int seatNumber))
            {
                await DisplayAlert("Error", "Invalid room number or seat number", "OK");
                return;
            }

            var hubUrl = HubUrlEntry.Text?.Trim();
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

            isGameOwner = false;  // Joiner is not the owner
            // Join the existing game
            if (!await connectionManager.JoinGameAsync(roomNumber, seatNumber))
            {
                return;
            }
        }

        // Role selection button handlers
        private void OnLangRenClicked(object? sender, EventArgs e)
        {
            if (sender is Button button)
            {
                string role = button.Text;
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
            }
        }

        private void OnJiaMianClicked(object? sender, EventArgs e)
        {
            selectedJiaMian = !selectedJiaMian;
            if (sender is Button button)
            {
                button.BackgroundColor = selectedJiaMian ? Colors.Green : Colors.LightGray;
            }
        }

        private void OnNvWuClicked(object? sender, EventArgs e)
        {
            selectedNvWu = !selectedNvWu;
            if (sender is Button button)
            {
                button.BackgroundColor = selectedNvWu ? Colors.Green : Colors.LightGray;
            }
        }

        private void OnYuYanJiaClicked(object? sender, EventArgs e)
        {
            selectedYuYanJia = !selectedYuYanJia;
            if (sender is Button button)
            {
                button.BackgroundColor = selectedYuYanJia ? Colors.Green : Colors.LightGray;
            }
        }

        private void OnWuZheClicked(object? sender, EventArgs e)
        {
            selectedWuZhe = !selectedWuZhe;
            if (sender is Button button)
            {
                button.BackgroundColor = selectedWuZhe ? Colors.Green : Colors.LightGray;
            }
        }

        private void OnPingMinClicked(object? sender, EventArgs e)
        {
            if (sender is Button button)
            {
                string role = button.Text;
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
            }
        }
    }
}

