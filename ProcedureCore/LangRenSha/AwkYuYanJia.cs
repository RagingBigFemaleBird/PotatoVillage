using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcedureCore.LangRenSha
{
    /// <summary>
    /// AwkYuYanJia (觉醒预言家 - Awakened Seer) role.
    /// Acts after YuYanJia. Each night may select 1 OR 2 players to ChaYan.
    /// The aggregate result is "evil" if ANY of the selected players is evil,
    /// otherwise "good".
    /// </summary>
    public class AwkYuYanJia : Role
    {
        private static Dictionary<string, object> roleDict = new()
        {
            { YuYanJia.dictYuYanJiaResult, 1 },
            { LangRenSha.dictPlayerAlliance, 1 },
            { LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.God },
        };

        private static List<int> actionOrders = new()
        {
            (int)ActionConstant.AwkYuYanJia_OpenEyes,
            (int)ActionConstant.AwkYuYanJia_ChaYan,
            (int)ActionConstant.AwkYuYanJia_Result,
            (int)ActionConstant.AwkYuYanJia_CloseEyes
        };

        public AwkYuYanJia() { }

        public Dictionary<string, object> RoleDict
        {
            get { return roleDict; }
        }

        public string Name
        {
            get { return "AwkYuYanJia"; }
        }

        public int Version
        {
            get { return 1; }
        }

        public List<int> ActionOrders
        {
            get { return actionOrders; }
        }

        public int ActionDuration
        {
            get { return 30; }
        }

        /// <summary>
        /// ChaYan over 1 or 2 targets. The aggregate result is evil (2) if ANY of
        /// the targets is evil, otherwise good (1). The result is stored on the
        /// initiator's player dictionary under YuYanJia.dictYuYanJiaLastResult so
        /// the existing YuYanJia info handler can display it unchanged.
        /// </summary>
        public void ChaYan(Game game, int initiator, List<int> targets, Dictionary<string, object> update)
        {
            int aggregate = 1;
            foreach (var t in targets)
            {
                if (t <= 0) continue;
                // MoShuShi swap applies per target.
                var actualTarget = MoShuShi.GetSwappedTarget(game, t);
                var result = LangRenSha.GetPlayerProperty(game, actualTarget, YuYanJia.dictYuYanJiaResult, 1);
                if (result == 2)
                {
                    aggregate = 2;
                    break;
                }
            }
            LangRenSha.SetPlayerProperty(game, initiator, YuYanJia.dictYuYanJiaLastResult, aggregate.ToString(), update);
        }

        /// <summary>
        /// TongLing variant (used when mice-tagged or alliance is evil) - reveals
        /// the role of each of the 1 or 2 selected targets.
        /// </summary>
        public void TongLing(Game game, int initiator, List<int> targets, Dictionary<string, object> update)
        {
            var results = new List<string>();
            foreach (var t in targets)
            {
                if (t <= 0) continue;
                var actualTarget = MoShuShi.GetSwappedTarget(game, t);
                var role = LangRenSha.GetPlayerProperty(game, actualTarget, TongLingShi.dictTongLingShiResult, "");
                if (string.IsNullOrEmpty(role))
                {
                    role = LangRenSha.GetPlayerProperty(game, actualTarget, LangRenSha.dictRole, "");
                }
                results.Add($"{actualTarget}:{role}");
            }
            LangRenSha.SetPlayerProperty(game, initiator, TongLingShi.dictTongLingResult, string.Join(",", results), update);
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

            if (LangRenSha.AnnouncerAction(game, update, false, ActionOrders[0], ActionOrders[3], 50, 51, Name, 4) == GameActionResult.Restart)
            {
                return GameActionResult.Restart;
            }

            // ChaYan phase: select 1 or 2 targets.
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == ActionOrders[1])
            {
                var awkYuYanJia = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
                var awkYuYanJiaAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
                var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
                var miceTagged = awkYuYanJiaAlive.Count > 0 ? (awkYuYanJiaAlive[0] == Game.GetGameDictionaryProperty(game, LaoShu.dictMiceTag, 0)) : false;
                var isEvil = awkYuYanJiaAlive.Count > 0 ? (LangRenSha.GetPlayerProperty(game, awkYuYanJiaAlive[0], LangRenSha.dictPlayerAlliance, 0) == 2) : false;
                var actionDuration = Game.GetGameDictionaryProperty(game, LangRenSha.dictDurationPlayerReact, ActionDuration);
                var awkYuYanJiaPlayer = awkYuYanJia.Count > 0 ? awkYuYanJia[0] : 0;
                var skillDisabled = awkYuYanJiaPlayer > 0 && LangRenSha.GetPlayerProperty(game, awkYuYanJiaPlayer, LangRenSha.dictSkillTransformation, 0) == (int)LangRenSha.SkillTransformation.Disabled;

                if (awkYuYanJiaAlive.Count == 0 || skillDisabled)
                {
                    actionDuration = new Random().Next(3, 6);
                }

                if (UserAction.EndUserAction(game, update))
                {
                    if (awkYuYanJiaAlive.Count > 0)
                    {
                        LangRenSha.SetPlayerProperty(game, awkYuYanJiaAlive[0], LangRenSha.dictSkillTransformation, (int)LangRenSha.SkillTransformation.None, update);
                        LangRenSha.SetPlayerProperty(game, awkYuYanJiaAlive[0], YuYanJia.dictYuYanJiaLastResult, "", update);
                        LangRenSha.SetPlayerProperty(game, awkYuYanJiaAlive[0], TongLingShi.dictTongLingResult, "", update);
                    }
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, actionDuration, update))
                    {
                        List<int> targets;
                        if (skillDisabled)
                        {
                            targets = new List<int> { -100 };
                            update[UserAction.dictUserActionInfo3] = "1";
                        }
                        else
                        {
                            targets = alivePlayers;
                        }
                        update[UserAction.dictUserActionTargets] = targets;
                        update[UserAction.dictUserActionUsers] = awkYuYanJia;
                        update[UserAction.dictUserActionTargetsCount] = 2;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.AwkYuYanJia_ChaYan;
                        update[UserAction.dictUserActionRole] = Name;
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, awkYuYanJiaAlive, update);
                        if (inputValid)
                        {
                            // Single user (awkYuYanJia) so Input mode returns the targets list as-is.
                            var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.Input, -1);
                            // Filter to valid positive player ids (drop -100/sentinel values).
                            var validTargets = targets.Where(t => t > 0).Distinct().Take(2).ToList();
                            if (validTargets.Count == 0)
                            {
                                // Skipped / no selection - just advance.
                            }
                            else
                            {
                                var initiator = awkYuYanJiaAlive.Count > 0 ? awkYuYanJiaAlive[0] : 0;
                                if (miceTagged || isEvil)
                                {
                                    TongLing(game, initiator, validTargets, update);
                                }
                                else
                                {
                                    ChaYan(game, initiator, validTargets, update);
                                }
                            }
                            if (awkYuYanJiaAlive.Count > 0)
                            {
                                LangRenSha.SetPlayerProperty(game, awkYuYanJiaAlive[0], LangRenSha.dictSkillTransformation, (int)LangRenSha.SkillTransformation.None, update);
                            }
                            UserAction.EndUserAction(game, update, true);
                            LangRenSha.AdvanceAction(game, update);
                            return GameActionResult.Restart;
                        }
                    }
                }
                return GameActionResult.NotExecuted;
            }

            // Result phase.
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == ActionOrders[2])
            {
                var awkYuYanJia = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
                var awkYuYanJiaAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
                if (UserAction.EndUserAction(game, update))
                {
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, 5, update))
                    {
                        update[UserAction.dictUserActionTargets] = new List<int>() { 0 };
                        update[UserAction.dictUserActionUsers] = awkYuYanJia;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.AwkYuYanJia_Result;
                        update[UserAction.dictUserActionRole] = Name;
                        var lastResult = "";
                        if (awkYuYanJiaAlive.Count > 0)
                        {
                            lastResult = LangRenSha.GetPlayerProperty(game, awkYuYanJiaAlive[0], YuYanJia.dictYuYanJiaLastResult, "");
                            if (string.IsNullOrEmpty(lastResult))
                            {
                                lastResult = LangRenSha.GetPlayerProperty(game, awkYuYanJiaAlive[0], TongLingShi.dictTongLingResult, "");
                            }
                        }
                        update[UserAction.dictUserActionInfo] = $"{lastResult}";
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, awkYuYanJiaAlive, update);
                        if (inputValid)
                        {
                            var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, -1);
                            if (targets.Count == 0)
                            {
                                return GameActionResult.NotExecuted;
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
