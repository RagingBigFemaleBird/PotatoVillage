using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProcedureCore.LangRenSha
{
    public class MoShuShi : Role
    {
        // Global game state - the currently active swap pair (only valid during the night).
        public static string dictSwapA = "moshushi_swap_a";
        public static string dictSwapB = "moshushi_swap_b";

        // Per-player property - list of players already used as swap targets.
        public static string dictSelectedTargets = "moshushi_selected";

        private static Dictionary<string, object> roleDict = new()
        {
            { YuYanJia.dictYuYanJiaResult, 1 },
            { LangRenSha.dictPlayerAlliance, 1 },
            { LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.God },
        };

        private static List<int> actionOrders = new()
            { (int)ActionConstant.MoShuShi_OpenEyes, (int)ActionConstant.MoShuShi_Swap, (int)ActionConstant.MoShuShi_CloseEyes };

        public MoShuShi()
        {
        }

        public Dictionary<string, object> RoleDict => roleDict;
        public string Name => "MoShuShi";
        public int Version => 1;
        public List<int> ActionOrders => actionOrders;
        public int ActionDuration => 30;

        /// <summary>
        /// Returns the actual target that <paramref name="target"/> gets swapped to during the
        /// current night. If no swap is active or the given player isn't part of the pair,
        /// the original target is returned unchanged.
        /// </summary>
        public static int GetSwappedTarget(Game game, int target)
        {
            if (target <= 0) return target;
            var a = Game.GetGameDictionaryProperty(game, dictSwapA, 0);
            var b = Game.GetGameDictionaryProperty(game, dictSwapB, 0);
            if (a <= 0 || b <= 0 || a == b) return target;
            if (target == a) return b;
            if (target == b) return a;
            return target;
        }

        public static void ClearSwap(Dictionary<string, object> update)
        {
            update[dictSwapA] = 0;
            update[dictSwapB] = 0;
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

            // Open/close eyes announcer
            if (LangRenSha.AnnouncerAction(game, update, false, ActionOrders[0], ActionOrders[2], 50, 51, Name, 4) == GameActionResult.Restart)
            {
                return GameActionResult.Restart;
            }

            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == ActionOrders[1])
            {
                var moShuShi = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
                var moShuShiAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
                var moShuShiPlayer = moShuShiAlive.Count > 0 ? moShuShiAlive[0] : 0;
                var actionDuration = Game.GetGameDictionaryProperty(game, LangRenSha.dictDurationPlayerReact, ActionDuration);

                // Build list of candidate targets: alive players not yet used by this MoShuShi.
                var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
                var selected = moShuShiPlayer > 0
                    ? LangRenSha.GetPlayerProperty(game, moShuShiPlayer, dictSelectedTargets, new List<int>())
                    : new List<int>();
                var candidates = alivePlayers.Where(p => !selected.Contains(p)).ToList();

                // Check if skill is disabled by MengYan
                var skillDisabled = moShuShiPlayer > 0 && LangRenSha.GetPlayerProperty(game, moShuShiPlayer, LangRenSha.dictSkillTransformation, 0) == (int)LangRenSha.SkillTransformation.Disabled;

                if (moShuShiAlive.Count == 0 || skillDisabled || candidates.Count < 2)
                {
                    actionDuration = new Random().Next(3, 6);
                }

                if (UserAction.EndUserAction(game, update))
                {
                    (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, moShuShiAlive, update);
                    if (inputValid)
                    {
                        ProcessInput(game, moShuShiPlayer, input, update);
                    }
                    if (moShuShiPlayer > 0)
                    {
                        LangRenSha.SetPlayerProperty(game, moShuShiPlayer, LangRenSha.dictSkillTransformation, (int)LangRenSha.SkillTransformation.None, update);
                    }
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, actionDuration, update))
                    {
                        // Clear any previous swap at start of this night.
                        ClearSwap(update);
                        List<int> targets;
                        if (skillDisabled || candidates.Count < 2)
                        {
                            targets = new List<int> { -100 };
                            if (skillDisabled)
                            {
                                update[UserAction.dictUserActionInfo3] = "1";
                            }
                        }
                        else
                        {
                            targets = new List<int>(candidates);
                            targets.Add(-100);
                        }
                        update[UserAction.dictUserActionTargets] = targets;
                        update[UserAction.dictUserActionUsers] = moShuShi;
                        update[UserAction.dictUserActionTargetsCount] = 2;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.MoShuShi_Swap;
                        update[UserAction.dictUserActionRole] = Name;
                        update[UserAction.dictUserActionInfo] = string.Join(", ", selected);
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, moShuShiAlive, update);
                        if (inputValid)
                        {
                            if (ProcessInput(game, moShuShiPlayer, input, update))
                            {
                                if (moShuShiPlayer > 0)
                                {
                                    LangRenSha.SetPlayerProperty(game, moShuShiPlayer, LangRenSha.dictSkillTransformation, (int)LangRenSha.SkillTransformation.None, update);
                                }
                                UserAction.EndUserAction(game, update, true);
                                LangRenSha.AdvanceAction(game, update);
                                return GameActionResult.Restart;
                            }
                            return GameActionResult.NotExecuted;
                        }
                    }
                }
                return GameActionResult.NotExecuted;
            }

            return GameActionResult.NotExecuted;
        }

        private static bool ProcessInput(Game game, int moShuShiPlayer, Dictionary<string, object> input, Dictionary<string, object> update)
        {
            if (moShuShiPlayer <= 0 || !input.ContainsKey(moShuShiPlayer.ToString()))
            {
                return true;
            }
            var chosen = (List<int>)input[moShuShiPlayer.ToString()];
            var positive = chosen.Where(t => t > 0).Distinct().ToList();
            bool hasSkip = chosen.Contains(-100);

            // Skip the action: user picked only the "do not use" option (or nothing).
            if (positive.Count == 0)
            {
                return true;
            }

            // Mixing skip with a real pick is not a valid final selection.
            if (hasSkip)
            {
                return false;
            }

            // Need exactly two distinct positive targets to perform a swap.
            if (positive.Count < 2)
            {
                return false;
            }
            var a = positive[0];
            var b = positive[1];
            update[dictSwapA] = a;
            update[dictSwapB] = b;

            // Record selected history on MoShuShi's player property
            var selected = LangRenSha.GetPlayerProperty(game, moShuShiPlayer, dictSelectedTargets, new List<int>());
            if (!selected.Contains(a)) selected.Add(a);
            if (!selected.Contains(b)) selected.Add(b);
            LangRenSha.SetPlayerProperty(game, moShuShiPlayer, dictSelectedTargets, selected, update);
            return true;
        }
    }
}
