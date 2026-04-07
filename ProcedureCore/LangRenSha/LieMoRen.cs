using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcedureCore.LangRenSha
{
    /// <summary>
    /// LieMoRen (猎魔人/Demon Hunter) - A god role that can hunt demons.
    /// Day 1+ only: Can choose a target at night. If the target's alliance differs from LieMoRen's,
    /// the target dies. If the target has the same alliance, LieMoRen dies instead.
    /// Can also choose to skip the action.
    /// </summary>
    public class LieMoRen : Role
    {
        private static Dictionary<string, object> roleDict = new()
        {
            { YuYanJia.dictYuYanJiaResult, 1 },
            { LangRenSha.dictPlayerAlliance, 1 },
            { LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.God },
        };

        private static List<int> actionOrders = new()
        {
            (int)ActionConstant.LieMoRen_OpenEyes,
            (int)ActionConstant.LieMoRen_Act,
            (int)ActionConstant.LieMoRen_CloseEyes
        };

        public LieMoRen()
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
                return "LieMoRen";
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

            var day = Game.GetGameDictionaryProperty(game, LangRenSha.dictDay, 0);
            var action = Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0);

            // Day 0: Skip all actions (this role only acts on day 1+)
            if (day == 0 && ActionOrders.Contains(action))
            {
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }

            // Day 1+: Announcer actions for open/close eyes
            if (LangRenSha.AnnouncerAction(game, update, false, ActionOrders[0], ActionOrders[2],
                (int)HintConstant.OpenEyes, (int)HintConstant.CloseEyes, Name, 4) == GameActionResult.Restart)
            {
                return GameActionResult.Restart;
            }

            // LieMoRen Act - choose a target to hunt
            if (action == ActionOrders[1])
            {
                return HandleHuntAction(game, update);
            }

            return GameActionResult.NotExecuted;
        }

        private GameActionResult HandleHuntAction(Game game, Dictionary<string, object> update)
        {
            var lieMoRen = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
            var lieMoRenAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
            var lieMoRenPlayer = lieMoRenAlive.Count > 0 ? lieMoRenAlive[0] : 0;
            var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
            var actionDuration = Game.GetGameDictionaryProperty(game, LangRenSha.dictDurationPlayerReact, ActionDuration);

            if (lieMoRenAlive.Count == 0)
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
                    // Build list of valid targets (all alive players except self)
                    var targets = new List<int>();
                    foreach (var target in alivePlayers)
                    {
                        if (target != lieMoRenPlayer)
                        {
                            targets.Add(target);
                        }
                    }
                    // Add -100 option to skip using the skill
                    targets.Add(-100);

                    update[UserAction.dictUserActionTargets] = targets;
                    update[UserAction.dictUserActionUsers] = lieMoRen;
                    update[UserAction.dictUserActionTargetsCount] = 1;
                    update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.LieMoRen_Act;
                    update[UserAction.dictUserActionRole] = Name;
                    return GameActionResult.Restart;
                }
                else
                {
                    (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, lieMoRenAlive, update);
                    if (inputValid)
                    {
                        var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.Input, -1);
                        if (targets.Count == 1)
                        {
                            if (targets[0] == -100)
                            {
                                // Player chose to skip using the skill
                                UserAction.EndUserAction(game, update, true);
                                LangRenSha.AdvanceAction(game, update);
                                return GameActionResult.Restart;
                            }
                            if (targets[0] > 0)
                            {
                                // Get alliances
                                var lieMoRenAlliance = LangRenSha.GetPlayerProperty(game, lieMoRenPlayer, LangRenSha.dictPlayerAlliance, 1);
                                var targetAlliance = LangRenSha.GetPlayerProperty(game, targets[0], LangRenSha.dictPlayerAlliance, 1);

                                var aboutToDie = Game.GetGameDictionaryProperty(game, LangRenSha.dictAboutToDie, new List<int>());

                                if (targetAlliance != lieMoRenAlliance)
                                {
                                    // Target has different alliance - target dies
                                    LangRenSha.ChainKill(game, lieMoRenPlayer, targets[0], aboutToDie, update);
                                }
                                else
                                {
                                    // Target has same alliance - LieMoRen dies
                                    LangRenSha.ChainKill(game, lieMoRenPlayer, lieMoRenPlayer, aboutToDie, update);
                                }

                                update[LangRenSha.dictAboutToDie] = aboutToDie;

                                UserAction.EndUserAction(game, update, true);
                                LangRenSha.AdvanceAction(game, update);
                                return GameActionResult.Restart;
                            }
                        }
                    }
                }
            }
            return GameActionResult.NotExecuted;
        }
    }
}
