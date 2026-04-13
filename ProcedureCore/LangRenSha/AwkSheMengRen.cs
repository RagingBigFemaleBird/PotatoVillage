using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProcedureCore.LangRenSha
{
    /// <summary>
    /// AwkSheMengRen (觉醒摄梦人 - Awakened Dream Guardian) - A god role that guards and judges players.
    /// Each night: MUST select a player to guard (random if not selected). Target is immune to ALL deaths.
    /// Just before LieRen: Shows if target "acted" during night, then asks to kill or not.
    /// If kill is selected, target dies without ability to shoot guns.
    /// 
    /// "Acted" determination:
    /// - God faction (or beforeConversionFaction is God): acted UNLESS whitelisted (MengMianRen)
    /// - Evil faction: NOT acted UNLESS able to attack during the night
    /// </summary>
    public class AwkSheMengRen : Role
    {
        private static Dictionary<string, object> roleDict = new()
        {
            { YuYanJia.dictYuYanJiaResult, 1 },
            { LangRenSha.dictPlayerAlliance, 1 },
            { LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.God },
        };

        private static List<int> actionOrders = new()
        {
            (int)ActionConstant.AwkSheMengRen_OpenEyes,      // 0: 88 - Guard selection
            (int)ActionConstant.AwkSheMengRen_Act,           // 1: 89
            (int)ActionConstant.AwkSheMengRen_CloseEyes,     // 2: 90
            (int)ActionConstant.AwkSheMengRen_JudgeOpenEyes, // 3: 285 - Judge phase
            (int)ActionConstant.AwkSheMengRen_JudgeAct,      // 4: 286 - Show info + ask to kill
            (int)ActionConstant.AwkSheMengRen_JudgeCloseEyes // 5: 287
        };

        public AwkSheMengRen()
        {
        }

        public Dictionary<string, object> RoleDict => roleDict;
        public string Name => "AwkSheMengRen";
        public int Version => 1;
        public List<int> ActionOrders => actionOrders;
        public int ActionDuration => 20;

        // Dictionary keys
        public static string dictAwkSheMengTarget = "awkshemengren_target";
        public static string dictAwkSheMengLastTarget = "awkshemengren_last_target";
        public static string dictBeforeConversionFaction = "before_conversion_faction";
        public static string dictNightSkippedAct = "night_skippedAct";

        // Whitelist of roles that don't count as "acted" even if God faction
        private static readonly HashSet<string> NoActWhitelist = new() { "MengMianRen" , "BaiChi", "LieRen"};

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

            var action = Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0);

            // Guard phase announcer actions (open/close eyes)
            if (LangRenSha.AnnouncerAction(game, update, false, ActionOrders[0], ActionOrders[2], 50, 51, Name, 4) == GameActionResult.Restart)
            {
                return GameActionResult.Restart;
            }

            // Guard action - choose a target to guard
            if (action == ActionOrders[1])
            {
                return HandleGuardAction(game, update);
            }

            // Judge phase announcer actions (open/close eyes)
            if (action == ActionOrders[3] || action == ActionOrders[5])
            {
                if (LangRenSha.AnnouncerAction(game, update, false, ActionOrders[3], ActionOrders[5], 50, 51, Name, 4) == GameActionResult.Restart)
                {
                    return GameActionResult.Restart;
                }
            }

            // Judge action - show if target acted and ask to kill or not kill
            if (action == ActionOrders[4])
            {
                return HandleJudgeAction(game, update);
            }

            return GameActionResult.NotExecuted;
        }

        /// <summary>
        /// Handle the guard action - select a player to guard (immune to all deaths).
        /// If no selection at timeout, a random target (not last target) is selected.
        /// </summary>
        private GameActionResult HandleGuardAction(Game game, Dictionary<string, object> update)
        {
            var awkSheMengRen = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
            var awkSheMengRenAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
            var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
            var actionDuration = Game.GetGameDictionaryProperty(game, LangRenSha.dictDurationPlayerReact, ActionDuration);

            if (awkSheMengRenAlive.Count == 0)
            {
                actionDuration = new Random().Next(3, 6);
            }

            // Get last guard target - cannot guard same person twice in a row
            var lastTarget = Game.GetGameDictionaryProperty(game, dictAwkSheMengLastTarget, 0);

            // Build list of valid targets (alive players except last guarded)
            var validTargets = alivePlayers.Where(p => p != lastTarget).ToList();

            if (UserAction.EndUserAction(game, update))
            {
                // Time's up - if no selection, pick random target
                if (awkSheMengRenAlive.Count > 0 && validTargets.Count > 0)
                {
                    var currentTarget = Game.GetGameDictionaryProperty(game, dictAwkSheMengTarget, 0);
                    if (currentTarget == 0)
                    {
                        // No selection made - pick random
                        var randomIndex = new Random().Next(validTargets.Count);
                        var randomTarget = validTargets[randomIndex];
                        update[dictAwkSheMengTarget] = randomTarget;
                        update[dictAwkSheMengLastTarget] = randomTarget;
                        game.Log($"AwkSheMengRen: No selection, randomly guarding player {randomTarget}");
                    }
                }
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }
            else
            {
                if (UserAction.StartUserAction(game, actionDuration, update))
                {
                    update[UserAction.dictUserActionTargets] = awkSheMengRenAlive.Count > 0 ? validTargets : new List<int>();
                    update[UserAction.dictUserActionUsers] = awkSheMengRen;
                    update[UserAction.dictUserActionTargetsCount] = 1;
                    update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.AwkSheMengRen_Act;
                    update[UserAction.dictUserActionRole] = Name;
                    return GameActionResult.Restart;
                }
                else
                {
                    (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, awkSheMengRenAlive, update);
                    if (inputValid)
                    {
                        var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.Input, -1);
                        if (targets.Count == 1 && targets[0] > 0)
                        {
                            update[dictAwkSheMengTarget] = targets[0];
                            update[dictAwkSheMengLastTarget] = targets[0];
                            game.Log($"AwkSheMengRen: Guarding player {targets[0]}");

                            UserAction.EndUserAction(game, update, true);
                            LangRenSha.AdvanceAction(game, update);
                            return GameActionResult.Restart;
                        }
                    }
                }
            }
            return GameActionResult.NotExecuted;
        }

        /// <summary>
        /// Handle the judge action - show if target acted and decide whether to kill the guarded target.
        /// </summary>
        private GameActionResult HandleJudgeAction(Game game, Dictionary<string, object> update)
        {
            var awkSheMengRen = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
            var awkSheMengRenAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
            var target = Game.GetGameDictionaryProperty(game, dictAwkSheMengTarget, 0);
            var hasActed = target > 0 && DetermineIfTargetActed(game, target);
            var actionDuration = Game.GetGameDictionaryProperty(game, LangRenSha.dictDurationPlayerReact, 10);
            var day = Game.GetGameDictionaryProperty(game, LangRenSha.dictDay, 0);

            if (awkSheMengRenAlive.Count == 0 || target == 0)
            {
                actionDuration = new Random().Next(3, 6);
            }

            if (UserAction.EndUserAction(game, update))
            {
                // Clear the guard target at end of action
                update[dictAwkSheMengTarget] = 0;
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }
            else
            {
                if (UserAction.StartUserAction(game, actionDuration, update))
                {
                    // Targets: -1 = kill, -100 = don't kill
                    var targets = new List<int> { -100 };
                    if (day != 0)
                    {
                        targets.Add(-1);
                    }
                    update[UserAction.dictUserActionTargets] = awkSheMengRenAlive.Count > 0 && target > 0 ? targets : new List<int>();
                    update[UserAction.dictUserActionUsers] = awkSheMengRen;
                    update[UserAction.dictUserActionTargetsCount] = 1;
                    update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.AwkSheMengRen_JudgeAct;
                    update[UserAction.dictUserActionRole] = Name;
                    // Info format: "target,acted" where acted is "1" (acted) or "0" (not acted)
                    update[UserAction.dictUserActionInfo] = target > 0 ? $"{target},{(hasActed ? "1" : "0")}" : "";
                    return GameActionResult.Restart;
                }
                else
                {
                    (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, awkSheMengRenAlive, update);
                    if (inputValid)
                    {
                        var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.Input, -1);
                        if (targets.Count == 1)
                        {
                            if (targets[0] == -1 && target > 0)
                            {
                                // Kill the target - disable their shooting ability
                                LangRenSha.SetPlayerProperty(game, target, LieRen.dictHuntingDisabled, 1, update);
                                var aboutToDie = Game.GetGameDictionaryProperty(game, LangRenSha.dictAboutToDie, new List<int>());
                                if (!aboutToDie.Contains(target))
                                {
                                    aboutToDie.Add(target);
                                    update[LangRenSha.dictAboutToDie] = aboutToDie;
                                }
                                game.Log($"AwkSheMengRen: Killed guarded player {target}");
                            }
                            // Clear the guard target
                            update[dictAwkSheMengTarget] = 0;
                            UserAction.EndUserAction(game, update, true);
                            LangRenSha.AdvanceAction(game, update);
                            return GameActionResult.Restart;
                        }
                    }
                }
            }
            return GameActionResult.NotExecuted;
        }

        /// <summary>
        /// Determine if the target has "acted" during the night.
        /// - If night_skippedAct is 1, return false (not acted) - overrides all other checks
        /// - God faction (or beforeConversionFaction is God): acted UNLESS whitelisted
        /// - Evil faction: NOT acted UNLESS able to attack during the night
        /// </summary>
        private bool DetermineIfTargetActed(Game game, int target)
        {
            // Check night_skippedAct first - if 1, target chose not to act
            var skippedAct = LangRenSha.GetPlayerProperty(game, target, dictNightSkippedAct, 0);
            if (skippedAct == 1)
            {
                return false;
            }

            var role = LangRenSha.GetPlayerProperty(game, target, LangRenSha.dictRole, "");

            // Get current faction
            int faction = LangRenSha.GetPlayerProperty(game, target, LangRenSha.dictPlayerFaction, 0);

            // Check before conversion faction
            var beforeFaction = LangRenSha.GetPlayerProperty(game, target, dictBeforeConversionFaction, 0);

            // If current faction is God, or before conversion faction was God, they acted
            bool isGodFaction = (faction & (int)LangRenSha.PlayerFaction.God) != 0;
            bool wasGodFaction = (beforeFaction & (int)LangRenSha.PlayerFaction.God) != 0;

            if (isGodFaction || wasGodFaction)
            {
                if (!NoActWhitelist.Contains(role))
                {
                    return true;
                }
            }

            // If Evil faction, check if they can attack
            bool isEvilFaction = (faction & (int)LangRenSha.PlayerFaction.Evil) != 0;
            if (isEvilFaction)
            {
                // Check if player can attack (has succession that allows attacking)
                var succession = LangRenSha.GetPlayerProperty(game, target, LangRen.dictSuceession, 0);
                // Succession 0 or 1 means can attack as LangRen, 2/3 means converted and may attack if all LangRen dead
                if (succession == 0 || succession == 1)
                {
                    return true; // Regular LangRen can attack
                }
                if (succession == 2 || succession == 3)
                {
                    // Converted player - check if all normal LangRen are dead
                    var normalLangRenAlive = LangRenSha.GetPlayers(game, x =>
                        (string)x[LangRenSha.dictRole] == "LangRen" &&
                        (int)x[LangRenSha.dictAlive] == 1);
                    var succession1Alive = LangRenSha.GetPlayers(game, x =>
                        x.ContainsKey(LangRen.dictSuceession) &&
                        (int)x[LangRen.dictSuceession] == 1 &&
                        (int)x[LangRenSha.dictAlive] == 1);
                    if (normalLangRenAlive.Count + succession1Alive.Count == 0)
                    {
                        return true; // Converted player can attack now
                    }
                }
                return false; // Evil but cannot attack
            }

            // Default: civilians and third party don't act
            return false;
        }

        /// <summary>
        /// Check if a player is protected by AwkSheMengRen (immune to all deaths).
        /// </summary>
        public static bool IsProtected(Game game, int player)
        {
            var target = Game.GetGameDictionaryProperty(game, dictAwkSheMengTarget, 0);
            return target == player && target > 0;
        }

        /// <summary>
        /// Set the night_skippedAct flag for a player.
        /// Call with skipped=true when player selects -100 (no act), false otherwise.
        /// For LangRen, call this for all LangRen players.
        /// </summary>
        public static void SetSkippedAct(Game game, int player, bool skipped, Dictionary<string, object> update)
        {
            LangRenSha.SetPlayerProperty(game, player, dictNightSkippedAct, skipped ? 1 : 0, update);
        }

        /// <summary>
        /// Set the night_skippedAct flag for all players in a list (used for LangRen).
        /// </summary>
        public static void SetSkippedActForAll(Game game, List<int> players, bool skipped, Dictionary<string, object> update)
        {
            foreach (var player in players)
            {
                LangRenSha.SetPlayerProperty(game, player, dictNightSkippedAct, skipped ? 1 : 0, update);
            }
        }
    }
}
