using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProcedureCore.LangRenSha
{
    /// <summary>
    /// AwkShiXiangGui (觉醒石像鬼 - Awakened Gargoyle) - A wolf role that converts adjacent players.
    /// Day 0 only: Choose a player that is adjacent to any LangRen role (alliance = 2) and is not a LangRen itself.
    /// If there are 10 players total, player 1 and 10 are considered adjacent (circular seating).
    /// The target is selected early in the night, but actual conversion happens at the very end (actions 305-307).
    /// This means if YuYanJia checks the target before conversion, they still appear as good.
    /// After conversion, the target's alliance changes to 2 (evil), YuYanJia result to 2, and succession to 3.
    /// Actions 13-15: Converted player open/close eyes to check attack status (Day 1+, after ShenLangGongWu1).
    /// Action 125: Select target during LangRen phase (Day 0 only, shares LangRen open/close eyes).
    /// Actions 305-307: Lucky one check - actual conversion happens here (Day 0 only, very last).
    /// </summary>
    public class AwkShiXiangGui : Role
    {
        private static Dictionary<string, object> roleDict = new()
        {
            { YuYanJia.dictYuYanJiaResult, 2 },
            { LangRenSha.dictPlayerAlliance, 2 },
            { LangRen.dictSuceession, 1 },
            { LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.Evil },
        };

        private static List<int> actionOrders = new()
        {
            (int)ActionConstant.AwkShiXiangGui_ConvertedOpenEyes,      // 0: 16 - Day 1+ converted check
            (int)ActionConstant.AwkShiXiangGui_ConvertedCheckAttack,   // 1: 17
            (int)ActionConstant.AwkShiXiangGui_ConvertedCloseEyes,     // 2: 18
            125,                                                       // 3: 125 - Day 0 selection (shares LangRen open/close eyes)
            (int)ActionConstant.AwkShiXiangGui_LuckyOneOpenEyes,       // 4: 305 - Day 0 lucky one check (very last)
            (int)ActionConstant.AwkShiXiangGui_CheckConversion,        // 5: 306 - Actual conversion happens here
            (int)ActionConstant.AwkShiXiangGui_LuckyOneCloseEyes,      // 6: 307
        };

        // Dictionary key for storing selected target (before conversion)
        public static string dictSelectedTarget = "awk_shi_xiang_gui_target";

        public AwkShiXiangGui() { }

        public Dictionary<string, object> RoleDict => roleDict;
        public string Name => "AwkShiXiangGui";
        public int Version => 1;
        public List<int> ActionOrders => actionOrders;
        public int ActionDuration => 15;

        /// <summary>
        /// Handle reveal self action - AwkShiXiangGui can reveal itself during day phase.
        /// Similar to LangRen's reveal functionality.
        /// </summary>
        public static GameActionResult RevealSelf(Game game, int player, List<int> targets, Dictionary<string, object> update)
        {
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictSpeak, 0) == (int)SpeakConstant.SheriffSpeech || 
                Game.GetGameDictionaryProperty(game, LangRenSha.dictSpeak, 0) == (int)SpeakConstant.DaySpeech || 
                Game.GetGameDictionaryProperty(game, LangRenSha.dictSpeak, 0) == (int)SpeakConstant.WithdrawOrReveal)
            {
                var awkShiXiangGui = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == "AwkShiXiangGui" && (int)x[LangRenSha.dictAlive] == 1);
                if (awkShiXiangGui.Contains(player))
                {
                    if (targets.Contains(-10))
                    {
                        var aboutToDie = Game.GetGameDictionaryProperty(game, LangRenSha.dictAboutToDie, new List<int>());
                        update[LangRenSha.dictAboutToDie] = new List<int>() { player };
                        update[LangRenSha.dictSkipDaySpeech] = 1;
                        var interrupted = new Dictionary<string, object>();
                        var currentSpeak = Game.GetGameDictionaryProperty(game, LangRenSha.dictSpeak, 0);
                        interrupted[LangRenSha.dictAboutToDie] = aboutToDie;
                        interrupted[LangRenSha.dictSpeak] = (int)SpeakConstant.DeathAnnouncement;
                        update[LangRenSha.dictSpeak] = (int)SpeakConstant.DeathHandlingInterrupt;
                        update[LangRenSha.dictInterrupt] = interrupted;
                        UserAction.EndUserAction(game, update, true);
                        return GameActionResult.Restart;
                    }
                }
            }
            return GameActionResult.NotExecuted;
        }

        public GameActionResult GenerateStateDiff(Game game, Dictionary<string, object> update)
        {
            if (game.StateSequenceNumber == 1)
            {
                var addSelf = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
                if (addSelf.Count > 0)
                {
                    var no = Game.GetGameDictionaryProperty(game, LangRenSha.dictNightOrders, new List<int>());
                    no.AddRange(actionOrders);
                    update[LangRenSha.dictNightOrders] = no;
                }
                return GameActionResult.Continue;
            }

            var day = Game.GetGameDictionaryProperty(game, LangRenSha.dictDay, 0);
            var action = Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0);

            // Actions 13-15: Converted player open/close eyes - Day 1+ only (after ShenLangGongWu1's check)
            if (LangRenSha.AnnouncerAction(game, update, true, ActionOrders[0], ActionOrders[2],
                (int)HintConstant.ConvertedOpenEyes, (int)HintConstant.ConvertedCloseEyes, Name, 4) == GameActionResult.Restart)
            {
                return GameActionResult.Restart;
            }

            // Action 14: Converted player checks attack status - Day 1+ only
            if (action == ActionOrders[1])
            {
                if (day == 0)
                {
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                return HandleConvertedAttackCheck(game, update);
            }

            // Action 125: AwkShiXiangGui selects a target to convert - Day 0 only (shares LangRen open/close eyes)
            if (action == ActionOrders[3])
            {
                if (day != 0)
                {
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                return HandleConvertAction(game, update);
            }

            // Actions 305 and 307: CheckPrivate announcer actions for all players to open/close eyes - Day 0 only
            if (action == ActionOrders[4] || action == ActionOrders[6])
            {
                if (day != 0)
                {
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                if (LangRenSha.AnnouncerAction(game, update, false, ActionOrders[4], ActionOrders[6],
                    (int)HintConstant.CheckPrivate, (int)HintConstant.NightTime, "", 4) == GameActionResult.Restart)
                {
                    return GameActionResult.Restart;
                }
            }

            // Action 306: All players check for conversion status - Day 0 only
            if (action == ActionOrders[5])
            {
                if (day != 0)
                {
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                return HandleConversionCheck(game, update);
            }

            return GameActionResult.NotExecuted;
        }

        /// <summary>
        /// Handle the selection action - AwkShiXiangGui selects a target to convert.
        /// Target must be adjacent to any LangRen player and not be a LangRen itself.
        /// Note: Only stores the target here. Actual conversion happens at the end of night (CheckConversion).
        /// </summary>
        private GameActionResult HandleConvertAction(Game game, Dictionary<string, object> update)
        {
            var awkShiXiangGui = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
            var awkShiXiangGuiAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
            var awkPlayer = awkShiXiangGuiAlive.Count > 0 ? awkShiXiangGuiAlive[0] : 0;

            var actionDuration = Game.GetGameDictionaryProperty(game, LangRenSha.dictDurationLangRen, ActionDuration);
            if (awkShiXiangGuiAlive.Count == 0)
            {
                actionDuration = new Random().Next(3, 6);
            }

            // Get valid targets: players adjacent to any LangRen and not LangRen themselves
            var validTargets = GetValidConversionTargets(game);

            if (UserAction.EndUserAction(game, update))
            {
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.GameOver; // Game over if not selected.
            }
            else
            {
                if (UserAction.StartUserAction(game, actionDuration, update))
                {
                    update[UserAction.dictUserActionTargets] = awkShiXiangGuiAlive.Count > 0 ? validTargets : new List<int>();
                    update[UserAction.dictUserActionUsers] = awkShiXiangGui;
                    update[UserAction.dictUserActionTargetsCount] = 1;
                    update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.AwkShiXiangGui_Act;
                    update[UserAction.dictUserActionRole] = "LangRen";
                    return GameActionResult.Restart;
                }
                else
                {
                    (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, awkShiXiangGuiAlive, update);
                    if (inputValid)
                    {
                        var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, -1);
                        if (targets.Count > 0 && targets[0] > 0)
                        {
                            var target = targets[0];

                            // Only store the target here - actual conversion happens at end of night
                            update[dictSelectedTarget] = target;

                            // Set skippedAct to false (acted) for AwkShiXiangGui on day 0
                            if (awkPlayer > 0)
                            {
                                AwkSheMengRen.SetSkippedAct(game, awkPlayer, false, update);
                            }

                            game.Log($"AwkShiXiangGui: Player {target} selected for conversion (will convert at end of night)");

                            UserAction.EndUserAction(game, update, true);
                            LangRenSha.AdvanceAction(game, update);
                            return GameActionResult.Restart;
                        }
                    }
                }
            }
            return GameActionResult.NotExecuted;
        }

        /// <summary>
        /// Handle the conversion check action - performs actual conversion and all players see if they were converted.
        /// This happens at the very end of night, so YuYanJia check before this will still see the player as good.
        /// </summary>
        private GameActionResult HandleConversionCheck(Game game, Dictionary<string, object> update)
        {
            // Get the selected target
            var selectedTarget = Game.GetGameDictionaryProperty(game, dictSelectedTarget, 0);

            if (UserAction.EndUserAction(game, update))
            {
                // Perform actual conversion if a target was selected
                if (selectedTarget > 0)
                {
                    // Store before conversion faction for AwkSheMengRen
                    var currentFaction = LangRenSha.GetPlayerProperty(game, selectedTarget, LangRenSha.dictPlayerFaction, 0);
                    LangRenSha.SetPlayerProperty(game, selectedTarget, AwkSheMengRen.dictBeforeConversionFaction, currentFaction, update);

                    // Convert the target: change alliance to 2 (evil), YuYanJia result to 2, and set succession
                    update[ShenLangGongWu1.dictConvertedPlayer] = selectedTarget;
                    LangRenSha.SetPlayerProperty(game, selectedTarget, LangRenSha.dictPlayerAlliance, 2, update);
                    LangRenSha.SetPlayerProperty(game, selectedTarget, YuYanJia.dictYuYanJiaResult, 2, update);
                    LangRenSha.SetPlayerProperty(game, selectedTarget, LangRen.dictSuceession, 2, update);
                    LangRenSha.SetPlayerProperty(game, selectedTarget, LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.Evil, update);

                    game.Log($"AwkShiXiangGui: Player {selectedTarget} converted to evil alliance (end of night)");
                }
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }
            else
            {
                if (UserAction.StartUserAction(game, 5, update))
                {
                    var convertedPlayer = selectedTarget;
                    var allPlayers = LangRenSha.GetPlayers(game, x => true);
                    update[UserAction.dictUserActionTargets] = new List<int>();
                    update[UserAction.dictUserActionUsers] = allPlayers;
                    update[UserAction.dictUserActionTargetsCount] = 0;
                    update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.AwkShiXiangGui_CheckConversion;
                    update[UserAction.dictUserActionInfo] = convertedPlayer > 0 ? convertedPlayer.ToString() : "";
                    return GameActionResult.Restart;
                }
            }
            return GameActionResult.NotExecuted;
        }

        /// <summary>
        /// Handle the converted player attack status check - similar to ShenLangGongWu1.
        /// Day 1+ only: Converted player checks if they can attack (if all LangRen/Succession1 are dead).
        /// </summary>
        private GameActionResult HandleConvertedAttackCheck(Game game, Dictionary<string, object> update)
        {
            if (UserAction.EndUserAction(game, update))
            {
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }
            else
            {
                var actionDuration = 5;

                if (UserAction.StartUserAction(game, actionDuration, update))
                {
                    var langRenAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == "LangRen" && (int)x[LangRenSha.dictAlive] == 1);
                    var langRenSuccession1Alive = LangRenSha.GetPlayers(game, x => x.ContainsKey(LangRen.dictSuceession) && (int)x[LangRen.dictSuceession] == 1 && (int)x[LangRenSha.dictAlive] == 1);

                    var target = Game.GetGameDictionaryProperty(game, ShenLangGongWu1.dictConvertedPlayer, 0);
                    update[UserAction.dictUserActionTargets] = new List<int>();
                    update[UserAction.dictUserActionUsers] = new List<int>() { target };
                    update[UserAction.dictUserActionTargetsCount] = 0;
                    update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.LangRen_ConvertedSuccession;
                    update[UserAction.dictUserActionRole] = "LangRen";
                    if (langRenAlive.Count + langRenSuccession1Alive.Count == 0)
                    {
                        update[UserAction.dictUserActionInfo] = "Succession";
                    }
                    return GameActionResult.Restart;
                }
            }

            // Advance to next action
            LangRenSha.AdvanceAction(game, update);
            return GameActionResult.Restart;
        }

        /// <summary>
        /// Get valid conversion targets: players who are adjacent to at least one LangRen role (alliance = 2)
        /// and are not LangRen themselves. For circular seating (e.g., 10 players), 1 and 10 are adjacent.
        /// </summary>
        private List<int> GetValidConversionTargets(Game game)
        {
            var validTargets = new List<int>();

            // Get all players
            var allPlayers = LangRenSha.GetPlayers(game, x => true);
            var totalPlayers = allPlayers.Count;

            // Get all LangRen players (alliance = 2)
            var langRenPlayers = LangRenSha.GetPlayers(game, x =>
                x.ContainsKey(LangRenSha.dictPlayerAlliance) && (int)x[LangRenSha.dictPlayerAlliance] == 2);

            if (langRenPlayers.Count == 0)
            {
                return validTargets;
            }

            // Get alive players who are not LangRen (alliance != 2)
            var alivePlayers = LangRenSha.GetPlayers(game, x =>
                (int)x[LangRenSha.dictAlive] == 1 &&
                (!x.ContainsKey(LangRenSha.dictPlayerAlliance) || (int)x[LangRenSha.dictPlayerAlliance] != 2));

            foreach (var player in alivePlayers)
            {
                if (IsAdjacentToAnyLangRen(player, langRenPlayers, totalPlayers))
                {
                    validTargets.Add(player);
                }
            }

            return validTargets;
        }

        /// <summary>
        /// Check if a player is adjacent to ANY LangRen player.
        /// Adjacent means the player number differs by 1, or for circular seating,
        /// player 1 and player N (totalPlayers) are also adjacent.
        /// </summary>
        private bool IsAdjacentToAnyLangRen(int player, List<int> langRenPlayers, int totalPlayers)
        {
            foreach (var langRen in langRenPlayers)
            {
                if (IsAdjacent(player, langRen, totalPlayers))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Check if two players are adjacent in circular seating.
        /// </summary>
        private bool IsAdjacent(int player1, int player2, int totalPlayers)
        {
            var diff = Math.Abs(player1 - player2);

            // Adjacent if difference is 1
            if (diff == 1)
            {
                return true;
            }

            // For circular seating: 1 and totalPlayers are adjacent
            if (diff == totalPlayers - 1)
            {
                return true;
            }

            return false;
        }
    }
}
