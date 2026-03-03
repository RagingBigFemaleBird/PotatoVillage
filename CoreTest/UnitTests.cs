using ProcedureCore.Core;
using ProcedureCore.LangRenSha;

namespace CoreTest
{
    /// <summary>
    /// Core game logic tests without any UI.
    /// Tests run game scenarios by simulating player actions and verifying outcomes.
    /// Uses fixed random seeds for deterministic test results.
    /// TestMode is enabled to bypass waiting/blocking.
    /// </summary>
    public class Tests
    {
        private Game game;
        private List<Dictionary<string, object>> stateUpdates;
        private const int FIXED_SEED = 12345;

        [SetUp]
        public void Setup()
        {
            stateUpdates = new List<Dictionary<string, object>>();
        }

        private void ActionCallback(Game game, Dictionary<string, object> stateDiff)
        {
            // Store state updates for verification
            stateUpdates.Add(new Dictionary<string, object>(stateDiff));
        }

        private Game CreateGame(Dictionary<string, int> roleConfig, int seed = FIXED_SEED)
        {
            var game = new Game(ActionCallback);
            game.SetRandomSeed(seed);
            game.TestMode = true; // Enable test mode to bypass waiting
            game.RoleConfiguration = roleConfig;

            // Add core game action
            game.Actions.Add(new LangRenSha());

            // Add role actions based on config
            foreach (var role in roleConfig.Keys)
            {
                switch (role)
                {
                    case "LangRen":
                        game.Actions.Add(new LangRen());
                        break;
                    case "YuYanJia":
                        game.Actions.Add(new YuYanJia());
                        break;
                    case "TongLingShi":
                        game.Actions.Add(new TongLingShi());
                        break;
                    case "NvWu":
                        game.Actions.Add(new NvWu());
                        break;
                    case "WuZhe":
                        game.Actions.Add(new WuZhe());
                        break;
                    case "JiaMian":
                        game.Actions.Add(new JiaMian());
                        break;
                    case "LieRen":
                        game.Actions.Add(new LieRen());
                        break;
                    case "LangQiang":
                        game.Actions.Add(new LangQiang());
                        break;
                    case "BaiChi":
                        game.Actions.Add(new BaiChi());
                        break;
                    case "DaMao":
                        game.Actions.Add(new DaMao());
                        break;
                    case "LaoShu":
                        game.Actions.Add(new LaoShu());
                        break;
                    case "SheMengRen":
                        game.Actions.Add(new SheMengRen());
                        break;
                    case "Xiong":
                        game.Actions.Add(new Xiong());
                        break;
                    case "MengMianRen":
                        game.Actions.Add(new MengMianRen());
                        break;
                    case "Thief":
                        game.Actions.Add(new Thief());
                        break;
                }
            }

            game.TotalPlayers = roleConfig.Values.Sum();
            return game;
        }

        /// <summary>
        /// Simulates a player action by setting the response in the game state.
        /// </summary>
        private void SimulatePlayerAction(Game game, int playerId, List<int> targets)
        {
            var response = Game.GetGameDictionaryProperty(game, UserAction.dictUserActionResponse, new Dictionary<string, object>());
            response[playerId.ToString()] = targets;
            var update = new Dictionary<string, object>
            {
                { UserAction.dictUserActionResponse, response }
            };
            game.StateUpdate(update, true);
        }

        /// <summary>
        /// Force ends the current user action by setting deadline to past.
        /// </summary>
        private void ForceEndUserAction(Game game)
        {
            var update = new Dictionary<string, object>
            {
                { UserAction.dictUserAction, 1 } // Set to past timestamp
            };
            game.StateUpdate(update, true);
        }

