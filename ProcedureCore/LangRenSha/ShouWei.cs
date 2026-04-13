using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcedureCore.LangRenSha
{
    public class ShouWei : Role
    {
        private static Dictionary<string, object> roleDict = new()
            {
                { YuYanJia.dictYuYanJiaResult, 1 },
                { LangRenSha.dictPlayerAlliance, 1 },
                { LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.God },
            };
        private static List<int> actionOrders = new()
            { (int)ActionConstant.ShouWei_OpenEyes, (int)ActionConstant.ShouWei_Act, (int)ActionConstant.ShouWei_CloseEyes };

        public ShouWei()
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
                return "ShouWei";
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

        public static string dictGuardTarget = "shouwei_target";
        public static string dictLastGuardTarget = "shouwei_last_target";

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
                }
                return GameActionResult.Continue;
            }

            // Announcer actions for open/close eyes (uses generic 50/51 hints)
            if (LangRenSha.AnnouncerAction(game, update, false, ActionOrders[0], ActionOrders[2], 50, 51, Name, 4) == GameActionResult.Restart)
            {
                return GameActionResult.Restart;
            }

            // Guard action - choose a target to guard
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == ActionOrders[1])
            {
                var shouWei = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
                var shouWeiAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
                var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
                var actionDuration = Game.GetGameDictionaryProperty(game, LangRenSha.dictDurationPlayerReact, ActionDuration);

                if (shouWeiAlive.Count == 0)
                {
                    actionDuration = new Random().Next(3, 6);
                }
                if (UserAction.EndUserAction(game, update))
                {
                    // Timeout - set skippedAct to true (not acted)
                    if (shouWeiAlive.Count > 0)
                    {
                        AwkSheMengRen.SetSkippedAct(game, shouWeiAlive[0], true, update);
                    }
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, actionDuration, update))
                    {
                        // Get last guard target - cannot guard same person twice in a row
                        var lastTarget = Game.GetGameDictionaryProperty(game, dictLastGuardTarget, 0);

                        // Build list of valid targets (alive players except last guarded)
                        var targets = new List<int>();
                        foreach (var target in alivePlayers)
                        {
                            if (target != lastTarget)
                            {
                                targets.Add(target);
                            }
                        }

                        // Add -100 option to not use skill
                        targets.Add(-100);

                        update[UserAction.dictUserActionTargets] = targets;
                        update[UserAction.dictUserActionUsers] = shouWei;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.ShouWei_Act;
                        update[UserAction.dictUserActionRole] = Name;
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, shouWeiAlive, update);
                        if (inputValid)
                        {
                            var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.Input, -1);
                            if (targets.Count == 1)
                            {
                                if (targets[0] == -100)
                                {
                                    // Player chose to not use the skill, end action without storing target
                                    update[dictGuardTarget] = 0;
                                    update[dictLastGuardTarget] = 0; // Clear last target since skill was not used
                                    // Set skippedAct for ShouWei
                                    if (shouWeiAlive.Count > 0)
                                    {
                                        AwkSheMengRen.SetSkippedAct(game, shouWeiAlive[0], true, update);
                                    }
                                    UserAction.EndUserAction(game, update, true);
                                    LangRenSha.AdvanceAction(game, update);
                                    return GameActionResult.Restart;
                                }
                                if (targets[0] > 0)
                                {
                                    // Store the guard target
                                    update[dictGuardTarget] = targets[0];
                                    update[dictLastGuardTarget] = targets[0];
                                    // Set skippedAct for ShouWei (not skipped)
                                    if (shouWeiAlive.Count > 0)
                                    {
                                        AwkSheMengRen.SetSkippedAct(game, shouWeiAlive[0], false, update);
                                    }
                                    UserAction.EndUserAction(game, update, true);
                                    LangRenSha.AdvanceAction(game, update);
                                    return GameActionResult.Restart;
                                }
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
