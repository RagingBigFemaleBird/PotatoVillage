using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProcedureCore.LangRenSha
{
    /// <summary>
    /// JiXieLang (机械狼 - Mechanical Wolf) - A werewolf role that mimics another role's skill.
    /// Day 0: Select a target to mimic (ShouWei, NvWu, TongLingShi, LieRen, LangRen).
    /// Day 1+: Use the mimicked skill each night.
    /// - ShouWei: SuperGuard (blocks LangRen attack + reflects NvWu poison)
    /// - NvWu: Poison only (no save)
    /// - TongLingShi: TongLing (reveal exact role)
    /// - LieRen: Copied death handler (shoot on death)
    /// - LangRen: Kill immediately if all LangRen are dead (bypasses ShouWei)
    /// </summary>
    public class JiXieLang : Role
    {
        private static Dictionary<string, object> roleDict = new()
        {
            { YuYanJia.dictYuYanJiaResult, 2 },
            { LangRenSha.dictPlayerAlliance, 2 },
            { LangRen.dictSuceession, 2 },
            { LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.Evil },
        };

        private static List<int> actionOrders = new()
        {
            (int)ActionConstant.JiXieLang_OpenEyes,         // 0: 44
            (int)ActionConstant.JiXieLang_Act,              // 1: 45
            (int)ActionConstant.JiXieLang_Info,             // 2: 46
            (int)ActionConstant.JiXieLang_CloseEyes,        // 3: 47
            (int)ActionConstant.JiXieLang_ActAgain_OpenEyes,// 4: 133
            (int)ActionConstant.JiXieLang_ActAgain,         // 5: 134
            (int)ActionConstant.JiXieLang_ActAgain_Info,    // 6: 135
            (int)ActionConstant.JiXieLang_ActAgain_CloseEyes,// 7: 136
        };

        public JiXieLang() { }

        public Dictionary<string, object> RoleDict => roleDict;
        public string Name => "JiXieLang";
        public int Version => 1;
        public List<int> ActionOrders => actionOrders;
        public int ActionDuration => 15;

        // Player property keys
        public static string dictMimickedRole = "jixielang_mimic";       // Which role was mimicked (string)
        public static string dictSuperGuardTarget = "jixielang_superguard"; // SuperGuard target this night
        public static string dictLangRenKillUsed = "jixielang_kill_used";  // Whether LangRen kill has been used

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

            // Day 1+: Skip day-0-only actions (24-27)
            if (day >= 1 && action == actionOrders[1])
            {
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }

            // Day 0: Skip day-1+ actions (105-108)
            if (day == 0 && (action == actionOrders[4] || action == actionOrders[5] ||
                             action == actionOrders[6] || action == actionOrders[7]))
            {
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }

            // Day 0: Open/close eyes around Act (24 open, 27 close)
            if (LangRenSha.AnnouncerAction(game, update, false, actionOrders[0], actionOrders[3],
                (int)HintConstant.OpenEyes, (int)HintConstant.CloseEyes, Name, 4) == GameActionResult.Restart)
            {
                return GameActionResult.Restart;
            }

            // Day 1+: Open/close eyes around ActAgain (105 open, 108 close)
            if (LangRenSha.AnnouncerAction(game, update, false, actionOrders[4], actionOrders[7],
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

            // === Day 1+: ActAgain Info (TongLing result + attack status) ===
            if (action == actionOrders[6])
            {
                return HandleActAgainInfo(game, update);
            }

            return GameActionResult.NotExecuted;
        }

        /// <summary>
        /// Day 0: Select a player to mimic. Records the mimicked role and sets TongLing result.
        /// Returns GameOver if no skill learned when time expires.
        /// </summary>
        private GameActionResult HandleMimicSelection(Game game, Dictionary<string, object> update)
        {
            var jxl = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
            var jxlAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
            var jxlPlayer = jxlAlive.Count > 0 ? jxlAlive[0] : 0;
            var actionDuration = Game.GetGameDictionaryProperty(game, LangRenSha.dictDurationLangRen, ActionDuration);

            if (jxlAlive.Count == 0)
            {
                actionDuration = new Random().Next(3, 6);
            }

            var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
            alivePlayers.Remove(jxlPlayer); // Cannot mimic self

            if (UserAction.EndUserAction(game, update))
            {
                // Time expired — check if a skill was learned
                (var inputValid, var input, var _) = UserAction.GetUserResponse(game, true, jxlAlive, update);
                if (inputValid && jxlPlayer > 0)
                {
                    var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, -1);
                    if (targets.Count > 0 && targets[0] > 0)
                    {
                        ApplyMimicResult(game, jxlPlayer, targets[0], update);
                        LangRenSha.AdvanceAction(game, update);
                        return GameActionResult.Restart;
                    }
                }

                // No valid selection — game over
                return GameActionResult.GameOver;
            }

            if (UserAction.StartUserAction(game, actionDuration, update))
            {
                update[UserAction.dictUserActionTargets] = jxlAlive.Count > 0 ? alivePlayers : new List<int>();
                update[UserAction.dictUserActionUsers] = jxl;
                update[UserAction.dictUserActionTargetsCount] = 1;
                update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.JiXieLang_Act;
                update[UserAction.dictUserActionRole] = Name;
                return GameActionResult.Restart;
            }

            (var inputValid2, var input2, var _2) = UserAction.GetUserResponse(game, true, jxlAlive, update);
            if (inputValid2)
            {
                var targets = UserAction.TallyUserInput(input2, 0, UserAction.UserInputMode.VoteMost, -1);
                if (targets.Count > 0 && targets[0] > 0 && jxlPlayer > 0)
                {
                    ApplyMimicResult(game, jxlPlayer, targets[0], update);
                    UserAction.EndUserAction(game, update, true);
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
            }

            return GameActionResult.NotExecuted;
        }

        private void ApplyMimicResult(Game game, int jxlPlayer, int targetPlayer, Dictionary<string, object> update)
        {
            var targetRole = LangRenSha.GetPlayerProperty(game, targetPlayer, LangRenSha.dictRole, "");

            var validRoles = new HashSet<string> { "ShouWei", "NvWu", "TongLingShi", "LieRen", "LangRen" };
            if (!validRoles.Contains(targetRole))
            {
                targetRole = "PingMin";
            }

            LangRenSha.SetPlayerProperty(game, jxlPlayer, dictMimickedRole, targetRole, update);
            LangRenSha.SetPlayerProperty(game, jxlPlayer, TongLingShi.dictTongLingShiResult, targetRole, update);
        }

        /// <summary>
        /// Info action: Day 0 shows what role was learned. Day 1+ shows CanAttackTonight status.
        /// </summary>
        private GameActionResult HandleInfo(Game game, Dictionary<string, object> update)
        {
            var day = Game.GetGameDictionaryProperty(game, LangRenSha.dictDay, 0);
            var jxl = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
            var jxlAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
            var jxlPlayer = jxlAlive.Count > 0 ? jxlAlive[0] : 0;

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
                    info = jxlPlayer > 0
                        ? LangRenSha.GetPlayerProperty(game, jxlPlayer, dictMimickedRole, "")
                        : "";
                }
                else
                {
                    // Show attack status
                    info = CanAttackTonight(game, jxlPlayer) ? "1" : "0";
                }

                update[UserAction.dictUserActionTargets] = new List<int> { 0 };
                update[UserAction.dictUserActionUsers] = jxl;
                update[UserAction.dictUserActionTargetsCount] = 1;
                update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.JiXieLang_Info;
                update[UserAction.dictUserActionRole] = Name;
                update[UserAction.dictUserActionInfo] = info;
                return GameActionResult.Restart;
            }

            (var inputValid, var input, var _) = UserAction.GetUserResponse(game, true, jxlAlive, update);
            if (inputValid)
            {
                UserAction.EndUserAction(game, update, true);
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }

            return GameActionResult.NotExecuted;
        }

        /// <summary>
        /// Checks whether JiXieLang can attack tonight based on all LangRen being dead.
        /// Always returns attack status regardless of mimicked role.
        /// </summary>
        private bool CanAttackTonight(Game game, int jxlPlayer)
        {
            if (jxlPlayer == 0) return false;

            var langRenAlive = LangRenSha.GetPlayers(game, x =>
                (string)x[LangRenSha.dictRole] == "LangRen" && (int)x[LangRenSha.dictAlive] == 1);
            return langRenAlive.Count == 0;
        }

        /// <summary>
        /// Checks whether JiXieLang can use their mimicked skill tonight.
        /// </summary>
        private bool CanUseSkillTonight(Game game, int jxlPlayer, string mimicked)
        {
            if (jxlPlayer == 0 || string.IsNullOrEmpty(mimicked)) return false;

            switch (mimicked)
            {
                case "NvWu":
                    return LangRenSha.GetPlayerProperty(game, jxlPlayer, NvWu.dictPoisonUsed, 0) == 0;
                case "LangRen":
                {
                    if (LangRenSha.GetPlayerProperty(game, jxlPlayer, dictLangRenKillUsed, 0) != 0)
                        return false;
                    return CanAttackTonight(game, jxlPlayer);
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
            var jxl = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
            var jxlAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
            var jxlPlayer = jxlAlive.Count > 0 ? jxlAlive[0] : 0;
            var mimicked = jxlPlayer > 0
                ? LangRenSha.GetPlayerProperty(game, jxlPlayer, dictMimickedRole, "")
                : "";

            var actionDuration = Game.GetGameDictionaryProperty(game, LangRenSha.dictDurationPlayerReact, ActionDuration);
            bool canUse = CanUseSkillTonight(game, jxlPlayer, mimicked);

            if (jxlAlive.Count == 0 || !canUse)
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
                update[UserAction.dictUserActionUsers] = jxl;
                update[UserAction.dictUserActionTargetsCount] = 1;
                update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.JiXieLang_ActAgain;
                update[UserAction.dictUserActionRole] = Name;
                update[UserAction.dictUserActionInfo] = mimicked;
                return GameActionResult.Restart;
            }

            (var inputValid, var input, var _) = UserAction.GetUserResponse(game, true, canUse ? jxlAlive : new List<int>(), update);
            if (inputValid && canUse)
            {
                var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, -1);
                if (targets.Count > 0 && targets[0] > 0)
                {
                    ApplyMimickedSkill(game, jxlPlayer, targets[0], mimicked, update);
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
            var jxl = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
            var jxlAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
            var jxlPlayer = jxlAlive.Count > 0 ? jxlAlive[0] : 0;

            if (UserAction.EndUserAction(game, update))
            {
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }

            if (UserAction.StartUserAction(game, 5, update))
            {
                var mimicked = jxlPlayer > 0
                    ? LangRenSha.GetPlayerProperty(game, jxlPlayer, dictMimickedRole, "")
                    : "";

                string info = "";
                if (mimicked == "TongLingShi")
                {
                    info = Game.GetGameDictionaryProperty(game, TongLingShi.dictTongLingResult, "");
                }
                else if (mimicked == "LieRen")
                {
                    var canShoot = jxlPlayer > 0 &&
                        LangRenSha.GetPlayerProperty(game, jxlPlayer, LieRen.dictHuntingDisabled, 0) == 0;
                    info = "LieRen," + (canShoot ? "1" : "0");
                }

                update[UserAction.dictUserActionTargets] = new List<int> { 0 };
                update[UserAction.dictUserActionUsers] = jxl;
                update[UserAction.dictUserActionTargetsCount] = 1;
                update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.JiXieLang_ActAgain_Info;
                update[UserAction.dictUserActionRole] = Name;
                update[UserAction.dictUserActionInfo] = info;
                return GameActionResult.Restart;
            }

            (var inputValid, var input, var _) = UserAction.GetUserResponse(game, true, jxlAlive, update);
            if (inputValid)
            {
                UserAction.EndUserAction(game, update, true);
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }

            return GameActionResult.NotExecuted;
        }

        private void ApplyMimickedSkill(Game game, int jxlPlayer, int target, string mimicked, Dictionary<string, object> update)
        {
            switch (mimicked)
            {
                case "ShouWei":
                    // SuperGuard: prevents LangRen attack AND reflects NvWu poison
                    // Only set game-level property; NvWu.Poison and LangRen.Sha check this directly
                    update[dictSuperGuardTarget] = target;
                    break;

                case "NvWu":
                    // Poison only (no save)
                    var nvWu = new NvWu();
                    nvWu.Poison(game, jxlPlayer, target, update);
                    break;

                case "TongLingShi":
                    // TongLing: reveal exact role
                    var tongLingShi = new TongLingShi();
                    tongLingShi.TongLing(game, target, update);
                    break;

                case "LangRen":
                    // Kill target immediately (bypasses ShouWei), only if all LangRen dead
                    var langRenAlive = LangRenSha.GetPlayers(game, x =>
                        (string)x[LangRenSha.dictRole] == "LangRen" && (int)x[LangRenSha.dictAlive] == 1);
                    if (langRenAlive.Count == 0)
                    {
                        LangRenSha.MarkPlayerAboutToDie(game, target, update);
                        LangRenSha.SetPlayerProperty(game, jxlPlayer, dictLangRenKillUsed, 1, update);
                    }
                    break;
            }
        }

        /// <summary>
        /// Death handler for JiXieLang that mimicked LieRen - allows shooting on death.
        /// Must be registered as a dead player handler.
        /// </summary>
        public static (bool, GameActionResult) HandleJiXieLangDeathSkill(Game game, int deadPlayer, Dictionary<string, object> update)
        {
            var isJiXieLang = LangRenSha.GetPlayerProperty(game, deadPlayer, LangRenSha.dictRole, "") == "JiXieLang";
            var mimicked = LangRenSha.GetPlayerProperty(game, deadPlayer, dictMimickedRole, "");
            var disabled = LangRenSha.GetPlayerProperty(game, deadPlayer, LieRen.dictHuntingDisabled, 0) == 1;

            if (!isJiXieLang || mimicked != "LieRen" || disabled)
            {
                return (false, GameActionResult.NotExecuted);
            }

            // Same logic as LieRen.HandleHunterDeathSkill
            if (UserAction.EndUserAction(game, update))
            {
                return (true, GameActionResult.Restart);
            }

            if (UserAction.StartUserAction(game, 15, update))
            {
                var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
                alivePlayers.Remove(deadPlayer);

                update[UserAction.dictUserActionTargets] = alivePlayers;
                update[UserAction.dictUserActionUsers] = new List<int> { deadPlayer };
                update[UserAction.dictUserActionTargetsCount] = 1;
                update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.HunterKill;
                update[UserAction.dictUserActionRole] = "JiXieLang";
                update[UserAction.dictUserActionInfo] = "1";
                return (true, GameActionResult.Restart);
            }

            (var inputValid, var input, var _) = UserAction.GetUserResponse(game, true, new List<int> { deadPlayer }, update);
            if (inputValid && input.ContainsKey(deadPlayer.ToString()))
            {
                var targets = (List<int>)input[deadPlayer.ToString()];
                if (targets.Count > 0 && targets[0] > 0)
                {
                    var currentAboutToDie = Game.GetGameDictionaryProperty(game, LangRenSha.dictAboutToDie, new List<int>());
                    var target = targets[0];
                    LangRenSha.MarkPlayerAboutToDie(game, target, update);
                    LangRenSha.SetPlayerProperty(game, deadPlayer, LieRen.dictHuntingDisabled, 1, update);

                    var skillsProcessed = Game.GetGameDictionaryProperty(game, LangRenSha.dictDeadSkillsProcessed, new List<int>());
                    if (!skillsProcessed.Contains(deadPlayer))
                    {
                        skillsProcessed.Add(deadPlayer);
                        update[LangRenSha.dictDeadSkillsProcessed] = skillsProcessed;
                    }

                    // Set up interrupt chain: SkillUseAnnouncement -> DeathHandling -> Continue processing
                    var currentInterrupt = Game.GetGameDictionaryProperty(game, LangRenSha.dictInterrupt, new Dictionary<string, object>());
                    var currentDeadPlayers = Game.GetGameDictionaryProperty(game, LangRenSha.dictDeadPlayerAction, new List<int>());

                    // Inner interrupt: return to continue processing dead player skills
                    var deathHandlingInterrupt = new Dictionary<string, object>();
                    deathHandlingInterrupt[LangRenSha.dictSpeak] = 98; // Return to continue processing dead player skills
                    deathHandlingInterrupt[LangRenSha.dictInterrupt] = currentInterrupt;

                    // Save death-related fields for restoration
                    deathHandlingInterrupt[LangRenSha.dictDeadPlayerAction] = currentDeadPlayers;
                    deathHandlingInterrupt[LangRenSha.dictDeadSkillsProcessed] = skillsProcessed;
                    deathHandlingInterrupt[LangRenSha.dictAboutToDie] = currentAboutToDie;

                    // Outer interrupt: skill use announcement -> death handling
                    var skillAnnouncementInterrupt = new Dictionary<string, object>();
                    skillAnnouncementInterrupt[LangRenSha.dictSpeak] = (int)SpeakConstant.DeathHandlingInterrupt; // Go to death handling after announcement
                    skillAnnouncementInterrupt[LangRenSha.dictInterrupt] = deathHandlingInterrupt;

                    // Set up skill use announcement fields
                    update[LangRenSha.dictSkillUseFrom] = deadPlayer;
                    update[LangRenSha.dictSkillUseTo] = new List<int> { target };
                    update[LangRenSha.dictSkillUse] = "hunted";
                    update[LangRenSha.dictSkillUseResult] = "1"; // Succeeded

                    update[LangRenSha.dictInterrupt] = skillAnnouncementInterrupt;
                    update[LangRenSha.dictSpeak] = (int)SpeakConstant.SkillUseAnnouncement;
                    UserAction.EndUserAction(game, update, true);

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