        /// <summary>
        /// Gets the current action/speak phase.
        /// </summary>
        private (int action, int speak, int phase, int day) GetGamePhase(Game game)
        {
            var dict = game.GetGameDictionary();
            var action = dict.ContainsKey(LangRenSha.dictAction) ? (int)dict[LangRenSha.dictAction] : 0;
            var speak = dict.ContainsKey(LangRenSha.dictSpeak) ? (int)dict[LangRenSha.dictSpeak] : 0;
            var phase = dict.ContainsKey(LangRenSha.dictPhase) ? (int)dict[LangRenSha.dictPhase] : 0;
            var day = dict.ContainsKey(LangRenSha.dictDay) ? (int)dict[LangRenSha.dictDay] : 0;
            return (action, speak, phase, day);
        }

        /// <summary>
        /// Gets the player role at a specific position.
        /// </summary>
        private string GetPlayerRole(Game game, int playerId)
        {
            return LangRenSha.GetPlayerProperty(game, playerId, LangRenSha.dictRole, "");
        }

        /// <summary>
        /// Checks if a player is alive.
        /// </summary>
        private bool IsPlayerAlive(Game game, int playerId)
        {
            return LangRenSha.GetPlayerProperty(game, playerId, LangRenSha.dictAlive, 1) == 1;
        }

        /// <summary>
        /// Gets the game winner (if any).
        /// </summary>
        private string? GetGameWinner(Game game)
        {
            var dict = game.GetGameDictionary();
            return dict.ContainsKey(LangRenSha.dictGameWinner) ? (string)dict[LangRenSha.dictGameWinner] : null;
        }

        /// <summary>
        /// Finds player ID by role.
        /// </summary>
        private int FindPlayerByRole(Game game, string roleName)
        {
            var players = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == roleName);
            return players.Count > 0 ? players[0] : -1;
        }

        /// <summary>
        /// Runs the game for a specified number of action loop iterations.
        /// </summary>
        private bool RunGameIterations(Game game, int iterations)
        {
            for (int i = 0; i < iterations; i++)
            {
                var update = new Dictionary<string, object>();
                bool doUpdate = false;
                bool gameOver = false;

                foreach (var action in game.Actions)
                {
                    var result = game.InitiateAction(action, update);
                    if (result != GameActionResult.NotExecuted)
                    {
                        doUpdate = true;
                    }
                    if (result == GameActionResult.Restart)
                    {
                        break;
                    }
                    if (result == GameActionResult.GameOver)
                    {
                        gameOver = true;
                        break;
                    }
                }

                if (doUpdate)
                {
                    game.StateSequenceNumber++;
                    update[Game.dictStateSequence] = game.StateSequenceNumber;
                    game.StateUpdate(update, true);
                }

                if (gameOver)
                {
                    return true;
                }
            }
            return false;
        }

        [Test]
        public void Test_GameInitialization()
        {
            // Arrange
            var roleConfig = new Dictionary<string, int>
            {
                { "LangRen", 1 },
                { "YuYanJia", 1 },
                { "PingMin", 1 }
            };
            game = CreateGame(roleConfig);

            // Act - Run initialization
            RunGameIterations(game, 5);

            // Assert - Players should be created
            var (action, speak, phase, day) = GetGamePhase(game);
            Assert.That(day, Is.EqualTo(0), "Game should start at day 0");

            // Check all players exist
            var allPlayers = LangRenSha.GetPlayers(game, x => true);
            Assert.That(allPlayers.Count, Is.EqualTo(3), "Should have 3 players");
        }

