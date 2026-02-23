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
            { LangRen.RevealSelf, LangRenSha.WithdrawSheriff };
        public static List<Func<Game, int, List<int>, Dictionary<string, object>, GameActionResult>> InterruptHandlers
        {
            get
            {
                return interruptHandlers;
            }
        }

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
        public LangRenSha()
        {
            players = new List<(int, Role)>();
            RegisterDeadPlayerHandler(LieRen.HandleHunterDeathSkill);
            // Players will be initialized dynamically in GenerateStateDiff based on roleDict
        }

        private Role CreateRoleInstance(string roleName)
        {
            return roleName switch
            {
                "LangRen" => new LangRen(),
                "YuYanJia" => new YuYanJia(),
                "NvWu" => new NvWu(),
                "WuZhe" => new WuZhe(),
                "JiaMian" => new JiaMian(),
                "LieRen" => new LieRen(),
                "PingMin" => new PingMin(),
                "BaiChi" => new BaiChi(),
                "DaMao" => new DaMao(),
                "LaoShu" => new LaoShu(),
                _ => throw new ArgumentException($"Unknown role: {roleName}")
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

        public static string dictDurationLangRen = "duration_langren";
        public static string dictDurationSpeech = "duration_speech";
        public static string dictDurationPlayerReact = "duration_player_react";

        public int Version
        {
            get
            {
                return 1;
            }
        }

        public static int actionDuraionPlayerReact = 6;
        public static int actionDurationPlayerSpeak = 5;

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
                no.AddRange([1, 2, 3, 4, 1000]);
                update[LangRenSha.dictNightOrders] = no;
                return GameActionResult.Continue;
            }
            // Game begin announcement
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == 1)
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
                        update[UserAction.dictUserActionTargetsHint] = 1000;
                        return GameActionResult.Restart;
                    }
                }
            }

            // Role check
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == 2)
            {
                if (Game.GetGameDictionaryProperty(game, LangRenSha.dictDay, 0) != 0 || UserAction.EndUserAction(game, update))
                {
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, 4, update))
                    {
                        var allPlayers = LangRenSha.GetPlayers(game, x => true);
                        update[UserAction.dictUserActionTargets] = new List<int>();
                        update[UserAction.dictUserActionUsers] = allPlayers;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = 1000;
                        return GameActionResult.Restart;
                    }
                }
            }

            // Put down device announcement
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == 3)
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
                        update[UserAction.dictUserActionTargetsHint] = 1020;
                        return GameActionResult.Restart;
                    }
                }
            }

            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == 4)
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
                        update[UserAction.dictUserActionTargetsHint] = 1001;
                        return GameActionResult.Restart;
                    }
                }
            }
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == 1000)
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
                        update[UserAction.dictUserActionTargetsHint] = 1002;
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
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictSpeak, 0) == 1)
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

        public static GameActionResult HandleSpeaker(Game game, Dictionary<string, object> update)
        {
            var allPlayers = LangRenSha.GetPlayers(game, x => true);
            var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);

            // Sheriff volunteer
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 0)
            {

                if (Game.GetGameDictionaryProperty(game, dictDay, 0) == 0)
                {
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
                        if (sheriffPlayers.Count == allPlayers.Count || sheriffPlayers.Count == 0)
                        {
                            update[dictSpeak] = 9;
                        }
                        else if (sheriffPlayers.Count == 1)
                        {
                            update[dictSpeak] = 9;
                            update[dictCurrentSheriff] = sheriffPlayers[0];
                        }
                        else
                        {
                            update[dictSpeak] = 1;
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
                            update[UserAction.dictUserActionTargetsHint] = 100;
                            return GameActionResult.Restart;
                        }
                    }
                    return GameActionResult.NotExecuted;
                }
                else
                {
                    update[dictSpeak] = 9;
                    return GameActionResult.Restart;
                }

            }
            // Sheriff speech
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 1)
            {
                var sheriffPlayers = Game.GetGameDictionaryProperty(game, dictSheriff, new List<int>());
                var volunteersInfo = string.Join(", ", sheriffPlayers);
                return HandleRoundTableSpeak(game, sheriffPlayers, sheriffPlayers[game.GetRandomNumber() % sheriffPlayers.Count], (game.GetRandomNumber() % 2) == 1, update, 2, 104, volunteersInfo);
            }
            // Sheriff vote - tally only
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 2)
            {
                var sheriffPlayers = Game.GetGameDictionaryProperty(game, dictSheriff, new List<int>());
                var votePlayers = new List<int>();
                votePlayers.AddRange(allPlayers);
                votePlayers.RemoveAll(x => sheriffPlayers.Contains(x));

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
                    update[dictSpeak] = 3;
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, actionDuraionPlayerReact, update))
                    {
                        update[UserAction.dictUserActionTargets] = sheriffPlayers;
                        update[UserAction.dictUserActionUsers] = votePlayers;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = 103;
                        return GameActionResult.Restart;
                    }
                }
                return GameActionResult.NotExecuted;
            }
            // Sheriff vote - display result
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 3)
            {
                var voteOut = Game.GetGameDictionaryProperty(game, dictVoteOut, new List<int>());

                if (UserAction.EndUserAction(game, update))
                {
                    // Route based on vote result
                    if (voteOut.Count > 1)
                    {
                        // Tie - go to PK
                        update[dictSpeak] = 4;
                        update[dictPk] = voteOut;
                    }
                    else if (voteOut.Count == 1)
                    {
                        // Single target - go to death announcement
                        update[dictSpeak] = 9;
                        update[dictCurrentSheriff] = voteOut[0];
                    }
                    else
                    {
                        update[dictSpeak] = 9;
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
                        update[UserAction.dictUserActionTargetsHint] = 154;
                        update[UserAction.dictUserActionInfo] = Game.GetGameDictionaryProperty(game, dictVoteInfo, "");
                        return GameActionResult.Restart;
                    }
                }
                return GameActionResult.NotExecuted;
            }
            // Sheriff PK speech
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 4)
            {
                var pkPlayers = Game.GetGameDictionaryProperty(game, dictPk, new List<int>());
                var sheriffPlayers = Game.GetGameDictionaryProperty(game, dictSheriff, new List<int>());
                var first = sheriffPlayers[game.GetRandomNumber() % sheriffPlayers.Count];
                bool directionPlus = (game.GetRandomNumber() % 2) == 0;
                var nextPlayer = NextPlayer(pkPlayers, allPlayers.Count, first, directionPlus);
                var volunteersInfo = string.Join(", ", pkPlayers);
                return HandleRoundTableSpeak(game, pkPlayers, nextPlayer, directionPlus, update, 5, 105, volunteersInfo);
            }
            // Sheriff PK voting + tally
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 5)
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
                    update[dictSpeak] = 6;
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, actionDuraionPlayerReact, update))
                    {
                        update[UserAction.dictUserActionTargets] = pkPlayers;
                        update[UserAction.dictUserActionUsers] = votePlayers;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = 103;
                        return GameActionResult.Restart;
                    }
                }
                return GameActionResult.NotExecuted;
            }
            // Sheriff PK vote result display
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 6)
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
                    update[dictSpeak] = 9;
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
                        update[UserAction.dictUserActionTargetsHint] = 154;
                        update[UserAction.dictUserActionInfo] = Game.GetGameDictionaryProperty(game, dictVoteInfo, "");
                        return GameActionResult.Restart;
                    }
                }
                return GameActionResult.NotExecuted;
            }
            // Death announcement
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 9)
            {
                var deadPlayers = Game.GetGameDictionaryProperty(game, dictAboutToDie, new List<int>());

                var skipDaySpeech = Game.GetGameDictionaryProperty(game, dictSkipDaySpeech, 0) == 1;
                var day = Game.GetGameDictionaryProperty(game, dictDay, 0);
                var announcementDone = Game.GetGameDictionaryProperty(game, dictDay0AnnouncementDone, 0) == 1;
                if (skipDaySpeech && (day != 0 || announcementDone))
                {
                    update[dictSpeak] = 10;
                    return GameActionResult.Restart;
                }

                if (UserAction.EndUserAction(game, update))
                {
                    // Move to kill dead players phase
                    update[dictSpeak] = 10;
                    update[dictDay0AnnouncementDone] = 1;
                    return GameActionResult.Restart;
                }
                else
                {
                    // Display death announcement
                    if (UserAction.StartUserAction(game, 5, update))
                    {
                        var deathInfo = string.Join(", ", deadPlayers);
                        update[UserAction.dictUserActionTargets] = new List<int>();
                        update[UserAction.dictUserActionUsers] = new List<int>() { -1 };
                        update[UserAction.dictUserActionTargetsCount] = 0;
                        update[UserAction.dictUserActionTargetsHint] = 152; // Death announcement hint
                        update[UserAction.dictUserActionInfo] = deathInfo;
                        return GameActionResult.Restart;
                    }
                }
                return GameActionResult.NotExecuted;
            }
            // Dead player
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 10)
            {
                // Set up interrupt to go through death handling
                var interrupted = new Dictionary<string, object>();
                var skipDaySpeech = Game.GetGameDictionaryProperty(game, dictSkipDaySpeech, 0) == 1;
                if (skipDaySpeech)
                    interrupted[dictSpeak] = 40;
                else
                    interrupted[dictSpeak] = 20; // Go to win condition check first
                update[dictSpeak] = 97;
                update[dictSkipDaySpeech] = 0;
                update[dictInterrupt] = interrupted;
                game.UseRandomNumber(update);
                return GameActionResult.Restart;
            }
            // speak=20: Check win conditions (called from death handling)
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 20)
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
                            update[UserAction.dictUserActionTargetsHint] = 1003; // Game winner hint
                            update[UserAction.dictUserActionInfo] = winner;
                            return GameActionResult.Restart;
                        }
                    }
                    return GameActionResult.NotExecuted;
                }
                else
                {
                    // No winner yet, continue to day speech
                    update[dictSpeak] = 30;
                    return GameActionResult.Restart;
                }
            }

            // Sheriff choose day speech direction
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 30)
            {
                var sheriff = Game.GetGameDictionaryProperty(game, dictCurrentSheriff, 0);

                // Only ask sheriff if one exists, otherwise default to left (false)
                if (sheriff != 0)
                {
                    if (UserAction.EndUserAction(game, update))
                    {
                        (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, new List<int> { sheriff }, update);

                        bool directionRight = false;
                        if (inputValid && input.ContainsKey(sheriff.ToString()))
                        {
                            var targets = (List<int>)input[sheriff.ToString()];
                            if (targets.Count > 0 && targets[0] != -2)
                            {
                                directionRight = true;
                            }
                        }

                        update[UserAction.dictUserActionUsers] = new List<int>();
                        update[dictSpeak] = 31;
                        update[dictDaySpeechDirection] = directionRight;
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        if (UserAction.StartUserAction(game, actionDuraionPlayerReact, update))
                        {
                            update[UserAction.dictUserActionTargets] = new List<int> { -2, -1 }; // -2 = left, -1 = right
                            update[UserAction.dictUserActionUsers] = new List<int> { sheriff };
                            update[UserAction.dictUserActionTargetsCount] = 1;
                            update[UserAction.dictUserActionTargetsHint] = 153; // Sheriff choose direction hint
                            return GameActionResult.Restart;
                        }
                        else
                        {
                            (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, new List<int> { sheriff }, update);

                            bool directionRight = false;
                            if (inputValid && input.ContainsKey(sheriff.ToString()))
                            {
                                var targets = (List<int>)input[sheriff.ToString()];
                                if (targets.Count > 0 && targets[0] == -1)
                                {
                                    directionRight = true;
                                    update[UserAction.dictUserActionUsers] = new List<int>();
                                    update[dictSpeak] = 31;
                                    update[dictDaySpeechDirection] = directionRight;
                                    return GameActionResult.Restart;
                                }
                                if (targets.Count > 0 && targets[0] == -2)
                                {
                                    directionRight = false;
                                    update[UserAction.dictUserActionUsers] = new List<int>();
                                    update[dictSpeak] = 31;
                                    update[dictDaySpeechDirection] = directionRight;
                                    return GameActionResult.Restart;
                                }
                            }

                        }
                    }
                    return GameActionResult.NotExecuted;
                }
                else
                {
                    // No sheriff, default to left
                    update[dictSpeak] = 31;
                    update[dictDaySpeechDirection] = false;
                    return GameActionResult.Restart;
                }
            }
            // Day speech
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 31)
            {
                var ap = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
                var sheriff = Game.GetGameDictionaryProperty(game, dictCurrentSheriff, 0);
                var directionRight = Game.GetGameDictionaryProperty(game, dictDaySpeechDirection, false);

                int first = 0;
                if (sheriff != 0)
                {
                    first = sheriff;
                }
                else
                {
                    first = ap[game.GetRandomNumber() % (ap.Count == 0 ? 1 : ap.Count)];
                }
                bool dir = directionRight;

                return HandleRoundTableSpeak(game, ap, first, dir, update, 32, 102);
            }
            // Sheriff recommend vote
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 32)
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
                        update[dictSpeak] = 33;
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
                            update[UserAction.dictUserActionTargetsHint] = 110;
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
                                    update[dictSpeak] = 33;
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
                    update[dictSpeak] = 33;
                    return GameActionResult.Restart;
                }
            }
            // vote 1
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 33)
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
                        update[dictSpeak] = 34; // go to vote result display
                    }
                    else
                    {
                        update[dictSpeak] = 40;
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
                        update[UserAction.dictUserActionTargetsHint] = 111;
                        return GameActionResult.Restart;
                    }
                }
                return GameActionResult.NotExecuted;
            }
            // vote 1 result announcement
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 34)
            {
                var voteOut = Game.GetGameDictionaryProperty(game, dictVoteOut, new List<int>());

                if (UserAction.EndUserAction(game, update))
                {
                    // Route based on vote result
                    if (voteOut.Count > 1)
                    {
                        // Tie - go to voteout speech
                        update[dictSpeak] = 35;
                    }
                    else if (voteOut.Count == 1)
                    {
                        // Single target - go to voted out
                        update[dictSpeak] = 38;
                    }
                    else
                    {
                        update[dictSpeak] = 40;
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
                        update[UserAction.dictUserActionTargetsHint] = 154;
                        update[UserAction.dictUserActionInfo] = Game.GetGameDictionaryProperty(game, dictVoteInfo, "");
                        return GameActionResult.Restart;
                    }
                }
                return GameActionResult.NotExecuted;
            }
            // voteout speech
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 35)
            {
                var pkPlayers = Game.GetGameDictionaryProperty(game, dictVoteOut, new List<int>());
                var sheriffPlayers = Game.GetGameDictionaryProperty(game, dictSheriff, new List<int>());
                var first = sheriffPlayers.Count == 0 ? 1 : sheriffPlayers[game.GetRandomNumber() % sheriffPlayers.Count];
                bool directionPlus = (game.GetRandomNumber() % 2) == 1;
                var nextPlayer = NextPlayer(pkPlayers, allPlayers.Count, first, directionPlus);
                return HandleRoundTableSpeak(game, pkPlayers, nextPlayer, directionPlus, update, 36, 102);
            }
            // vote 2
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 36)
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
                        update[dictSpeak] = 37; // go to vote result display
                    }
                    else
                    {
                        update[dictSpeak] = 40;
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
                        update[UserAction.dictUserActionTargetsHint] = 111;
                        return GameActionResult.Restart;
                    }
                }
                return GameActionResult.NotExecuted;
            }
            // vote 2 result announcement
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 37)
            {
                var voteOut = Game.GetGameDictionaryProperty(game, dictVoteOut, new List<int>());

                if (UserAction.EndUserAction(game, update))
                {
                    // Route based on vote result
                    if (voteOut.Count > 1)
                    {
                        // Tie
                        update[dictSpeak] = 40;
                    }
                    else if (voteOut.Count == 1)
                    {
                        // Single target - go to voted out
                        update[dictSpeak] = 38;
                    }
                    else
                    {
                        update[dictSpeak] = 40;
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
                        update[UserAction.dictUserActionTargetsHint] = 154;
                        update[UserAction.dictUserActionInfo] = Game.GetGameDictionaryProperty(game, dictVoteInfo, "");
                        return GameActionResult.Restart;
                    }
                }
                return GameActionResult.NotExecuted;
            }
            // voted out
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 38)
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
                    interrupted[dictSpeak] = 40;
                    update[dictSpeak] = 97;
                    update[dictInterrupt] = interrupted;
                }
                else
                {
                    update[dictSpeak] = 40;
                }
                return GameActionResult.Restart;
            }

            // speak=40: End of day - check win conditions then advance
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 40)
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
                            update[UserAction.dictUserActionTargetsHint] = 1003; // Game winner hint
                            update[UserAction.dictUserActionInfo] = winner;
                            return GameActionResult.Restart;
                        }
                    }
                    return GameActionResult.NotExecuted;
                }
                else
                {
                    // No winner yet, advance to night
                    update[dictSpeak] = 0;
                    AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
            }

            // Death handling interrupt - consolidates all death-related logic
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 97)
            {
                // Kill the dead players
                KillDeadPlayers(game, update);

                // Move to dead player skills processing
                update[dictSpeak] = 98;
                return GameActionResult.Restart;
            }

            // Dead player skills processing
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 98)
            {
                return HandleDeadPlayerSkills(game, update);
            }

            // Dead player processing: handle sheriff handover
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 99)
            {
                return HandleDeadPlayerProcessing(game, update);
            }

            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 100)
            {
                var dp = Game.GetGameDictionaryProperty(game, dictDeadPlayerAction, new List<int>());
                var lastAction = (int)Game.GetGameDictionaryProperty(game, dictInterrupt, new Dictionary<string, object>())[dictSpeak];
                if (dp.Count > 0 && (lastAction == 40 || Game.GetGameDictionaryProperty(game, dictDay, 0) == 0))
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
        public static GameActionResult HandleRoundTableSpeak(Game game, List<int> players, int startingPlayer, bool directionPlus, Dictionary<string, object> update, int nextSpeak, int hint = 102, string userinfo = null)
        {
            var speakers = Game.GetGameDictionaryProperty(game, dictSpeaker, new List<int>());
            var allPlayers = LangRenSha.GetPlayers(game, x => true);
            if (UserAction.EndUserAction(game, update))
            {
                if (speakers.Count >= players.Count)
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
                    return GameActionResult.Restart;
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
                        if (players.Contains(next))
                        {
                            break;
                        }
                    } while (next != last);
                    speakers.Add(next);
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
                                if (speakers.Count >= players.Count)
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
                                }
                                UserAction.EndUserAction(game, update, true);
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
                                UserAction.EndUserAction(game, update, true);
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

            bool hasEvil = false;
            bool hasGod = false;
            bool hasCivilian = false;

            foreach (var player in alivePlayers)
            {
                var factionObj = GetPlayerProperty<object>(game, player, dictPlayerFaction, PlayerFaction.Civilian);

                PlayerFaction faction;
                if (factionObj is PlayerFaction pf)
                {
                    faction = pf;
                }
                else if (factionObj is int factionInt)
                {
                    faction = (PlayerFaction)factionInt;
                }
                else
                {
                    faction = PlayerFaction.Civilian;
                }

                if ((faction & PlayerFaction.Evil) != 0)
                    hasEvil = true;
                else if ((faction & PlayerFaction.God) != 0)
                    hasGod = true;
                else if ((faction & PlayerFaction.Civilian) != 0)
                    hasCivilian = true;
            }

            if (!hasCivilian)
            {
                return "evil";
            }
            if (!hasGod)
            {
                return "evil";
            }
            if (!hasEvil)
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
                info += $"{p}: {string.Join(", ", voters)}";
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
                    (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, new List<int> { currentSheriff }, update);
                    if (inputValid && input.ContainsKey(currentSheriff.ToString()))
                    {
                        var targets = (List<int>)input[currentSheriff.ToString()];
                        if (targets.Count > 0)
                        {
                            update[dictCurrentSheriff] = targets[0];
                        }
                        else
                        {
                            update[dictCurrentSheriff] = 0;
                        }
                    }
                    else
                    {
                        update[dictCurrentSheriff] = 0;
                    }

                    // Move to dead player skills phase
                    update[dictSpeak] = 98;
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
                        update[UserAction.dictUserActionTargetsHint] = 150; // Sheriff handover hint
                        return GameActionResult.Restart;
                    }
                }
                return GameActionResult.NotExecuted;
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
