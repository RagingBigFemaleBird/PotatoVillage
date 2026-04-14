using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace ProcedureCore.LangRenSha
{
    public class YuYanJia : Role
    {
        public static string dictYuYanJiaResult = "chayan";

        private static Dictionary<string, object> roleDict = new()
            {
                { YuYanJia.dictYuYanJiaResult, 1 },
                { LangRenSha.dictPlayerAlliance, 1 },
                { LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.God },
            };
        private static List<int> actionOrders = new()
            { (int)ActionConstant.YuYanJia_OpenEyes, (int)ActionConstant.YuYanJia_ChaYan, (int)ActionConstant.YuYanJia_Result, (int)ActionConstant.YuYanJia_CloseEyes };

        public YuYanJia()
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
                return "YuYanJia";
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

        public void ChaYan(Game game, int target, Dictionary<string, object> update)
        {
            var result = LangRenSha.GetPlayerProperty(game, target, dictYuYanJiaResult, 1);
            update[dictYuYanJiaResult] = result.ToString();
        }

        public void TongLing(Game game, int target, Dictionary<string, object> update)
        {
            var tongling_result = LangRenSha.GetPlayerProperty(game, target, TongLingShi.dictTongLingShiResult, "");
            if (string.IsNullOrEmpty(tongling_result))
            {
                tongling_result = LangRenSha.GetPlayerProperty(game, target, LangRenSha.dictRole, "");
            }
            update[dictYuYanJiaResult] = tongling_result;
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

            if (LangRenSha.AnnouncerAction(game, update, false, ActionOrders[0], ActionOrders[3], 50, 51, Name, 4) == GameActionResult.Restart)
            {
                return GameActionResult.Restart;
            }

            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == ActionOrders[1])
            {
                var yuYanJia = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
                var yuYanJiaAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
                var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
                var miceTagged = yuYanJiaAlive.Count > 0 ? (yuYanJiaAlive[0] == Game.GetGameDictionaryProperty(game, LaoShu.dictMiceTag, 0)) : false;
                var isEvil = yuYanJiaAlive.Count > 0 ? (LangRenSha.GetPlayerProperty(game, yuYanJiaAlive[0], LangRenSha.dictPlayerAlliance, 0) == 2) : false;
                var actionDuration = Game.GetGameDictionaryProperty(game, LangRenSha.dictDurationPlayerReact, ActionDuration);
                // Check if skill is disabled by MengYan
                var yuYanJiaPlayer = yuYanJia.Count > 0 ? yuYanJia[0] : 0;
                var skillDisabled = yuYanJiaPlayer > 0 && LangRenSha.GetPlayerProperty(game, yuYanJiaPlayer, LangRenSha.dictSkillTransformation, 0) == (int)LangRenSha.SkillTransformation.Disabled;

                if (yuYanJiaAlive.Count == 0 || skillDisabled)
                {
                    actionDuration = new Random().Next(3, 6);
                }

                if (UserAction.EndUserAction(game, update))
                {
                    // Reset skill transformation after action completes
                    if (yuYanJiaAlive.Count > 0)
                    {
                        LangRenSha.SetPlayerProperty(game, yuYanJiaAlive[0], LangRenSha.dictSkillTransformation, (int)LangRenSha.SkillTransformation.None, update);
                    }
                    update[dictYuYanJiaResult] = "";
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, actionDuration, update))
                    {

                        List<int> targets;
                        if (skillDisabled)
                        {
                            // Skill disabled - only allow -100 (do not use)
                            targets = new List<int> { -100 };
                            update[UserAction.dictUserActionInfo3] = "1"; // Indicate skill is disabled
                        }
                        else
                        {
                            targets = alivePlayers;
                        }
                        update[UserAction.dictUserActionTargets] = targets;
                        update[UserAction.dictUserActionUsers] = yuYanJia;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.YuYanJia_ChaYan;
                        update[UserAction.dictUserActionRole] = Name;
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, yuYanJiaAlive, update);
                        if (inputValid)
                        {
                            var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, -1);
                            if (targets.Count == 0)
                            {
                                return GameActionResult.NotExecuted;
                            }
                            if (targets[0] > 0)
                            {
                                if (miceTagged || isEvil)
                                {
                                    TongLing(game, targets[0], update);
                                }
                                else
                                {
                                    ChaYan(game, targets[0], update);
                                }
                            }
                            // Reset skill transformation after action completes
                            if (yuYanJiaAlive.Count > 0)
                            {
                                LangRenSha.SetPlayerProperty(game, yuYanJiaAlive[0], LangRenSha.dictSkillTransformation, (int)LangRenSha.SkillTransformation.None, update);
                            }
                            UserAction.EndUserAction(game, update, true);
                            LangRenSha.AdvanceAction(game, update);
                            return GameActionResult.Restart;
                        }
                    }
                }
                return GameActionResult.NotExecuted;
            }

            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == ActionOrders[2])
            {
                var yuYanJia = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
                var yuYanJiaAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
                var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
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
                        update[UserAction.dictUserActionUsers] = yuYanJia;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.YuYanJia_Result;
                        update[UserAction.dictUserActionRole] = Name;
                        update[UserAction.dictUserActionInfo] = $"{Game.GetGameDictionaryProperty(game, dictYuYanJiaResult, "")}";
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, yuYanJiaAlive, update);
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
