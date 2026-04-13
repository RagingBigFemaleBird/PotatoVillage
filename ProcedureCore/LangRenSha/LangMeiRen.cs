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

        // Player property keys
        public static string dictLangMeiRenLink = "langmeiren_link";

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
                var lastLink = lmrPlayer > 0 ? LangRenSha.GetPlayerProperty(game, lmrPlayer, dictLangMeiRenLink, 0) : 0;
                if (lastLink > 0)
                {
                    alivePlayers.Remove(lastLink); // Cannot link same target as last time
                }
                alivePlayers.Remove(lmrPlayer); // Cannot link self
                alivePlayers.Add(-100); // "Do not use" option

                if (UserAction.EndUserAction(game, update))
                {
                    LangRenSha.SetPlayerProperty(game, lmrPlayer, dictLangMeiRenLink, 0, update);
                    // Timeout - only set skippedAct to true if not already acted (LangMeiRen is part of LangRen)
                    if (lmrAlive.Count > 0)
                    {
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
                                LangRenSha.SetPlayerProperty(game, lmrPlayer, dictLangMeiRenLink, targets[0], update);
                                // Set skippedAct (not skipped) - linking someone means acted
                                AwkSheMengRen.SetSkippedAct(game, lmrPlayer, false, update);

                                UserAction.EndUserAction(game, update, true);
                                LangRenSha.AdvanceAction(game, update);
                                return GameActionResult.Restart;
                            }
                            if (targets.Count > 0 && targets[0] == -100)
                            {
                                LangRenSha.SetPlayerProperty(game, lmrPlayer, dictLangMeiRenLink, 0, update);
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
        /// Death handler for LangMeiRen - when LangMeiRen dies, the linked player also dies.
        /// The linked player cannot use death skills (e.g., hunter cannot shoot).
        /// Same mechanism as evil Xiong's death handler.
        /// </summary>
        public static (bool, GameActionResult) HandleLangMeiRenDeathSkill(Game game, int deadPlayer, Dictionary<string, object> update)
        {
            var isLangMeiRen = LangRenSha.GetPlayerProperty(game, deadPlayer, LangRenSha.dictRole, "") == "LangMeiRen";
            if (!isLangMeiRen)
            {
                return (false, GameActionResult.NotExecuted);
            }

            var linkedTarget = LangRenSha.GetPlayerProperty(game, deadPlayer, dictLangMeiRenLink, 0);
            if (linkedTarget <= 0)
            {
                return (false, GameActionResult.NotExecuted);
            }

            var targetAlive = LangRenSha.GetPlayerProperty(game, linkedTarget, LangRenSha.dictAlive, 0) == 1;
            if (!targetAlive)
            {
                return (false, GameActionResult.NotExecuted);
            }

            var aboutToDie = Game.GetGameDictionaryProperty(game, LangRenSha.dictAboutToDie, new List<int>());
            if (aboutToDie.Contains(linkedTarget))
            {
                return (false, GameActionResult.NotExecuted);
            }

            // Mark the linked target as about to die
            LangRenSha.MarkPlayerAboutToDie(game, linkedTarget, update);

            // Disable death skills for the linked target
            LangRenSha.SetPlayerProperty(game, linkedTarget, LieRen.dictHuntingDisabled, 1, update);

            var currentAboutToDie = Game.GetGameDictionaryProperty(game, LangRenSha.dictAboutToDie, new List<int>());

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
            update[LangRenSha.dictSkillUseTo] = new List<int> { linkedTarget };
            update[LangRenSha.dictSkillUse] = "linked";
            update[LangRenSha.dictSkillUseResult] = "1"; // Succeeded

            update[LangRenSha.dictInterrupt] = skillAnnouncementInterrupt;
            update[LangRenSha.dictSpeak] = (int)SpeakConstant.SkillUseAnnouncement;
            UserAction.EndUserAction(game, update, true);

            return (true, GameActionResult.Restart);
        }
    }
}
