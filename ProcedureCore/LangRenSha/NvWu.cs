using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcedureCore.LangRenSha
{
    public class NvWu : Role
    {
        public static Dictionary<string, object> roleDict;

        public NvWu()
        {
            roleDict = new Dictionary<string, object>()
            {
                { YuYanJia.dictYuYanJiaResult, 1 },
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
                return "NvWu";
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
                return 110;
            }
        }

        public int ActionDuration
        {
            get
            {
                return 30;
            }
        }

        public static string dictCannotBePoisoned = "immune_poison";
        public static string dictReflectPoison = "reflect_poison";
        public static string dictPoisonUsed = "poison_used";
        public static string dictSaveUsed = "save_used";

        public void Poison(Game game, int target, Dictionary<string, object> update)
        {
            var players = (Dictionary<string, object>)(game.StateDictionary[LangRenSha.dictPlayers]);
            var aboutToDie = LangRenSha.GetListIntGameDictionaryProperty(game, LangRenSha.dictAboutToDie); ;
            var nvWu = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);

            // TODO: lie ren, she meng
            if (LangRenSha.GetPlayerProperty(game, target, dictCannotBePoisoned, 0) != 0)
            {
                if (!aboutToDie.Contains(target))
                {
                    aboutToDie.Add(target);
                    update[LangRenSha.dictAboutToDie] = aboutToDie;
                    LangRenSha.SetPlayerProperty(game, nvWu[0], dictPoisonUsed, 1, update);
                }
            }
        }

        public void Save(Game game, Dictionary<string, object> update)
        {
            var players = (Dictionary<string, object>)(game.StateDictionary[LangRenSha.dictPlayers]);
            var attackTarget = LangRenSha.GetListIntGameDictionaryProperty(game, LangRen.dictAttackTarget);
            var aboutToDie = LangRenSha.GetListIntGameDictionaryProperty(game, LangRenSha.dictAboutToDie);
            var nvWu = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
            if (attackTarget.Count > 0)
            {
                // TODO: shou wei
                if (aboutToDie.Contains(attackTarget[0]))
                {
                    aboutToDie.Remove(attackTarget[0]);
                    update[LangRenSha.dictAboutToDie] = aboutToDie;
                    LangRenSha.SetPlayerProperty(game, nvWu[0], dictSaveUsed, 1, update);
                }
            }
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
                var players = (Dictionary<string, object>)(game.StateDictionary[LangRenSha.dictPlayers]);
                var nvWu = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
                var alivePlyaers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);

                if (UserAction.EndUserAction(game, update))
                {
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, ActionDuration, update))
                    {
                        bool saveUsed = false;
                        bool poisonUsed = false;
                        bool selfAttacked = false;
                        var attackTarget = LangRenSha.GetListIntGameDictionaryProperty(game, LangRen.dictAttackTarget);
                        if (attackTarget.Count > 0 && attackTarget[0] == nvWu[0])
                        {
                            selfAttacked = true;
                        }
                        if (LangRenSha.GetPlayerProperty(game, nvWu[0], dictSaveUsed, 0) != 0)
                        {
                            saveUsed = true;
                        }
                        if (LangRenSha.GetPlayerProperty(game, nvWu[0], dictPoisonUsed, 0) != 0)
                        {
                            poisonUsed = true;
                        }
                        var targets = new List<int>();
                        if (!saveUsed && !selfAttacked)
                        {
                            targets.Add(0);
                        }
                        if (!poisonUsed)
                        {
                            targets.AddRange(alivePlyaers);
                        }
                        update[UserAction.dictUserActionTargets] = targets;
                        update[UserAction.dictUserActionUsers] = nvWu;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = 3;
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        (var inputValid, var input) = UserAction.GetUserResponse(game, true, update);
                        if (inputValid)
                        {
                            var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost);
                            if (targets[0] > 0)
                            {
                                Poison(game, targets[0], update);
                            }
                            else
                            {
                                Save(game, update);
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