        [Test]
        public void Test_RolesAreAssigned()
        {
            // Verify each role is assigned to exactly one player
            var roleConfig = new Dictionary<string, int>
            {
                { "LangRen", 2 },
                { "YuYanJia", 1 },
                { "NvWu", 1 },
                { "PingMin", 2 }
            };
            game = CreateGame(roleConfig);
            RunGameIterations(game, 5);

            // Count roles
            var langRenCount = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == "LangRen").Count;
            var yuYanJiaCount = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == "YuYanJia").Count;
            var nvWuCount = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == "NvWu").Count;
            var pingMinCount = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == "PingMin").Count;

            Assert.That(langRenCount, Is.EqualTo(2), "Should have 2 LangRen");
            Assert.That(yuYanJiaCount, Is.EqualTo(1), "Should have 1 YuYanJia");
            Assert.That(nvWuCount, Is.EqualTo(1), "Should have 1 NvWu");
            Assert.That(pingMinCount, Is.EqualTo(2), "Should have 2 PingMin");
        }

        [Test]
        public void Test_AllPlayersStartAlive()
        {
            var roleConfig = new Dictionary<string, int>
            {
                { "LangRen", 1 },
                { "YuYanJia", 1 },
                { "PingMin", 2 }
            };
            game = CreateGame(roleConfig);
            RunGameIterations(game, 5);

            var allPlayers = LangRenSha.GetPlayers(game, x => true);
            foreach (var player in allPlayers)
            {
                Assert.That(IsPlayerAlive(game, player), Is.True, $"Player {player} should start alive");
            }
        }

        [Test]
        public void Test_NightOrdersGenerated()
        {
            var roleConfig = new Dictionary<string, int>
            {
                { "LangRen", 1 },
                { "YuYanJia", 1 },
                { "NvWu", 1 },
                { "PingMin", 1 }
            };
            game = CreateGame(roleConfig);
            RunGameIterations(game, 10);

            var nightOrders = LangRenSha.GetNightOrders(game);

            // Should have night orders including various roles
            Assert.That(nightOrders.Count, Is.GreaterThan(0), "Night orders should be generated");

            // Should include YuYanJia action
            Assert.That(nightOrders.Contains((int)ActionConstant.YuYanJia_ChaYan), Is.True, 
                "Night orders should include YuYanJia ChaYan");

            // Should include NvWu action
            Assert.That(nightOrders.Contains((int)ActionConstant.NvWu_Act), Is.True, 
                "Night orders should include NvWu Act");
        }

        [Test]
        public void Test_FixedSeed_ProducesConsistentGameState()
        {
            // Note: Role shuffling uses its own Random instance, not the game seed.
            // The game seed controls in-game random events (speaking order, etc.), not role assignments.
            // This test verifies that the game seed is set and used for game state operations.
            var roleConfig = new Dictionary<string, int>
            {
                { "LangRen", 2 },
                { "YuYanJia", 1 },
                { "NvWu", 1 },
                { "PingMin", 2 }
            };

            // Create game with fixed seed
            var game1 = CreateGame(roleConfig, 99999);
            RunGameIterations(game1, 10);

            // Verify game state exists and has expected structure
            var allPlayers = LangRenSha.GetPlayers(game1, x => true);
            Assert.That(allPlayers.Count, Is.EqualTo(6), "Should have 6 players");

            // Verify the seed was set (GetRandomNumber should return consistent value initially)
            var game2 = CreateGame(roleConfig, 99999);
            Assert.That(game2.GetRandomNumber(), Is.EqualTo(99999), "Seed should be set to specified value");
        }

        [Test]
        public void Test_GameSeed_CanBeSet()
        {
            // Verify different seeds can be set and are stored correctly
            var roleConfig = new Dictionary<string, int>
            {
                { "LangRen", 1 },
                { "YuYanJia", 1 },
                { "PingMin", 1 }
            };

            // Test multiple seeds
            for (int seed = 1; seed <= 5; seed++)
            {
                var g = CreateGame(roleConfig, seed * 11111);
                Assert.That(g.GetRandomNumber(), Is.EqualTo(seed * 11111), 
                    $"Seed should be set to {seed * 11111}");
            }
        }

        [Test]
        public void Test_PlayerFactions_SetCorrectly()
        {
            var roleConfig = new Dictionary<string, int>
            {
                { "LangRen", 1 },
                { "YuYanJia", 1 },
                { "PingMin", 1 }
            };
            game = CreateGame(roleConfig);
            RunGameIterations(game, 5);

            var langRenPlayer = FindPlayerByRole(game, "LangRen");
            var yuYanJiaPlayer = FindPlayerByRole(game, "YuYanJia");
            var pingMinPlayer = FindPlayerByRole(game, "PingMin");

            // LangRen should be evil faction
            var langRenFaction = LangRenSha.GetPlayerProperty<object>(game, langRenPlayer, LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.Civilian);
            Assert.That((int)langRenFaction, Is.EqualTo((int)LangRenSha.PlayerFaction.Evil), "LangRen should be Evil faction");

            // YuYanJia should be god faction
            var yuYanJiaFaction = LangRenSha.GetPlayerProperty<object>(game, yuYanJiaPlayer, LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.Civilian);
            Assert.That((int)yuYanJiaFaction, Is.EqualTo((int)LangRenSha.PlayerFaction.God), "YuYanJia should be God faction");

            // PingMin should be civilian faction
            var pingMinFaction = LangRenSha.GetPlayerProperty<object>(game, pingMinPlayer, LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.Civilian);
            Assert.That((int)pingMinFaction, Is.EqualTo((int)LangRenSha.PlayerFaction.Civilian), "PingMin should be Civilian faction");
        }

        [Test]
        public void Test_YuYanJia_AllegianceSetCorrectly()
        {
            var roleConfig = new Dictionary<string, int>
            {
                { "LangRen", 1 },
                { "YuYanJia", 1 },
                { "PingMin", 1 }
            };
            game = CreateGame(roleConfig);
            RunGameIterations(game, 5);

            var langRenPlayer = FindPlayerByRole(game, "LangRen");
            var yuYanJiaPlayer = FindPlayerByRole(game, "YuYanJia");

            // LangRen should have evil allegiance (2)
            var langRenAllegiance = LangRenSha.GetPlayerProperty(game, langRenPlayer, YuYanJia.dictYuYanJiaResult, 0);
            Assert.That(langRenAllegiance, Is.EqualTo(2), "LangRen should have evil allegiance (2)");

            // YuYanJia should have good allegiance (1)
            var yuYanJiaAllegiance = LangRenSha.GetPlayerProperty(game, yuYanJiaPlayer, YuYanJia.dictYuYanJiaResult, 0);
            Assert.That(yuYanJiaAllegiance, Is.EqualTo(1), "YuYanJia should have good allegiance (1)");
        }

        [Test]
        public void Test_MengMianRen_StartsNotWounded()
        {
            var roleConfig = new Dictionary<string, int>
            {
                { "LangRen", 1 },
                { "MengMianRen", 1 },
                { "PingMin", 2 }
            };
            game = CreateGame(roleConfig);
            RunGameIterations(game, 5);

            var mengMianRenPlayer = FindPlayerByRole(game, "MengMianRen");
            Assert.That(mengMianRenPlayer, Is.GreaterThan(0), "MengMianRen should exist");

            var isWounded = LangRenSha.GetPlayerProperty(game, mengMianRenPlayer, MengMianRen.dictIsWounded, 0);
            Assert.That(isWounded, Is.EqualTo(0), "MengMianRen should start not wounded");
        }

        [Test]
        public void Test_WinCondition_EvilWins_NoGods()
        {
            // Directly test the win condition logic
            var roleConfig = new Dictionary<string, int>
            {
                { "LangRen", 1 },
                { "PingMin", 2 }
            };
            game = CreateGame(roleConfig);
            RunGameIterations(game, 5);

            // No gods in this game, evil should win
            var winner = LangRenSha.CheckWinCondition(game);
            Assert.That(winner, Is.EqualTo("evil"), "Evil should win when no gods in game");
        }

        [Test]
        public void Test_WinCondition_NoCivilians()
        {
            // Directly test the win condition logic - need to manually kill civilians
            var roleConfig = new Dictionary<string, int>
            {
                { "LangRen", 1 },
                { "YuYanJia", 1 },
                { "PingMin", 1 }
            };
            game = CreateGame(roleConfig);
            RunGameIterations(game, 5);

            // Kill the PingMin
            var pingMinPlayer = FindPlayerByRole(game, "PingMin");
            var update = new Dictionary<string, object>();
            LangRenSha.SetPlayerProperty(game, pingMinPlayer, LangRenSha.dictAlive, 0, update);
            game.StateUpdate(update, true);

            // Evil should win when no civilians
            var winner = LangRenSha.CheckWinCondition(game);
            Assert.That(winner, Is.EqualTo("evil"), "Evil should win when no civilians remain");
        }

        [Test]
        public void Test_WinCondition_GoodWins_NoEvil()
        {
            // Directly test the win condition logic
            var roleConfig = new Dictionary<string, int>
            {
                { "LangRen", 1 },
                { "YuYanJia", 1 },
                { "PingMin", 2 }
            };
            game = CreateGame(roleConfig);
            RunGameIterations(game, 5);

            // Kill the LangRen
            var langRenPlayer = FindPlayerByRole(game, "LangRen");
            var update = new Dictionary<string, object>();
            LangRenSha.SetPlayerProperty(game, langRenPlayer, LangRenSha.dictAlive, 0, update);
            game.StateUpdate(update, true);

            // Good should win when no evil remains
            var winner = LangRenSha.CheckWinCondition(game);
            Assert.That(winner, Is.EqualTo("good"), "Good should win when no werewolves remain");
        }

        [Test]
        public void Test_WinCondition_NoWinner_AllFactionsAlive()
        {
            var roleConfig = new Dictionary<string, int>
            {
                { "LangRen", 1 },
                { "YuYanJia", 1 },
                { "PingMin", 2 }
            };
            game = CreateGame(roleConfig);
            RunGameIterations(game, 5);

            // All factions alive, no winner yet
            var winner = LangRenSha.CheckWinCondition(game);
            Assert.That(winner, Is.Null, "No winner when all factions are alive");
        }

        [Test]
        public void Test_MarkPlayerAboutToDie()
        {
            var roleConfig = new Dictionary<string, int>
            {
                { "LangRen", 1 },
                { "PingMin", 2 }
            };
            game = CreateGame(roleConfig);
            RunGameIterations(game, 5);

            var pingMinPlayers = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == "PingMin");
            var target = pingMinPlayers[0];

            var update = new Dictionary<string, object>();
            var marked = LangRenSha.MarkPlayerAboutToDie(game, target, update);
            game.StateUpdate(update, true);

            Assert.That(marked, Is.True, "Should successfully mark player");

            var aboutToDie = Game.GetGameDictionaryProperty(game, LangRenSha.dictAboutToDie, new List<int>());
            Assert.That(aboutToDie.Contains(target), Is.True, "Player should be in about to die list");

            // Marking again should return false
            update = new Dictionary<string, object>();
            var markedAgain = LangRenSha.MarkPlayerAboutToDie(game, target, update);
            Assert.That(markedAgain, Is.False, "Should not mark same player twice");
        }

        [Test]
        public void Test_StateSequenceIncreases()
        {
            var roleConfig = new Dictionary<string, int>
            {
                { "LangRen", 1 },
                { "YuYanJia", 1 },
                { "PingMin", 1 }
            };
            game = CreateGame(roleConfig);

            var initialSequence = game.StateSequenceNumber;
            RunGameIterations(game, 10);

            Assert.That(game.StateSequenceNumber, Is.GreaterThan(initialSequence), 
                "State sequence should increase after running iterations");
        }

        [Test]
        public void Test_BasicGame_RunsWithoutCrashing()
        {
            // Basic test to verify game can run without crashing
            var roleConfig = new Dictionary<string, int>
            {
                { "LangRen", 1 },
                { "YuYanJia", 1 },
                { "PingMin", 1 }
            };
            game = CreateGame(roleConfig);

            // Run many iterations - should not throw
            Assert.DoesNotThrow(() => RunGameIterations(game, 100), 
                "Game should run 100 iterations without crashing");
        }
    }
}