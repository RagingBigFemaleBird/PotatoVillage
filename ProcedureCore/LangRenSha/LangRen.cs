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
        private static Dictionary<string, object> roleDict = new()
            {
                { YuYanJia.dictYuYanJiaResult, 2 },
                { LangRenSha.dictPlayerAlliance, 2 },
                { LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.Evil },
            };
        private static List<int> actionOrders = new()
            { 99, 100, 101 };

        public LangRen()
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

        public static GameActionResult RevealSelf(Game game, int player, List<int> targets, Dictionary<string, object> update)
        {
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictSpeak, 0) == 1 || Game.GetGameDictionaryProperty(game, LangRenSha.dictSpeak, 0) == 31)
            {
                var langRen = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == "LangRen" && (int)x[LangRenSha.dictAlive] == 1);
                if (langRen.Contains(player))
                {
                    if (targets.Contains(-10))
                    {
                        LangRenSha.MarkPlayerAboutToDie(game, player, update);
                        update[LangRenSha.dictSkipDaySpeech] = 1;
                        var interrupted = new Dictionary<string, object>();
                        var currentSpeak = Game.GetGameDictionaryProperty(game, LangRenSha.dictSpeak, 0);
                        interrupted[LangRenSha.dictSpeak] = 9;
                        update[LangRenSha.dictSpeak] = 97;
                        update[LangRenSha.dictInterrupt] = interrupted;
                        return GameActionResult.Restart;
                    }    
                }
            }
            return GameActionResult.NotExecuted;
        }

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
                var miceTag = Game.GetGameDictionaryProperty(game, LaoShu.dictMiceTag, 0);
                var laoShu = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == "LaoShu");
                var laoShuPlayer = laoShu.Count > 0 ? laoShu[0] : -1;

                if (guardTarget != target && !wuZhe.Contains(target) && (target != laoShuPlayer || miceTag == laoShuPlayer) && !aboutToDie.Contains(target))
                {
                    aboutToDie.Add(target);
                }
                if (guardTarget != target && !wuZhe.Contains(target) && (target == miceTag) && !aboutToDie.Contains(laoShuPlayer))
                {
                    aboutToDie.Add(laoShuPlayer);
                }

            }
            update[dictAttackTarget] = attackTarget;
            update[LangRenSha.dictAboutToDie] = aboutToDie;
        }
        public GameActionResult GenerateStateDiff(Game game, Dictionary<string, object> update)
        {
            if (game.StateSequenceNumber == 1)
            {
                // Langren always added
                var no = Game.GetGameDictionaryProperty(game, LangRenSha.dictNightOrders, new List<int>());
                no.AddRange(ActionOrders);
                update[LangRenSha.dictNightOrders] = no;
                return GameActionResult.Continue;
            }
            if (LangRenSha.AnnouncerAction(game, update, false, ActionOrders[0], ActionOrders[2], 50, 51, Name, 4) == GameActionResult.Restart)
            {
                return GameActionResult.Restart;
            }

            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == ActionOrders[1])
            {
                var langRen = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
                var langRenSuccession1 = LangRenSha.GetPlayers(game, x => x.ContainsKey(dictSuceession) && (int)x[dictSuceession] == 1);
                var langRenSuccession2 = LangRenSha.GetPlayers(game, x => x.ContainsKey(dictSuceession) && (int)x[dictSuceession] == 2);
                var langRenSuccession3 = LangRenSha.GetPlayers(game, x => x.ContainsKey(dictSuceession) && (int)x[dictSuceession] == 3);
                var langRenAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
                var langRenSuccession1Alive = LangRenSha.GetPlayers(game, x => x.ContainsKey(dictSuceession) && (int)x[dictSuceession] == 1 && (int)x[LangRenSha.dictAlive] == 1);
                var langRenSuccession2Alive = LangRenSha.GetPlayers(game, x => x.ContainsKey(dictSuceession) && (int)x[dictSuceession] == 2 && (int)x[LangRenSha.dictAlive] == 1);
                var langRenSuccession3Alive = LangRenSha.GetPlayers(game, x => x.ContainsKey(dictSuceession) && (int)x[dictSuceession] == 3 && (int)x[LangRenSha.dictAlive] == 1);

                langRen.AddRange(langRenSuccession1);
                langRenAlive.AddRange(langRenSuccession1Alive);

                if (langRenAlive.Count == 0)
                {
                    langRen = langRenSuccession2;
                    langRenAlive = langRenSuccession2Alive;
                }
                if (langRenAlive.Count == 0)
                {
                    langRen = langRenSuccession3;
                    langRenAlive = langRenSuccession3Alive;
                }
                if (langRenAlive.Count == 0)
                {
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }

                var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
                if (UserAction.EndUserAction(game, update))
                {
                    (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, langRenAlive, update);
                    if (inputValid)
                    {
                        var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, -1);
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
                    var actionDuration = Game.GetGameDictionaryProperty(game, LangRenSha.dictDurationLangRen, ActionDuration);

                    if (UserAction.StartUserAction(game, actionDuration, update))
                    {
                        update[UserAction.dictUserActionTargets] = alivePlayers;
                        update[UserAction.dictUserActionUsers] = langRen;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = 1;
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, false, langRenAlive, update);
                        if (inputValid)
                        {
                            bool allResponded = true;
                            foreach (var lang in langRenAlive)
                            {
                                if (!input.ContainsKey(lang.ToString()))
                                {
                                    allResponded = false;
                                    break;
                                }
                            }
                            if (allResponded)
                            {
                                (inputValid, input, input_others) = UserAction.GetUserResponse(game, true, langRenAlive, update);
                                var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, -1);
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
