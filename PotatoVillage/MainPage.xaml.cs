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

            if (!int.TryParse(GameIdEntry.Text, out gameId) || !int.TryParse(PlayerIdEntry.Text, out playerId))
            {
                await DisplayAlert("Error", "Invalid game or player id", "OK");
                return;
            }

            connectionManager = new HubConnectionManager();
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

            // Auto-register after connecting
            if (!await connectionManager.RegisterAsync(gameId, playerId))
            {
                return;
            }
        }

        private async void OnRegistered(int registeredGameId, int registeredPlayerId)
        {
            gameId = registeredGameId;
            playerId = registeredPlayerId;

            
            MainThread.BeginInvokeOnMainThread(() =>
            {
                StartGameBtn.IsEnabled = true;
                ConnectBtn.IsEnabled = false;
                GameIdEntry.IsEnabled = false;
                PlayerIdEntry.IsEnabled = false;
                HubUrlEntry.IsEnabled = false;
            });
        }

        private async void OnStartGameClicked(object? sender, EventArgs e)
        {
            if (connectionManager == null)
            {
                await DisplayAlert("Error", "Not connected", "OK");
                return;
            }

            if (!await connectionManager.StartGameAsync(gameId))
            {
                return;
            }

            // Navigate to game view
            await Navigation.PushAsync(new GameView(connectionManager, gameId, playerId));
        }
    }
}

