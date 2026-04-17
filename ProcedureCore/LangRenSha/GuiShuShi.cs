using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProcedureCore.LangRenSha
{
    /// <summary>
    /// GuiShuShi (诡术师) - Trickster.
    /// Each night, picks two alive players. When either of them is voted out
    /// during the following day, the voted-out player is swapped with the other
    /// one (the other dies instead). Any player chosen once cannot be chosen
    /// again. The player may also choose not to act.
    /// </summary>
    public class GuiShuShi : Role
    {
        // Current pending swap pair. Set at night, consumed on voteout.
        public static string dictSwapA = "guishushi_swap_a";
        public static string dictSwapB = "guishushi_swap_b";

        // Per-player property - list of players already used as swap targets.
        public static string dictSelectedTargets = "guishushi_selected";

        private static Dictionary<string, object> roleDict = new()
        {
            { YuYanJia.dictYuYanJiaResult, 2 },
            { LangRenSha.dictPlayerAlliance, 2 },
            { LangRen.dictSuceession, 1 },
            { LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.Evil },
        };

        private static List<int> actionOrders = new()
            { (int)ActionConstant.GuiShuShi_OpenEyes, (int)ActionConstant.GuiShuShi_Act, (int)ActionConstant.GuiShuShi_CloseEyes };

        public GuiShuShi()
        {
        }

        public Dictionary<string, object> RoleDict => roleDict;
        public string Name => "GuiShuShi";
        public int Version => 1;
        public List<int> ActionOrders => actionOrders;
        public int ActionDuration => 30;

        /// <summary>
        /// Voted-out handler: if the voted-out player is part of the current
        /// swap pair, return the partner instead (swap takes effect on voteout).
        /// The pair is cleared after use.
        /// </summary>
        public static List<int> HandleVotedOut(Game game, List<int> voteout, Dictionary<string, object> update)
        {
            if (voteout == null || voteout.Count != 1) return voteout ?? new List<int>();

            var a = Game.GetGameDictionaryProperty(game, dictSwapA, 0);
            var b = Game.GetGameDictionaryProperty(game, dictSwapB, 0);
            if (a <= 0 || b <= 0 || a == b) return voteout;

            var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);

            var target = voteout[0];
            int swapTo = 0;
            if (target == a && alivePlayers.Contains(b))
            {
                swapTo = b;
            }
            else if (target == b && alivePlayers.Contains(a))
            {
                swapTo = a;
            }

            if (swapTo == 0)
            {
                // Partner is dead (or target is not part of the pair). Clear any
                // stale pair involving a dead player; the voted-out target dies.
                if (target == a || target == b)
                {
                    update[dictSwapA] = 0;
                    update[dictSwapB] = 0;
                }
                return voteout;
            }

            // Swap active - clear pair
            update[dictSwapA] = 0;
            update[dictSwapB] = 0;

            // Set up interrupt chain so the SkillUseAnnouncement is shown before
            // VotedOutAnnouncement continues. After the announcement, flow returns
            // to VotedOutAnnouncement (the normal next stage) to announce the
            // final swapped-to victim and proceed to death handling.
            var guiShuShiAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == "GuiShuShi" && (int)x[LangRenSha.dictAlive] == 1);
            var guiShuShiPlayer = guiShuShiAlive.Count > 0 ? guiShuShiAlive[0] : 0;

            var currentInterrupt = Game.GetGameDictionaryProperty(game, LangRenSha.dictInterrupt, new Dictionary<string, object>());
            var skillAnnouncementInterrupt = new Dictionary<string, object>();
            skillAnnouncementInterrupt[LangRenSha.dictSpeak] = (int)SpeakConstant.VotedOutAnnouncement;
            skillAnnouncementInterrupt[LangRenSha.dictInterrupt] = currentInterrupt;

            update[LangRenSha.dictSkillUseFrom] = swapTo;
            update[LangRenSha.dictSkillUseTo] = new List<int> { target };
            update[LangRenSha.dictSkillUse] = "swapped";
            update[LangRenSha.dictSkillUseResult] = "1"; // Succeeded

            update[LangRenSha.dictInterrupt] = skillAnnouncementInterrupt;
            update[LangRenSha.dictSpeak] = (int)SpeakConstant.SkillUseAnnouncement;

            return new List<int> { swapTo };
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
                var guiShuShi = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
                var guiShuShiAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
                var guiShuShiPlayer = guiShuShiAlive.Count > 0 ? guiShuShiAlive[0] : 0;
                var actionDuration = Game.GetGameDictionaryProperty(game, LangRenSha.dictDurationPlayerReact, ActionDuration);

                // Build list of candidate targets: alive players not yet used by this GuiShuShi.
                var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
                var selected = guiShuShiPlayer > 0
                    ? LangRenSha.GetPlayerProperty(game, guiShuShiPlayer, dictSelectedTargets, new List<int>())
                    : new List<int>();
                var candidates = alivePlayers.Where(p => !selected.Contains(p)).ToList();

                // Check if skill is disabled by MengYan
                var skillDisabled = guiShuShiPlayer > 0 && LangRenSha.GetPlayerProperty(game, guiShuShiPlayer, LangRenSha.dictSkillTransformation, 0) == (int)LangRenSha.SkillTransformation.Disabled;

                if (guiShuShiAlive.Count == 0 || skillDisabled || candidates.Count < 2)
                {
                    actionDuration = new Random().Next(3, 6);
                }

                if (UserAction.EndUserAction(game, update))
                {
                    (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, guiShuShiAlive, update);
                    if (inputValid)
                    {
                        ProcessInput(game, guiShuShiPlayer, input, update);
                    }
                    if (guiShuShiPlayer > 0)
                    {
                        LangRenSha.SetPlayerProperty(game, guiShuShiPlayer, LangRenSha.dictSkillTransformation, (int)LangRenSha.SkillTransformation.None, update);
                    }
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, actionDuration, update))
                    {
                        // Clear any leftover swap at start of this night (new pick replaces previous).
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
                        update[UserAction.dictUserActionUsers] = guiShuShi;
                        update[UserAction.dictUserActionTargetsCount] = 2;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.GuiShuShi_Swap;
                        update[UserAction.dictUserActionRole] = Name;
                        update[UserAction.dictUserActionInfo] = string.Join(", ", selected);
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, guiShuShiAlive, update);
                        if (inputValid)
                        {
                            if (ProcessInput(game, guiShuShiPlayer, input, update))
                            {
                                if (guiShuShiPlayer > 0)
                                {
                                    LangRenSha.SetPlayerProperty(game, guiShuShiPlayer, LangRenSha.dictSkillTransformation, (int)LangRenSha.SkillTransformation.None, update);
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

        private static bool ProcessInput(Game game, int guiShuShiPlayer, Dictionary<string, object> input, Dictionary<string, object> update)
        {
            if (guiShuShiPlayer <= 0 || !input.ContainsKey(guiShuShiPlayer.ToString()))
            {
                return true;
            }
            var chosen = (List<int>)input[guiShuShiPlayer.ToString()];
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

            // Record selected history on GuiShuShi's player property
            var selected = LangRenSha.GetPlayerProperty(game, guiShuShiPlayer, dictSelectedTargets, new List<int>());
            if (!selected.Contains(a)) selected.Add(a);
            if (!selected.Contains(b)) selected.Add(b);
            LangRenSha.SetPlayerProperty(game, guiShuShiPlayer, dictSelectedTargets, selected, update);
            return true;
        }
    }
}
