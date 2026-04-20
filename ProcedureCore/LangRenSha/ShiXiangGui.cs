using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcedureCore.LangRenSha
{
    /// <summary>
    /// ShiXiangGui (石像鬼 - Stone Gargoyle) role.
    /// Same as TongLingShi but with alliance = 2 (evil) and succession = 2.
    /// </summary>
    public class ShiXiangGui : Role
    {
        private static Dictionary<string, object> roleDict = new()
        {
            { YuYanJia.dictYuYanJiaResult, 1 },
            { LangRenSha.dictPlayerAlliance, 2 },
            { LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.Evil },
            { LangRen.dictSuceession, 2 },
        };

        private static List<int> actionOrders = new()
        {
            (int)ActionConstant.ShiXiangGui_OpenEyes,
            (int)ActionConstant.ShiXiangGui_ChaYan,
            (int)ActionConstant.ShiXiangGui_Result,
            (int)ActionConstant.ShiXiangGui_CloseEyes
        };

        public ShiXiangGui() { }

        public Dictionary<string, object> RoleDict
        {
            get { return roleDict; }
        }

        public string Name
        {
            get { return "ShiXiangGui"; }
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

            // Action: ShiXiangGui selects target to reveal
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == ActionOrders[1])
            {
                var shiXiangGui = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
                var shiXiangGuiAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
                var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
                var actionDuration = Game.GetGameDictionaryProperty(game, LangRenSha.dictDurationPlayerReact, ActionDuration);

                // Check if ShiXiangGui can attack (succession = 2, so only when normal LangRen and succession 1 are dead)
                var langRenAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == "LangRen" && (int)x[LangRenSha.dictAlive] == 1);
                var succession1Alive = LangRenSha.GetPlayers(game, x => x.ContainsKey(LangRen.dictSuceession) && (int)x[LangRen.dictSuceession] == 1 && (int)x[LangRenSha.dictAlive] == 1);
                var canAttack = langRenAlive.Count == 0 && succession1Alive.Count == 0;

                if (shiXiangGuiAlive.Count == 0)
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
                        // Info shows attack status
                        var attackStatusInfo = canAttack ? "Succession" : "";

                        update[UserAction.dictUserActionTargets] = alivePlayers;
                        update[UserAction.dictUserActionUsers] = shiXiangGui;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.ShiXiangGui_ChaYan;
                        update[UserAction.dictUserActionRole] = Name;
                        update[UserAction.dictUserActionInfo] = attackStatusInfo;
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, shiXiangGuiAlive, update);
                        if (inputValid)
                        {
                            var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, -1);
                            if (targets.Count == 0)
                            {
                                return GameActionResult.NotExecuted;
                            }
                            // Always use TongLing to reveal exact role (reuses YuYanJia's implementation)
                            var initiator = shiXiangGuiAlive.Count > 0 ? shiXiangGuiAlive[0] : 0;
                            new YuYanJia().TongLing(game, initiator, targets[0], update);
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
                var shiXiangGui = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
                var shiXiangGuiAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
                
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
                        update[UserAction.dictUserActionUsers] = shiXiangGui;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.ShiXiangGui_Result;
                        update[UserAction.dictUserActionRole] = Name;
                        var lastResult = shiXiangGuiAlive.Count > 0
                            ? LangRenSha.GetPlayerProperty(game, shiXiangGuiAlive[0], YuYanJia.dictYuYanJiaLastResult, "")
                            : "";
                        update[UserAction.dictUserActionInfo] = $"{lastResult}";
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, shiXiangGuiAlive, update);
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
