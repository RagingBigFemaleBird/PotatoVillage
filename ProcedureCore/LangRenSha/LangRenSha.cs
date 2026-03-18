using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Media;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ProcedureCore.LangRenSha
{
    public class LangRenSha : GameAction
    {
        private List<(int, Role)> players;
        private static List<Func<Game, int, List<int>, Dictionary<string, object>, GameActionResult>> interruptHandlers = new()
            { LangRen.RevealSelf, LangQiang.RevealSelf, LangRenSha.WithdrawSheriff, LangRenSha.OverrideDayVote, LangRenSha.OverrideSheriffVote };
        public static List<Func<Game, int, List<int>, Dictionary<string, object>, GameActionResult>> InterruptHandlers
        {
            get
            {
                return interruptHandlers;
            }
        }

        // Dictionary key for owner vote override
        public static string dictOwnerVoteOverride = "owner_vote_override";

        // Dead player skill handlers - return true if handled, false if not handled
        private static List<Func<Game, int, Dictionary<string, object>, (bool, GameActionResult)>> deadPlayerHandlers = new();

        public static void RegisterDeadPlayerHandler(Func<Game, int, Dictionary<string, object>, (bool, GameActionResult)> handler)
        {
            if (!deadPlayerHandlers.Contains(handler))
            {
                deadPlayerHandlers.Add(handler);
            }
        }

        public static List<Func<Game, int, Dictionary<string, object>, (bool, GameActionResult)>> DeadPlayerHandlers
        {
            get
            {
                return deadPlayerHandlers;
            }
        }

        // AfterSpeak handlers - called after a player finishes speaking during the day
        private static List<Func<Game, int, Dictionary<string, object>, (bool, GameActionResult)>> afterSpeakHandlers = new();

        public static void RegisterAfterSpeakHandler(Func<Game, int, Dictionary<string, object>, (bool, GameActionResult)> handler)
        {
            if (!afterSpeakHandlers.Contains(handler))
            {
                afterSpeakHandlers.Add(handler);
            }
        }

        public static List<Func<Game, int, Dictionary<string, object>, (bool, GameActionResult)>> AfterSpeakHandlers
        {
            get
            {
                return afterSpeakHandlers;
            }
        }

        public LangRenSha()
        {
            players = new List<(int, Role)>();
            RegisterDeadPlayerHandler(LieRen.HandleHunterDeathSkill);
            RegisterDeadPlayerHandler(LangQiang.HandleLangQiangDeathSkill);
            RegisterDeadPlayerHandler(Xiong.HandleXiongDeathSkill);
            RegisterDeadPlayerHandler(LangMeiRen.HandleLangMeiRenDeathSkill);
            RegisterDeadPlayerHandler(FuChouZhe.HandleRevengerDeathSkill);
            RegisterDeadPlayerHandler(JiXieLang.HandleJiXieLangDeathSkill);
            RegisterDeadPlayerHandler(MengMianRen.HandleDeathSkill);
            RegisterAfterSpeakHandler(MengMianRen.HandleAfterSpeak);
            // Players will be initialized dynamically in GenerateStateDiff based on roleDict
        }

        private Role CreateRoleInstance(string roleName)
        {
            return roleName switch
            {
                "LangRen" => new LangRen(),
                "YuYanJia" => new YuYanJia(),
                "TongLingShi" => new TongLingShi(),
                "NvWu" => new NvWu(),
                "WuZhe" => new WuZhe(),
                "JiaMian" => new JiaMian(),
                "LieRen" => new LieRen(),
                "LangQiang" => new LangQiang(),
                "PingMin" => new PingMin(),
                "BaiChi" => new BaiChi(),
                "DaMao" => new DaMao(),
                "LaoShu" => new LaoShu(),
                "SheMengRen" => new SheMengRen(),
                "Xiong" => new Xiong(),
                "Thief" => new Thief(),
                "MengMianRen" => new MengMianRen(),
                "ShouWei" => new ShouWei(),
                "YingZi" => new YingZi(),
                "FuChouZhe" => new FuChouZhe(),
                "HunZi" => new HunZi(),
                "JiXieLang" => new JiXieLang(),
                "LangMeiRen" => new LangMeiRen(),
                _ => throw new ArgumentException($"Not a role: {roleName}")
            };
        }

        public enum PlayerFaction
        {
            Evil = 1,
            God = 2,
            Civilian = 4,
            ThirdParty = 8,
        }

        public static string dictPlayers = "players";
        public static string dictRole = "role";
        public static string dictRoleVersion = "role_version";
        public static string dictRoleCheckComplete = "role_check_complete";
        public static string dictNightOrders = "night_orders";
        public static string dictAlive = "alive";
        public static string dictDay = "day";
        public static string dictPhase = "phase";
        public static string dictAction = "action";
        public static string dictSpeak = "speak";
        public static string dictSpeaker = "speaker";
        public static string dictAboutToDie = "about_to_die";
        public static string dictDeadPlayerAction = "dead_player";
        public static string dictDeadSkillsProcessed = "dead_skills_processed";
        public static string dictPlayerAlliance = "alliance";
        public static string dictPlayerFaction = "faction";
        public static string dictOriginalSheriff = "original_sheriff";
        public static string dictSheriff = "sheriff";
        public static string dictCurrentSheriff = "current_sheriff";
        public static string dictPk = "pk";
        public static string dictInterrupt = "interrupt";
        public static string dictVoteOut = "voteout";
        public static string dictVoteInfo = "voteinfo";
        public static string dictSheriffVote = "sheriffvote";
        public static string dictDaySpeechDirection = "day_speech_direction";
        public static string dictSkipDaySpeech = "skip_day_speech";
        public static string dictDay0AnnouncementDone = "day0_announcement_done";
        public static string dictGameWinner = "game_winner";
        public static string dictGameOwner = "game_owner";

        public static string dictDurationLangRen = "duration_langren";
        public static string dictDurationSpeech = "duration_speech";
        public static string dictDurationPlayerReact = "duration_player_react";
        public static string dictRoundTableMode = "round_table_mode"; // 0 = start from sheriff, 1 = start from dead player (if single dead)
        public static string dictOwnerControlEnabled = "owner_control_enabled"; // 0 = disabled, 1 = owner can override day flow
        public static string dictSeatCounterClockwise = "seat_counter_clockwise"; // 0 = clockwise (default), 1 = counter-clockwise

        public int Version
        {
            get
            {
                return 1;
            }
        }

        public static int actionDuraionPlayerReact = 10;
        public static int actionDurationPlayerSpeak = 90;

        public GameActionResult GenerateStateDiff(Game game, Dictionary<string, object> update)
        {
            // Game init
            if (game.StateSequenceNumber == 0)
            {
                // Initialize players from roleDict if not already done
                if (players.Count == 0)
                {
                    InitializePlayersFromRoleDict(game);
                }

                // Add people
                update[dictPlayers] = new Dictionary<string, object>();
                foreach (var player in players)
                {
                    var (position, role) = player;
                    ((Dictionary<string, object>)update[dictPlayers])[position.ToString()] = new Dictionary<string, object>();
                    ((Dictionary<string, object>)((Dictionary<string, object>)update[dictPlayers])[position.ToString()])[dictRole] = role.Name;
                    ((Dictionary<string, object>)((Dictionary<string, object>)update[dictPlayers])[position.ToString()])[dictRoleVersion] = role.Version;
                    ((Dictionary<string, object>)((Dictionary<string, object>)update[dictPlayers])[position.ToString()])[dictAlive] = 1;
                    foreach (var rd in role.RoleDict)
                    {
                        ((Dictionary<string, object>)((Dictionary<string, object>)update[dictPlayers])[position.ToString()])[rd.Key] = role.RoleDict[rd.Key];
                    }
                }
                update[dictNightOrders] = new List<int>();
                update[dictDay] = 0;
                update[dictPhase] = 0;
                update[dictAction] = 0;
                update[dictSpeak] = 0;
                return GameActionResult.Restart;
            }
            if (game.StateSequenceNumber == 1)
            {
                var no = Game.GetGameDictionaryProperty(game, LangRenSha.dictNightOrders, new List<int>());
                no.AddRange([(int)ActionConstant.GameBeginAnnouncement, (int)ActionConstant.RoleCheck, (int)ActionConstant.PutDownDevice, (int)ActionConstant.NightTimeAnnouncement, (int)ActionConstant.DayTimeAnnouncement]);
                update[LangRenSha.dictNightOrders] = no;
                return GameActionResult.Continue;
            }
            // Game begin announcement
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == (int)ActionConstant.GameBeginAnnouncement)
            {
                if (Game.GetGameDictionaryProperty(game, LangRenSha.dictDay, 0) != 0 || UserAction.EndUserAction(game, update))
                {
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, 5, update))
                    {
                        update[UserAction.dictUserActionTargets] = new List<int>();
                        update[UserAction.dictUserActionUsers] = new List<int> { -1 };
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.CheckPrivate;
                        return GameActionResult.Restart;
                    }
                }
            }

            // Role check
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == (int)ActionConstant.RoleCheck)
            {
                if (Game.GetGameDictionaryProperty(game, LangRenSha.dictDay, 0) != 0 || UserAction.EndUserAction(game, update))
                {
                    update[dictRoleCheckComplete] = null; // Clear role check tracking
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                else
                {
                    var allPlayers = LangRenSha.GetPlayers(game, x => true);
                    var roleCheckComplete = Game.GetGameDictionaryProperty(game, dictRoleCheckComplete, new List<int>());

                    if (UserAction.StartUserAction(game, 60, update))
                    {
                        // Only include players who haven't completed role check yet
                        var pendingPlayers = allPlayers.Where(p => !roleCheckComplete.Contains(p)).ToList();
                        update[UserAction.dictUserActionTargets] = new List<int>() { 0 };
                        update[UserAction.dictUserActionUsers] = pendingPlayers;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.CheckPrivate;
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        // Check for early completions
                        var pendingPlayers = allPlayers.Where(p => !roleCheckComplete.Contains(p)).ToList();
                        (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, pendingPlayers, update);
                        if (inputValid)
                        {
                            bool anyNewComplete = false;
                            foreach (var entry in input)
                            {
                                var player = int.Parse(entry.Key);
                                var targets = (List<int>)entry.Value;
                                // If player responded (any response means they acknowledged their role)
                                if (targets.Count > 0 && !roleCheckComplete.Contains(player))
                                {
                                    roleCheckComplete.Add(player);
                                    anyNewComplete = true;
                                }
                            }

                            if (anyNewComplete)
                            {
                                update[dictRoleCheckComplete] = roleCheckComplete;

                                // Check if everyone has completed
                                if (roleCheckComplete.Count >= allPlayers.Count)
                                {
                                    update[dictRoleCheckComplete] = null;
                                    UserAction.EndUserAction(game, update, true);
                                    LangRenSha.AdvanceAction(game, update);
                                    return GameActionResult.Restart;
                                }

                                // Update the action users to exclude completed players
                                var remainingPlayers = allPlayers.Where(p => !roleCheckComplete.Contains(p)).ToList();
                                update[UserAction.dictUserActionUsers] = remainingPlayers;
                                return GameActionResult.Continue;
                            }
                        }
                    }
                }
            }

            // Put down device announcement
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == (int)ActionConstant.PutDownDevice)
            {
                if (Game.GetGameDictionaryProperty(game, LangRenSha.dictDay, 0) != 0 || UserAction.EndUserAction(game, update))
                {
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, 3, update))
                    {
                        update[UserAction.dictUserActionTargets] = new List<int>();
                        update[UserAction.dictUserActionUsers] = new List<int> { -1 };
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.PutDownDevice;
                        return GameActionResult.Restart;
                    }
                }
            }

            // Night time announcement
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == (int)ActionConstant.NightTimeAnnouncement)
            {
                if (UserAction.EndUserAction(game, update))
                {
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, 7, update))
                    {
                        var allPlayers = LangRenSha.GetPlayers(game, x => true);
                        update[UserAction.dictUserActionTargets] = new List<int>();
                        update[UserAction.dictUserActionUsers] = new List<int>() { -1 };
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.NightTime;
                        return GameActionResult.Restart;
                    }
                }
            }
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == (int)ActionConstant.DayTimeAnnouncement)
            {
                if (UserAction.EndUserAction(game, update))
                {
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, 3, update))
                    {
                        var allPlayers = LangRenSha.GetPlayers(game, x => true);
                        update[UserAction.dictUserActionTargets] = new List<int>();
                        update[UserAction.dictUserActionUsers] = new List<int>() { -1 };
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.DayTime;
                        return GameActionResult.Restart;
                    }
                }
            }
            if (game.StateSequenceNumber >= 2)
            {
                if (Game.GetGameDictionaryProperty(game, dictAction, 0) == 0 && Game.GetGameDictionaryProperty(game, dictPhase, 0) == 0)
                {
                    AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                else if (Game.GetGameDictionaryProperty(game, dictPhase, 0) == 1)
                {
                    return HandleSpeaker(game, update);
                }
            }
            return GameActionResult.NotExecuted;
        }

        private void InitializePlayersFromRoleDict(Game game)
        {
            var roleList = new List<Role>();

            // If roleDict is configured, use it; otherwise use defaults
            if (game.RoleConfiguration != null && game.RoleConfiguration.Count > 0)
            {
                foreach (var roleEntry in game.RoleConfiguration)
                {
                    string roleName = roleEntry.Key;
                    int count = roleEntry.Value;

                    for (int i = 0; i < count; i++)
                    {
                        try
                        {
                            var role = CreateRoleInstance(roleName);
                            roleList.Add(role);
                        }
                        catch (ArgumentException ex)
                        {
                            Console.WriteLine($"Warning: {ex.Message}");
                        }
                    }
                }
            }
            else
            {
                roleList.Add(new YuYanJia());
            }

            // Shuffle the roles randomly
            ShuffleRoles(roleList);

            // Assign shuffled roles to positions
            for (int position = 1; position <= roleList.Count; position++)
            {
                players.Add((position, roleList[position - 1]));
            }
        }

        private void ShuffleRoles(List<Role> roles)
        {
            Random random = new Random();
            int count = roles.Count;

            // Fisher-Yates shuffle algorithm
            for (int i = count - 1; i > 0; i--)
            {
                int randomIndex = random.Next(i + 1);

                // Swap
                (roles[randomIndex], roles[i]) = (roles[i], roles[randomIndex]);
            }
        }

        public static GameActionResult WithdrawSheriff(Game game, int player, List<int> targets, Dictionary<string, object> update)
        {
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictSpeak, 0) == (int)SpeakConstant.SheriffSpeech || Game.GetGameDictionaryProperty(game, LangRenSha.dictSpeak, 0) == (int)SpeakConstant.WithdrawOrReveal)
            {
                if (targets.Contains(-2))
                {
                    var sheriffPlayers = Game.GetGameDictionaryProperty(game, dictSheriff, new List<int>());
                    if (sheriffPlayers.Remove(player))
                    {
                        update[LangRenSha.dictSheriff] = sheriffPlayers;
                    }
                    return GameActionResult.Continue;
                }
            }
            return GameActionResult.NotExecuted;

        }

        /// <summary>
        /// Interrupt handler for game owner to override day vote.
        /// When triggered with target -100 during DaySpeech phase, it interrupts the voting flow
        /// and transitions to OwnerVoteSelect phase where the owner chooses who to vote out.
        /// Only works if owner_control_enabled is set to 1.
        /// </summary>
        public static GameActionResult OverrideDayVote(Game game, int player, List<int> targets, Dictionary<string, object> update)
        {
            // Check if this is a vote override request (contains -100)
            if (!targets.Contains(-100))
            {
                return GameActionResult.NotExecuted;
            }

            // Check if owner control is enabled
            var ownerControlEnabled = Game.GetGameDictionaryProperty(game, dictOwnerControlEnabled, 0);
            if (ownerControlEnabled != 1)
            {
                return GameActionResult.NotExecuted;
            }

            // Only allow during DaySpeech phase
            var currentSpeak = Game.GetGameDictionaryProperty(game, LangRenSha.dictSpeak, 0);
            if (currentSpeak != (int)SpeakConstant.DaySpeech)
            {
                return GameActionResult.NotExecuted;
            }

            // End current user action and jump to owner vote select phase
            UserAction.EndUserAction(game, update, true);
            update[dictSpeak] = (int)SpeakConstant.OwnerVoteSelect;
            update[dictSpeaker] = null; // Clear speaker state

            return GameActionResult.Restart;
        }

        /// <summary>
        /// Interrupt handler for game owner to override sheriff vote.
        /// When triggered with target -101 during SheriffSpeech or WithdrawOrReveal phase, it interrupts the sheriff voting flow
        /// and transitions to OwnerSheriffSelect phase where the owner chooses who becomes sheriff.
        /// Only works if owner_control_enabled is set to 1.
        /// </summary>
        public static GameActionResult OverrideSheriffVote(Game game, int player, List<int> targets, Dictionary<string, object> update)
        {
            // Check if this is a sheriff override request (contains -101)
            if (!targets.Contains(-101))
            {
                return GameActionResult.NotExecuted;
            }

            // Check if owner control is enabled
            var ownerControlEnabled = Game.GetGameDictionaryProperty(game, dictOwnerControlEnabled, 0);
            if (ownerControlEnabled != 1)
            {
                return GameActionResult.NotExecuted;
            }

            // Only allow during SheriffSpeech or WithdrawOrReveal phase
            var currentSpeak = Game.GetGameDictionaryProperty(game, LangRenSha.dictSpeak, 0);
            if (currentSpeak != (int)SpeakConstant.SheriffSpeech && currentSpeak != (int)SpeakConstant.WithdrawOrReveal)
            {
                return GameActionResult.NotExecuted;
            }

            // End current user action and jump to owner sheriff select phase
            UserAction.EndUserAction(game, update, true);
            update[dictSpeak] = (int)SpeakConstant.OwnerSheriffSelect;
            update[dictSpeaker] = null; // Clear speaker state

            return GameActionResult.Restart;
        }

        public static GameActionResult HandleSpeaker(Game game, Dictionary<string, object> update)
        {
            var allPlayers = LangRenSha.GetPlayers(game, x => true);
            var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);

            // Sheriff volunteer
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == (int)SpeakConstant.SheriffVolunteer)
            {
                var day = Game.GetGameDictionaryProperty(game, dictDay, 0);

                if (day == 0)
                {
                    // Day 0: Full sheriff volunteer phase
                    if (UserAction.EndUserAction(game, update))
                    {
                        (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, allPlayers, update);
                        var sheriffPlayers = new List<int>();
                        if (inputValid)
                        {
                            foreach (var player in allPlayers)
                            {
                                if (input.ContainsKey(player.ToString()) && ((List<int>)input[player.ToString()]).Contains(-1))
                                {
                                    sheriffPlayers.Add(player);
                                }
                            }
                        }
                        update[dictSpeaker] = null;
                        update[dictSheriff] = sheriffPlayers;
                        update[dictOriginalSheriff] = new List<int>(sheriffPlayers);
                        if (sheriffPlayers.Count == allPlayers.Count || sheriffPlayers.Count == 0)
                        {
                            update[dictSpeak] = (int)SpeakConstant.DeathAnnouncement;
                        }
                        else if (sheriffPlayers.Count == 1)
                        {
                            update[dictSpeak] = (int)SpeakConstant.DeathAnnouncement;
                            update[dictCurrentSheriff] = sheriffPlayers[0];
                        }
                        else
                        {
                            update[dictSpeak] = (int)SpeakConstant.SheriffSpeech;
                        }
                        return GameActionResult.Restart;
                    }
                    else
                    {

                        if (UserAction.StartUserAction(game, actionDuraionPlayerReact, update))
                        {
                            update[UserAction.dictUserActionTargets] = new List<int> { -1, 0 };
                            update[UserAction.dictUserActionUsers] = allPlayers;
                            update[UserAction.dictUserActionTargetsCount] = 1;
                            update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.SheriffVolunteer;
                            return GameActionResult.Restart;
                        }
                    }
                    return GameActionResult.NotExecuted;
                }
                else if (day == 1)
                {
                    // Day 1: Check if there are pending sheriff candidates, go to WithdrawOrReveal
                    var sheriffPlayers = Game.GetGameDictionaryProperty(game, dictSheriff, new List<int>());
                    var currentSheriff = Game.GetGameDictionaryProperty(game, dictCurrentSheriff, 0);

                    if (currentSheriff == 0 && sheriffPlayers.Count > 1)
                    {
                        // No sheriff elected yet and multiple candidates - go to WithdrawOrReveal
                        update[dictSpeak] = (int)SpeakConstant.WithdrawOrReveal;
                    }
                    else
                    {
                        // Sheriff already elected or no candidates - proceed to death announcement
                        update[dictSpeak] = (int)SpeakConstant.DeathAnnouncement;
                    }
                    return GameActionResult.Restart;
                }
                else
                {
                    // Day 2+: Skip directly to DeathAnnouncement
                    update[dictSpeak] = (int)SpeakConstant.DeathAnnouncement;
                    return GameActionResult.Restart;
                }

            }
            // Sheriff speech
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == (int)SpeakConstant.SheriffSpeech)
            {
                var sheriffPlayers = Game.GetGameDictionaryProperty(game, dictSheriff, new List<int>());
                var volunteersInfo = string.Join(", ", sheriffPlayers);
                return HandleRoundTableSpeak(game, sheriffPlayers, sheriffPlayers[game.GetRandomNumber() % sheriffPlayers.Count], (game.GetRandomNumber() % 2) == 1, update, (int)SpeakConstant.WithdrawOrReveal, (int)HintConstant.SheriffSpeech, volunteersInfo);
            }
            // WithdrawOrReveal phase (退水自爆) - Players can withdraw from sheriff or LangRen can reveal
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == (int)SpeakConstant.WithdrawOrReveal)
            {
                var sheriffPlayers = Game.GetGameDictionaryProperty(game, dictSheriff, new List<int>());
                var gameOwner = Game.GetGameDictionaryProperty(game, dictGameOwner, 0);

                if (UserAction.EndUserAction(game, update))
                {
                    // Check remaining sheriff candidates after withdrawals
                    var remainingSheriff = Game.GetGameDictionaryProperty(game, dictSheriff, new List<int>());

                    if (remainingSheriff.Count == 0)
                    {
                        // No candidates left, no sheriff
                        update[dictCurrentSheriff] = 0;
                        update[dictSpeak] = (int)SpeakConstant.DeathAnnouncement;
                    }
                    else if (remainingSheriff.Count == 1)
                    {
                        // Only one candidate left, they become sheriff
                        update[dictCurrentSheriff] = remainingSheriff[0];
                        update[dictSpeak] = (int)SpeakConstant.DeathAnnouncement;
                    }
                    else
                    {
                        // Multiple candidates, proceed to vote
                        update[dictSpeak] = (int)SpeakConstant.SheriffVoteTally;
                    }
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, actionDuraionPlayerReact, update))
                    {
                        // Build list of users who can respond
                        var respondUsers = new List<int>();
                        respondUsers.AddRange(allPlayers);

                        // Targets: -2 = withdraw from sheriff
                        var targets = new List<int> { -2 };

                        update[UserAction.dictUserActionTargets] = targets;
                        update[UserAction.dictUserActionUsers] = respondUsers;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.WithdrawOrReveal;
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        // Check for early completion - process withdrawals and reveals
                        (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, allPlayers, update);
                        if (inputValid)
                        {
                            bool anyAction = false;

                            // Process withdrawals from sheriff
                            foreach (var entry in input)
                            {
                                var player = int.Parse(entry.Key);
                                var playerTargets = (List<int>)entry.Value;

                                if (playerTargets.Contains(-2))
                                {
                                    // Player withdraws from sheriff
                                    if (sheriffPlayers.Remove(player))
                                    {
                                        update[dictSheriff] = sheriffPlayers;
                                        anyAction = true;
                                    }
                                }
                            }

                            // Process interrupt handlers (LangRen reveal, owner override)
                            foreach (var int_input in input_others)
                            {
                                var key = int.Parse(int_input.Key);
                                var value = (List<int>)int_input.Value;
                                foreach (var handler in LangRenSha.InterruptHandlers)
                                {
                                    var result = handler(game, key, value, update);
                                    if (result != GameActionResult.NotExecuted)
                                    {
                                        return result;
                                    }
                                }
                            }

                            if (anyAction)
                            {
                                return GameActionResult.Continue;
                            }
                        }
                    }
                }
                return GameActionResult.NotExecuted;
            }
            // Sheriff vote - tally only
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == (int)SpeakConstant.SheriffVoteTally)
            {
                var sheriffPlayers = Game.GetGameDictionaryProperty(game, dictSheriff, new List<int>());
                var originalSheriffPlayers = Game.GetGameDictionaryProperty(game, dictOriginalSheriff, new List<int>());
                var votePlayers = new List<int>();
                var alivePlayers1 = LangRenSha.GetPlayers(game, x => (int)x[dictAlive] == 1);
                sheriffPlayers.RemoveAll(x => !alivePlayers1.Contains(x));
                votePlayers.AddRange(alivePlayers1);
                votePlayers.RemoveAll(x => originalSheriffPlayers.Contains(x));

                if (UserAction.EndUserAction(game, update))
                {
                    (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, votePlayers, update);
                    if (inputValid)
                    {
                        var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, -1);
                        update[dictVoteOut] = targets;
                        update[dictVoteInfo] = FormatVoteTally(input);
                    }
                    else
                    {
                        update[dictVoteOut] = new List<int>();
                        update[dictVoteInfo] = "";
                    }
                    update[dictSpeak] = (int)SpeakConstant.SheriffVoteResult;
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, actionDuraionPlayerReact, update))
                    {
                        update[UserAction.dictUserActionTargets] = sheriffPlayers;
                        update[UserAction.dictUserActionUsers] = votePlayers;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.SheriffVoteVote;
                        return GameActionResult.Restart;
                    }
                }
                return GameActionResult.NotExecuted;
            }
            // Sheriff vote - display result
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == (int)SpeakConstant.SheriffVoteResult)
            {
                var voteOut = Game.GetGameDictionaryProperty(game, dictVoteOut, new List<int>());

                if (UserAction.EndUserAction(game, update))
                {
                    // Route based on vote result
                    if (voteOut.Count > 1)
                    {
                        // Tie - go to PK
                        update[dictSpeak] = (int)SpeakConstant.SheriffPKSpeech;
                        update[dictPk] = voteOut;
                    }
                    else if (voteOut.Count == 1)
                    {
                        // Single target - go to death announcement
                        update[dictSpeak] = (int)SpeakConstant.DeathAnnouncement;
                        update[dictCurrentSheriff] = voteOut[0];
                    }
                    else
                    {
                        update[dictSpeak] = (int)SpeakConstant.DeathAnnouncement;
                        update[dictCurrentSheriff] = 0;
                    }
                    return GameActionResult.Restart;
                }
                else
                {
                    // Display vote result
                    if (UserAction.StartUserAction(game, 5, update))
                    {
                        update[UserAction.dictUserActionTargets] = new List<int>();
                        update[UserAction.dictUserActionUsers] = new List<int>() { -1 };
                        update[UserAction.dictUserActionTargetsCount] = 0;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.VoteResult;
                        update[UserAction.dictUserActionInfo] = Game.GetGameDictionaryProperty(game, dictVoteInfo, "");
                        return GameActionResult.Restart;
                    }
                }
                return GameActionResult.NotExecuted;
            }
            // Sheriff PK speech
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == (int)SpeakConstant.SheriffPKSpeech)
            {
                var pkPlayers = Game.GetGameDictionaryProperty(game, dictPk, new List<int>());
                var sheriffPlayers = Game.GetGameDictionaryProperty(game, dictSheriff, new List<int>());
                var first = sheriffPlayers[game.GetRandomNumber() % sheriffPlayers.Count];
                bool directionPlus = (game.GetRandomNumber() % 2) == 0;
                var nextPlayer = NextPlayer(pkPlayers, allPlayers.Count, first, directionPlus);
                var volunteersInfo = string.Join(", ", pkPlayers);
                return HandleRoundTableSpeak(game, pkPlayers, nextPlayer, directionPlus, update, (int)SpeakConstant.SheriffPKVote, (int)HintConstant.SheriffPK, volunteersInfo);
            }
            // Sheriff PK voting + tally
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == (int)SpeakConstant.SheriffPKVote)
            {
                var pkPlayers = Game.GetGameDictionaryProperty(game, dictPk, new List<int>());
                var votePlayers = new List<int>();
                votePlayers.AddRange(allPlayers);
                votePlayers.RemoveAll(x => pkPlayers.Contains(x));

                if (UserAction.EndUserAction(game, update))
                {
                    (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, votePlayers, update);
                    if (inputValid)
                    {
                        var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, -1);
                        update[dictVoteOut] = targets;
                        update[dictVoteInfo] = FormatVoteTally(input);
                    }
                    else
                    {
                        update[dictVoteOut] = new List<int>();
                        update[dictVoteInfo] = "";
                    }
                    update[dictSpeak] = (int)SpeakConstant.SheriffPKResult;
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, actionDuraionPlayerReact, update))
                    {
                        update[UserAction.dictUserActionTargets] = pkPlayers;
                        update[UserAction.dictUserActionUsers] = votePlayers;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.SheriffVoteVote;
                        return GameActionResult.Restart;
                    }
                }
                return GameActionResult.NotExecuted;
            }
            // Sheriff PK vote result display
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == (int)SpeakConstant.SheriffPKResult)
            {
                var voteOut = Game.GetGameDictionaryProperty(game, dictVoteOut, new List<int>());

                if (UserAction.EndUserAction(game, update))
                {
                    if (voteOut.Count == 1)
                    {
                        update[dictCurrentSheriff] = voteOut[0];
                    }
                    else
                    {
                        update[dictCurrentSheriff] = 0;
                    }
                    update[dictPk] = null;
                    update[dictVoteOut] = null;
                    update[dictVoteInfo] = null;
                    update[dictSpeak] = (int)SpeakConstant.DeathAnnouncement;
                    return GameActionResult.Restart;
                }
                else
                {
                    // Display vote result
                    if (UserAction.StartUserAction(game, 5, update))
                    {
                        update[UserAction.dictUserActionTargets] = new List<int>();
                        update[UserAction.dictUserActionUsers] = new List<int>() { -1 };
                        update[UserAction.dictUserActionTargetsCount] = 0;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.VoteResult;
                        update[UserAction.dictUserActionInfo] = Game.GetGameDictionaryProperty(game, dictVoteInfo, "");
                        return GameActionResult.Restart;
                    }
                }
                return GameActionResult.NotExecuted;
            }
            // Owner sheriff select - game owner manually chooses who becomes sheriff
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == (int)SpeakConstant.OwnerSheriffSelect)
            {
                // Get the game owner player ID
                var gameOwner = Game.GetGameDictionaryProperty(game, dictGameOwner, 0);
                var ownerList = gameOwner > 0 ? new List<int> { gameOwner } : new List<int>();
                var allAlivePlayers = LangRenSha.GetPlayers(game, x => (int)x[dictAlive] == 1);

                // Owner selects who becomes sheriff
                if (UserAction.EndUserAction(game, update))
                {
                    // No one selected, no sheriff
                    update[dictCurrentSheriff] = 0;
                    update[dictSpeak] = (int)SpeakConstant.DeathAnnouncement;
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, 999, update))
                    {
                        // Show sheriff candidates as selectable targets (plus 0 for no sheriff)
                        var selectableTargets = new List<int>(allAlivePlayers);
                        selectableTargets.Insert(0, 0); // Add 0 at the beginning for "no sheriff"
                        update[UserAction.dictUserActionTargets] = selectableTargets;
                        update[UserAction.dictUserActionUsers] = ownerList;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.OwnerSheriffSelect;
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        // Check for early completion
                        (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, ownerList, update);
                        if (inputValid)
                        {
                            foreach (var entry in input)
                            {
                                var targets = (List<int>)entry.Value;
                                if (targets.Count > 0 && targets[0] > 0)
                                {
                                    // Set the selected player as sheriff
                                    update[dictCurrentSheriff] = targets[0];
                                    update[dictSpeak] = (int)SpeakConstant.DeathAnnouncement;
                                    UserAction.EndUserAction(game, update, true);
                                    return GameActionResult.Restart;
                                }
                                if (targets.Count > 0 && targets[0] == 0)
                                {
                                    // Owner selected no one (0 = no sheriff)
                                    update[dictCurrentSheriff] = 0;
                                    update[dictSpeak] = (int)SpeakConstant.DeathAnnouncement;
                                    UserAction.EndUserAction(game, update, true);
                                    return GameActionResult.Restart;
                                }
                            }
                        }
                    }
                }
                return GameActionResult.NotExecuted;
            }
            // Death announcement
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == (int)SpeakConstant.DeathAnnouncement)
            {
                var deadPlayers = Game.GetGameDictionaryProperty(game, dictAboutToDie, new List<int>());

                var skipDaySpeech = Game.GetGameDictionaryProperty(game, dictSkipDaySpeech, 0) == 1;
                var day = Game.GetGameDictionaryProperty(game, dictDay, 0);
                var announcementDone = Game.GetGameDictionaryProperty(game, dictDay0AnnouncementDone, 0) == 1;
                if (skipDaySpeech && (day != 0 || announcementDone))
                {
                    update[dictSpeak] = (int)SpeakConstant.DeathProcessingEntry;
                    return GameActionResult.Restart;
                }

                if (UserAction.EndUserAction(game, update))
                {
                    // Move to kill dead players phase
                    update[dictSpeak] = (int)SpeakConstant.DeathProcessingEntry;
                    update[dictDay0AnnouncementDone] = 1;
                    return GameActionResult.Restart;
                }
                else
                {
                    // Display death announcement
                    if (UserAction.StartUserAction(game, 5, update))
                    {
                        var deathInfo = string.Join(", ", deadPlayers);

                        // Add Xiong bark info if available (1 = barked, 2 = not barked)
                        var xiongBark = Game.GetGameDictionaryProperty(game, Xiong.dictXiongBark, 0);
                        if (xiongBark == 1 || xiongBark == 2)
                        {
                            deathInfo += $";{xiongBark}";
                        }

                        update[UserAction.dictUserActionTargets] = new List<int>();
                        update[UserAction.dictUserActionUsers] = new List<int>() { -1 };
                        update[UserAction.dictUserActionTargetsCount] = 0;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.DeathAnnouncement;
                        update[UserAction.dictUserActionInfo] = deathInfo;
                        return GameActionResult.Restart;
                    }
                }
                return GameActionResult.NotExecuted;
            }
            // Dead player
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == (int)SpeakConstant.DeathProcessingEntry)
            {
                // Set up interrupt to go through death handling
                var interrupted = new Dictionary<string, object>();
                var skipDaySpeech = Game.GetGameDictionaryProperty(game, dictSkipDaySpeech, 0) == 1;
                if (skipDaySpeech)
                    interrupted[dictSpeak] = (int)SpeakConstant.EndOfDay;
                else
                    interrupted[dictSpeak] = (int)SpeakConstant.WinConditionCheck; // Go to win condition check first
                update[dictSpeak] = (int)SpeakConstant.DeathHandlingInterrupt;
                update[dictSkipDaySpeech] = 0;
                update[dictInterrupt] = interrupted;
                game.UseRandomNumber(update);
                return GameActionResult.Restart;
            }
            // speak=20: Check win conditions (called from death handling)
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == (int)SpeakConstant.WinConditionCheck)
            {
                var winner = CheckWinCondition(game);
                if (winner != null)
                {
                    update[dictGameWinner] = winner;
                    // Game over - display winner
                    if (UserAction.EndUserAction(game, update))
                    {
                        // Game ends here
                        return GameActionResult.GameOver;
                    }
                    else
                    {
                        if (UserAction.StartUserAction(game, 10, update))
                        {
                            update[UserAction.dictUserActionTargets] = new List<int>();
                            update[UserAction.dictUserActionUsers] = new List<int>() { -1 };
                            update[UserAction.dictUserActionTargetsCount] = 0;
                            update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.GameOver;
                            update[UserAction.dictUserActionInfo] = winner;
                            return GameActionResult.Restart;
                        }
                    }
                    return GameActionResult.NotExecuted;
                }
                else
                {
                    // No winner yet, continue to day speech
                    update[dictSpeak] = (int)SpeakConstant.SheriffChooseDirection;
                    return GameActionResult.Restart;
                }
            }

            // Sheriff choose day speech direction
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == (int)SpeakConstant.SheriffChooseDirection)
            {
                var sheriff = Game.GetGameDictionaryProperty(game, dictCurrentSheriff, 0);
                var gameOwner = Game.GetGameDictionaryProperty(game, dictGameOwner, 0);
                var ownerControlEnabled = Game.GetGameDictionaryProperty(game, dictOwnerControlEnabled, 0);

                // Build list of users who can respond (sheriff and/or owner)
                var respondUsers = new List<int>();
                if (sheriff != 0)
                {
                    respondUsers.Add(sheriff);
                    if (gameOwner > 0 && ownerControlEnabled == 1 && !respondUsers.Contains(gameOwner))
                    {
                        respondUsers.Add(gameOwner);
                    }
                }

                // Only ask if there's someone to ask
                if (respondUsers.Count > 0)
                {
                    if (UserAction.EndUserAction(game, update))
                    {
                        (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, respondUsers, update);

                        bool directionRight = false;
                        // Check sheriff's response first, then owner's
                        foreach (var user in respondUsers)
                        {
                            if (inputValid && input.ContainsKey(user.ToString()))
                            {
                                var targets = (List<int>)input[user.ToString()];
                                if (targets.Count > 0 && targets[0] == -1)
                                {
                                    directionRight = true;
                                    break;
                                }
                                if (targets.Count > 0 && targets[0] == -2)
                                {
                                    directionRight = false;
                                    break;
                                }
                            }
                        }

                        update[UserAction.dictUserActionUsers] = new List<int>();
                        update[dictSpeak] = (int)SpeakConstant.DaySpeech;
                        update[dictDaySpeechDirection] = directionRight;
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        if (UserAction.StartUserAction(game, actionDuraionPlayerReact, update))
                        {
                            update[UserAction.dictUserActionTargets] = new List<int> { -2, -1 }; // -2 = left, -1 = right
                            update[UserAction.dictUserActionUsers] = respondUsers;
                            update[UserAction.dictUserActionTargetsCount] = 1;
                            update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.SheriffChooseDirection;
                            return GameActionResult.Restart;
                        }
                        else
                        {
                            (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, respondUsers, update);

                            // Check for early completion from any respondUser
                            foreach (var user in respondUsers)
                            {
                                if (inputValid && input.ContainsKey(user.ToString()))
                                {
                                    var targets = (List<int>)input[user.ToString()];
                                    if (targets.Count > 0 && targets[0] == -1)
                                    {
                                        update[UserAction.dictUserActionUsers] = new List<int>();
                                        update[dictSpeak] = (int)SpeakConstant.DaySpeech;
                                        update[dictDaySpeechDirection] = true;
                                        UserAction.EndUserAction(game, update, true);
                                        return GameActionResult.Restart;
                                    }
                                    if (targets.Count > 0 && targets[0] == -2)
                                    {
                                        update[UserAction.dictUserActionUsers] = new List<int>();
                                        update[dictSpeak] = (int)SpeakConstant.DaySpeech;
                                        update[dictDaySpeechDirection] = false;
                                        UserAction.EndUserAction(game, update, true);
                                        return GameActionResult.Restart;
                                    }
                                }
                            }
                        }
                    }
                    return GameActionResult.NotExecuted;
                }
                else
                {
                    // No sheriff and no owner, default to left
                    update[dictSpeak] = (int)SpeakConstant.DaySpeech;
                    update[dictDaySpeechDirection] = false;
                    return GameActionResult.Restart;
                }
            }
            // Day speech
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == (int)SpeakConstant.DaySpeech)
            {
                var ap = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
                var sheriff = Game.GetGameDictionaryProperty(game, dictCurrentSheriff, 0);
                var directionRight = Game.GetGameDictionaryProperty(game, dictDaySpeechDirection, false);
                var roundTableMode = Game.GetGameDictionaryProperty(game, dictRoundTableMode, 0);
                var deadPlayers = Game.GetGameDictionaryProperty(game, dictDeadPlayerAction, new List<int>());

                int first = 0;
                int lastPlayer = -1; // Sheriff speaks last in mode 0

                if (roundTableMode == 0 && deadPlayers.Count == 1)
                {
                    // Mode 0: Start from left/right of the dead player (if single dead)
                    // Sheriff speaks last
                    var deadPlayer = deadPlayers[0];
                    first = deadPlayer;
                    if (sheriff != 0 && ap.Contains(sheriff))
                    {
                        lastPlayer = sheriff;
                    }
                }
                else
                {
                    // Mode 0 (default) or multiple dead: Start from sheriff
                    if (sheriff != 0)
                    {
                        first = sheriff;
                    }
                    else
                    {
                        first = ap[game.GetRandomNumber() % (ap.Count == 0 ? 1 : ap.Count)];
                    }
                }

                bool dir = directionRight;

                return HandleRoundTableSpeak(game, ap, first, dir, update, (int)SpeakConstant.SheriffRecommendVote, (int)HintConstant.RoundTable, null, lastPlayer);
            }
            // Sheriff recommend vote
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == (int)SpeakConstant.SheriffRecommendVote)
            {
                var sheriff = Game.GetGameDictionaryProperty(game, dictCurrentSheriff, 0);
                if (sheriff != 0)
                {
                    var sheriffArray = new List<int>();
                    sheriffArray.Add(sheriff);

                    if (UserAction.EndUserAction(game, update))
                    {
                        (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, sheriffArray, update);
                        if (inputValid)
                        {
                            if (input.ContainsKey(sheriff.ToString()))
                            {
                                var targets = (List<int>)input[sheriff.ToString()];
                                update[dictSheriffVote] = targets;
                            }
                        }
                        update[dictSpeak] = (int)SpeakConstant.Vote1;
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        var alivePlayersNow = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
                        if (UserAction.StartUserAction(game, actionDuraionPlayerReact, update))
                        {
                            update[UserAction.dictUserActionTargets] = alivePlayersNow;
                            update[UserAction.dictUserActionUsers] = sheriffArray;
                            update[UserAction.dictUserActionTargetsCount] = -1;
                            update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.SheriffRecommendVote;
                            return GameActionResult.Restart;
                        }
                        else
                        {
                            (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, sheriffArray, update);
                            if (inputValid)
                            {
                                if (input.ContainsKey(sheriff.ToString()))
                                {
                                    var targets = (List<int>)input[sheriff.ToString()];
                                    update[dictSheriffVote] = targets;
                                    update[dictSpeak] = (int)SpeakConstant.Vote1;
                                    UserAction.EndUserAction(game, update, true);
                                    return GameActionResult.Restart;
                                }
                            }
                        }
                    }
                    return GameActionResult.NotExecuted;
                }
                else
                {
                    update[dictSpeak] = (int)SpeakConstant.Vote1;
                    return GameActionResult.Restart;
                }
            }
            // vote 1
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == (int)SpeakConstant.Vote1)
            {
                if (UserAction.EndUserAction(game, update))
                {
                    (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, allPlayers, update);
                    if (inputValid)
                    {
                        var sheriffTargets = Game.GetGameDictionaryProperty(game, dictSheriffVote, new List<int>());
                        int weighted = -1;
                        if (sheriffTargets.Count == 1)
                        {
                            weighted = Game.GetGameDictionaryProperty(game, dictCurrentSheriff, 0);
                        }
                        var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, weighted);
                        update[dictVoteOut] = targets;
                        update[dictVoteInfo] = FormatVoteTally(input);
                        update[dictSpeak] = (int)SpeakConstant.Vote1Result; // go to vote result display
                    }
                    else
                    {
                        update[dictSpeak] = (int)SpeakConstant.EndOfDay;
                    }
                    return GameActionResult.Restart;
                }
                else
                {
                    var alivePlayersNow = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
                    if (UserAction.StartUserAction(game, actionDuraionPlayerReact, update))
                    {
                        update[UserAction.dictUserActionTargets] = alivePlayersNow;
                        update[UserAction.dictUserActionUsers] = alivePlayersNow;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.VoteOut;
                        return GameActionResult.Restart;
                    }
                }
                return GameActionResult.NotExecuted;
            }
            // vote 1 result announcement
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == (int)SpeakConstant.Vote1Result)
            {
                var voteOut = Game.GetGameDictionaryProperty(game, dictVoteOut, new List<int>());

                if (UserAction.EndUserAction(game, update))
                {
                    // Route based on vote result
                    if (voteOut.Count > 1)
                    {
                        // Tie - go to voteout speech
                        update[dictSpeak] = (int)SpeakConstant.VoteoutSpeech;
                    }
                    else if (voteOut.Count == 1)
                    {
                        // Single target - go to voted out
                        update[dictSpeak] = (int)SpeakConstant.VotedOut;
                    }
                    else
                    {
                        update[dictSpeak] = (int)SpeakConstant.EndOfDay;
                    }
                    return GameActionResult.Restart;
                }
                else
                {
                    // Display vote result
                    if (UserAction.StartUserAction(game, 5, update))
                    {
                        update[UserAction.dictUserActionTargets] = new List<int>();
                        update[UserAction.dictUserActionUsers] = new List<int>() { -1 };
                        update[UserAction.dictUserActionTargetsCount] = 0;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.VoteResult;
                        update[UserAction.dictUserActionInfo] = Game.GetGameDictionaryProperty(game, dictVoteInfo, "");
                        return GameActionResult.Restart;
                    }
                }
                return GameActionResult.NotExecuted;
            }
            // voteout speech
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == (int)SpeakConstant.VoteoutSpeech)
            {
                var pkPlayers = Game.GetGameDictionaryProperty(game, dictVoteOut, new List<int>());
                var sheriffPlayers = Game.GetGameDictionaryProperty(game, dictSheriff, new List<int>());
                var first = sheriffPlayers.Count == 0 ? 1 : sheriffPlayers[game.GetRandomNumber() % sheriffPlayers.Count];
                bool directionPlus = (game.GetRandomNumber() % 2) == 1;
                var nextPlayer = NextPlayer(pkPlayers, allPlayers.Count, first, directionPlus);
                return HandleRoundTableSpeak(game, pkPlayers, nextPlayer, directionPlus, update, (int)SpeakConstant.Vote2, (int)HintConstant.RoundTable);
            }
            // vote 2
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == (int)SpeakConstant.Vote2)
            {
                if (UserAction.EndUserAction(game, update))
                {
                    (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, allPlayers, update);
                    if (inputValid)
                    {
                        var sheriffTargets = Game.GetGameDictionaryProperty(game, dictSheriffVote, new List<int>());
                        int weighted = -1;
                        if (sheriffTargets.Count == 1)
                        {
                            weighted = Game.GetGameDictionaryProperty(game, dictCurrentSheriff, 0);
                        }
                        var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, weighted);
                        update[dictVoteOut] = targets;
                        update[dictVoteInfo] = FormatVoteTally(input);
                        update[dictSpeak] = (int)SpeakConstant.Vote2Result; // go to vote result display
                    }
                    else
                    {
                        update[dictSpeak] = (int)SpeakConstant.EndOfDay;
                    }
                    return GameActionResult.Restart;
                }
                else
                {
                    var alivePlayersNow = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
                    var voteout = Game.GetGameDictionaryProperty(game, dictVoteOut, new List<int>());
                    alivePlayersNow.RemoveAll(x => voteout.Contains(x));

                    if (UserAction.StartUserAction(game, actionDuraionPlayerReact, update))
                    {
                        update[UserAction.dictUserActionTargets] = voteout;
                        update[UserAction.dictUserActionUsers] = alivePlayersNow;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.VoteOut;
                        return GameActionResult.Restart;
                    }
                }
                return GameActionResult.NotExecuted;
            }
            // vote 2 result announcement
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == (int)SpeakConstant.Vote2Result)
            {
                var voteOut = Game.GetGameDictionaryProperty(game, dictVoteOut, new List<int>());

                if (UserAction.EndUserAction(game, update))
                {
                    // Route based on vote result
                    if (voteOut.Count > 1)
                    {
                        // Tie
                        update[dictSpeak] = (int)SpeakConstant.EndOfDay;
                    }
                    else if (voteOut.Count == 1)
                    {
                        // Single target - go to voted out
                        update[dictSpeak] = (int)SpeakConstant.VotedOut;
                    }
                    else
                    {
                        update[dictSpeak] = (int)SpeakConstant.EndOfDay;
                    }
                    return GameActionResult.Restart;
                }
                else
                {
                    // Display vote 2 result
                    if (UserAction.StartUserAction(game, 5, update))
                    {
                        update[UserAction.dictUserActionTargets] = new List<int>();
                        update[UserAction.dictUserActionUsers] = new List<int>() { -1 };
                        update[UserAction.dictUserActionTargetsCount] = 0;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.VoteResult;
                        update[UserAction.dictUserActionInfo] = Game.GetGameDictionaryProperty(game, dictVoteInfo, "");
                        return GameActionResult.Restart;
                    }
                }
                return GameActionResult.NotExecuted;
            }
            // Owner vote select - game owner manually chooses who to vote out
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == (int)SpeakConstant.OwnerVoteSelect)
            {
                // Get the game owner player ID
                var gameOwner = Game.GetGameDictionaryProperty(game, dictGameOwner, 0);
                var ownerList = gameOwner > 0 ? new List<int> { gameOwner } : new List<int>();

                // Owner selects who to vote out
                if (UserAction.EndUserAction(game, update))
                {
                    // No one selected, skip to end of day
                    update[dictSpeak] = (int)SpeakConstant.EndOfDay;
                    update[dictOwnerVoteOverride] = 1;
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, 999, update))
                    {
                        // Show all alive players as selectable targets (plus 0 for skip)
                        var selectableTargets = new List<int>(alivePlayers);
                        selectableTargets.Insert(0, 0); // Add 0 at the beginning for "skip/no one"
                        update[UserAction.dictUserActionTargets] = selectableTargets;
                        update[UserAction.dictUserActionUsers] = ownerList;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.OwnerVoteSelect;
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        // Check for early completion
                        (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, ownerList, update);
                        if (inputValid)
                        {
                            foreach (var entry in input)
                            {
                                var targets = (List<int>)entry.Value;
                                if (targets.Count > 0 && targets[0] > 0)
                                {
                                    // Mark the selected player as about to die
                                    MarkPlayerAboutToDie(game, targets[0], update);

                                    // Set up interrupt to go through death handling then end of day
                                    var interrupted = new Dictionary<string, object>();
                                    interrupted[dictSpeak] = (int)SpeakConstant.EndOfDay;
                                    update[dictSpeak] = (int)SpeakConstant.DeathHandlingInterrupt;
                                    update[dictInterrupt] = interrupted;
                                    update[dictVoteInfo] = $"Owner selected: {targets[0]}";
                                    update[dictOwnerVoteOverride] = 1;
                                    UserAction.EndUserAction(game, update, true);
                                    return GameActionResult.Restart;
                                }
                                if (targets.Count > 0 && targets[0] == 0)
                                {
                                    // Owner selected no one (0 = skip)
                                    update[dictSpeak] = (int)SpeakConstant.EndOfDay;
                                    update[dictOwnerVoteOverride] = 1;
                                    UserAction.EndUserAction(game, update, true);
                                    return GameActionResult.Restart;
                                }
                            }
                        }
                    }
                }
                return GameActionResult.NotExecuted;
            }
            // voted out
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == (int)SpeakConstant.VotedOut)
            {
                var voteout = Game.GetGameDictionaryProperty(game, dictVoteOut, new List<int>());

                if (voteout.Count == 1)
                {
                    // Mark the voteout player as about to die
                    foreach (var player in voteout)
                    {
                        MarkPlayerAboutToDie(game, player, update);
                    }

                    // Set up interrupt to go through death handling
                    var interrupted = new Dictionary<string, object>();
                    interrupted[dictSpeak] = (int)SpeakConstant.EndOfDay;
                    update[dictSpeak] = (int)SpeakConstant.DeathHandlingInterrupt;
                    update[dictInterrupt] = interrupted;
                }
                else
                {
                    update[dictSpeak] = (int)SpeakConstant.EndOfDay;
                }
                return GameActionResult.Restart;
            }

            // speak=40: End of day - check win conditions then advance
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == (int)SpeakConstant.EndOfDay)
            {
                var winner = CheckWinCondition(game);
                if (winner != null)
                {
                    update[dictGameWinner] = winner;
                    // Game over - display winner
                    if (UserAction.EndUserAction(game, update))
                    {
                        // Game ends here
                        return GameActionResult.GameOver;
                    }
                    else
                    {
                        if (UserAction.StartUserAction(game, 10, update))
                        {
                            update[UserAction.dictUserActionTargets] = new List<int>();
                            update[UserAction.dictUserActionUsers] = new List<int>() { -1 };
                            update[UserAction.dictUserActionTargetsCount] = 0;
                            update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.GameOver;
                            update[UserAction.dictUserActionInfo] = winner;
                            return GameActionResult.Restart;
                        }
                    }
                    return GameActionResult.NotExecuted;
                }
                else
                {
                    // No winner yet, advance to night
                    update[dictSpeak] = (int)SpeakConstant.SheriffVolunteer;
                    AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
            }

            // Death handling interrupt - consolidates all death-related logic
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == (int)SpeakConstant.DeathHandlingInterrupt)
            {
                // Kill the dead players
                KillDeadPlayers(game, update);

                // Move to dead player skills processing
                update[dictSpeak] = (int)SpeakConstant.DeadPlayerSkillsProcessing;
                return GameActionResult.Restart;
            }

            // Dead player skills processing
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == (int)SpeakConstant.DeadPlayerSkillsProcessing)
            {
                return HandleDeadPlayerSkills(game, update);
            }

            // Dead player processing: handle sheriff handover
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == (int)SpeakConstant.DeadPlayerSheriffHandover)
            {
                return HandleDeadPlayerProcessing(game, update);
            }

            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == (int)SpeakConstant.DeadPlayerSpeak)
            {
                var dp = Game.GetGameDictionaryProperty(game, dictDeadPlayerAction, new List<int>());
                var lastAction = (int)Game.GetGameDictionaryProperty(game, dictInterrupt, new Dictionary<string, object>())[dictSpeak];
                if (dp.Count > 0 && (lastAction == (int)SpeakConstant.EndOfDay || Game.GetGameDictionaryProperty(game, dictDay, 0) == 0))
                {
                    return HandleRoundTableSpeak(game, dp, dp[game.GetRandomNumber() % dp.Count], game.GetRandomNumber() % 2 == 1, update, -1, 102);
                }
                else
                {
                    var interrupted = Game.GetGameDictionaryProperty(game, dictInterrupt, new Dictionary<string, object>());
                    foreach (var item in interrupted)
                    {
                        update[item.Key] = item.Value;
                    }
                    return GameActionResult.Restart;
                }
            }

            // MengMianRen death announcement (speak=101)
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == (int)SpeakConstant.MengMianRenDeath)
            {
                var mengMianRenDeadPlayer = Game.GetGameDictionaryProperty(game, MengMianRen.dictMengMianRenDeadPlayer, 0);

                if (UserAction.EndUserAction(game, update))
                {
                    var winner = CheckWinCondition(game);
                    if (winner != null)
                    {
                        update[dictGameWinner] = winner;
                        // Game over - display winner
                        // Game ends here
                        return GameActionResult.GameOver;
                    }
                    // Return to interrupted task
                    var interrupted = Game.GetGameDictionaryProperty(game, dictInterrupt, new Dictionary<string, object>());
                    foreach (var item in interrupted)
                    {
                        update[item.Key] = item.Value;
                    }
                    update[MengMianRen.dictMengMianRenDeadPlayer] = null;
                    return GameActionResult.Restart;
                }
                else
                {
                    // Broadcast MengMianRen death announcement
                    if (UserAction.StartUserAction(game, 5, update))
                    {
                        update[UserAction.dictUserActionTargets] = new List<int>();
                        update[UserAction.dictUserActionUsers] = new List<int> { -1 }; // Announcement
                        update[UserAction.dictUserActionTargetsCount] = 0;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.MengMianRen_Death;
                        update[UserAction.dictUserActionInfo] = mengMianRenDeadPlayer.ToString();
                        return GameActionResult.Restart;
                    }
                }
                return GameActionResult.NotExecuted;
            }

            return GameActionResult.NotExecuted;
        }

        public static int NextPlayer(List<int> players, int totalPlayers, int startingPlayer, bool directionPlus)
        {
            int next = startingPlayer;
            do
            {
                if (directionPlus)
                {
                    next++;
                    if (next > totalPlayers)
                    {
                        next = 0;
                    }
                }
                else
                {
                    next--;
                    if (next <= 0)
                    {
                        next = totalPlayers;
                    }
                }
            } while (!players.Contains(next));
            return next;
        }

        private static (bool, GameActionResult) RestoreInterrupted(Game game, List<int> speakers, int nextSpeak, Dictionary<string, object> update)
        {
            if (speakers.Count != speakers.Distinct().Count())
            {
                update[dictSpeaker] = null;
                if (nextSpeak > 0)
                {

                    update[dictSpeak] = nextSpeak;
                }
                else
                {
                    var interrupted = Game.GetGameDictionaryProperty(game, dictInterrupt, new Dictionary<string, object>());
                    foreach (var item in interrupted)
                    {
                        update[item.Key] = item.Value;
                    }
                }
                return (true, GameActionResult.Restart);
            }
            return (false, GameActionResult.NotExecuted);
        }

        public static GameActionResult HandleRoundTableSpeak(Game game, List<int> players, int startingPlayer, bool directionPlus, Dictionary<string, object> update, int nextSpeak, int hint = 102, string? userinfo = null, int lastPlayer = -1)
        {
            var speakers = Game.GetGameDictionaryProperty(game, dictSpeaker, new List<int>());
            var allPlayers = LangRenSha.GetPlayers(game, x => true);
            var counterClockWise = Game.GetGameDictionaryProperty(game, dictSeatCounterClockwise, 1);

            if (counterClockWise == 0)
            {
                directionPlus = !directionPlus;
            }

            var (handled, intResult) = RestoreInterrupted(game, speakers, nextSpeak, update);
            if (handled)
            {
                UserAction.EndUserAction(game, update, true);
                return intResult;
            }

            if (UserAction.EndUserAction(game, update))
            {
                // Speaking time ended - call AfterSpeak handlers for the current speaker
                var currentSpeaker = speakers.Count > 0 ? speakers.Last() : 0;
                if (currentSpeaker > 0)
                {
                    foreach (var handler in AfterSpeakHandlers)
                    {
                        var (handled2, result) = handler(game, currentSpeaker, update);
                        if (handled2)
                        {
                            return result;
                        }
                    }
                }
                return GameActionResult.Restart;
            }
            else
            {
                var actionDuration = Game.GetGameDictionaryProperty(game, LangRenSha.dictDurationSpeech, actionDurationPlayerSpeak);

                if (UserAction.StartUserAction(game, actionDuration, update))
                {
                    var last = speakers.Count == 0 ? startingPlayer : speakers.Last();
                    var first = speakers.Count == 0 ? startingPlayer : speakers.First();
                    var next = last;

                    // Determine which players still need to speak (excluding those who already spoke)
                    var remainingPlayers = players.Where(p => !speakers.Contains(p)).ToList();

                    // Check if only lastPlayer remains to speak
                    bool onlyLastPlayerRemains = lastPlayer > 0 && 
                                                  remainingPlayers.Count == 1 && 
                                                  remainingPlayers.Contains(lastPlayer);

                    // Circular logic to find the next player
                    do
                    {
                        if (directionPlus)
                        {
                            next++;
                            if (next > allPlayers.Count)
                            {
                                next = 1;
                            }
                        }
                        else
                        {
                            next--;
                            if (next <= 0)
                            {
                                next = allPlayers.Count;
                            }
                        }

                        if (next == lastPlayer && !onlyLastPlayerRemains)
                        {
                            continue; // Skip lastPlayer for now if they are not the only one remaining
                        }

                        // Check if this player should speak
                        if (players.Contains(next))
                        {
                            break;
                        }
                    } while (next != last);

                    if (speakers.Contains(next) && onlyLastPlayerRemains)
                    {
                        speakers.Add(lastPlayer);
                    }
                    else
                    {
                        speakers.Add(next);
                    }
                    var (h, ir) = RestoreInterrupted(game, speakers, nextSpeak, update);
                    if (h)
                    {
                        UserAction.EndUserAction(game, update, true);
                        return ir;
                    }

                    update[dictSpeaker] = speakers;
                    update[UserAction.dictUserActionTargets] = new List<int> { -1, 0 };
                    update[UserAction.dictUserActionUsers] = allPlayers;
                    update[UserAction.dictUserActionTargetsCount] = 1;
                    update[UserAction.dictUserActionTargetsHint] = hint;
                    if (userinfo != null)
                    {
                        update[UserAction.dictUserActionInfo] = userinfo;
                    }
                    return GameActionResult.Restart;
                }
                else
                {
                    (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, allPlayers, update);
                    if (inputValid)
                    {
                        foreach (var entry in input)
                        {
                            var key = int.Parse(entry.Key);
                            var value = (List<int>)entry.Value;
                            if (key > 0 && value.Count > 0 && value[0] == -1)
                            {
                                // Player finished speaking - call AfterSpeak handlers
                                var currentSpeaker = speakers.Count > 0 ? speakers.Last() : 0;
                                if (currentSpeaker > 0)
                                {
                                    foreach (var handler in AfterSpeakHandlers)
                                    {
                                        var (handled2, result) = handler(game, currentSpeaker, update);
                                        if (handled2)
                                        {
                                            UserAction.EndUserAction(game, update, true);
                                            return result;
                                        }
                                    }
                                }
                                UserAction.EndUserAction(game, update, true);
                                return GameActionResult.Restart;
                            }
                            if (key > 0 && value.Count > 0 && value[0] == 0)
                            {
                                UserAction.PauseUnpause(game, update);
                                return GameActionResult.Restart;
                            }
                        }
                        foreach (var int_input in input_others)
                        {
                            var key = int.Parse(int_input.Key);
                            var value = (List<int>)int_input.Value;
                            foreach (var handler in LangRenSha.InterruptHandlers)
                            {
                                var result = handler(game, key, value, update);
                                if (result == GameActionResult.NotExecuted)
                                {
                                    continue;
                                }
                                return result;
                            }

                        }
                    }

                }
            }

            return GameActionResult.NotExecuted;
        }

        /// <summary>
        /// Checks win conditions by iterating through all alive players and their factions.
        /// Returns the winning faction name if a win condition is met, null otherwise.
        /// Win conditions checked in order: Evil, God, Civilian
        /// </summary>
        public static string? CheckWinCondition(Game game)
        {
            var alivePlayers = GetPlayers(game, x => (int)x[dictAlive] == 1);
            var day = Game.GetGameDictionaryProperty(game, dictDay, 0);

            if (day >= 8)
            {
                return "evil";
            }

            bool hasEvil = false;
            bool hasGod = false;
            bool hasCivilian = false;
            bool hasThirdParty = false;

            foreach (var player in alivePlayers)
            {
                var faction = GetPlayerProperty(game, player, dictPlayerFaction, (int)PlayerFaction.Civilian);

                if ((faction & (int)PlayerFaction.Evil) != 0)
                    hasEvil = true;
                else if ((faction & (int)PlayerFaction.God) != 0)
                    hasGod = true;
                else if ((faction & (int)PlayerFaction.Civilian) != 0)
                    hasCivilian = true;
                else if ((faction & (int)PlayerFaction.ThirdParty) != 0)
                    hasThirdParty = true;
            }

            if (!hasCivilian)
            {
                return "evil";
            }
            if (!hasGod)
            {
                return "evil";
            }
            if (!hasEvil && !hasThirdParty)
            {
                return "good";
            }

            return null; // No winner yet
        }

        public static void AdvanceAction(Game game, Dictionary<string, object> update)
        {
            if (Game.GetGameDictionaryProperty(game, dictPhase, 0) == 1)
            {
                update[dictPhase] = 0;
                update[dictDay] = Game.GetGameDictionaryProperty(game, dictDay, 0) + 1;
            }
            else
            {
                var current = Game.GetGameDictionaryProperty(game, dictAction, 0);
                var nightOrders = GetNightOrders(game);
                int next = nightOrders.Where(n => n > current).OrderBy(n => n).FirstOrDefault();
                if (next == 0)
                {
                    update[dictAction] = 0;
                    if (Game.GetGameDictionaryProperty(game, dictPhase, 0) == 0)
                    {
                        update[dictPhase] = 1;
                        // dictSpeak will be set to 0 by HandleSpeaker for sheriff volunteer
                    }
                }
                else
                {
                    update[dictAction] = next;
                }
            }

        }
        public static List<int> GetNightOrders(Game game)
        {
            return Game.GetGameDictionaryProperty(game, dictNightOrders, new List<int>());
        }

        public static List<int> GetPlayers(Game game, Func<Dictionary<string, object>, bool> conditional)
        {
            var ret = new List<int>();
            var players = Game.GetGameDictionaryProperty(game, dictPlayers, new Dictionary<string, object>());
            foreach (var player in players)
            {
                if (conditional((Dictionary<string, object>)players[player.Key.ToString()]))
                {
                    ret.Add(int.Parse(player.Key));
                }
            }
            return ret;
        }

        public static void KillDeadPlayers(Game game, Dictionary<string, object> update)
        {
            var aboutToDie = Game.GetGameDictionaryProperty(game, dictAboutToDie, new List<int>());
            var players = Game.GetGameDictionaryProperty(game, dictPlayers, new Dictionary<string, object>());
            foreach (var player in aboutToDie)
            {
                ((Dictionary<string, object>)players[player.ToString()])[dictAlive] = 0;
            }
            if (players.Count > 0)
            {
                update[dictPlayers] = players;
            }
            update[LangRen.dictAttackTarget] = null;
            update[dictDeadPlayerAction] = aboutToDie;
            update[dictAboutToDie] = null;
        }

        public static bool MarkPlayerAboutToDie(Game game, int target, Dictionary<string, object> update)
        {
            var aboutToDie = Game.GetGameDictionaryProperty(game, dictAboutToDie, new List<int>());
            if (aboutToDie.Contains(target))
            {
                return false;
            }

            aboutToDie.Add(target);
            update[dictAboutToDie] = aboutToDie;
            return true;
        }

        public static void ChainKill(Game game, int source, int target, List<int> currentAboutToDie, Dictionary<string, object> update)
        {
            var miceTag = Game.GetGameDictionaryProperty(game, LaoShu.dictMiceTag, 0);
            var laoShu = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == "LaoShu");
            var laoShuPlayer = laoShu.Count > 0 ? laoShu[0] : -1;
            var sheMengRenTarget = Game.GetGameDictionaryProperty(game, SheMengRen.dictSheMengTarget, 0);
            var xiongAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == "Xiong" && (int)x[LangRenSha.dictAlive] == 1);
            var xiongPlayer = xiongAlive.Count > 0 ? xiongAlive[0] : 0;
            var xiongLinkPlayer = xiongPlayer > 0 ? LangRenSha.GetPlayerProperty(game, xiongPlayer, Xiong.dictXiongLink, 0) : 0;
            var lmrAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == "LangMeiRen" && (int)x[LangRenSha.dictAlive] == 1);
            var lmrPlayer = lmrAlive.Count > 0 ? lmrAlive[0] : 0;
            var lmrLinkPlayer = lmrPlayer > 0 ? LangRenSha.GetPlayerProperty(game, lmrPlayer, LangMeiRen.dictLangMeiRenLink, 0) : 0;
            var sheMengRenAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == "SheMengRen" && (int)x[LangRenSha.dictAlive] == 1);
            var sheMengRenPlayer = sheMengRenAlive.Count > 0 ? sheMengRenAlive[0] : 0;

            if (currentAboutToDie.Contains(target))
            {
                return;
            }

            if (sheMengRenTarget == target && source != sheMengRenPlayer)
            {
                return;
            }

            if (target == laoShuPlayer && laoShuPlayer != miceTag)
            {
                return;
            }

            currentAboutToDie.Add(target);

            if (target == sheMengRenPlayer)
            {
                LangRenSha.SetPlayerProperty(game, sheMengRenTarget, LieRen.dictHuntingDisabled, 1, update);
                ChainKill(game, target, sheMengRenTarget, currentAboutToDie, update);
            }

            if (target == miceTag)
            {
                ChainKill(game, target, laoShuPlayer, currentAboutToDie, update);
            }

            if (target == xiongPlayer && xiongLinkPlayer > 0)
            {
                LangRenSha.SetPlayerProperty(game, xiongLinkPlayer, LieRen.dictHuntingDisabled, 1, update);
                ChainKill(game, target, xiongLinkPlayer, currentAboutToDie, update);
            }

            if (target == lmrPlayer && lmrLinkPlayer > 0)
            {
                LangRenSha.SetPlayerProperty(game, lmrLinkPlayer, LieRen.dictHuntingDisabled, 1, update);
                ChainKill(game, target, lmrLinkPlayer, currentAboutToDie, update);
            }

        }

        public static T GetPlayerProperty<T>(Game game, int player, string key, T defaultValue)
        {
            var players = Game.GetGameDictionaryProperty(game, dictPlayers, new Dictionary<string, object>());
            if (!((Dictionary<string, object>)players[player.ToString()]).ContainsKey(key))
            {
                return defaultValue;
            }
            return (T)((Dictionary<string, object>)players[player.ToString()])[key];
        }

        public static void SetPlayerProperty<T>(Game game, int player, string key, T value, Dictionary<string, object> update)
        {
            var players = Game.GetGameDictionaryProperty(game, dictPlayers, new Dictionary<string, object>());

            ((Dictionary<string, object>)players[player.ToString()])[key] = value;

            update[LangRenSha.dictPlayers] = players;
        }

        public static string FormatVoteTally(Dictionary<string, object> input)
        {
            var tally = new Dictionary<int, List<int>>();
            foreach (var entry in input)
            {
                var p = int.Parse(entry.Key);
                var tars = (List<int>)entry.Value;
                var tt = tars[0];
                if (!tally.ContainsKey(tt))
                {
                    tally[tt] = new List<int>();
                }
                tally[tt].Add(p);
            }
            var tally1 = tally.OrderByDescending(pair => pair.Value.Count);
            string info = "";
            foreach (var entry1 in tally)
            {
                var p = entry1.Key;
                var voters = entry1.Value;
                if (info.Length > 0)
                {
                    info += "| ";
                }
                info += $"{string.Join(", ", voters)} > {p}";
            }
            return info;
        }

        public static GameActionResult AnnouncerAction(Game game, Dictionary<string, object> update, bool skipDay0, int announcerActionIdIn, int announcerActionIdOut, int announcerHintIn, int announcerHintOut, string announcerInfo, int announcerDelay)
        {
            var action = Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0);
            if (action == announcerActionIdIn || action == announcerActionIdOut)
            {
                if ((skipDay0 && Game.GetGameDictionaryProperty(game, LangRenSha.dictDay, 0) == 0) || UserAction.EndUserAction(game, update))
                {
                    update[UserAction.dictUserActionUsers] = new List<int>();
                    update[UserAction.dictUserActionInfo] = null;
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, announcerDelay, update))
                    {
                        update[UserAction.dictUserActionTargets] = new List<int>();
                        update[UserAction.dictUserActionUsers] = new List<int>() { -1 };
                        update[UserAction.dictUserActionTargetsCount] = 0;
                        update[UserAction.dictUserActionTargetsHint] = action == announcerActionIdIn ? announcerHintIn : announcerHintOut;
                        update[UserAction.dictUserActionInfo] = announcerInfo;
                        return GameActionResult.Restart;
                    }
                }
                return GameActionResult.NotExecuted;
            }
            return GameActionResult.NotExecuted;
        }

        public static GameActionResult HandleDeadPlayerProcessing(Game game, Dictionary<string, object> update)
        {
            var deadPlayers = Game.GetGameDictionaryProperty(game, dictDeadPlayerAction, new List<int>());
            var allPlayers = GetPlayers(game, x => true);
            var alivePlayers = GetPlayers(game, x => (int)x[dictAlive] == 1);
            var currentSheriff = Game.GetGameDictionaryProperty(game, dictCurrentSheriff, 0);
            var playersDict = Game.GetGameDictionaryProperty(game, dictPlayers, new Dictionary<string, object>());

            // Phase 1: Handle sheriff handover if current sheriff is dead
            if (currentSheriff != 0 && deadPlayers.Contains(currentSheriff))
            {
                // Ask current sheriff (who is dead) to select handover target
                if (UserAction.EndUserAction(game, update))
                {
                    update[dictCurrentSheriff] = 0;
                    update[dictSpeak] = 100;
                    return GameActionResult.Restart;
                }
                else
                {
                    // Ask current sheriff to select who to hand over to
                    if (UserAction.StartUserAction(game, actionDuraionPlayerReact, update))
                    {
                        var handoverTargets = new List<int>();
                        handoverTargets.AddRange(alivePlayers);
                        handoverTargets.Remove(currentSheriff);

                        update[UserAction.dictUserActionTargets] = handoverTargets;
                        update[UserAction.dictUserActionUsers] = new List<int> { currentSheriff };
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.SheriffHandover;
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, new List<int> { currentSheriff }, update);
                        if (inputValid && input.ContainsKey(currentSheriff.ToString()))
                        {
                            var targets = (List<int>)input[currentSheriff.ToString()];
                            if (targets.Count > 0)
                            {
                                update[dictCurrentSheriff] = targets[0];
                                update[dictSpeak] = 100;
                                UserAction.EndUserAction(game, update, true);
                                return GameActionResult.Restart;
                            }
                        }
                    }
                    return GameActionResult.NotExecuted;
                }
            }
            else
            {
                // Current sheriff is not dead, proceed to speak
                update[dictSpeak] = 100;
                return GameActionResult.Restart;
            }
        }

        public static GameActionResult HandleDeadPlayerSkills(Game game, Dictionary<string, object> update)
        {
            var deadPlayers = Game.GetGameDictionaryProperty(game, dictDeadPlayerAction, new List<int>());
            var skillsProcessed = Game.GetGameDictionaryProperty(game, dictDeadSkillsProcessed, new List<int>());

            // Find first dead player with unprocessed death skill
            var deadPlayerWithSkill = -1;
            foreach (var deadPlayer in deadPlayers)
            {
                if (skillsProcessed.Contains(deadPlayer))
                    continue;

                deadPlayerWithSkill = deadPlayer;
                break;
            }

            // If there's a dead player to process, ask handlers if they want to handle it
            if (deadPlayerWithSkill != -1)
            {

                // Try each handler to see if it wants to handle this dead player's skill
                foreach (var handler in deadPlayerHandlers)
                {
                    var (handled, result) = handler(game, deadPlayerWithSkill, update);
                    if (handled)
                        return result;

                }

                // Mark this player's skill as processed
                skillsProcessed.Add(deadPlayerWithSkill);
                update[dictDeadSkillsProcessed] = skillsProcessed;

                // Continue checking other dead players
                return GameActionResult.Restart;
            }
            else
            {
                // All dead player skills have been processed, clear the tracking and move on
                update[dictDeadSkillsProcessed] = null;
                update[dictSpeak] = 99;
                return GameActionResult.Restart;
            }
        }
    }
}
