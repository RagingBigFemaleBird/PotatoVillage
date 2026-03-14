using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcedureCore.LangRenSha
{
    /// <summary>
    /// YingZi (Shadow) - A god role that shadows another player.
    /// On Day 0: Choose a player to shadow.
    /// On Day 1+: If the shadowed player dies, copy all their attributes and become that role.
    /// Also receives info if the shadowed player's role changes.
    /// </summary>
    public class YingZi : Role
    {
        private static Dictionary<string, object> roleDict = new()
        {
            { YuYanJia.dictYuYanJiaResult, 1 },
            { LangRenSha.dictPlayerAlliance, 1 },
            { LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.God | LangRenSha.PlayerFaction.Civilian },
        };

        private static List<int> actionOrders = new()
        {
            (int)ActionConstant.YingZi_OpenEyes,
            (int)ActionConstant.YingZi_Act,
            (int)ActionConstant.YingZi_Info,
            (int)ActionConstant.YingZi_CloseEyes
        };

        public YingZi()
        {
        }

        public Dictionary<string, object> RoleDict
        {
            get
            {
                return roleDict;
            }
        }

        public string Name
        {
            get
            {
                return "YingZi";
            }
        }

        public int Version
        {
            get
            {
                return 1;
            }
        }

        public List<int> ActionOrders
        {
            get
            {
                return actionOrders;
            }
        }

        public int ActionDuration
        {
            get
            {
                return 30;
            }
        }

        // Global dictionary keys - use global dict instead of player dict because role can change
        public static string dictYingZiPlayer = "yingzi_player";           // The player who is YingZi
        public static string dictYingZiTarget = "yingzi_target";           // The player being shadowed
        public static string dictYingZiIsThirdParty = "yingzi_is_thirdparty"; // 1 if YingZi shadowed FuChouZhe and became ThirdParty

        public GameActionResult GenerateStateDiff(Game game, Dictionary<string, object> update)
        {
            if (game.StateSequenceNumber == 1)
            {
                var addSelf = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
                if (addSelf.Count > 0)
                {
                    var no = Game.GetGameDictionaryProperty(game, LangRenSha.dictNightOrders, new List<int>());
                    no.AddRange(ActionOrders);
                    update[LangRenSha.dictNightOrders] = no;

                    // Store who the YingZi player is (important: use this instead of role check later)
                    update[dictYingZiPlayer] = addSelf[0];
                }
                return GameActionResult.Continue;
            }

            // Announcer actions for open/close eyes (uses generic 50/51 hints)
            if (LangRenSha.AnnouncerAction(game, update, false, ActionOrders[0], ActionOrders[3], 50, 51, Name, 4) == GameActionResult.Restart)
            {
                return GameActionResult.Restart;
            }

            // YingZi Act - choose target on Day 0, or transform on Day 1+
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == ActionOrders[1])
            {
                return HandleYingZiAct(game, update);
            }

            // YingZi Info - show role change info
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == ActionOrders[2])
            {
                return HandleYingZiInfo(game, update);
            }

            return GameActionResult.NotExecuted;
        }

        private GameActionResult HandleYingZiAct(Game game, Dictionary<string, object> update)
        {
            // Get YingZi player from global dict (not by role, since role can change)
            var yingZiPlayer = Game.GetGameDictionaryProperty(game, dictYingZiPlayer, 0);
            if (yingZiPlayer == 0)
            {
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }

            var yingZiList = new List<int> { yingZiPlayer };
            var isYingZiAlive = LangRenSha.GetPlayerProperty(game, yingZiPlayer, LangRenSha.dictAlive, 0) == 1;
            var yingZiAliveList = isYingZiAlive ? yingZiList : new List<int>();

            var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
            var actionDuration = Game.GetGameDictionaryProperty(game, LangRenSha.dictDurationPlayerReact, ActionDuration);
            var day = Game.GetGameDictionaryProperty(game, LangRenSha.dictDay, 0);

            // Day 0: Choose a player to shadow
            if (day == 0)
            {
                if (UserAction.EndUserAction(game, update))
                {
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, actionDuration + 15, update))
                    {
                        // Can shadow any player except self
                        var targets = alivePlayers.Where(p => p != yingZiPlayer).ToList();

                        update[UserAction.dictUserActionTargets] = targets;
                        update[UserAction.dictUserActionUsers] = yingZiList;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.YingZi_Act;
                        update[UserAction.dictUserActionRole] = Name;
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, yingZiAliveList, update);
                        if (inputValid)
                        {
                            var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.Input, -1);
                            if (targets.Count == 1 && targets[0] > 0)
                            {
                                var selectedTarget = targets[0];

                                // Store the shadow target
                                update[dictYingZiTarget] = selectedTarget;
                                var fuChouZhe = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == "FuChouZhe");
                                var hunZi = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == "HunZi");
                                int hunZiTarget = 0;
                                if (hunZi.Count > 0)
                                {
                                    hunZiTarget = LangRenSha.GetPlayerProperty(game, hunZi[0], HunZi.dictHunZiTarget, 0);
                                }

                                if (fuChouZhe.Contains(selectedTarget) || (hunZi.Contains(selectedTarget) && fuChouZhe.Contains(hunZiTarget)))
                                {
                                    // Both YingZi and FuChouZhe become ThirdParty
                                    LangRenSha.SetPlayerProperty(game, yingZiPlayer, LangRenSha.dictPlayerFaction, (int)LangRenSha.PlayerFaction.ThirdParty, update);
                                    LangRenSha.SetPlayerProperty(game, yingZiPlayer, LangRenSha.dictPlayerAlliance, 3, update);
                                    LangRenSha.SetPlayerProperty(game, selectedTarget, LangRenSha.dictPlayerFaction, (int)LangRenSha.PlayerFaction.ThirdParty, update);
                                    LangRenSha.SetPlayerProperty(game, selectedTarget, LangRenSha.dictPlayerAlliance, 3, update);
                                    if (!fuChouZhe.Contains(selectedTarget))
                                    {
                                        var fuChouZhePlayer = fuChouZhe[0];
                                        LangRenSha.SetPlayerProperty(game, fuChouZhePlayer, LangRenSha.dictPlayerFaction, (int)LangRenSha.PlayerFaction.ThirdParty, update);
                                        LangRenSha.SetPlayerProperty(game, fuChouZhePlayer, LangRenSha.dictPlayerAlliance, 3, update);
                                    }

                                    // Mark that YingZi is now ThirdParty (for info display)
                                    update[dictYingZiIsThirdParty] = 1;
                                }
                                else
                                {
                                    var targetAlliance = LangRenSha.GetPlayerProperty<int>(game, selectedTarget, LangRenSha.dictPlayerAlliance, 1);
                                    if (targetAlliance == 2)
                                    {
                                        var fuChouZhePlayer = fuChouZhe.Count > 0 ? fuChouZhe[0] : 0;
                                        LangRenSha.SetPlayerProperty(game, yingZiPlayer, LangRenSha.dictPlayerFaction, (int)LangRenSha.PlayerFaction.Evil, update);
                                        LangRenSha.SetPlayerProperty(game, yingZiPlayer, LangRenSha.dictPlayerAlliance, 2, update);
                                        if (fuChouZhePlayer > 0)
                                        {
                                            LangRenSha.SetPlayerProperty(game, fuChouZhePlayer, LangRenSha.dictPlayerFaction, (int)LangRenSha.PlayerFaction.God, update);
                                            LangRenSha.SetPlayerProperty(game, fuChouZhePlayer, LangRenSha.dictPlayerAlliance, 1, update);
                                        }
                                    }
                                }


                                UserAction.EndUserAction(game, update, true);
                                LangRenSha.AdvanceAction(game, update);
                                return GameActionResult.Restart;
                            }
                        }
                    }
                }
                return GameActionResult.NotExecuted;
            }
            else
            {
                // Day 1+: Check if shadowed target is dead, if so, transform
                var shadowedTarget = Game.GetGameDictionaryProperty(game, dictYingZiTarget, 0);
                var isThirdParty = Game.GetGameDictionaryProperty(game, dictYingZiIsThirdParty, 0) == 1;

                if (shadowedTarget == 0 || !isYingZiAlive || isThirdParty)
                {
                    // No target selected or YingZi is dead, skip
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }

                var isTargetDead = LangRenSha.GetPlayerProperty(game, shadowedTarget, LangRenSha.dictAlive, 1) == 0;

                if (isTargetDead)
                {
                    // Copy all attributes from shadowed target to YingZi
                    var playersDict = Game.GetGameDictionaryProperty(game, LangRenSha.dictPlayers, new Dictionary<string, object>());

                    if (playersDict.ContainsKey(shadowedTarget.ToString()) && playersDict.ContainsKey(yingZiPlayer.ToString()))
                    {
                        var targetPlayerDict = (Dictionary<string, object>)playersDict[shadowedTarget.ToString()];
                        var yingZiPlayerDict = (Dictionary<string, object>)playersDict[yingZiPlayer.ToString()];

                        // Copy every single attribute from target to YingZi
                        foreach (var kvp in targetPlayerDict)
                        {
                            // Keep YingZi alive (don't copy alive status)
                            if (kvp.Key == LangRenSha.dictAlive)
                                continue;

                            yingZiPlayerDict[kvp.Key] = kvp.Value;
                        }

                        update[LangRenSha.dictPlayers] = playersDict;

                        // Clear the shadow target since transformation is complete
                        update[dictYingZiTarget] = 0;
                    }
                }

                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }
        }

        private GameActionResult HandleYingZiInfo(Game game, Dictionary<string, object> update)
        {
            // Get YingZi player from global dict
            var yingZiPlayer = Game.GetGameDictionaryProperty(game, dictYingZiPlayer, 0);
            var isThirdParty = Game.GetGameDictionaryProperty(game, dictYingZiIsThirdParty, 0) == 1;
            var actionDuration = 6;
            var currentRole = LangRenSha.GetPlayerProperty<string>(game, yingZiPlayer, LangRenSha.dictRole, "");

            if (yingZiPlayer == 0)
            {
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }

            var isYingZiAlive = LangRenSha.GetPlayerProperty(game, yingZiPlayer, LangRenSha.dictAlive, 0) == 1;

            // Determine what info to show
            string infoToShow;
            if (isThirdParty)
            {
                // Show third party message
                infoToShow = "thirdparty";
            }
            else
            {
                // Show current role of shadowed target
                infoToShow = currentRole;
            }

            var yingZiList = new List<int> { yingZiPlayer };

            if (UserAction.EndUserAction(game, update))
            {
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }
            else
            {
                if (UserAction.StartUserAction(game, actionDuration, update))
                {
                    update[UserAction.dictUserActionTargets] = new List<int>();
                    update[UserAction.dictUserActionUsers] = yingZiList;
                    update[UserAction.dictUserActionTargetsCount] = 0;
                    update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.YingZi_Info;
                    update[UserAction.dictUserActionInfo] = infoToShow;
                    update[UserAction.dictUserActionRole] = Name;
                    return GameActionResult.Restart;
                }
            }
            return GameActionResult.NotExecuted;
        }
    }
}
