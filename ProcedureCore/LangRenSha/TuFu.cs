using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcedureCore.LangRenSha
{
    /// <summary>
    /// TuFu (屠夫/Butcher) - A werewolf role with succession = 2.
    /// Day 1+ only: Can select a target to kill during night.
    /// The kill is added to the normal LangRen attack target and executed at LangRen_Kill.
    /// Shows attack status (whether it can attack on LangRen's turn).
    /// </summary>
    public class TuFu : Role
    {
        private static Dictionary<string, object> roleDict = new()
        {
            { YuYanJia.dictYuYanJiaResult, 1 },
            { LangRenSha.dictPlayerAlliance, 2 },
            { LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.Evil },
            { LangRen.dictSuceession, 2 },
        };

        private static List<int> actionOrders = new()
        {
            (int)ActionConstant.TuFu_OpenEyes,
            (int)ActionConstant.TuFu_Act,
            (int)ActionConstant.TuFu_CloseEyes
        };

        public TuFu()
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
                return "TuFu";
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

            // TuFu Act - select a target to kill
            if (action == ActionOrders[1])
            {
                return HandleTuFuAction(game, update);
            }

            return GameActionResult.NotExecuted;
        }

        private GameActionResult HandleTuFuAction(Game game, Dictionary<string, object> update)
        {
            var tuFu = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
            var tuFuAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
            var tuFuPlayer = tuFuAlive.Count > 0 ? tuFuAlive[0] : 0;

            // Check if TuFu can attack (succession = 2, so only when normal LangRen are dead)
            var langRenAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == "LangRen" && (int)x[LangRenSha.dictAlive] == 1);
            var succession1Alive = LangRenSha.GetPlayers(game, x => x.ContainsKey(LangRen.dictSuceession) && (int)x[LangRen.dictSuceession] == 1 && (int)x[LangRenSha.dictAlive] == 1);
            var canAttack = langRenAlive.Count == 0 && succession1Alive.Count == 0;

            var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
            var actionDuration = Game.GetGameDictionaryProperty(game, LangRenSha.dictDurationPlayerReact, ActionDuration);

            if (tuFuAlive.Count == 0)
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
                    // Build list of valid targets
                    var targets = new List<int>(alivePlayers);
                    // Add -100 option to skip using the skill
                    targets.Add(-100);

                    // Info shows attack status
                    var attackStatusInfo = canAttack ? "Succession" : "";

                    update[UserAction.dictUserActionTargets] = targets;
                    update[UserAction.dictUserActionUsers] = tuFu;
                    update[UserAction.dictUserActionTargetsCount] = 1;
                    update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.TuFu_Act;
                    update[UserAction.dictUserActionRole] = Name;
                    update[UserAction.dictUserActionInfo] = attackStatusInfo;
                    return GameActionResult.Restart;
                }
                else
                {
                    (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, tuFuAlive, update);
                    if (inputValid)
                    {
                        var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.Input, -1);
                        if (targets.Count == 1)
                        {
                            if (targets[0] == -100)
                            {
                                // Player chose to skip
                                UserAction.EndUserAction(game, update, true);
                                LangRenSha.AdvanceAction(game, update);
                                return GameActionResult.Restart;
                            }
                            if (targets[0] > 0)
                            {
                                // Add target to attack list (same as normal LangRen attack)
                                var attackTarget = Game.GetGameDictionaryProperty(game, LangRen.dictAttackTarget, new List<int>());
                                if (!attackTarget.Contains(targets[0]))
                                {
                                    attackTarget.Add(targets[0]);
                                }
                                update[LangRen.dictAttackTarget] = attackTarget;

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
