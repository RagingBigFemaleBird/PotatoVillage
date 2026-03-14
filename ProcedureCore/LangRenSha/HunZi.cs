using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProcedureCore.LangRenSha
{
    /// <summary>
    /// HunZi (Follower/混子) - A civilian role that follows another player.
    /// Can only act during Day 0 (first night).
    /// During act, selects a player to "follow" and stores the target.
    /// </summary>
    public class HunZi : Role
    {
        private static Dictionary<string, object> roleDict = new()
        {
            { YuYanJia.dictYuYanJiaResult, 1 },  // Appears as good to seer
            { LangRenSha.dictPlayerAlliance, 1 },  // Good alliance
            { LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.Civilian },
        };

        private static List<int> actionOrders = new() 
        { 
            (int)ActionConstant.HunZi_OpenEyes, 
            (int)ActionConstant.HunZi_Act, 
            (int)ActionConstant.HunZi_CloseEyes 
        };

        public HunZi()
        {
        }

        public Dictionary<string, object> RoleDict
        {
            get { return roleDict; }
        }

        public string Name
        {
            get { return "HunZi"; }
        }

        public int Version
        {
            get { return 1; }
        }

        public List<int> ActionOrders
        {
            get { return actionOrders; }
        }

        public int ActionDuration
        {
            get { return 15; }
        }

        // Dictionary keys for HunZi state
        public static string dictHunZiTarget = "hunzi_target";  // Player property: followed target

        public GameActionResult GenerateStateDiff(Game game, Dictionary<string, object> update)
        {
            if (game.StateSequenceNumber == 1)
            {
                // Add HunZi's actions to night orders
                var addSelf = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
                if (addSelf.Count > 0)
                {
                    var no = Game.GetGameDictionaryProperty(game, LangRenSha.dictNightOrders, new List<int>());
                    no.AddRange(ActionOrders);
                    update[LangRenSha.dictNightOrders] = no;
                }
                return GameActionResult.Continue;
            }

            var day = Game.GetGameDictionaryProperty(game, LangRenSha.dictDay, 0);
            var action = Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0);
            if (day >= 1 && (action == ActionOrders[0] || action == ActionOrders[1] || action == ActionOrders[2]))
            {
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }

            // Actions 20 and 22 are announcer actions (open eyes, close eyes)
            if (LangRenSha.AnnouncerAction(game, update, false, ActionOrders[0], ActionOrders[2], 50, 51, Name, 4) == GameActionResult.Restart)
            {
                return GameActionResult.Restart;
            }

            // Action 21: HunZi selects a target to follow (only on Day 0)
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == ActionOrders[1])
            {
                var hunzi = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
                var hunziAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
                var hunziPlayer = hunziAlive.Count > 0 ? hunziAlive[0] : 0;

                var actionDuration = Game.GetGameDictionaryProperty(game, LangRenSha.dictDurationPlayerReact, ActionDuration);
                
                var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
                alivePlayers.Remove(hunziPlayer);  // Cannot follow self

                if (UserAction.EndUserAction(game, update))
                {
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, actionDuration, update))
                    {
                        update[UserAction.dictUserActionTargets] = alivePlayers;
                        update[UserAction.dictUserActionUsers] = hunzi;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.HunZi_Act;
                        update[UserAction.dictUserActionRole] = Name;
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        // Action in progress - check for early completion
                        (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, hunziAlive, update);
                        if (inputValid)
                        {
                            var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, -1);
                            if (targets.Count > 0 && targets[0] > 0)
                            {
                                // Store the followed target as player property
                                LangRenSha.SetPlayerProperty(game, hunziPlayer, dictHunZiTarget, targets[0], update);

                                UserAction.EndUserAction(game, update, true);
                                LangRenSha.AdvanceAction(game, update);
                                return GameActionResult.Restart;
                            }
                        }
                    }
                }
                return GameActionResult.NotExecuted;
            }

            return GameActionResult.NotExecuted;
        }
    }
}
