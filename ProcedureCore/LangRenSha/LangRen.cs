using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading.Tasks;

namespace ProcedureCore.LangRenSha
{
    public class LangRen : Role
    {
        private static Dictionary<string, object> roleDict;
        private static List<int> actionOrders;

        public LangRen()
        {
            roleDict = new Dictionary<string, object>()
            {
                { YuYanJia.dictYuYanJiaResult, 2 },
                { LangRenSha.dictPlayerAlliance, 2 },

            };
            actionOrders = new List<int> { 100 };
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
                return "LangRen";
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
                return 15;
            }
        }

        public static string dictAttackTarget = "attack_target";
        public static string dictSuceession = "lang_succession";

        public void Sha(Game game, List<int> targets, Dictionary<string, object> update)
        {
            var attackTarget = Game.GetGameDictionaryProperty(game, dictAttackTarget, new List<int>());
            var aboutToDie = Game.GetGameDictionaryProperty(game, LangRenSha.dictAboutToDie, new List<int>());
            foreach (var target in targets)
            {
                attackTarget.Add(target);
                int guardTarget = -1;
                var shouWei = Game.GetGameDictionaryProperty(game, ShouWei.dictGuardTarget, 0);
                var wuZhe = Game.GetGameDictionaryProperty(game, WuZhe.dictDanced, new List<int>());
                
                if (guardTarget != target && !wuZhe.Contains(target) && !aboutToDie.Contains(target))
                {
                    aboutToDie.Add(target);
                }
            }
            update[dictAttackTarget] = attackTarget;
            update[LangRenSha.dictAboutToDie] = aboutToDie;
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

            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == ActionOrders[0])
            {
                var langRen = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
                if (langRen.Count == 0)
                {
                    langRen = LangRenSha.GetPlayers(game, x => x.ContainsKey(dictSuceession) && (int)x[dictSuceession] == 1);
                }
                if (langRen.Count == 0)
                {
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
                if (UserAction.EndUserAction(game, update))
                {
                    (var inputValid, var input) = UserAction.GetUserResponse(game, true, langRen, update);
                    if (inputValid)
                    {
                        var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost);
                        var choose = new List<int>();
                        if (targets.Count > 0)
                        {
                            choose.Add(targets[0]);
                        }
                        Sha(game, choose, update);
                    }
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, ActionDuration, update))
                    {
                        update[UserAction.dictUserActionTargets] = alivePlayers;
                        update[UserAction.dictUserActionUsers] = langRen;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = 1;
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        (var inputValid, var input) = UserAction.GetUserResponse(game, false, langRen, update);
                        if (inputValid)
                        {
                            bool allResponded = true;
                            foreach (var lang in langRen)
                            {
                                if (!input.ContainsKey(lang.ToString()))
                                {
                                    allResponded = false;
                                    break;
                                }
                            }
                            if (allResponded)
                            {
                                (inputValid, input) = UserAction.GetUserResponse(game, true, langRen, update);
                                var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost);
                                var choose = new List<int>();
                                if (targets.Count > 0)
                                {
                                    choose.Add(targets[0]);
                                }
                                Sha(game, choose, update);
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
