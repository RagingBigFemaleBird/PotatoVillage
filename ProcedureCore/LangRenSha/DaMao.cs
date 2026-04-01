using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcedureCore.LangRenSha
{
    public class DaMao : Role
    {
        public static string dictCatMark = "cat_mark";
        public static string dictCatTrapped = "cat_trapped";

        private static Dictionary<string, object> roleDict = new()
            {
                { YuYanJia.dictYuYanJiaResult, 1 },
                { LangRenSha.dictPlayerAlliance, 2 },
                { LangRen.dictSuceession, 2 },
                { LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.Evil },
            };
        private static List<int> actionOrders = new()
            { (int)ActionConstant.DaMao_OpenEyes, (int)ActionConstant.DaMao_Act, (int)ActionConstant.DaMao_CloseEyes };

        public DaMao()
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
                return "DaMao";
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

            // Actions 20 and 22 are announcer actions
            if (LangRenSha.AnnouncerAction(game, update, false, ActionOrders[0], ActionOrders[2], 50, 51, Name, 4) == GameActionResult.Restart)
            {
                return GameActionResult.Restart;
            }

            // Action 21: select a live target to watch
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == ActionOrders[1])
            {
                var daMao = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
                var daMaoAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
                var teammates = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == "LangRen");
                var actionDuration = Game.GetGameDictionaryProperty(game, LangRenSha.dictDurationPlayerReact, ActionDuration) + 10;
                if (daMaoAlive.Count == 0)
                {
                    actionDuration = new Random().Next(6, 10);
                }

                var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);

                if (UserAction.EndUserAction(game, update))
                {
                    // Time expired - just advance, ignore any response (no cat trap check)
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
                        update[UserAction.dictUserActionUsers] = daMao;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.DaMao_Act;
                        update[UserAction.dictUserActionRole] = Name;
                        update[UserAction.dictUserActionInfo] = "";
                        if (langRenAlive.Count + langRenSuccession1Alive.Count == 0)
                        {
                            update[UserAction.dictUserActionInfo] = "Succession";
                        }
                        update[UserAction.dictUserActionInfo] += ";" + string.Join(", ", teammates);
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, daMaoAlive, update);
                        if (inputValid)
                        {
                            var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, -1);
                            if (targets.Count == 0)
                            {
                                return GameActionResult.NotExecuted;
                            }
                            var target = targets[0];

                            // Check if target is cat trapped (gifted player) - DaMao dies if visiting them
                            if (target > 0 && daMaoAlive.Count > 0)
                            {
                                var isCatTrapped = LangRenSha.GetPlayerProperty(game, target, dictCatTrapped, 0);
                                if (isCatTrapped == 1)
                                {
                                    // DaMao dies when visiting a cat trapped player
                                    LangRenSha.MarkPlayerAboutToDie(game, daMaoAlive[0], update);
                                    update[dictCatMark] = 0; // No cat mark since DaMao died
                                }
                                else
                                {
                                    update[dictCatMark] = target;
                                }
                            }
                            else
                            {
                                update[dictCatMark] = target;
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
    }
}
