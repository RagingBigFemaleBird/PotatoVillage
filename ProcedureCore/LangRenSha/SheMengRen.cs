using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcedureCore.LangRenSha
{
    public class SheMengRen : Role
    {
        private static Dictionary<string, object> roleDict = new()
        {
            { YuYanJia.dictYuYanJiaResult, 1 },
            { LangRenSha.dictPlayerAlliance, 1 },
            { LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.God },
        };

        private static List<int> actionOrders = new() { (int)ActionConstant.SheMengRen_OpenEyes, (int)ActionConstant.SheMengRen_Act, (int)ActionConstant.SheMengRen_CloseEyes };

        public SheMengRen()
        {
        }

        public Dictionary<string, object> RoleDict
        {
            get { return roleDict; }
        }

        public string Name
        {
            get { return "SheMengRen"; }
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
            get { return 20; }
        }

        // Dictionary keys for SheMengRen state
        public static string dictSheMengTarget = "shemengren_target";
        public static string dictSheMengPrevTarget = "shemengren_prev_target";

        public GameActionResult GenerateStateDiff(Game game, Dictionary<string, object> update)
        {
            if (game.StateSequenceNumber == 1)
            {
                // Add SheMengRen's actions to night orders
                var addSelf = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
                if (addSelf.Count > 0)
                {
                    var no = Game.GetGameDictionaryProperty(game, LangRenSha.dictNightOrders, new List<int>());
                    no.AddRange(ActionOrders);
                    update[LangRenSha.dictNightOrders] = no;
                }
                return GameActionResult.Continue;
            }

            // Actions 40 and 42 are announcer actions (open eyes, close eyes)
            if (LangRenSha.AnnouncerAction(game, update, false, ActionOrders[0], ActionOrders[2], 50, 51, Name, 4) == GameActionResult.Restart)
            {
                return GameActionResult.Restart;
            }

            // Action 41: SheMengRen selects a target for protection
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == ActionOrders[1])
            {
                var sheMengRen = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
                var sheMengRenAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);

                var actionDuration = Game.GetGameDictionaryProperty(game, LangRenSha.dictDurationPlayerReact, ActionDuration);
                if (sheMengRenAlive.Count == 0)
                {
                    actionDuration = new Random().Next(3, 6);
                }

                var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
                var sheMengRenPlayer = sheMengRenAlive.Count > 0 ? sheMengRenAlive[0] : 0;
                alivePlayers.Remove(sheMengRenPlayer); // Cannot protect self

                if (UserAction.EndUserAction(game, update))
                {
                    // Time's up - get final response and process
                    (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, sheMengRenAlive, update);
                    int target = alivePlayers[0]; // Must use skill
                    if (inputValid && sheMengRenAlive.Count > 0)
                    {
                        var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, -1);
                        if (targets.Count > 0 && targets[0] > 0)
                        {
                            target = targets[0];
                        }
                        ProcessSheMengTarget(game, sheMengRenPlayer, target, update);
                    }
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, actionDuration, update))
                    {
                        // Action just started - setup targets and users
                        update[UserAction.dictUserActionTargets] = sheMengRenAlive.Count > 0 ? alivePlayers : new List<int>();
                        update[UserAction.dictUserActionUsers] = sheMengRen;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.SheMengRen_Act;
                        update[UserAction.dictUserActionRole] = Name;
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        // Action in progress - check for early completion
                        (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, sheMengRenAlive, update);
                        if (inputValid)
                        {
                            var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, -1);
                            if (targets.Count > 0 && targets[0] > 0 && sheMengRenAlive.Count > 0)
                            {
                                ProcessSheMengTarget(game, sheMengRenPlayer, targets[0], update);
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

        private void ProcessSheMengTarget(Game game, int sheMengRenPlayer, int target, Dictionary<string, object> update)
        {
            var prevTarget = Game.GetGameDictionaryProperty(game, dictSheMengPrevTarget, 0);

            // Check if same target as previous night
            if (prevTarget == target && prevTarget > 0)
            {
                // Same target selected twice in a row - target dies without death skills
                LangRenSha.MarkPlayerAboutToDie(game, target, update);
                
                // Disable hunter's shooting ability if the target is a hunter
                LangRenSha.SetPlayerProperty(game, target, LieRen.dictHuntingDisabled, 1, update);
            }
            else
            {
                // Mark target as protected for this night
                update[dictSheMengTarget] = target;
            }

            // Store current target as previous for next night
            update[dictSheMengPrevTarget] = target;
        }

    }
}
