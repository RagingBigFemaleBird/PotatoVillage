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
        private static Dictionary<string, object> roleDict = new()
            {
                { YuYanJia.dictYuYanJiaResult, 1 },
                { LangRenSha.dictPlayerAlliance, 1 },
                { LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.God },
            };
        private static List<int> actionOrders = new()
            { (int)ActionConstant.NvWu_OpenEyes, (int)ActionConstant.NvWu_Act, (int)ActionConstant.NvWu_CloseEyes };

        public NvWu()
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

        public void Poison(Game game, int source, int target, Dictionary<string, object> update)
        {
            // Guard / immunity checks use the ORIGINAL target (what NvWu chose);
            // only the actual kill is redirected to the MoShuShi-swapped target.
            var originalTarget = target;
            var actualTarget = MoShuShi.GetSwappedTarget(game, target);

            var aboutToDie = Game.GetGameDictionaryProperty(game, LangRenSha.dictAboutToDie, new List<int>()); ;
            var miceTag = Game.GetGameDictionaryProperty(game, LaoShu.dictMiceTag, 0);
            var miceTagged = miceTag == source;

            // SuperGuard from JiXieLang: blocks poison and reflects it back to NvWu (unless ZhuangJiaLang present)
            var superGuardTarget = Game.GetGameDictionaryProperty(game, JiXieLang.dictSuperGuardTarget, 0);
            if (originalTarget == superGuardTarget && superGuardTarget > 0)
            {
                // When ZhuangJiaLang is present, SuperGuard only protects (no reflection)
                if (!ZhuangJiaLang.IsPresent(game))
                {
                    LangRenSha.ChainKill(game, source, source, aboutToDie, update);
                    update[LangRenSha.dictAboutToDie] = aboutToDie;
                }
                if (!miceTagged)
                {
                    LangRenSha.SetPlayerProperty(game, source, dictPoisonUsed, 1, update);
                }
                return;
            }

            // SuperGuard from ZhuangJiaLang: blocks poison but does NOT reflect
            var zjlSuperGuardTarget = Game.GetGameDictionaryProperty(game, ZhuangJiaLang.dictSuperGuardTarget, 0);
            if (originalTarget == zjlSuperGuardTarget && zjlSuperGuardTarget > 0)
            {
                if (!miceTagged)
                {
                    LangRenSha.SetPlayerProperty(game, source, dictPoisonUsed, 1, update);
                }
                return;
            }

            if (LangRenSha.GetPlayerProperty(game, actualTarget, dictCannotBePoisoned, 0) == 0)
            {
                // MoShuShi swap: kill the actual (swapped) target
                LangRenSha.ChainKill(game, source, actualTarget, aboutToDie, update);
                if (aboutToDie.Contains(actualTarget))
                {
                    LangRenSha.SetPlayerProperty(game, actualTarget, LieRen.dictHuntingDisabled, 1, update);
                }
                update[LangRenSha.dictAboutToDie] = aboutToDie;
            }


            if (!miceTagged)
            {
                LangRenSha.SetPlayerProperty(game, source, dictPoisonUsed, 1, update);
            }
        }

        public void Save(Game game, Dictionary<string, object> update)
        {
            var attackTarget = Game.GetGameDictionaryProperty(game, LangRen.dictAttackTarget, new List<int>());
            var nvWu = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
            var miceTagged = Game.GetGameDictionaryProperty(game, LaoShu.dictMiceTag, 0) == nvWu[0];
            var guardTarget = Game.GetGameDictionaryProperty(game, ShouWei.dictGuardTarget, 0);

            if (attackTarget.Count > 0)
            {
                if (attackTarget[0] != guardTarget)
                {
                    attackTarget.Remove(attackTarget[0]);
                    update[LangRen.dictAttackTarget] = attackTarget;
                }
                else
                {
                    var aboutToDie = Game.GetGameDictionaryProperty(game, LangRenSha.dictAboutToDie, new List<int>());
                    if (!aboutToDie.Contains(guardTarget))
                    {
                        aboutToDie.Add(guardTarget);
                        update[LangRenSha.dictAboutToDie] = aboutToDie;
                    }
                }
                if (!miceTagged)
                {
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

            var thiefPresent = Game.GetGameDictionaryProperty(game, Thief.dictThiefPlayer, 0) > 0;

            if (LangRenSha.AnnouncerAction(game, update, thiefPresent, ActionOrders[0], ActionOrders[2], 50, 51, Name, 4) == GameActionResult.Restart)
            {
                return GameActionResult.Restart;
            }


            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == ActionOrders[1])
            {
                var nvWu = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
                var nvWuAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
                var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
                var actionDuration = Game.GetGameDictionaryProperty(game, LangRenSha.dictDurationPlayerReact, ActionDuration);
                var day0 = Game.GetGameDictionaryProperty(game, LangRenSha.dictDay, 0) == 0;

                if (nvWuAlive.Count == 0)
                {
                    actionDuration = new Random().Next(3, 6);
                }

                if ((day0 && thiefPresent) || UserAction.EndUserAction(game, update))
                {
                    (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, nvWuAlive, update);
                    if (inputValid)
                    {
                        var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, -1);
                        bool skippedAct = targets.Count == 0 || targets[0] == -100;
                        if (targets.Count > 0)
                        {
                            if (targets[0] > 0)
                            {
                                Poison(game, nvWuAlive[0], targets[0], update);
                                skippedAct = false;
                            }
                            else if (targets[0] == 0)
                            {
                                Save(game, update);
                                skippedAct = false;
                            }
                        }
                        // Set skippedAct for NvWu
                        if (nvWuAlive.Count > 0)
                        {
                            AwkSheMengRen.SetSkippedAct(game, nvWuAlive[0], skippedAct, update);
                            // Reset skill transformation after action completes
                            LangRenSha.SetPlayerProperty(game, nvWuAlive[0], LangRenSha.dictSkillTransformation, (int)LangRenSha.SkillTransformation.None, update);
                        }
                        UserAction.EndUserAction(game, update, true);
                        LangRenSha.AdvanceAction(game, update);
                        return GameActionResult.Restart;
                    }

                    // Timeout without input - set skippedAct to true
                    if (nvWuAlive.Count > 0)
                    {
                        AwkSheMengRen.SetSkippedAct(game, nvWuAlive[0], true, update);
                        // Reset skill transformation after action completes
                        LangRenSha.SetPlayerProperty(game, nvWuAlive[0], LangRenSha.dictSkillTransformation, (int)LangRenSha.SkillTransformation.None, update);
                    }
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                else
                {

                    if (UserAction.StartUserAction(game, actionDuration, update))
                    {
                        bool saveUsed = false;
                        bool poisonUsed = false;
                        bool selfAttacked = false;
                        var attackTarget = Game.GetGameDictionaryProperty(game, LangRen.dictAttackTarget, new List<int>());
                        var nvWuPlayer = nvWu.Count > 0 ? nvWu[0] : 0;
                        if (attackTarget.Count > 0 && attackTarget[0] == nvWuPlayer)
                        {
                            selfAttacked = true;
                        }
                        if (thiefPresent || nvWuPlayer == 0 || LangRenSha.GetPlayerProperty(game, nvWuPlayer, dictSaveUsed, 0) != 0)
                        {
                            saveUsed = true;
                        }
                        if (nvWuPlayer == 0 || LangRenSha.GetPlayerProperty(game, nvWuPlayer, dictPoisonUsed, 0) != 0)
                        {
                            poisonUsed = true;
                        }

                        // Check if skill is disabled by MengYan
                        var skillDisabled = nvWuPlayer > 0 && LangRenSha.GetPlayerProperty(game, nvWuPlayer, LangRenSha.dictSkillTransformation, 0) == (int)LangRenSha.SkillTransformation.Disabled;

                        var targets = new List<int>();
                        if (skillDisabled)
                        {
                            // Skill disabled - only allow -100 (do not use)
                            targets.Add(-100);
                            update[UserAction.dictUserActionInfo3] = "1"; // Indicate skill is disabled
                        }
                        else
                        {
                            if (!saveUsed && !selfAttacked)
                            {
                                targets.Add(0);
                            }
                            if (!poisonUsed)
                            {
                                targets.AddRange(alivePlayers);
                            }
                            targets.Add(-100);
                        }
                        update[UserAction.dictUserActionTargets] = targets;
                        update[UserAction.dictUserActionUsers] = nvWu;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.NvWu_Act;
                        update[UserAction.dictUserActionRole] = Name;
                        var at = attackTarget.Count > 0 ? attackTarget[0] : 0;
                        update[UserAction.dictUserActionInfo] = $"{(saveUsed ? 0 : at)}";
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        if (!day0 || actionDuration > 30)
                        {
                            (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, nvWuAlive, update);
                            if (inputValid)
                            {
                                var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, -1);
                                if (targets.Count == 0)
                                {
                                    return GameActionResult.NotExecuted;
                                }
                                bool skippedAct = targets[0] == -100;
                                if (targets[0] > 0)
                                {
                                    Poison(game, nvWuAlive[0], targets[0], update);
                                    skippedAct = false;
                                }
                                else if (targets[0] == 0)
                                {
                                    Save(game, update);
                                    skippedAct = false;
                                }
                                // Set skippedAct for NvWu
                                if (nvWuAlive.Count > 0)
                                {
                                    AwkSheMengRen.SetSkippedAct(game, nvWuAlive[0], skippedAct, update);
                                    // Reset skill transformation after action completes
                                    LangRenSha.SetPlayerProperty(game, nvWuAlive[0], LangRenSha.dictSkillTransformation, (int)LangRenSha.SkillTransformation.None, update);
                                }
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
