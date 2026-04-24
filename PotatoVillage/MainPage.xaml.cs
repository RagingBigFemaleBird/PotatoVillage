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

        // Preferences keys
        private const string NicknamePreferenceKey = "user_nickname";
        private const string SpeechDurationPreferenceKey = "create_game_speech_duration";
        private const string WerewolfDurationPreferenceKey = "create_game_werewolf_duration";
        private const string GodDurationPreferenceKey = "create_game_god_duration";
        private const string SheriffExtraTimePreferenceKey = "create_game_sheriff_extra_time";
        private const string RoundTableModePreferenceKey = "create_game_round_table_mode";
        private const string OwnerControlPreferenceKey = "create_game_owner_control";
        private const string SeatCounterClockwisePreferenceKey = "create_game_seat_counter_clockwise";
        private const string ViewRoleInTurnPreferenceKey = "create_game_view_role_in_turn";
        private const string RoleViewingGroupSizePreferenceKey = "create_game_role_viewing_group_size";

        // UI colors for popups
        private static readonly Color PopupTextColor = Color.FromArgb("#FFF8DC");
        private static readonly Color ButtonDefaultColor = Color.FromArgb("#2D2D2D"); // Very dark gray
        private static readonly Color ButtonSelectedColor = Color.FromArgb("#8B0000"); // Dark red
        private static readonly Color SwitchOnColor = Color.FromArgb("#8B0000"); // Dark red

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
        private bool selectedHunZi = false;
        private bool selectedJiXieLang = false;
        private bool selectedZhuangJiaLang = false;
        private bool selectedLangMeiRen = false;
        private bool selectedAwkShiXiangGui = false;
        private bool selectedGhostBride = false;
        private bool selectedMeiYangYang = false;
        private bool selectedHongTaiLang = false;
        private bool selectedLieMoRen = false;
        private bool selectedTuFu = false;
        private bool selectedShouMuRen = false;
        private bool selectedAwkSheMengRen = false;
        private bool selectedShiXiangGui = false;
        private bool selectedXueYue = false;
        private bool selectedMengYan = false;
        private bool selectedMoShuShi = false;
        private bool selectedGuiShuShi = false;
        private bool selectedDingXuWangZi = false;
        private bool selectedAwkYuYanJia = false;
        private bool selectedXunXiangMeiYing = false;
        private HashSet<string> selectedPingMin = new();

        // Dictionary to store button references for template application
        private Dictionary<string, Button> roleButtons = new();

        // Role templates: when a template role is selected as the first/only role, auto-select the whole template
        private static readonly Dictionary<string, Dictionary<string, int>> RoleTemplates = new()
        {
            ["AwkSheMengRen"] = new()
            {
                ["LangRen"] = 1,
                ["LangQiang"] = 1,
                ["AwkShiXiangGui"] = 1,
                ["YuYanJia"] = 1,
                ["NvWu"] = 1,
                ["LieRen"] = 1,
                ["ShouMuRen"] = 1,
                ["PingMin"] = 4
            },
            ["ShenLangGongWu1"] = new()
            {
                ["LangRen"] = 2,
                ["LangQiang"] = 1,
                ["YuYanJia"] = 1,
                ["NvWu"] = 1,
                ["LieRen"] = 1,
                ["SheMengRen"] = 1,
                ["Xiong"] = 1,
                ["PingMin"] = 4
            },
            ["Thief"] = new()
            {
                ["LangRen"] = 3,
                ["LangQiang"] = 1,
                ["TongLingShi"] = 1,
                ["NvWu"] = 1,
                ["LieRen"] = 1,
                ["SheMengRen"] = 1,
                ["MengMianRen"] = 1,
                ["PingMin"] = 4
            },
            ["JiXieLang"] = new()
            {
                ["LangRen"] = 3,
                ["TongLingShi"] = 1,
                ["NvWu"] = 1,
                ["LieRen"] = 1,
                ["ShouWei"] = 1,
                ["PingMin"] = 4
            },
            ["ZhuangJiaLang"] = new()
            {
                ["LangRen"] = 2,
                ["JiXieLang"] = 1,
                ["TongLingShi"] = 1,
                ["NvWu"] = 1,
                ["LieRen"] = 1,
                ["ShouWei"] = 1,
                ["PingMin"] = 4
            },
            ["YingZi"] = new()
            {
                ["LangRen"] = 4,
                ["YuYanJia"] = 1,
                ["NvWu"] = 1,
                ["LieRen"] = 1,
                ["ShouWei"] = 1,
                ["FuChouZhe"] = 1,
                ["PingMin"] = 4
            },
            ["FuChouZhe"] = new()
            {
                ["LangRen"] = 4,
                ["YuYanJia"] = 1,
                ["NvWu"] = 1,
                ["LieRen"] = 1,
                ["ShouWei"] = 1,
                ["YingZi"] = 1,
                ["PingMin"] = 4
            },
            ["GhostBride"] = new()
            {
                ["LangRen"] = 4,
                ["YuYanJia"] = 1,
                ["NvWu"] = 1,
                ["LieRen"] = 1,
                ["ShouWei"] = 1,
                ["PingMin"] = 3
            },
            ["LaoShu"] = new()
            {
                ["LangRen"] = 3,
                ["DaMao"] = 1,
                ["YuYanJia"] = 1,
                ["NvWu"] = 1,
                ["LieRen"] = 1,
                ["PingMin"] = 4
            },
            ["DaMao"] = new()
            {
                ["LangRen"] = 3,
                ["LaoShu"] = 1,
                ["YuYanJia"] = 1,
                ["NvWu"] = 1,
                ["LieRen"] = 1,
                ["PingMin"] = 4
            },
            ["MeiYangYang"] = new()
            {
                ["LangRen"] = 3,
                ["HongTaiLang"] = 1,
                ["YuYanJia"] = 1,
                ["NvWu"] = 1,
                ["LieRen"] = 1,
                ["PingMin"] = 4
            },
            ["HongTaiLang"] = new()
            {
                ["LangRen"] = 3,
                ["MeiYangYang"] = 1,
                ["YuYanJia"] = 1,
                ["NvWu"] = 1,
                ["LieRen"] = 1,
                ["PingMin"] = 4
            },
            ["JiaMian"] = new()
            {
                ["LangRen"] = 3,
                ["YuYanJia"] = 1,
                ["NvWu"] = 1,
                ["WuZhe"] = 1,
                ["BaiChi"] = 1,
                ["PingMin"] = 4
            },
            ["XueYue"] = new()
            {
                ["LangRen"] = 3,
                ["YuYanJia"] = 1,
                ["NvWu"] = 1,
                ["LieMoRen"] = 1,
                ["BaiChi"] = 1,
                ["PingMin"] = 4
            },
            ["ShiXiangGui"] = new()
            {
                ["LangRen"] = 3,
                ["YuYanJia"] = 1,
                ["NvWu"] = 1,
                ["LieRen"] = 1,
                ["ShouMuRen"] = 1,
                ["PingMin"] = 4
            },
            ["AwkShiXiangGui"] = new()
            {
                ["LangRen"] = 2,
                ["YuYanJia"] = 1,
                ["NvWu"] = 1,
                ["LieRen"] = 1,
                ["ShouMuRen"] = 1,
                ["ShouWei"] = 1,
                ["PingMin"] = 4
            },
            ["Tufu"] = new()
            {
                ["LangRen"] = 4,
                ["YuYanJia"] = 1,
                ["NvWu"] = 1,
                ["LieRen"] = 1,
                ["ShouWei"] = 1,
                ["LieMoRen"] = 1,
                ["PingMin"] = 5
            },
            ["XunXiangMeiYing"] = new()
            {
                ["LangRen"] = 3,
                ["AwkYuYanJia"] = 1,
                ["NvWu"] = 1,
                ["LieRen"] = 1,
                ["ShouWei"] = 1,
                ["PingMin"] = 4
            },
            ["GuiShuShi"] = new()
            {
                ["LangRen"] = 3,
                ["YuYanJia"] = 1,
                ["NvWu"] = 1,
                ["MoShuShi"] = 1,
                ["DingXuWangZi"] = 1,
                ["PingMin"] = 4
            },
            ["MengYan"] = new()
            {
                ["LangRen"] = 3,
                ["YuYanJia"] = 1,
                ["NvWu"] = 1,
                ["LieRen"] = 1,
                ["ShouWei"] = 1,
                ["PingMin"] = 4
            },
        };

        // Server URL (discovered at startup, not persisted)
        private string currentServerUrl = "";

        // Session-level settings for join game popup
        private string sessionRoomNumber = "";
        private string sessionSeatNumber = "";

        // Duration settings (stored for create game popup)
        private int speechDuration;
        private int werewolfDuration;
        private int godDuration;
        private int sheriffExtraTime;
        private bool roundTableMode;
        private bool ownerControlEnabled;
        private bool seatCounterClockwise;
        private bool viewRoleInTurn;
        private int roleViewingGroupSize;

        // Track if server discovery has been done this session
        private static bool serverDiscoveryDone = false;

        public MainPage()
        {
            InitializeComponent();

            // Auto-discover server only once on first startup
            if (!serverDiscoveryDone)
            {
                _ = DiscoverServerAsync();
            }
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

            // Load create game settings from preferences
            speechDuration = Preferences.Get(SpeechDurationPreferenceKey, 95);
            werewolfDuration = Preferences.Get(WerewolfDurationPreferenceKey, 125);
            godDuration = Preferences.Get(GodDurationPreferenceKey, 18);
            sheriffExtraTime = Preferences.Get(SheriffExtraTimePreferenceKey, 0);
            roundTableMode = Preferences.Get(RoundTableModePreferenceKey, false);
            ownerControlEnabled = Preferences.Get(OwnerControlPreferenceKey, true);
            seatCounterClockwise = Preferences.Get(SeatCounterClockwisePreferenceKey, false);
            viewRoleInTurn = Preferences.Get(ViewRoleInTurnPreferenceKey, false);
            roleViewingGroupSize = Preferences.Get(RoleViewingGroupSizePreferenceKey, 3);
        }

        private async Task DiscoverServerAsync()
        {
            if (isDiscovering) return;
            isDiscovering = true;

            try
            {
                // Try to discover server
                var serverUrl = await ServerDiscoveryService.DiscoverServerAsync(2000);
                currentServerUrl = serverUrl;
                serverDiscoveryDone = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Server discovery failed: {ex.Message}");
                // Fall back to default URL
                currentServerUrl = ServerDiscoveryService.DefaultServerUrl;
            }
            finally
            {
                isDiscovering = false;
            }
        }

        private void ResetUIState()
        {
            NicknameEntry.IsEnabled = true;
            JoinGameBtn.IsEnabled = true;
            CreateGameBtn.IsEnabled = true;
            ChangeServerBtn.IsEnabled = true;
            AboutBtn.IsEnabled = true;

            // Restore original button colors
            var originalColor = Colors.Transparent;
            JoinGameBtn.BackgroundColor = originalColor;
            CreateGameBtn.BackgroundColor = originalColor;
            ChangeServerBtn.BackgroundColor = originalColor;
            AboutBtn.BackgroundColor = originalColor;

            // Reset connection manager
            connectionManager = null;
        }

        private bool HasRolesSelected()
        {
            return selectedLangRen.Count > 0 ||
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
                   selectedHunZi ||
                   selectedJiXieLang ||
                   selectedZhuangJiaLang ||
                   selectedLangMeiRen ||
                   selectedAwkShiXiangGui ||
                   selectedGhostBride ||
                   selectedMeiYangYang ||
                   selectedHongTaiLang ||
                   selectedLieMoRen ||
                   selectedTuFu ||
                   selectedShouMuRen ||
                   selectedAwkSheMengRen ||
                   selectedShiXiangGui ||
                   selectedXueYue ||
                   selectedMengYan ||
                   selectedMoShuShi ||
                   selectedGuiShuShi ||
                   selectedDingXuWangZi ||
                   selectedAwkYuYanJia ||
                   selectedXunXiangMeiYing ||
                   selectedPingMin.Count > 0;
        }

        private async void OnJoinGameClicked(object? sender, EventArgs e)
        {
            var localization = Services.LocalizationManager.Instance;

            // Create a temporary connection to fetch pending games
            if (string.IsNullOrEmpty(currentServerUrl))
            {
                await DisplayAlertAsync(localization.GetString("error"), localization.GetString("server_url_required"), localization.GetString("yes"));
                return;
            }

            var nickname = NicknameEntry.Text?.Trim() ?? "";
            Preferences.Set(NicknamePreferenceKey, nickname);

            HubConnectionManager? tempConnectionManager = null;

            // Wrapped in array so closures (LoadPendingGames, click/tap handlers,
            // popup.Disappearing) can both read and update the same flag.
            // Set to true as soon as the popup starts going away, so any in-flight
            // ConnectAsync/GetPendingGamesAsync continuation skips touching the
            // (now disposed) UI elements.
            var popupDismissed = new bool[1];

            try
            {
                tempConnectionManager = new HubConnectionManager(nickname);

                // Show loading indicator
                var loadingLabel = new Label
                {
                    Text = localization.GetString("loading", "Loading..."),
                    FontSize = 16,
                    TextColor = PopupTextColor,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                };

                var pendingGamesContainer = new FlexLayout
                {
                    Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
                    JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Start,
                    AlignContent = Microsoft.Maui.Layouts.FlexAlignContent.Start,
                    Margin = new Thickness(0, 10, 0, 10)
                };

                var scrollView = new ScrollView { Content = pendingGamesContainer, HeightRequest = 300 };

                // Manual join section (secondary option)
                var manualJoinLabel = new Label
                {
                    Text = localization.GetString("join_by_room_seat", "Or join by room and seat number:"),
                    FontSize = 14,
                    TextColor = PopupTextColor,
                    Margin = new Thickness(0, 20, 0, 10)
            };

            var roomEntry = new Entry
            {
                Placeholder = localization.GetString("room_number", "Room Number"),
                Text = sessionRoomNumber,
                Keyboard = Keyboard.Numeric,
                TextColor = PopupTextColor,
                BackgroundColor = Colors.Transparent,
                HorizontalOptions = LayoutOptions.Fill
            };

            var seatEntry = new Entry
            {
                Placeholder = localization.GetString("seat_number", "Seat Number"),
                Text = sessionSeatNumber,
                Keyboard = Keyboard.Numeric,
                TextColor = PopupTextColor,
                BackgroundColor = Colors.Transparent,
                HorizontalOptions = LayoutOptions.Fill
            };

            var manualJoinBtn = new Button
            {
                Text = localization.GetString("join", "Join"),
                BackgroundColor = Colors.Transparent,
                TextColor = Colors.White,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var refreshBtn = new Button
            {
                Text = localization.GetString("refresh", "Refresh"),
                BackgroundColor = Colors.Transparent,
                TextColor = Colors.White,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var backBtn = new Button
            {
                Text = localization.GetString("back", "Back"),
                BackgroundColor = Colors.Transparent,
                TextColor = Colors.White,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var mainStack = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 10,
                Children =
                {
                    new Label { Text = localization.GetString("join_existing_game", "Join Existing Game"), FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = PopupTextColor },
                    new Label { Text = localization.GetString("available_games", "Available Games:"), FontSize = 14, TextColor = PopupTextColor },
                    scrollView,
                    refreshBtn,
                    manualJoinLabel,
                    roomEntry,
                    seatEntry,
                    manualJoinBtn,
                    backBtn
                }
            };

            var popup = new ContentPage
            {
                Title = localization.GetString("join_existing_game", "Join Game"),
                Content = new Grid
                {
                    Children =
                    {
                        new Image
                        {
                            Source = "bg.png",
                            Aspect = Aspect.AspectFill,
                            HorizontalOptions = LayoutOptions.Fill,
                            VerticalOptions = LayoutOptions.Fill
                        },
                        new ScrollView { Content = mainStack }
                    }
                }
            };
            NavigationPage.SetHasNavigationBar(popup, false);

            // Mark the popup as dismissed as soon as it starts going away, so any
            // in-flight LoadPendingGames continuation skips UI mutations.
            popup.Disappearing += (_, _) => popupDismissed[0] = true;

            // Function to load pending games
            async Task LoadPendingGames()
            {
                if (popupDismissed[0]) return;

                pendingGamesContainer.Clear();
                pendingGamesContainer.Children.Add(loadingLabel);

                bool needsDisconnect = false;
                try
                {
                    if (!tempConnectionManager.IsConnected)
                    {
                        bool connected = await tempConnectionManager.ConnectAsync(currentServerUrl);
                        if (popupDismissed[0])
                        {
                            // User left the popup while we were connecting. Clean up
                            // and bail out before touching any UI element.
                            if (connected)
                            {
                                try { await tempConnectionManager.Disconnect(); } catch { }
                            }
                            return;
                        }
                        if (!connected)
                        {
                            // Server unreachable / down - show error and stop.
                            // Without this guard the code below would call GetPendingGamesAsync
                            // on a null connection and silently render "no games available",
                            // and any later refresh attempt on the dead manager could crash.
                            pendingGamesContainer.Clear();
                            pendingGamesContainer.Children.Add(new Label
                            {
                                Text = localization.GetString("server_unreachable", "Server is unreachable. Please try again later."),
                                FontSize = 14,
                                TextColor = Colors.Red,
                                HorizontalOptions = LayoutOptions.Center,
                                HorizontalTextAlignment = TextAlignment.Center
                            });
                            return;
                        }
                        needsDisconnect = connected;
                    }

                    var pendingGames = await tempConnectionManager.GetPendingGamesAsync();

                    if (popupDismissed[0]) return;

                    pendingGamesContainer.Clear();

                    if (pendingGames.Count == 0)
                    {
                        pendingGamesContainer.Children.Add(new Label
                        {
                            Text = localization.GetString("no_pending_games", "No games available. Create one!"),
                            FontSize = 14,
                            TextColor = PopupTextColor,
                            HorizontalOptions = LayoutOptions.Center
                        });
                    }
                    else
                    {
                        foreach (var game in pendingGames)
                        {
                            var gameBox = CreatePendingGameBox(game, localization, popup, tempConnectionManager, popupDismissed);
                            pendingGamesContainer.Children.Add(gameBox);
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (popupDismissed[0])
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to load pending games (popup already dismissed): {ex.Message}");
                        return;
                    }
                    try
                    {
                        pendingGamesContainer.Clear();
                        pendingGamesContainer.Children.Add(new Label
                        {
                            Text = localization.GetString("failed_to_load_games", "Failed to load games"),
                            FontSize = 14,
                            TextColor = Colors.Red,
                            HorizontalOptions = LayoutOptions.Center
                        });
                    }
                    catch { /* popup may have been torn down already */ }
                    System.Diagnostics.Debug.WriteLine($"Failed to load pending games: {ex.Message}");
                }
                finally
                {
                    if (needsDisconnect)
                    {
                        try
                        {
                            await tempConnectionManager.Disconnect();
                        }
                        catch { /* Ignore disconnect errors */ }
                    }
                }
            }

            refreshBtn.Clicked += async (s, args) =>
            {
                if (popupDismissed[0]) return;
                await LoadPendingGames();
            };

            backBtn.Clicked += async (s, args) =>
            {
                if (popupDismissed[0]) return;
                popupDismissed[0] = true;

                // Remember values for session
                sessionRoomNumber = roomEntry.Text ?? "";
                sessionSeatNumber = seatEntry.Text ?? "";

                // Pop the modal FIRST so the UI is never blocked. We must NOT await
                // Disconnect() here: if ConnectAsync is still in progress on the temp
                // manager, HubConnection.StopAsync can block/deadlock waiting for the
                // in-flight StartAsync, which would freeze the app on a fast Back tap.
                await Navigation.PopModalAsync();

                // Fire-and-forget cleanup. LoadPendingGames's finally block will also
                // disconnect once its in-flight ConnectAsync/GetPendingGamesAsync
                // returns, so this is just best-effort.
                _ = Task.Run(async () =>
                {
                    try { await tempConnectionManager.Disconnect(); } catch { }
                });
            };

            manualJoinBtn.Clicked += async (s, args) =>
            {
                if (popupDismissed[0]) return;

                // Remember values for session
                sessionRoomNumber = roomEntry.Text ?? "";
                sessionSeatNumber = seatEntry.Text ?? "";

                if (!int.TryParse(roomEntry.Text, out int roomNumber) || roomNumber <= 0)
                {
                    await DisplayAlertAsync(localization.GetString("error"), localization.GetString("invalid_room_number"), localization.GetString("yes"));
                    return;
                }

                if (!int.TryParse(seatEntry.Text, out int seatNumber) || seatNumber <= 0)
                {
                    await DisplayAlertAsync(localization.GetString("error"), localization.GetString("invalid_seat_number"), localization.GetString("yes"));
                    return;
                }

                popupDismissed[0] = true;

                // Pop modal first; fire-and-forget Disconnect (see backBtn for rationale).
                await Navigation.PopModalAsync();
                _ = Task.Run(async () =>
                {
                    try { await tempConnectionManager.Disconnect(); } catch { }
                });

                await JoinGameAsync(roomNumber, seatNumber);
            };

            await Navigation.PushModalAsync(new NavigationPage(popup));

            // Load pending games after popup is shown
            await LoadPendingGames();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnJoinGameClicked error: {ex.Message}");

                // Clean up temp connection if it exists
                if (tempConnectionManager != null)
                {
                    try
                    {
                        await tempConnectionManager.Disconnect();
                    }
                    catch { /* Ignore cleanup errors */ }
                }

                // Show error and return to main page
                await DisplayAlertAsync(localization.GetString("error"), localization.GetString("failed_to_load_games", "Failed to load games"), localization.GetString("yes"));
            }
        }

        private Border CreatePendingGameBox(HubConnectionManager.PendingGameInfo game, LocalizationManager localization, ContentPage popup, HubConnectionManager tempConnectionManager, bool[] popupDismissed)
        {
            var border = new Border
            {
                Padding = new Thickness(12),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                BackgroundColor = Color.FromArgb("#3D3D3D"),
                Stroke = Colors.Gray,
                StrokeThickness = 1,
                WidthRequest = 150,
                HeightRequest = 90,
                Margin = new Thickness(5),
            };

            var stack = new VerticalStackLayout
            {
                Spacing = 4,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };

            // Room number (large, centered)
            var roomLabel = new Label
            {
                Text = $"{localization.GetString("room_number_label", "Room:")} {game.GameId}",
                FontAttributes = FontAttributes.Bold,
                FontSize = 18,
                HorizontalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center,
                TextColor = PopupTextColor
            };
            stack.Add(roomLabel);

            // Owner name (smaller, centered)
            var ownerName = string.IsNullOrEmpty(game.OwnerName) ? "-" : game.OwnerName;
            if (ownerName.Length > 10) ownerName = ownerName.Substring(0, 10) + "..";

            var ownerLabel = new Label
            {
                Text = ownerName,
                FontSize = 13,
                HorizontalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center,
                TextColor = PopupTextColor,
                LineBreakMode = LineBreakMode.TailTruncation,
                MaxLines = 1
            };
            stack.Add(ownerLabel);

            // Player count
            var countLabel = new Label
            {
                Text = $"{game.JoinedPlayers}/{game.TotalPlayers}",
                FontSize = 12,
                HorizontalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center,
                TextColor = Colors.LightGray
            };
            stack.Add(countLabel);

            border.Content = stack;

            // Add tap gesture to join this game
            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += async (s, e) =>
            {
                if (popupDismissed[0]) return;
                popupDismissed[0] = true;

                // Pop modal first; fire-and-forget Disconnect so a stuck StopAsync
                // (when ConnectAsync is still in-flight) can't freeze the UI.
                await Navigation.PopModalAsync();
                _ = Task.Run(async () =>
                {
                    try { await tempConnectionManager.Disconnect(); } catch { }
                });

                // Join with seat number 0 for auto-assignment
                await JoinGameAsync(game.GameId, 0);
            };
            border.GestureRecognizers.Add(tapGesture);

            return border;
        }

        private async Task JoinGameAsync(int roomNumber, int seatNumber)
        {
            var localization = Services.LocalizationManager.Instance;

            // Disable all buttons while connecting and gray them out
            NicknameEntry.IsEnabled = false;
            JoinGameBtn.IsEnabled = false;
            CreateGameBtn.IsEnabled = false;
            ChangeServerBtn.IsEnabled = false;
            AboutBtn.IsEnabled = false;

            JoinGameBtn.BackgroundColor = Colors.Gray;
            CreateGameBtn.BackgroundColor = Colors.Gray;
            ChangeServerBtn.BackgroundColor = Colors.Gray;
            AboutBtn.BackgroundColor = Colors.Gray;

            if (string.IsNullOrEmpty(currentServerUrl))
            {
                await DisplayAlertAsync(localization.GetString("error"), localization.GetString("server_url_required"), localization.GetString("yes"));
                ResetUIState();
                return;
            }

            var nickname = NicknameEntry.Text?.Trim() ?? "";
            Preferences.Set(NicknamePreferenceKey, nickname);

            connectionManager = new HubConnectionManager(nickname);
            connectionManager.ConnectionFailed += async (msg) =>
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await DisplayAlertAsync(localization.GetString("error"), msg, localization.GetString("yes"));
                    ResetUIState();
                });
            };
            connectionManager.Registered += OnRegistered;

            if (!await connectionManager.ConnectAsync(currentServerUrl))
            {
                ResetUIState();
                return;
            }

            isGameOwner = false;

            var (success, errorMessage) = await connectionManager.JoinGameAsync(roomNumber, seatNumber);
            if (!success)
            {
                // Check if this is a version mismatch error
                if (errorMessage.StartsWith("VERSION_TOO_OLD:"))
                {
                    var minVersion = errorMessage.Replace("VERSION_TOO_OLD:", "");
                    await ShowUpdateRequiredPopup(minVersion);
                }
                else
                {
                    await DisplayAlertAsync(localization.GetString("error"), errorMessage, localization.GetString("yes"));
                }
                await connectionManager.Disconnect();
                connectionManager = null;
                ResetUIState();
            }
        }

        private async void OnCreateGameClicked(object? sender, EventArgs e)
        {
            var localization = Services.LocalizationManager.Instance;

            // Reset role selections
            ResetRoleSelections();

            // Create popup with all role selection elements
            var popup = CreateCreateGamePopup(localization);
            await Navigation.PushModalAsync(new NavigationPage(popup));
        }

        private void ResetRoleSelections()
        {
            selectedLangRen.Clear();
            selectedJiaMian = false;
            selectedNvWu = false;
            selectedYuYanJia = false;
            selectedTongLingShi = false;
            selectedWuZhe = false;
            selectedLieRen = false;
            selectedLangQiang = false;
            selectedDaMao = false;
            selectedLaoShu = false;
            selectedBaiChi = false;
            selectedSheMengRen = false;
            selectedXiong = false;
            selectedShenLangGongWu1 = false;
            selectedThief = false;
            selectedMengMianRen = false;
            selectedShouWei = false;
            selectedYingZi = false;
            selectedFuChouZhe = false;
            selectedHunZi = false;
            selectedJiXieLang = false;
            selectedZhuangJiaLang = false;
            selectedLangMeiRen = false;
            selectedAwkShiXiangGui = false;
            selectedGhostBride = false;
            selectedMeiYangYang = false;
            selectedHongTaiLang = false;
            selectedLieMoRen = false;
            selectedTuFu = false;
            selectedShouMuRen = false;
            selectedAwkSheMengRen = false;
            selectedShiXiangGui = false;
            selectedXueYue = false;
            selectedMengYan = false;
            selectedMoShuShi = false;
            selectedGuiShuShi = false;
            selectedDingXuWangZi = false;
            selectedAwkYuYanJia = false;
            selectedXunXiangMeiYing = false;
            selectedPingMin.Clear();
            roleButtons.Clear();
        }

        private ContentPage CreateCreateGamePopup(LocalizationManager localization)
        {
            var scrollView = new ScrollView();
            var mainStack = new VerticalStackLayout { Padding = 20, Spacing = 12 };

            // Title
            mainStack.Children.Add(new Label
            {
                Text = localization.GetString("create_game", "Create Game"),
                FontSize = 20,
                FontAttributes = FontAttributes.Bold,
                TextColor = PopupTextColor
            });

            // Duration settings
            mainStack.Children.Add(new Label { Text = localization.GetString("duration_settings", "Duration Settings"), FontAttributes = FontAttributes.Bold, FontSize = 14, TextColor = PopupTextColor, Margin = new Thickness(0, 12, 0, 0) });

            var speechEntry = new Entry { Text = speechDuration.ToString(), Keyboard = Keyboard.Numeric, TextColor = PopupTextColor, BackgroundColor = Colors.Transparent, WidthRequest = 40 };
            var werewolfEntry = new Entry { Text = werewolfDuration.ToString(), Keyboard = Keyboard.Numeric, TextColor = PopupTextColor, BackgroundColor = Colors.Transparent, WidthRequest = 40 };
            var godEntry = new Entry { Text = godDuration.ToString(), Keyboard = Keyboard.Numeric, TextColor = PopupTextColor, BackgroundColor = Colors.Transparent, WidthRequest = 40 };
            var sheriffExtraTimeEntry = new Entry { Text = sheriffExtraTime.ToString(), Keyboard = Keyboard.Numeric, TextColor = PopupTextColor, BackgroundColor = Colors.Transparent, WidthRequest = 40 };
            var roleViewingGroupSizeEntry = new Entry { Text = roleViewingGroupSize.ToString(), Keyboard = Keyboard.Numeric, TextColor = PopupTextColor, BackgroundColor = Colors.Transparent, WidthRequest = 40 };

            mainStack.Children.Add(CreateHorizontalEntry(speechEntry, localization.GetString("speech_duration", "Speech Duration")));
            mainStack.Children.Add(CreateHorizontalEntry(werewolfEntry, localization.GetString("werewolf_duration", "Werewolf Duration")));
            mainStack.Children.Add(CreateHorizontalEntry(godEntry, localization.GetString("god_duration", "God Duration")));
            mainStack.Children.Add(CreateHorizontalEntry(sheriffExtraTimeEntry, localization.GetString("sheriff_extra_time", "Sheriff Extra Time")));

            // Switches
            var roundTableSwitch = new Switch { IsToggled = roundTableMode };
            var ownerControlSwitch = new Switch { IsToggled = ownerControlEnabled };
            var counterClockwiseSwitch = new Switch { IsToggled = seatCounterClockwise };
            var viewRoleInTurnSwitch = new Switch { IsToggled = viewRoleInTurn };

            mainStack.Children.Add(CreateHorizontalSwitch(roundTableSwitch, localization.GetString("round_table_mode", "Round Table Mode")));
            mainStack.Children.Add(CreateHorizontalSwitch(ownerControlSwitch, localization.GetString("owner_control_mode", "Owner Control Mode")));
            mainStack.Children.Add(CreateHorizontalSwitch(counterClockwiseSwitch, localization.GetString("seat_counter_clockwise", "Seat Counter-Clockwise")));
            mainStack.Children.Add(CreateHorizontalSwitch(viewRoleInTurnSwitch, localization.GetString("view_role_in_turn", "View Role In Turn")));
            mainStack.Children.Add(CreateHorizontalEntry(roleViewingGroupSizeEntry, localization.GetString("role_viewing_group_size", "Role Viewing Group Size")));

            // Role Selection
            mainStack.Children.Add(new Label { Text = localization.GetString("select_roles_new_game", "Select Roles"), FontAttributes = FontAttributes.Bold, FontSize = 14, TextColor = PopupTextColor, Margin = new Thickness(0, 12, 0, 0) });

            // Game Mode
            var shenLangGongWu1Btn = CreateRoleButton(localization.GetString("ShenLangGongWu1", "ShenLangGongWu1"), "ShenLangGongWu1", () => selectedShenLangGongWu1, v => selectedShenLangGongWu1 = v);
            mainStack.Children.Add(new HorizontalStackLayout { Spacing = 4, Children = { shenLangGongWu1Btn } });
            mainStack.Children.Add(CreateCategorySeparator());

            // LangRen row
            var langRenBtns = new HorizontalStackLayout { Spacing = 4 };
            for (int i = 1; i <= 5; i++)
            {
                var id = $"LangRen{i}";
                langRenBtns.Children.Add(CreateMultiRoleButton(localization.GetString("LangRen", "LangRen"), id, selectedLangRen));
            }
            mainStack.Children.Add(langRenBtns);
            mainStack.Children.Add(CreateCategorySeparator());

            // Special LangRen row
            var specialLangRenBtns1 = new HorizontalStackLayout { Spacing = 4 };
            specialLangRenBtns1.Children.Add(CreateRoleButton(localization.GetString("LangQiang", "LangQiang"), "LangQiang", () => selectedLangQiang, v => selectedLangQiang = v));
            specialLangRenBtns1.Children.Add(CreateRoleButton(localization.GetString("LangMeiRen", "LangMeiRen"), "LangMeiRen", () => selectedLangMeiRen, v => selectedLangMeiRen = v));
            specialLangRenBtns1.Children.Add(CreateRoleButton(localization.GetString("DaMao", "DaMao"), "DaMao", () => selectedDaMao, v => selectedDaMao = v));
            specialLangRenBtns1.Children.Add(CreateRoleButton(localization.GetString("JiXieLang", "JiXieLang"), "JiXieLang", () => selectedJiXieLang, v => selectedJiXieLang = v));
            specialLangRenBtns1.Children.Add(CreateRoleButton(localization.GetString("ZhuangJiaLang", "ZhuangJiaLang"), "ZhuangJiaLang", () => selectedZhuangJiaLang, v => selectedZhuangJiaLang = v));
            mainStack.Children.Add(specialLangRenBtns1);

            var specialLangRenBtns2 = new HorizontalStackLayout { Spacing = 4 };
            specialLangRenBtns2.Children.Add(CreateRoleButton(localization.GetString("HongTaiLang", "HongTaiLang"), "HongTaiLang", () => selectedHongTaiLang, v => selectedHongTaiLang = v));
            specialLangRenBtns2.Children.Add(CreateRoleButton(localization.GetString("TuFu", "TuFu"), "TuFu", () => selectedTuFu, v => selectedTuFu = v));
            specialLangRenBtns2.Children.Add(CreateRoleButton(localization.GetString("AwkShiXiangGui", "AwkShiXiangGui"), "AwkShiXiangGui", () => selectedAwkShiXiangGui, v => selectedAwkShiXiangGui = v));
            specialLangRenBtns2.Children.Add(CreateRoleButton(localization.GetString("ShiXiangGui", "ShiXiangGui"), "ShiXiangGui", () => selectedShiXiangGui, v => selectedShiXiangGui = v));
            mainStack.Children.Add(specialLangRenBtns2);

            var specialLangRenBtns3 = new HorizontalStackLayout { Spacing = 4 };
            specialLangRenBtns3.Children.Add(CreateRoleButton(localization.GetString("XueYue", "XueYue"), "XueYue", () => selectedXueYue, v => selectedXueYue = v));
            specialLangRenBtns3.Children.Add(CreateRoleButton(localization.GetString("MengYan", "MengYan"), "MengYan", () => selectedMengYan, v => selectedMengYan = v));
            specialLangRenBtns3.Children.Add(CreateRoleButton(localization.GetString("JiaMian", "JiaMian"), "JiaMian", () => selectedJiaMian, v => selectedJiaMian = v));
            specialLangRenBtns3.Children.Add(CreateRoleButton(localization.GetString("GuiShuShi", "GuiShuShi"), "GuiShuShi", () => selectedGuiShuShi, v => selectedGuiShuShi = v));
            specialLangRenBtns3.Children.Add(CreateRoleButton(localization.GetString("XunXiangMeiYing", "XunXiangMeiYing"), "XunXiangMeiYing", () => selectedXunXiangMeiYing, v => selectedXunXiangMeiYing = v));
            mainStack.Children.Add(specialLangRenBtns3);
            mainStack.Children.Add(CreateCategorySeparator());

            // God row 1
            var godBtns1 = new HorizontalStackLayout { Spacing = 4 };
            godBtns1.Children.Add(CreateRoleButton(localization.GetString("NvWu", "NvWu"), "NvWu", () => selectedNvWu, v => selectedNvWu = v));
            godBtns1.Children.Add(CreateRoleButton(localization.GetString("YuYanJia", "YuYanJia"), "YuYanJia", () => selectedYuYanJia, v => selectedYuYanJia = v));
            godBtns1.Children.Add(CreateRoleButton(localization.GetString("TongLingShi", "TongLingShi"), "TongLingShi", () => selectedTongLingShi, v => selectedTongLingShi = v));
            godBtns1.Children.Add(CreateRoleButton(localization.GetString("WuZhe", "WuZhe"), "WuZhe", () => selectedWuZhe, v => selectedWuZhe = v));
            godBtns1.Children.Add(CreateRoleButton(localization.GetString("LieRen", "LieRen"), "LieRen", () => selectedLieRen, v => selectedLieRen = v));
            mainStack.Children.Add(godBtns1);

            // God row 2
            var godBtns2 = new HorizontalStackLayout { Spacing = 4 };
            godBtns2.Children.Add(CreateRoleButton(localization.GetString("LaoShu", "LaoShu"), "LaoShu", () => selectedLaoShu, v => selectedLaoShu = v));
            godBtns2.Children.Add(CreateRoleButton(localization.GetString("BaiChi", "BaiChi"), "BaiChi", () => selectedBaiChi, v => selectedBaiChi = v));
            godBtns2.Children.Add(CreateRoleButton(localization.GetString("SheMengRen", "SheMengRen"), "SheMengRen", () => selectedSheMengRen, v => selectedSheMengRen = v));
            godBtns2.Children.Add(CreateRoleButton(localization.GetString("Thief", "Thief"), "Thief", () => selectedThief, v => selectedThief = v));
            godBtns2.Children.Add(CreateRoleButton(localization.GetString("ShouMuRen", "ShouMuRen"), "ShouMuRen", () => selectedShouMuRen, v => selectedShouMuRen = v));
            mainStack.Children.Add(godBtns2);

            // God row 3
            var godBtns3 = new HorizontalStackLayout { Spacing = 4 };
            godBtns3.Children.Add(CreateRoleButton(localization.GetString("MengMianRen", "MengMianRen"), "MengMianRen", () => selectedMengMianRen, v => selectedMengMianRen = v));
            godBtns3.Children.Add(CreateRoleButton(localization.GetString("ShouWei", "ShouWei"), "ShouWei", () => selectedShouWei, v => selectedShouWei = v));
            godBtns3.Children.Add(CreateRoleButton(localization.GetString("MeiYangYang", "MeiYangYang"), "MeiYangYang", () => selectedMeiYangYang, v => selectedMeiYangYang = v));
            godBtns3.Children.Add(CreateRoleButton(localization.GetString("LieMoRen", "LieMoRen"), "LieMoRen", () => selectedLieMoRen, v => selectedLieMoRen = v));
            godBtns3.Children.Add(CreateRoleButton(localization.GetString("Xiong", "Xiong"), "Xiong", () => selectedXiong, v => selectedXiong = v));
            mainStack.Children.Add(godBtns3);

            // God row 4
            var godBtns4 = new HorizontalStackLayout { Spacing = 4 };
            godBtns4.Children.Add(CreateRoleButton(localization.GetString("AwkSheMengRen", "AwkSheMengRen"), "AwkSheMengRen", () => selectedAwkSheMengRen, v => selectedAwkSheMengRen = v));
            godBtns4.Children.Add(CreateRoleButton(localization.GetString("MoShuShi", "MoShuShi"), "MoShuShi", () => selectedMoShuShi, v => selectedMoShuShi = v));
            godBtns4.Children.Add(CreateRoleButton(localization.GetString("DingXuWangZi", "DingXuWangZi"), "DingXuWangZi", () => selectedDingXuWangZi, v => selectedDingXuWangZi = v));
            godBtns4.Children.Add(CreateRoleButton(localization.GetString("AwkYuYanJia", "AwkYuYanJia"), "AwkYuYanJia", () => selectedAwkYuYanJia, v => selectedAwkYuYanJia = v));
            mainStack.Children.Add(godBtns4);
            mainStack.Children.Add(CreateCategorySeparator());

            // Third party row
            var thirdPartyBtns = new HorizontalStackLayout { Spacing = 4 };
            thirdPartyBtns.Children.Add(CreateRoleButton(localization.GetString("YingZi", "YingZi"), "YingZi", () => selectedYingZi, v => selectedYingZi = v));
            thirdPartyBtns.Children.Add(CreateRoleButton(localization.GetString("FuChouZhe", "FuChouZhe"), "FuChouZhe", () => selectedFuChouZhe, v => selectedFuChouZhe = v));
            thirdPartyBtns.Children.Add(CreateRoleButton(localization.GetString("HunZi", "HunZi"), "HunZi", () => selectedHunZi, v => selectedHunZi = v));
            thirdPartyBtns.Children.Add(CreateRoleButton(localization.GetString("GhostBride", "GhostBride"), "GhostBride", () => selectedGhostBride, v => selectedGhostBride = v));
            mainStack.Children.Add(thirdPartyBtns);
            mainStack.Children.Add(CreateCategorySeparator());

            // PingMin row
            var pingMinBtns = new HorizontalStackLayout { Spacing = 4 };
            for (int i = 1; i <= 5; i++)
            {
                var id = $"PingMin{i}";
                pingMinBtns.Children.Add(CreateMultiRoleButton(localization.GetString("PingMin", "PingMin"), id, selectedPingMin));
            }
            mainStack.Children.Add(pingMinBtns);

            // Create Game button
            var createBtn = new Button
            {
                Text = localization.GetString("connect", "Create"),
                BackgroundColor = Colors.Transparent,
                TextColor = Colors.White,
                Margin = new Thickness(0, 20, 0, 0)
            };

            createBtn.Clicked += async (s, args) =>
            {
                // Update duration settings and save to preferences
                speechDuration = int.TryParse(speechEntry.Text, out var sd) ? sd : 95;
                werewolfDuration = int.TryParse(werewolfEntry.Text, out var wd) ? wd : 155;
                godDuration = int.TryParse(godEntry.Text, out var gd) ? gd : 19;
                sheriffExtraTime = int.TryParse(sheriffExtraTimeEntry.Text, out var set) ? set : 30;
                roleViewingGroupSize = int.TryParse(roleViewingGroupSizeEntry.Text, out var rvgs) ? rvgs : 3;
                roundTableMode = roundTableSwitch.IsToggled;
                ownerControlEnabled = ownerControlSwitch.IsToggled;
                seatCounterClockwise = counterClockwiseSwitch.IsToggled;
                viewRoleInTurn = viewRoleInTurnSwitch.IsToggled;

                Preferences.Set(SpeechDurationPreferenceKey, speechDuration);
                Preferences.Set(WerewolfDurationPreferenceKey, werewolfDuration);
                Preferences.Set(GodDurationPreferenceKey, godDuration);
                Preferences.Set(SheriffExtraTimePreferenceKey, sheriffExtraTime);
                Preferences.Set(RoundTableModePreferenceKey, roundTableMode);
                Preferences.Set(OwnerControlPreferenceKey, ownerControlEnabled);
                Preferences.Set(SeatCounterClockwisePreferenceKey, seatCounterClockwise);
                Preferences.Set(ViewRoleInTurnPreferenceKey, viewRoleInTurn);
                Preferences.Set(RoleViewingGroupSizePreferenceKey, roleViewingGroupSize);

                if (!HasRolesSelected())
                {
                    await DisplayAlertAsync(localization.GetString("error"), localization.GetString("select_at_least_one_role"), localization.GetString("yes"));
                    return;
                }

                await Navigation.PopModalAsync();
                await CreateGameAsync();
            };

            mainStack.Children.Add(createBtn);

            // Back button
            var backBtn = new Button
            {
                Text = localization.GetString("back", "Back"),
                BackgroundColor = Colors.Transparent,
                TextColor = Colors.White,
                Margin = new Thickness(0, 10, 0, 0)
            };

            backBtn.Clicked += async (s, args) =>
            {
                await Navigation.PopModalAsync();
            };

            mainStack.Children.Add(backBtn);

            scrollView.Content = mainStack;
            var page = new ContentPage
            {
                Title = localization.GetString("create_game", "Create Game"),
                Content = new Grid
                {
                    Children =
                    {
                        new Image
                        {
                            Source = "bg.png",
                            Aspect = Aspect.AspectFill,
                            HorizontalOptions = LayoutOptions.Fill,
                            VerticalOptions = LayoutOptions.Fill
                        },
                        scrollView
                    }
                }
            };
            NavigationPage.SetHasNavigationBar(page, false);
            return page;
        }

        private HorizontalStackLayout CreateHorizontalEntry(Entry entry, string label)
        {
            return new HorizontalStackLayout
            {
                Spacing = 8,
                Children = { entry, new Label { Text = label, TextColor = PopupTextColor, VerticalOptions = LayoutOptions.Center } }
            };
        }

        private HorizontalStackLayout CreateHorizontalSwitch(Switch sw, string label)
        {
            sw.OnColor = SwitchOnColor;
            sw.ThumbColor = Colors.White;
            return new HorizontalStackLayout
            {
                Spacing = 8,
                HeightRequest = 40,
                Children = { sw, new Label { Text = label, TextColor = PopupTextColor, VerticalOptions = LayoutOptions.Center } }
            };
        }

        private BoxView CreateCategorySeparator()
        {
            return new BoxView
            {
                HeightRequest = 1,
                Color = Colors.Gray,
                Margin = new Thickness(0, 8, 0, 8),
                HorizontalOptions = LayoutOptions.Fill
            };
        }

        private Button CreateRoleButton(string text, string roleName, Func<bool> getSelected, Action<bool> setSelected)
        {
            var btn = new Button
            {
                Text = text,
                BackgroundColor = ButtonDefaultColor,
                TextColor = Colors.White
            };
            roleButtons[roleName] = btn;
            btn.Clicked += (s, e) =>
            {
                var newValue = !getSelected();

                // Check if we should apply a template (first role selected and it's a template role)
                if (newValue && !HasRolesSelected() && RoleTemplates.TryGetValue(roleName, out var template))
                {
                    ApplyRoleTemplate(roleName, template);
                }
                else
                {
                    setSelected(newValue);
                    btn.BackgroundColor = newValue ? ButtonSelectedColor : ButtonDefaultColor;
                }
            };
            return btn;
        }

        private Button CreateMultiRoleButton(string text, string id, HashSet<string> selectedSet)
        {
            var btn = new Button
            {
                Text = text,
                BackgroundColor = ButtonDefaultColor,
                TextColor = Colors.White
            };
            roleButtons[id] = btn;
            btn.Clicked += (s, e) =>
            {
                if (selectedSet.Contains(id))
                {
                    selectedSet.Remove(id);
                    btn.BackgroundColor = ButtonDefaultColor;
                }
                else
                {
                    selectedSet.Add(id);
                    btn.BackgroundColor = ButtonSelectedColor;
                }
            };
            return btn;
        }

        private void ApplyRoleTemplate(string triggerRole, Dictionary<string, int> template)
        {
            // First, select the trigger role itself
            SelectRoleByName(triggerRole, true);
            if (roleButtons.TryGetValue(triggerRole, out var triggerBtn))
            {
                triggerBtn.BackgroundColor = ButtonSelectedColor;
            }

            // Then apply all roles in the template
            foreach (var (role, count) in template)
            {
                if (role == "LangRen")
                {
                    for (int i = 1; i <= count && i <= 5; i++)
                    {
                        var id = $"LangRen{i}";
                        selectedLangRen.Add(id);
                        if (roleButtons.TryGetValue(id, out var langRenBtn))
                        {
                            langRenBtn.BackgroundColor = ButtonSelectedColor;
                        }
                    }
                }
                else if (role == "PingMin")
                {
                    for (int i = 1; i <= count && i <= 5; i++)
                    {
                        var id = $"PingMin{i}";
                        selectedPingMin.Add(id);
                        if (roleButtons.TryGetValue(id, out var pingMinBtn))
                        {
                            pingMinBtn.BackgroundColor = ButtonSelectedColor;
                        }
                    }
                }
                else
                {
                    SelectRoleByName(role, true);
                    if (roleButtons.TryGetValue(role, out var roleBtn))
                    {
                        roleBtn.BackgroundColor = ButtonSelectedColor;
                    }
                }
            }
        }

        private void SelectRoleByName(string roleName, bool selected)
        {
            switch (roleName)
            {
                case "JiaMian": selectedJiaMian = selected; break;
                case "NvWu": selectedNvWu = selected; break;
                case "YuYanJia": selectedYuYanJia = selected; break;
                case "TongLingShi": selectedTongLingShi = selected; break;
                case "WuZhe": selectedWuZhe = selected; break;
                case "LieRen": selectedLieRen = selected; break;
                case "LangQiang": selectedLangQiang = selected; break;
                case "DaMao": selectedDaMao = selected; break;
                case "LaoShu": selectedLaoShu = selected; break;
                case "BaiChi": selectedBaiChi = selected; break;
                case "SheMengRen": selectedSheMengRen = selected; break;
                case "Xiong": selectedXiong = selected; break;
                case "ShenLangGongWu1": selectedShenLangGongWu1 = selected; break;
                case "Thief": selectedThief = selected; break;
                case "MengMianRen": selectedMengMianRen = selected; break;
                case "ShouWei": selectedShouWei = selected; break;
                case "YingZi": selectedYingZi = selected; break;
                case "FuChouZhe": selectedFuChouZhe = selected; break;
                case "HunZi": selectedHunZi = selected; break;
                case "JiXieLang": selectedJiXieLang = selected; break;
                case "ZhuangJiaLang": selectedZhuangJiaLang = selected; break;
                case "LangMeiRen": selectedLangMeiRen = selected; break;
                case "AwkShiXiangGui": selectedAwkShiXiangGui = selected; break;
                case "GhostBride": selectedGhostBride = selected; break;
                case "MeiYangYang": selectedMeiYangYang = selected; break;
                case "HongTaiLang": selectedHongTaiLang = selected; break;
                case "LieMoRen": selectedLieMoRen = selected; break;
                case "TuFu": selectedTuFu = selected; break;
                case "ShouMuRen": selectedShouMuRen = selected; break;
                case "AwkSheMengRen": selectedAwkSheMengRen = selected; break;
                case "ShiXiangGui": selectedShiXiangGui = selected; break;
                case "XueYue": selectedXueYue = selected; break;
                case "DingXuWangZi": selectedDingXuWangZi = selected; break;
                case "AwkYuYanJia": selectedAwkYuYanJia = selected; break;
                case "XunXiangMeiYing": selectedXunXiangMeiYing = selected; break;
            }
        }

        private async Task CreateGameAsync()
        {
            var localization = Services.LocalizationManager.Instance;

            // Disable all buttons while connecting and gray them out
            NicknameEntry.IsEnabled = false;
            JoinGameBtn.IsEnabled = false;
            CreateGameBtn.IsEnabled = false;
            ChangeServerBtn.IsEnabled = false;
            AboutBtn.IsEnabled = false;

            JoinGameBtn.BackgroundColor = Colors.Gray;
            CreateGameBtn.BackgroundColor = Colors.Gray;
            ChangeServerBtn.BackgroundColor = Colors.Gray;
            AboutBtn.BackgroundColor = Colors.Gray;

            if (string.IsNullOrEmpty(currentServerUrl))
            {
                await DisplayAlertAsync(localization.GetString("error"), localization.GetString("server_url_required"), localization.GetString("yes"));
                ResetUIState();
                return;
            }

            var nickname = NicknameEntry.Text?.Trim() ?? "";
            Preferences.Set(NicknamePreferenceKey, nickname);

            connectionManager = new HubConnectionManager(nickname);
            connectionManager.ConnectionFailed += async (msg) =>
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await DisplayAlertAsync(localization.GetString("error"), msg, localization.GetString("yes"));
                    ResetUIState();
                });
            };
            connectionManager.Registered += OnRegistered;

            if (!await connectionManager.ConnectAsync(currentServerUrl))
            {
                ResetUIState();
                return;
            }

            // Build roleDict from selected roles
            var roleDict = BuildRoleDict();

            // Always include LangRenSha
            roleDict["LangRenSha"] = 1;

            // Calculate total players
            int totalPlayers = roleDict.Values.Sum() - 1;
            if (roleDict.ContainsKey("ShenLangGongWu1"))
                totalPlayers -= 1;
            if (roleDict.ContainsKey("Thief"))
                totalPlayers -= 3;

            if (totalPlayers <= 0)
            {
                await DisplayAlertAsync(localization.GetString("error"), localization.GetString("select_at_least_one_role"), localization.GetString("yes"));
                ResetUIState();
                return;
            }

            isGameOwner = true;
            int rtMode = roundTableMode ? 1 : 0;
            int ownerCtrl = ownerControlEnabled ? 1 : 0;
            int ccw = seatCounterClockwise ? 1 : 0;
            int vrit = viewRoleInTurn ? 1 : 0;

            if (!await connectionManager.CreateRoomAsync2(totalPlayers, roleDict, speechDuration, werewolfDuration, godDuration, rtMode, ownerCtrl, ccw, sheriffExtraTime, vrit, roleViewingGroupSize))
            {
                ResetUIState();
            }
        }

        private Dictionary<string, int> BuildRoleDict()
        {
            var roleDict = new Dictionary<string, int>();

            if (selectedLangRen.Count > 0) roleDict["LangRen"] = selectedLangRen.Count;
            if (selectedJiaMian) roleDict["JiaMian"] = 1;
            if (selectedNvWu) roleDict["NvWu"] = 1;
            if (selectedYuYanJia) roleDict["YuYanJia"] = 1;
            if (selectedTongLingShi) roleDict["TongLingShi"] = 1;
            if (selectedWuZhe) roleDict["WuZhe"] = 1;
            if (selectedLieRen) roleDict["LieRen"] = 1;
            if (selectedLangQiang) roleDict["LangQiang"] = 1;
            if (selectedDaMao) roleDict["DaMao"] = 1;
            if (selectedLaoShu) roleDict["LaoShu"] = 1;
            if (selectedBaiChi) roleDict["BaiChi"] = 1;
            if (selectedSheMengRen) roleDict["SheMengRen"] = 1;
            if (selectedXiong) roleDict["Xiong"] = 1;
            if (selectedShenLangGongWu1) roleDict["ShenLangGongWu1"] = 1;
            if (selectedThief) roleDict["Thief"] = 1;
            if (selectedMengMianRen) roleDict["MengMianRen"] = 1;
            if (selectedShouWei) roleDict["ShouWei"] = 1;
            if (selectedYingZi) roleDict["YingZi"] = 1;
            if (selectedFuChouZhe) roleDict["FuChouZhe"] = 1;
            if (selectedHunZi) roleDict["HunZi"] = 1;
            if (selectedJiXieLang) roleDict["JiXieLang"] = 1;
            if (selectedZhuangJiaLang) roleDict["ZhuangJiaLang"] = 1;
            if (selectedLangMeiRen) roleDict["LangMeiRen"] = 1;
            if (selectedAwkShiXiangGui) roleDict["AwkShiXiangGui"] = 1;
            if (selectedGhostBride) roleDict["GhostBride"] = 1;
            if (selectedMeiYangYang) roleDict["MeiYangYang"] = 1;
            if (selectedHongTaiLang) roleDict["HongTaiLang"] = 1;
            if (selectedLieMoRen) roleDict["LieMoRen"] = 1;
            if (selectedTuFu) roleDict["TuFu"] = 1;
            if (selectedShouMuRen) roleDict["ShouMuRen"] = 1;
            if (selectedAwkSheMengRen) roleDict["AwkSheMengRen"] = 1;
            if (selectedShiXiangGui) roleDict["ShiXiangGui"] = 1;
            if (selectedXueYue) roleDict["XueYue"] = 1;
            if (selectedMengYan) roleDict["MengYan"] = 1;
            if (selectedMoShuShi) roleDict["MoShuShi"] = 1;
            if (selectedGuiShuShi) roleDict["GuiShuShi"] = 1;
            if (selectedDingXuWangZi) roleDict["DingXuWangZi"] = 1;
            if (selectedAwkYuYanJia) roleDict["AwkYuYanJia"] = 1;
            if (selectedXunXiangMeiYing) roleDict["XunXiangMeiYing"] = 1;
            if (selectedPingMin.Count > 0) roleDict["PingMin"] = selectedPingMin.Count;

            return roleDict;
        }

        private async void OnRegistered(int registeredGameId, int registeredPlayerId, bool gameStarted)
        {
            gameId = registeredGameId;
            playerId = registeredPlayerId;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                NicknameEntry.IsEnabled = false;
                JoinGameBtn.IsEnabled = false;
                CreateGameBtn.IsEnabled = false;
                ChangeServerBtn.IsEnabled = false;
                AboutBtn.IsEnabled = false;

                if (gameStarted)
                {
                    await Navigation.PushAsync(new GameView(connectionManager!, gameId, playerId, isGameOwner));
                }
                else
                {
                    await Navigation.PushAsync(new RoomView(connectionManager!, gameId, playerId, isGameOwner));
                }
            });
        }

        private async void OnChangeServerClicked(object? sender, EventArgs e)
        {
            var localization = Services.LocalizationManager.Instance;

            var serverEntry = new Entry
            {
                Placeholder = localization.GetString("hub_url", "Server URL"),
                Text = currentServerUrl,
                TextColor = PopupTextColor,
                BackgroundColor = Colors.Transparent,
                HorizontalOptions = LayoutOptions.Fill
            };

            var confirmBtn = new Button
            {
                Text = localization.GetString("confirm", "Confirm"),
                BackgroundColor = Colors.Transparent,
                TextColor = Colors.White,
                Margin = new Thickness(0, 20, 0, 0)
            };

            var backBtn = new Button
            {
                Text = localization.GetString("back", "Back"),
                BackgroundColor = Colors.Transparent,
                TextColor = Colors.White,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var popup = new ContentPage
            {
                Title = localization.GetString("change_server", "Change Server"),
                Content = new Grid
                {
                    Children =
                    {
                        new Image
                        {
                            Source = "bg.png",
                            Aspect = Aspect.AspectFill,
                            HorizontalOptions = LayoutOptions.Fill,
                            VerticalOptions = LayoutOptions.Fill
                        },
                        new VerticalStackLayout
                        {
                            Padding = 20,
                            Spacing = 15,
                            Children =
                            {
                                new Label { Text = localization.GetString("change_server", "Change Server"), FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = PopupTextColor },
                                serverEntry,
                                confirmBtn,
                                backBtn
                            }
                        }
                    }
                }
            };
            NavigationPage.SetHasNavigationBar(popup, false);

            backBtn.Clicked += async (s, args) =>
            {
                await Navigation.PopModalAsync();
            };

            confirmBtn.Clicked += async (s, args) =>
            {
                var url = serverEntry.Text?.Trim() ?? "";
                if (!string.IsNullOrEmpty(url))
                {
                    currentServerUrl = url;
                }
                await Navigation.PopModalAsync();
            };

            await Navigation.PushModalAsync(new NavigationPage(popup));
        }

        private async void OnAboutClicked(object? sender, EventArgs e)
        {
            var localization = Services.LocalizationManager.Instance;
            var title = localization.GetString("about", "About");
            var message = localization.GetString("about_message", "Author: Bi Wu.\nRole design: Ke Ji.\nVoiceover: Tu dou.");
            var ok = localization.GetString("yes", "OK");

            await DisplayAlertAsync(title, message, ok);
        }

        private async Task ShowUpdateRequiredPopup(string requiredVersion)
        {
            var localization = Services.LocalizationManager.Instance;
            var title = localization.GetString("update_required", "Update Required");
            var message = localization.GetString("update_required_message", "The room requires a newer version of the app. Please update to continue.");
            var updateBtn = localization.GetString("update_now", "Update Now");
            var cancelBtn = localization.GetString("cancel", "Cancel");

            var result = await DisplayAlertAsync(title, message, updateBtn, cancelBtn);
            if (result)
            {
                // Open the appropriate app store based on platform
                string storeUrl = "";
#if ANDROID
                storeUrl = "https://play.google.com/store/apps/details?id=com.biwuenterprise.potatovillage";
#elif IOS || MACCATALYST
                storeUrl = "https://apps.apple.com/app/id6744692044";
#endif
                if (!string.IsNullOrEmpty(storeUrl))
                {
                    try
                    {
                        await Launcher.OpenAsync(new Uri(storeUrl));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to open store: {ex.Message}");
                    }
                }
            }
        }
    }
}

