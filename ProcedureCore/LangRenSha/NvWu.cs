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
        private static Dictionary<string, object> roleDict;
        private static List<int> actionOrders;

        public NvWu()
        {
            roleDict = new Dictionary<string, object>()
            {
                { YuYanJia.dictYuYanJiaResult, 1 },
                { LangRenSha.dictPlayerAlliance, 1 },
            };
            actionOrders = new List<int> { 120 };

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

        public static string dictCannotBePoisoned = "immune_poison";
        public static string dictReflectPoison = "reflect_poison";
        public static string dictPoisonUsed = "poison_used";
        public static string dictSaveUsed = "save_used";

        public void Poison(Game game, int target, Dictionary<string, object> update)
        {
            var aboutToDie = Game.GetGameDictionaryProperty(game, LangRenSha.dictAboutToDie, new List<int>()); ;
            var nvWu = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);

            // TODO: lie ren, she meng
            if (LangRenSha.GetPlayerProperty(game, target, dictCannotBePoisoned, 0) == 0)
            {
                if (!aboutToDie.Contains(target))
                {
                    aboutToDie.Add(target);
                    update[LangRenSha.dictAboutToDie] = aboutToDie;
                }
            }
            LangRenSha.SetPlayerProperty(game, nvWu[0], dictPoisonUsed, 1, update);
        }

        public void Save(Game game, Dictionary<string, object> update)
        {
            var attackTarget = Game.GetGameDictionaryProperty(game, LangRen.dictAttackTarget, new List<int>());
            var aboutToDie = Game.GetGameDictionaryProperty(game, LangRenSha.dictAboutToDie, new List<int>());
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
                var nvWu = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
                var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);

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
                        var attackTarget = Game.GetGameDictionaryProperty(game, LangRen.dictAttackTarget, new List<int>());
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
                            targets.AddRange(alivePlayers);
                        }
                        update[UserAction.dictUserActionTargets] = targets;
                        update[UserAction.dictUserActionUsers] = nvWu;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = 3;
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        (var inputValid, var input) = UserAction.GetUserResponse(game, true, nvWu, update);
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
