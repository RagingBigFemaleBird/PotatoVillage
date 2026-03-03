using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcedureCore.LangRenSha
{
    public class JiaMian : Role
    {
        private static Dictionary<string, object> roleDict = new()
            {
                { YuYanJia.dictYuYanJiaResult, 2 },
                { LangRenSha.dictPlayerAlliance, 2 },
                { NvWu.dictCannotBePoisoned, 1 },
                { LangRen.dictSuceession, 2 },
                { LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.Evil },
            };
        private static List<int> actionOrders = new()
            { (int)ActionConstant.JiaMian_OpenEyes, (int)ActionConstant.JiaMian_ChaYan, (int)ActionConstant.JiaMian_Reverse, (int)ActionConstant.JiaMian_CloseEyes };
        public JiaMian()
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
                return "JiaMian";
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

        public static string dictJiaMianChaYan = "jiamian_chayan";
        public static string dictJiaMianReversed = "jiamian_reversed";

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

            if (LangRenSha.AnnouncerAction(game, update, true, ActionOrders[0], ActionOrders[3], 50, 51, Name, 4) == GameActionResult.Restart)
            {
                return GameActionResult.Restart;
            }

            var actionDuration = Game.GetGameDictionaryProperty(game, LangRenSha.dictDurationPlayerReact, ActionDuration) + 5;

            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == ActionOrders[1])
            {
                var jiaMian = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
                var jiaMianAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
                var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);

                if (Game.GetGameDictionaryProperty(game, LangRenSha.dictDay, 0) == 0 || UserAction.EndUserAction(game, update))
                {
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                else
                {
                    var langRenAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == "LangRen" && (int)x[LangRenSha.dictAlive] == 1);
                    var langRenSuccession1Alive = LangRenSha.GetPlayers(game, x => x.ContainsKey(LangRen.dictSuceession) && (int)x[LangRen.dictSuceession] == 1 && (int)x[LangRenSha.dictAlive] == 1);

                    if (UserAction.StartUserAction(game, actionDuration, update))
                    {
                        update[UserAction.dictUserActionTargets] = alivePlayers;
                        update[UserAction.dictUserActionUsers] = jiaMian;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.JiaMian_OpenEyes;
                        update[UserAction.dictUserActionRole] = Name;
                        if (langRenAlive.Count + langRenSuccession1Alive.Count == 0)
                        {
                            update[UserAction.dictUserActionInfo] = "Succession";
                        }
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, jiaMianAlive, update);
                        if (inputValid)
                        {
                            var danced = Game.GetGameDictionaryProperty(game, WuZhe.dictDanced, new List<int>());
                            var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, -1);
                            if (targets.Count == 0)
                            {
                                return GameActionResult.NotExecuted;
                            }
                            if (danced.Contains(targets[0]))
                            {
                                update[dictJiaMianChaYan] = 1;
                            }
                            else
                            {
                                update[dictJiaMianChaYan] = 0;
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
                var jiaMian = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
                var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);

                if (Game.GetGameDictionaryProperty(game, LangRenSha.dictDay, 0) == 0 || UserAction.EndUserAction(game, update))
                {
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, actionDuration, update))
                    {
                        update[UserAction.dictUserActionTargets] = alivePlayers;
                        update[UserAction.dictUserActionUsers] = jiaMian;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.JiaMian_ChaYan;
                        update[UserAction.dictUserActionRole] = Name;
                        var chayan = Game.GetGameDictionaryProperty(game, dictJiaMianChaYan, 0);
                        update[UserAction.dictUserActionInfo] = chayan.ToString();
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, jiaMian, update);
                        if (inputValid)
                        {
                            var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, -1);
                            if (targets.Count == 0)
                            {
                                return GameActionResult.NotExecuted;
                            }
                            update[dictJiaMianReversed] = targets[0];
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
