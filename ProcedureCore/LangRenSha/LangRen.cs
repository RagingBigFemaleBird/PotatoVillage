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
        public static Dictionary<string, object> roleDict;

        public LangRen()
        {
            roleDict = new Dictionary<string, object>()
            {
                { YuYanJia.dictYuYanJiaResult, 2 },
            };
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

        public int ActionOrder
        {
            get
            {
                return 100;
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
        public void Sha(Game game, List<int> targets, Dictionary<string, object> update)
        {
            var attackTarget = new List<int>();
            var aboutToDie = new List<int>();
            if (game.StateDictionary.ContainsKey(dictAttackTarget))
            {
                attackTarget = (List<int>)game.StateDictionary[dictAttackTarget];
            }
            if (game.StateDictionary.ContainsKey(LangRenSha.dictAboutToDie))
            {
                aboutToDie = (List<int>)game.StateDictionary[LangRenSha.dictAboutToDie];
            }
            foreach (var target in targets)
            {
                attackTarget.Add(target);
                int guardTarget = -1;
                if (game.StateDictionary.ContainsKey(ShouWei.dictGuardTarget))
                {
                    guardTarget = (int)game.StateDictionary[ShouWei.dictGuardTarget];
                }
                if (guardTarget != target)
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

                var players = (Dictionary<string, object>)(game.StateDictionary[LangRenSha.dictPlayers]);
                foreach (var player in players)
                {
                    if (((Dictionary<string, object>)((Dictionary<string, object>)game.StateDictionary[LangRenSha.dictPlayers])[player.Key])[LangRenSha.dictRole].ToString() == Name)
                    {
                        // TODO: version mismatch
                        update[LangRenSha.dictNightOrders] = (List<int>)game.StateDictionary[LangRenSha.dictNightOrders];
                        ((List<int>)update[LangRenSha.dictNightOrders]).Add(ActionOrder);
                        break;
                    }
                }
                return GameActionResult.Continue;
            }

            if ((int)game.StateDictionary[LangRenSha.dictAction] == ActionOrder)
            {
                var langRen = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
                if (UserAction.EndUserAction(game, update))
                {
                    (var inputValid, var input) = UserAction.GetUserResponse(game, true, update);
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
                        update[UserAction.dictUserActionTargets] = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
                        update[UserAction.dictUserActionUsers] = langRen;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = 1;
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        (var inputValid, var input) = UserAction.GetUserResponse(game, false, update);
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
                                (inputValid, input) = UserAction.GetUserResponse(game, true, update);
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
