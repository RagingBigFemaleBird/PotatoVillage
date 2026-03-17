using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcedureCore.LangRenSha
{
    /// <summary>
    /// TongLingShi (通灵师 - Prophet) role.
    /// Similar to YuYanJia but always uses TongLing (reveals exact role) instead of ChaYan.
    /// </summary>
    public class TongLingShi : Role
    {
        public static string dictTongLingResult = "tongling_result";
        public static string dictTongLingShiResult = "tongling";


        private static Dictionary<string, object> roleDict = new()
        {
            { YuYanJia.dictYuYanJiaResult, 1 },
            { LangRenSha.dictPlayerAlliance, 1 },
            { LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.God },
        };

        private static List<int> actionOrders = new()
        {
            (int)ActionConstant.TongLingShi_OpenEyes,
            (int)ActionConstant.TongLingShi_ChaYan,
            (int)ActionConstant.TongLingShi_Result,
            (int)ActionConstant.TongLingShi_CloseEyes
        };

        public TongLingShi() { }

        public Dictionary<string, object> RoleDict
        {
            get { return roleDict; }
        }

        public string Name
        {
            get { return "TongLingShi"; }
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
            get { return 30; }
        }

        /// <summary>
        /// TongLing reveals the exact role of the target player.
        /// </summary>
        public void TongLing(Game game, int target, Dictionary<string, object> update)
        {
            var tongling_result = LangRenSha.GetPlayerProperty(game, target, TongLingShi.dictTongLingShiResult, "");
            if (string.IsNullOrEmpty(tongling_result))
            {
                tongling_result = LangRenSha.GetPlayerProperty(game, target, LangRenSha.dictRole, "");
            }
            update[dictTongLingResult] = tongling_result;
        }

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

            // Open/close eyes announcer
            if (LangRenSha.AnnouncerAction(game, update, false, ActionOrders[0], ActionOrders[3], 
                (int)HintConstant.OpenEyes, (int)HintConstant.CloseEyes, Name, 4) == GameActionResult.Restart)
            {
                return GameActionResult.Restart;
            }

            // Action: TongLingShi selects target to reveal
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == ActionOrders[1])
            {
                var tongLingShi = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
                var tongLingShiAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
                var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
                var actionDuration = Game.GetGameDictionaryProperty(game, LangRenSha.dictDurationPlayerReact, ActionDuration);
                
                if (tongLingShiAlive.Count == 0)
                {
                    actionDuration = new Random().Next(3, 6);
                }

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
                        update[UserAction.dictUserActionUsers] = tongLingShi;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.TongLingShi_ChaYan;
                        update[UserAction.dictUserActionRole] = Name;
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, tongLingShiAlive, update);
                        if (inputValid)
                        {
                            var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, -1);
                            if (targets.Count == 0)
                            {
                                return GameActionResult.NotExecuted;
                            }
                            // Always use TongLing to reveal exact role
                            TongLing(game, targets[0], update);
                            UserAction.EndUserAction(game, update, true);
                            LangRenSha.AdvanceAction(game, update);
                            return GameActionResult.Restart;
                        }
                    }
                }
                return GameActionResult.NotExecuted;
            }

            // Action: Show TongLing result
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == ActionOrders[2])
            {
                var tongLingShi = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
                var tongLingShiAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
                
                if (UserAction.EndUserAction(game, update))
                {
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, 5, update))
                    {
                        update[UserAction.dictUserActionTargets] = new List<int>() { 0 };
                        update[UserAction.dictUserActionUsers] = tongLingShi;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.TongLingShi_Result;
                        update[UserAction.dictUserActionRole] = Name;
                        update[UserAction.dictUserActionInfo] = $"{Game.GetGameDictionaryProperty(game, dictTongLingResult, "")}";
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, tongLingShiAlive, update);
                        if (inputValid)
                        {
                            var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, -1);
                            if (targets.Count == 0)
                            {
                                return GameActionResult.NotExecuted;
                            }
                            UserAction.EndUserAction(game, update, true);
                            LangRenSha.AdvanceAction(game, update);
                            return GameActionResult.Restart;
                        }
                    }
                }
                return GameActionResult.NotExecuted;
            }

            return GameActionResult.NotExecuted;
        }
    }
}
