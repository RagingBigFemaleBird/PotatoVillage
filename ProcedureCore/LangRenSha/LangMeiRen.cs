using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProcedureCore.LangRenSha
{
    /// <summary>
    /// LangMeiRen (狼美人 - Wolf Beauty) - An evil wolf role that links a player each night.
    /// Acts during the LangRen phase (action 102).
    /// When LangMeiRen dies, the linked player also dies with skills disabled.
    /// Same linking mechanism as evil Xiong.
    /// </summary>
    public class LangMeiRen : Role
    {
        private static Dictionary<string, object> roleDict = new()
        {
            { YuYanJia.dictYuYanJiaResult, 2 },
            { LangRenSha.dictPlayerAlliance, 2 },
            { LangRen.dictSuceession, 1 },
            { LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.Evil },
        };

        private static List<int> actionOrders = new()
        {
            (int)ActionConstant.LangMeiRen_Act,
        };

        public LangMeiRen() { }

        public Dictionary<string, object> RoleDict => roleDict;
        public string Name => "LangMeiRen";
        public int Version => 1;
        public List<int> ActionOrders => actionOrders;
        public int ActionDuration => 15;

        // LangMeiRen's link target is now stored under the unified GhostBride.dictLinkedTo
        // key so the central HandleLinkedDeathSkill / ChainKill paths handle it.

        public GameActionResult GenerateStateDiff(Game game, Dictionary<string, object> update)
        {
            if (game.StateSequenceNumber == 1)
            {
                var addSelf = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
                if (addSelf.Count > 0)
                {
                    var no = Game.GetGameDictionaryProperty(game, LangRenSha.dictNightOrders, new List<int>());
                    no.AddRange(actionOrders);
                    update[LangRenSha.dictNightOrders] = no;
                }
                return GameActionResult.Continue;
            }

            // Action 102: LangMeiRen selects a target to link (shares LangRen open/close eyes)
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == ActionOrders[0])
            {
                var lmr = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
                var lmrAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
                var lmrPlayer = lmrAlive.Count > 0 ? lmrAlive[0] : 0;

                var actionDuration = Game.GetGameDictionaryProperty(game, LangRenSha.dictDurationPlayerReact, ActionDuration);
                if (lmrAlive.Count == 0)
                {
                    actionDuration = new Random().Next(3, 6);
                }

                var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
                var lastLink = lmrPlayer > 0 ? LangRenSha.GetPlayerProperty(game, lmrPlayer, GhostBride.dictLinkedTo, 0) : 0;
                if (lastLink > 0)
                {
                    alivePlayers.Remove(lastLink); // Cannot link same target as last time
                }
                alivePlayers.Remove(lmrPlayer); // Cannot link self
                alivePlayers.Add(-100); // "Do not use" option

                if (UserAction.EndUserAction(game, update))
                {
                    // Timeout - only set skippedAct to true if not already acted (LangMeiRen is part of LangRen)
                    if (lmrAlive.Count > 0)
                    {
                        // Clear any stale link from a prior night before timing out.
                        LangRenSha.SetPlayerProperty(game, lmrPlayer, GhostBride.dictLinkedTo, 0, update);
                        var currentSkippedAct = LangRenSha.GetPlayerProperty(game, lmrPlayer, AwkSheMengRen.dictNightSkippedAct, 1);
                        if (currentSkippedAct != 0)
                        {
                            AwkSheMengRen.SetSkippedAct(game, lmrPlayer, true, update);
                        }
                    }
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, actionDuration, update))
                    {
                        update[UserAction.dictUserActionTargets] = lmrAlive.Count > 0 ? alivePlayers : new List<int>();
                        update[UserAction.dictUserActionUsers] = lmr;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.LangMeiRen_Act;
                        update[UserAction.dictUserActionRole] = "LangRen";
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, lmrAlive, update);
                        if (inputValid)
                        {
                            var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, -1);
                            if (targets.Count > 0 && targets[0] > 0)
                            {
                                LangRenSha.SetPlayerProperty(game, lmrPlayer, GhostBride.dictLinkedTo, targets[0], update);
                                // Set skippedAct (not skipped) - linking someone means acted
                                AwkSheMengRen.SetSkippedAct(game, lmrPlayer, false, update);

                                UserAction.EndUserAction(game, update, true);
                                LangRenSha.AdvanceAction(game, update);
                                return GameActionResult.Restart;
                            }
                            if (targets.Count > 0 && targets[0] == -100)
                            {
                                LangRenSha.SetPlayerProperty(game, lmrPlayer, GhostBride.dictLinkedTo, 0, update);
                                // Only set skippedAct to true if not already acted (LangMeiRen is part of LangRen, 
                                // so if they already participated in the kill, don't change to skipped)
                                var currentSkippedAct = LangRenSha.GetPlayerProperty(game, lmrPlayer, AwkSheMengRen.dictNightSkippedAct, 1);
                                if (currentSkippedAct != 0)
                                {
                                    AwkSheMengRen.SetSkippedAct(game, lmrPlayer, true, update);
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

        /// <summary>
        /// LangMeiRen's death-link skill is now handled by the unified
        /// <see cref="LangRenSha.HandleLinkedDeathSkill"/> via the shared
        /// <see cref="GhostBride.dictLinkedTo"/> player property.
        /// </summary>
    }
}
