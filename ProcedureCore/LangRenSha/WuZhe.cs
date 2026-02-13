using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcedureCore.LangRenSha
{
    public class WuZhe : Role
    {
        private static Dictionary<string, object> roleDict = new()
            {
                { YuYanJia.dictYuYanJiaResult, 1 },
                { LangRenSha.dictPlayerAlliance, 1 },
                { NvWu.dictCannotBePoisoned, 1 },
            };
        private static List<int> actionOrders = new()
            { 59, 60, 61, 130 };
        public WuZhe()
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
                return "WuZhe";
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
                return 60;
            }
        }

        public static string dictDanced = "danced";

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

            if (LangRenSha.AnnouncerAction(game, update, true, ActionOrders[0], ActionOrders[2], 50, 51, Name, 4) == GameActionResult.Restart)
            {
                return GameActionResult.Restart;
            }


            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == ActionOrders[1])
            {
                var wuZhe = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
                var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);

                if (Game.GetGameDictionaryProperty(game, LangRenSha.dictDay, 0) == 0 || UserAction.EndUserAction(game, update))
                {
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, ActionDuration, update))
                    {
                        var danced = LangRenSha.GetPlayerProperty(game, wuZhe[0], dictDanced, new List<int>());
                        
                        var targets = new List<int>();
                        foreach (var target in alivePlayers)
                        {
                            if (!danced.Contains(target))
                            {
                                targets.Add(target);
                            }
                        }
                        update[UserAction.dictUserActionTargets] = targets;
                        update[UserAction.dictUserActionUsers] = wuZhe;
                        update[UserAction.dictUserActionTargetsCount] = 3;
                        update[UserAction.dictUserActionTargetsHint] = 4;
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, wuZhe, update);
                        if (inputValid)
                        {
                            var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.Input, -1);
                            if (targets.Count == 3 && targets[0] != targets[1] && targets[1] != targets[2] && targets[2] != targets[0])
                            {
                                var danced = LangRenSha.GetPlayerProperty(game, wuZhe[0], dictDanced, new List<int>());
                                danced.AddRange(targets);
                                LangRenSha.SetPlayerProperty(game, wuZhe[0], dictDanced, danced, update);
                                update[dictDanced] = targets;
                            }
                            UserAction.EndUserAction(game, update, true);
                            LangRenSha.AdvanceAction(game, update);
                            return GameActionResult.Restart;
                        }
                    }
                }
                return GameActionResult.NotExecuted;
            }

            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == ActionOrders[3])
            {
                var aboutToDie = Game.GetGameDictionaryProperty(game, LangRenSha.dictAboutToDie, new List<int>()); ;

                var danced = Game.GetGameDictionaryProperty(game, dictDanced, new List<int>());
                var vote = new Dictionary<int, int>();
                foreach (var target in danced)
                {
                    var v = LangRenSha.GetPlayerProperty(game, target, LangRenSha.dictPlayerAlliance, 1);
                    if (Game.GetGameDictionaryProperty(game, JiaMian.dictJiaMianReversed, 0) == target)
                    {
                        v = 3 - v;
                    }
                    if (!vote.ContainsKey(v))
                    {
                        vote[v] = 0;
                    }
                    vote[v]++;
                }
                foreach (var target in danced)
                {
                    var v = LangRenSha.GetPlayerProperty(game, target, LangRenSha.dictPlayerAlliance, 1);
                    if (Game.GetGameDictionaryProperty(game, JiaMian.dictJiaMianReversed, 0) == target)
                    {
                        v = 3 - v;
                    }
                    if (vote[v] == 1)
                    {
                        if (!aboutToDie.Contains(target))
                        {
                            aboutToDie.Add(target);
                        }
                        update[LangRenSha.dictAboutToDie] = aboutToDie;
                    }
                }
                update[JiaMian.dictJiaMianReversed] = 0;
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }

            return GameActionResult.NotExecuted;
        }


    }
}
