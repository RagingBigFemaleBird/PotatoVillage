using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProcedureCore.LangRenSha
{
    /// <summary>
    /// ZhuangJiaLang (装甲狼 - Armored Wolf) - A werewolf role that mimics another role's skill.
    /// Similar to JiXieLang but acts after it and has succession 3.
    /// Day 0: Select a target to mimic (ShouWei, NvWu, TongLingShi, LieRen, LangRen).
    ///        If selects JiXieLang, mimics what JiXieLang mimicked.
    /// Day 1+: Use the mimicked skill each night.
    /// - ShouWei: SuperGuard (blocks LangRen attack, protects but does NOT reflect NvWu poison when ZhuangJiaLang present)
    /// - NvWu: Poison only (no save)
    /// - TongLingShi: TongLing (reveal exact role)
    /// - LieRen: Copied death handler (shoot on death)
    /// - LangRen: Kill (goes through normal attack check when ZhuangJiaLang present - can double attack to bypass guard)
    /// </summary>
    public class ZhuangJiaLang : Role
    {
        private static Dictionary<string, object> roleDict = new()
        {
            { YuYanJia.dictYuYanJiaResult, 2 },
            { LangRenSha.dictPlayerAlliance, 2 },
            { LangRen.dictSuceession, 3 },
            { LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.Evil },
        };

        private static List<int> actionOrders = new()
        {
            (int)ActionConstant.ZhuangJiaLang_OpenEyes,                  // 0: 48
            (int)ActionConstant.ZhuangJiaLang_Act,                       // 1: 49
            (int)ActionConstant.ZhuangJiaLang_Info,                      // 2: 50
            (int)ActionConstant.ZhuangJiaLang_CloseEyes,                 // 3: 51
            (int)ActionConstant.ZhuangJiaLang_ActAgain_OpenEyes,         // 4: 137
            (int)ActionConstant.ZhuangJiaLang_ActAgain,                  // 5: 138
            (int)ActionConstant.ZhuangJiaLang_ActAgain_CloseEyes,        // 6: 140
            (int)ActionConstant.ZhuangJiaLang_ActAgain_Info_OpenEyes,    // 7: 275 (moved later, before LieRen)
            (int)ActionConstant.ZhuangJiaLang_ActAgain_Info,             // 8: 276
            (int)ActionConstant.ZhuangJiaLang_ActAgain_Info_CloseEyes,   // 9: 277
        };

        public ZhuangJiaLang() { }

        public Dictionary<string, object> RoleDict => roleDict;
        public string Name => "ZhuangJiaLang";
        public int Version => 1;
        public List<int> ActionOrders => actionOrders;
        public int ActionDuration => 15;

        // Player property keys (reuse JiXieLang keys where appropriate)
        public static string dictMimickedRole = "zhuangjialang_mimic";       // Which role was mimicked (string)
        public static string dictSuperGuardTarget = "zhuangjialang_superguard"; // SuperGuard target this night
        public static string dictLangRenKillUsed = "zhuangjialang_kill_used";  // Whether LangRen kill has been used

        /// <summary>
        /// Returns true if ZhuangJiaLang is present in the game.
        /// Used by JiXieLang and ZhuangJiaLang to modify behavior.
        /// </summary>
        public static bool IsPresent(Game game)
        {
            var zjl = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == "ZhuangJiaLang");
            return zjl.Count > 0;
        }

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

            var day = Game.GetGameDictionaryProperty(game, LangRenSha.dictDay, 0);
            var action = Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0);

            // Day 1+: Skip day-0-only actions
            if (day >= 1 && action == actionOrders[1])
            {
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }

            // Day 0: Skip ActAgain act phase entirely (open/act/close, indices 4-6).
            // Skill use is already disallowed on day 0, and the ActAgain Info phase (indices 7-9)
            // always runs in its own later slot, so this skip leaks no information about what was learned.
            if (day == 0 && (action == actionOrders[4] || action == actionOrders[5] || action == actionOrders[6]))
            {
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }

            // Day 0: Open/close eyes around Act
            if (LangRenSha.AnnouncerAction(game, update, false, actionOrders[0], actionOrders[3],
                (int)HintConstant.OpenEyes, (int)HintConstant.CloseEyes, Name, 4) == GameActionResult.Restart)
            {
                return GameActionResult.Restart;
            }

            // Day 1+: Open/close eyes around ActAgain act phase
            if (LangRenSha.AnnouncerAction(game, update, false, actionOrders[4], actionOrders[6],
                (int)HintConstant.OpenEyes, (int)HintConstant.CloseEyes, Name, 4) == GameActionResult.Restart)
            {
                return GameActionResult.Restart;
            }

            // Open/close eyes around ActAgain info phase (moved later, before LieRen).
            // This ensures gun status (for LieRen mimicry) reflects all hunt-disabling skills.
            if (LangRenSha.AnnouncerAction(game, update, false, actionOrders[7], actionOrders[9],
                (int)HintConstant.OpenEyes, (int)HintConstant.CloseEyes, Name, 4) == GameActionResult.Restart)
            {
                return GameActionResult.Restart;
            }

            // === Day 0: Select a target to mimic ===
            if (action == actionOrders[1])
            {
                return HandleMimicSelection(game, update);
            }

            // === Info: Day 0 shows learned role, Day 1+ shows attack status ===
            if (action == actionOrders[2])
            {
                return HandleInfo(game, update);
            }

            // === Day 1+: Use mimicked skill ===
            if (action == actionOrders[5])
            {
                return HandleActAgain(game, update);
            }

            // === ActAgain Info (TongLing result + LieRen gun status) - now in its own later phase ===
            if (action == actionOrders[8])
            {
                return HandleActAgainInfo(game, update);
            }

            return GameActionResult.NotExecuted;
        }

        /// <summary>
        /// Day 0: Select a player to mimic. Records the mimicked role.
        /// If selecting JiXieLang, copies what JiXieLang mimicked.
        /// Returns GameOver if no skill learned when time expires.
        /// </summary>
        private GameActionResult HandleMimicSelection(Game game, Dictionary<string, object> update)
        {
            var zjl = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
            var zjlAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
            var zjlPlayer = zjlAlive.Count > 0 ? zjlAlive[0] : 0;
            var actionDuration = Game.GetGameDictionaryProperty(game, LangRenSha.dictDurationLangRen, ActionDuration);

            if (zjlAlive.Count == 0)
            {
                actionDuration = new Random().Next(3, 6);
            }

            var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
            alivePlayers.Remove(zjlPlayer); // Cannot mimic self

            if (UserAction.EndUserAction(game, update))
            {
                // Time expired — check if a skill was learned
                (var inputValid, var input, var _) = UserAction.GetUserResponse(game, true, zjlAlive, update);
                if (inputValid && zjlPlayer > 0)
                {
                    var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, -1);
                    if (targets.Count > 0 && targets[0] > 0)
                    {
                        ApplyMimicResult(game, zjlPlayer, targets[0], update);
                        LangRenSha.AdvanceAction(game, update);
                        return GameActionResult.Restart;
                    }
                }

                // No valid selection — game over
                return GameActionResult.GameOver;
            }

            if (UserAction.StartUserAction(game, actionDuration, update))
            {
                update[UserAction.dictUserActionTargets] = zjlAlive.Count > 0 ? alivePlayers : new List<int>();
                update[UserAction.dictUserActionUsers] = zjl;
                update[UserAction.dictUserActionTargetsCount] = 1;
                update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.JiXieLang_Act;
                update[UserAction.dictUserActionRole] = Name;
                return GameActionResult.Restart;
            }

            (var inputValid2, var input2, var _2) = UserAction.GetUserResponse(game, true, zjlAlive, update);
            if (inputValid2)
            {
                var targets = UserAction.TallyUserInput(input2, 0, UserAction.UserInputMode.VoteMost, -1);
                if (targets.Count > 0 && targets[0] > 0 && zjlPlayer > 0)
                {
                    ApplyMimicResult(game, zjlPlayer, targets[0], update);
                    UserAction.EndUserAction(game, update, true);
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
            }

            return GameActionResult.NotExecuted;
        }

        private void ApplyMimicResult(Game game, int zjlPlayer, int targetPlayer, Dictionary<string, object> update)
        {
            var targetRole = LangRenSha.GetPlayerProperty(game, targetPlayer, LangRenSha.dictRole, "");

            // Special case: If selecting JiXieLang, copy what JiXieLang mimicked
            if (targetRole == "JiXieLang")
            {
                var jxlMimicked = LangRenSha.GetPlayerProperty(game, targetPlayer, JiXieLang.dictMimickedRole, "");
                if (!string.IsNullOrEmpty(jxlMimicked))
                {
                    targetRole = jxlMimicked;
                }
                else
                {
                    targetRole = "LangRen";
                }
            }
            else
            {
                var validRoles = new HashSet<string> { "ShouWei", "NvWu", "TongLingShi", "LieRen", "LangRen", "HunZi" };
                if (!validRoles.Contains(targetRole))
                {
                    targetRole = "PingMin";
                }
            }

            LangRenSha.SetPlayerProperty(game, zjlPlayer, dictMimickedRole, targetRole, update);
            LangRenSha.SetPlayerProperty(game, zjlPlayer, TongLingShi.dictTongLingShiResult, targetRole, update);
        }

        /// <summary>
        /// Info action: Day 0 shows what role was learned. Day 1+ shows CanAttackTonight status.
        /// </summary>
        private GameActionResult HandleInfo(Game game, Dictionary<string, object> update)
        {
            var day = Game.GetGameDictionaryProperty(game, LangRenSha.dictDay, 0);
            var zjl = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
            var zjlAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
            var zjlPlayer = zjlAlive.Count > 0 ? zjlAlive[0] : 0;

            if (UserAction.EndUserAction(game, update))
            {
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }

            if (UserAction.StartUserAction(game, 5, update))
            {
                string info;
                if (day == 0)
                {
                    // Show the mimicked role
                    info = zjlPlayer > 0
                        ? LangRenSha.GetPlayerProperty(game, zjlPlayer, dictMimickedRole, "")
                        : "";
                }
                else
                {
                    // Show attack status
                    info = CanAttackTonight(game, zjlPlayer) ? "1" : "0";
                }

                update[UserAction.dictUserActionTargets] = new List<int> { 0 };
                update[UserAction.dictUserActionUsers] = zjl;
                update[UserAction.dictUserActionTargetsCount] = 1;
                update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.JiXieLang_Info;
                update[UserAction.dictUserActionRole] = Name;
                update[UserAction.dictUserActionInfo] = info;
                return GameActionResult.Restart;
            }

            (var inputValid, var input, var _) = UserAction.GetUserResponse(game, true, zjlAlive, update);
            if (inputValid)
            {
                UserAction.EndUserAction(game, update, true);
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }

            return GameActionResult.NotExecuted;
        }

        /// <summary>
        /// Checks whether ZhuangJiaLang can attack tonight based on succession rules.
        /// ZhuangJiaLang (succession 3) can attack when all LangRen (succession 0), succession 1, and succession 2 players are dead.
        /// </summary>
        private bool CanAttackTonight(Game game, int zjlPlayer)
        {
            if (zjlPlayer == 0) return false;

            // Get LangRen with succession 0 or 1 (they attack first)
            var langRenAlive = LangRenSha.GetPlayers(game, x =>
                (string)x[LangRenSha.dictRole] == "LangRen" && 
                (int)x[LangRenSha.dictAlive] == 1 &&
                (!x.ContainsKey(LangRen.dictSuceession) || (int)x[LangRen.dictSuceession] == 0 || (int)x[LangRen.dictSuceession] == 1));

            // Get succession 1 players (they also attack with LangRen)
            var succession1Alive = LangRenSha.GetPlayers(game, x =>
                x.ContainsKey(LangRen.dictSuceession) && (int)x[LangRen.dictSuceession] == 1 && (int)x[LangRenSha.dictAlive] == 1);

            // Get succession 2 players (they attack after LangRen and succession 1 are dead)
            var succession2Alive = LangRenSha.GetPlayers(game, x =>
                x.ContainsKey(LangRen.dictSuceession) && (int)x[LangRen.dictSuceession] == 2 && (int)x[LangRenSha.dictAlive] == 1);

            // ZhuangJiaLang can attack when all LangRen, succession 1, and succession 2 players are dead
            return langRenAlive.Count == 0 && succession1Alive.Count == 0 && succession2Alive.Count == 0;
        }

        /// <summary>
        /// Checks whether ZhuangJiaLang can use their mimicked skill tonight.
        /// </summary>
        private bool CanUseSkillTonight(Game game, int zjlPlayer, string mimicked)
        {
            if (zjlPlayer == 0 || string.IsNullOrEmpty(mimicked)) return false;

            switch (mimicked)
            {
                case "NvWu":
                    return LangRenSha.GetPlayerProperty(game, zjlPlayer, NvWu.dictPoisonUsed, 0) == 0;
                case "LangRen":
                    {
                        if (LangRenSha.GetPlayerProperty(game, zjlPlayer, dictLangRenKillUsed, 0) != 0)
                            return false;
                        return CanAttackTonight(game, zjlPlayer);
                    }
                case "ShouWei":
                case "TongLingShi":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Day 1+: Use mimicked skill.
        /// </summary>
        private GameActionResult HandleActAgain(Game game, Dictionary<string, object> update)
        {
            var zjl = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
            var zjlAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
            var zjlPlayer = zjlAlive.Count > 0 ? zjlAlive[0] : 0;
            var mimicked = zjlPlayer > 0
                ? LangRenSha.GetPlayerProperty(game, zjlPlayer, dictMimickedRole, "")
                : "";

            var actionDuration = Game.GetGameDictionaryProperty(game, LangRenSha.dictDurationPlayerReact, ActionDuration);
            var dayForSkill = Game.GetGameDictionaryProperty(game, LangRenSha.dictDay, 0);
            // On day 0, ActAgain runs only to deliver gun status (when LieRen was learned).
            // No skill may be used regardless of what was mimicked.
            bool canUse = dayForSkill >= 1 && CanUseSkillTonight(game, zjlPlayer, mimicked);

            if (zjlAlive.Count == 0 || !canUse)
            {
                actionDuration = new Random().Next(3, 6);
            }

            var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);

            if (UserAction.EndUserAction(game, update))
            {
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }

            if (UserAction.StartUserAction(game, actionDuration, update))
            {
                var targets = canUse ? new List<int>(alivePlayers) : new List<int>();
                targets.Add(-100);

                update[UserAction.dictUserActionTargets] = targets;
                update[UserAction.dictUserActionUsers] = zjl;
                update[UserAction.dictUserActionTargetsCount] = 1;
                update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.JiXieLang_ActAgain;
                update[UserAction.dictUserActionRole] = Name;
                update[UserAction.dictUserActionInfo] = mimicked;
                return GameActionResult.Restart;
            }

            (var inputValid, var input, var _) = UserAction.GetUserResponse(game, true, canUse ? zjlAlive : new List<int>(), update);
            if (inputValid && canUse)
            {
                var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, -1);
                if (targets.Count > 0 && targets[0] > 0)
                {
                    ApplyMimickedSkill(game, zjlPlayer, targets[0], mimicked, update);
                    UserAction.EndUserAction(game, update, true);
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                if (targets.Count > 0 && targets[0] == -100)
                {
                    UserAction.EndUserAction(game, update, true);
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
            }

            return GameActionResult.NotExecuted;
        }

        /// <summary>
        /// Day 1+ ActAgain Info: Shows TongLing result (if TongLingShi) or LieRen shooting status (if LieRen).
        /// </summary>
        private GameActionResult HandleActAgainInfo(Game game, Dictionary<string, object> update)
        {
            var zjl = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
            var zjlAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
            var zjlPlayer = zjlAlive.Count > 0 ? zjlAlive[0] : 0;

            if (UserAction.EndUserAction(game, update))
            {
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }

            if (UserAction.StartUserAction(game, 5, update))
            {
                var mimicked = zjlPlayer > 0
                    ? LangRenSha.GetPlayerProperty(game, zjlPlayer, dictMimickedRole, "")
                    : "";

                string info = "";
                if (mimicked == "TongLingShi")
                {
                    info = zjlPlayer > 0
                        ? LangRenSha.GetPlayerProperty(game, zjlPlayer, TongLingShi.dictTongLingResult, "")
                        : "";
                }
                else if (mimicked == "LieRen")
                {
                    var canShoot = zjlPlayer > 0 &&
                        LangRenSha.GetPlayerProperty(game, zjlPlayer, LieRen.dictHuntingDisabled, 0) == 0;
                    info = "LieRen," + (canShoot ? "1" : "0");
                }

                update[UserAction.dictUserActionTargets] = new List<int> { 0 };
                update[UserAction.dictUserActionUsers] = zjl;
                update[UserAction.dictUserActionTargetsCount] = 1;
                update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.JiXieLang_ActAgain_Info;
                update[UserAction.dictUserActionRole] = Name;
                update[UserAction.dictUserActionInfo] = info;
                return GameActionResult.Restart;
            }

            (var inputValid, var input, var _) = UserAction.GetUserResponse(game, true, zjlAlive, update);
            if (inputValid)
            {
                UserAction.EndUserAction(game, update, true);
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }

            return GameActionResult.NotExecuted;
        }

        private void ApplyMimickedSkill(Game game, int zjlPlayer, int target, string mimicked, Dictionary<string, object> update)
        {
            switch (mimicked)
            {
                case "ShouWei":
                    // SuperGuard: prevents LangRen attack
                    // When ZhuangJiaLang is present, does NOT reflect poison
                    update[dictSuperGuardTarget] = target;
                    break;

                case "NvWu":
                    // Poison only (no save)
                    var nvWu = new NvWu();
                    nvWu.Poison(game, zjlPlayer, target, update);
                    break;

                case "TongLingShi":
                    // TongLing: reveal exact role
                    var tongLingShi = new TongLingShi();
                    tongLingShi.TongLing(game, zjlPlayer, target, update);
                    break;

                case "LangRen":
                    // When ZhuangJiaLang is present, the kill goes through normal attack check
                    // Add to attack list - can double attack to bypass guard
                    var langRenAlive = LangRenSha.GetPlayers(game, x =>
                        (string)x[LangRenSha.dictRole] == "LangRen" && (int)x[LangRenSha.dictAlive] == 1);
                    if (langRenAlive.Count == 0)
                    {
                        var currentAttack = Game.GetGameDictionaryProperty(game, LangRen.dictAttackTarget, new List<int>());
                        currentAttack.Add(target);
                        update[LangRen.dictAttackTarget] = currentAttack;
                        LangRenSha.SetPlayerProperty(game, zjlPlayer, dictLangRenKillUsed, 1, update);
                    }
                    break;
            }
        }

        /// <summary>
        /// Death handler for ZhuangJiaLang that mimicked LieRen - allows shooting on death.
        /// </summary>
        public static (bool, GameActionResult) HandleZhuangJiaLangDeathSkill(Game game, int deadPlayer, Dictionary<string, object> update)
        {
            var isZhuangJiaLang = LangRenSha.GetPlayerProperty(game, deadPlayer, LangRenSha.dictRole, "") == "ZhuangJiaLang";
            var mimicked = LangRenSha.GetPlayerProperty(game, deadPlayer, dictMimickedRole, "");
            var disabled = LangRenSha.GetPlayerProperty(game, deadPlayer, LieRen.dictHuntingDisabled, 0) == 1;

            if (!isZhuangJiaLang || mimicked != "LieRen" || disabled)
            {
                return (false, GameActionResult.NotExecuted);
            }

            // Same logic as LieRen.HandleHunterDeathSkill
            if (UserAction.EndUserAction(game, update))
            {
                return (false, GameActionResult.Restart);
            }

            if (UserAction.StartUserAction(game, 15, update))
            {
                var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
                alivePlayers.Remove(deadPlayer);

                update[UserAction.dictUserActionTargets] = alivePlayers;
                update[UserAction.dictUserActionUsers] = new List<int> { deadPlayer };
                update[UserAction.dictUserActionTargetsCount] = 1;
                update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.HunterKill;
                update[UserAction.dictUserActionRole] = "ZhuangJiaLang";
                update[UserAction.dictUserActionInfo] = "1";
                return (true, GameActionResult.Restart);
            }

            (var inputValid, var input, var _) = UserAction.GetUserResponse(game, true, new List<int> { deadPlayer }, update);
            if (inputValid && input.ContainsKey(deadPlayer.ToString()))
            {
                var targets = (List<int>)input[deadPlayer.ToString()];
                if (targets.Count > 0 && targets[0] > 0)
                {
                    var target = targets[0];
                    LangRenSha.MarkPlayerAboutToDie(game, target, update);
                    LangRenSha.SetPlayerProperty(game, deadPlayer, LieRen.dictHuntingDisabled, 1, update);

                    // Skill-processed tracking owned by HandleDeadPlayerSkills.
                    LangRenSha.SetupSkillUseInterrupt(game, deadPlayer, new List<int> { target }, "hunted", "1", update);

                    return (true, GameActionResult.Restart);
                }
            }
            else
            {
                return (true, GameActionResult.NotExecuted);
            }

            return (true, GameActionResult.Restart);
        }
    }
}
