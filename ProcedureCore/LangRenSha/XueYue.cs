using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProcedureCore.LangRenSha
{
    /// <summary>
    /// XueYue (血月 - Blood Moon) - A special wolf role that activates upon revealing itself.
    /// When XueYue reveals itself during the day (similar to LangRen reveal), it sets "XueYueRevealed" in game dictionary.
    /// The next night, XueYue's night actions trigger:
    /// - Action 13: LangRen open eyes
    /// - Action 14: XueYue selects a player to kill immediately
    /// - Action 15: LangRen close eyes, then immediately go to day time
    /// </summary>
    public class XueYue : Role
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
            (int)ActionConstant.XueYue_OpenEyes,       // 13
            (int)ActionConstant.XueYue_SelectTarget,   // 14
            (int)ActionConstant.XueYue_CloseEyes,      // 15
        };

        // Dictionary key for tracking XueYue revealed status
        public static string dictXueYueRevealed = "xue_yue_revealed";

        public XueYue() { }

        public Dictionary<string, object> RoleDict => roleDict;
        public string Name => "XueYue";
        public int Version => 1;
        public List<int> ActionOrders => actionOrders;
        public int ActionDuration => 15;

        /// <summary>
        /// Handle reveal self action - XueYue can reveal itself during day phase.
        /// Similar to LangRen's reveal functionality, but also sets XueYueRevealed flag.
        /// </summary>
        public static GameActionResult RevealSelf(Game game, int player, List<int> targets, Dictionary<string, object> update)
        {
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictSpeak, 0) == (int)SpeakConstant.SheriffSpeech ||
                Game.GetGameDictionaryProperty(game, LangRenSha.dictSpeak, 0) == (int)SpeakConstant.DaySpeech ||
                Game.GetGameDictionaryProperty(game, LangRenSha.dictSpeak, 0) == (int)SpeakConstant.WithdrawOrReveal)
            {
                var xueYue = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == "XueYue" && (int)x[LangRenSha.dictAlive] == 1);
                if (xueYue.Contains(player))
                {
                    if (targets.Contains(-10))
                    {
                        // Set XueYueRevealed flag
                        update[dictXueYueRevealed] = 1;

                        var aboutToDie = Game.GetGameDictionaryProperty(game, LangRenSha.dictAboutToDie, new List<int>());
                        update[LangRenSha.dictAboutToDie] = new List<int>() { player };
                        update[LangRenSha.dictSkipDaySpeech] = 1;
                        var interrupted = new Dictionary<string, object>();
                        var currentSpeak = Game.GetGameDictionaryProperty(game, LangRenSha.dictSpeak, 0);
                        interrupted[LangRenSha.dictAboutToDie] = aboutToDie;
                        interrupted[LangRenSha.dictSpeak] = (int)SpeakConstant.DeathAnnouncement;
                        update[LangRenSha.dictSpeak] = (int)SpeakConstant.DeathHandlingInterrupt;
                        update[LangRenSha.dictInterrupt] = interrupted;
                        UserAction.EndUserAction(game, update, true);
                        return GameActionResult.Restart;
                    }
                }
            }
            return GameActionResult.NotExecuted;
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

            var action = Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0);
            var xueYueRevealed = Game.GetGameDictionaryProperty(game, dictXueYueRevealed, 0);

            // Skip all XueYue actions if XueYue has not revealed
            if (xueYueRevealed == 0)
            {
                if (action == ActionOrders[0] || action == ActionOrders[1] || action == ActionOrders[2])
                {
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                return GameActionResult.NotExecuted;
            }

            // Action 13: Open eyes announcement
            if (action == ActionOrders[0])
            {
                if (UserAction.EndUserAction(game, update))
                {
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, 4, update))
                    {
                        update[UserAction.dictUserActionTargets] = new List<int>();
                        update[UserAction.dictUserActionUsers] = new List<int>() { -1 };
                        update[UserAction.dictUserActionTargetsCount] = 0;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.OpenEyes;
                        update[UserAction.dictUserActionInfo] = "LangRen";
                        return GameActionResult.Restart;
                    }
                }
                return GameActionResult.NotExecuted;
            }

            // Action 14: LangRen selects a target to kill immediately
            if (action == ActionOrders[1])
            {
                return HandleSelectTarget(game, update);
            }

            // Action 15: Close eyes and go to day time immediately
            if (action == ActionOrders[2])
            {
                if (UserAction.EndUserAction(game, update))
                {
                    // Clear XueYueRevealed flag and go directly to day time
                    update[dictXueYueRevealed] = 0;
                    update[LangRenSha.dictAction] = (int)ActionConstant.DayTimeAnnouncement;
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, 4, update))
                    {
                        update[UserAction.dictUserActionTargets] = new List<int>();
                        update[UserAction.dictUserActionUsers] = new List<int>() { -1 };
                        update[UserAction.dictUserActionTargetsCount] = 0;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.CloseEyes;
                        update[UserAction.dictUserActionInfo] = "LangRen";
                        return GameActionResult.Restart;
                    }
                }
                return GameActionResult.NotExecuted;
            }

            return GameActionResult.NotExecuted;
        }

        private GameActionResult HandleSelectTarget(Game game, Dictionary<string, object> update)
        {
            // Get LangRen players with succession logic (same as LangRen.cs)
            var langRen = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == "LangRen" && (!x.ContainsKey(LangRen.dictSuceession) || (int)x[LangRen.dictSuceession] == 0 || (int)x[LangRen.dictSuceession] == 1));
            var langRenSuccession1 = LangRenSha.GetPlayers(game, x => x.ContainsKey(LangRen.dictSuceession) && (int)x[LangRen.dictSuceession] == 1);
            var langRenSuccession2 = LangRenSha.GetPlayers(game, x => x.ContainsKey(LangRen.dictSuceession) && (int)x[LangRen.dictSuceession] == 2);
            var langRenSuccession3 = LangRenSha.GetPlayers(game, x => x.ContainsKey(LangRen.dictSuceession) && (int)x[LangRen.dictSuceession] == 3);
            var langRenAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == "LangRen" && (int)x[LangRenSha.dictAlive] == 1 && (!x.ContainsKey(LangRen.dictSuceession) || (int)x[LangRen.dictSuceession] == 0 || (int)x[LangRen.dictSuceession] == 1));
            var langRenSuccession1Alive = LangRenSha.GetPlayers(game, x => x.ContainsKey(LangRen.dictSuceession) && (int)x[LangRen.dictSuceession] == 1 && (int)x[LangRenSha.dictAlive] == 1);
            var langRenSuccession2Alive = LangRenSha.GetPlayers(game, x => x.ContainsKey(LangRen.dictSuceession) && (int)x[LangRen.dictSuceession] == 2 && (int)x[LangRenSha.dictAlive] == 1);
            var langRenSuccession3Alive = LangRenSha.GetPlayers(game, x => x.ContainsKey(LangRen.dictSuceession) && (int)x[LangRen.dictSuceession] == 3 && (int)x[LangRenSha.dictAlive] == 1);

            langRen.AddRange(langRenSuccession1);
            langRenAlive.AddRange(langRenSuccession1Alive);

            if (langRenAlive.Count == 0)
            {
                langRen = langRenSuccession2;
                langRenAlive = langRenSuccession2Alive;
            }
            if (langRenAlive.Count == 0)
            {
                langRen = langRenSuccession3;
                langRenAlive = langRenSuccession3Alive;
            }
            if (langRenAlive.Count == 0)
            {
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }

            // Get all alive players as potential targets (excluding LangRen alliance)
            var alivePlayers = LangRenSha.GetPlayers(game, x => 
                (int)x[LangRenSha.dictAlive] == 1 && 
                (int)x[LangRenSha.dictPlayerAlliance] != 2);

            var actionDuration = Game.GetGameDictionaryProperty(game, LangRenSha.dictDurationLangRen, ActionDuration);

            if (UserAction.EndUserAction(game, update))
            {
                (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, langRenAlive, update);
                if (inputValid)
                {
                    var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, -1);
                    if (targets.Count > 0 && targets[0] > 0)
                    {
                        var target = targets[0];
                        // Kill the target immediately
                        var aboutToDie = Game.GetGameDictionaryProperty(game, LangRenSha.dictAboutToDie, new List<int>());
                        aboutToDie.Add(target);
                        LangRenSha.SetPlayerProperty(game, target, LieRen.dictHuntingDisabled, 1, update);
                        update[LangRenSha.dictAboutToDie] = aboutToDie;
                    }
                }
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }
            else
            {
                if (UserAction.StartUserAction(game, actionDuration, update))
                {
                    update[UserAction.dictUserActionTargets] = alivePlayers;
                    update[UserAction.dictUserActionUsers] = langRen;
                    update[UserAction.dictUserActionTargetsCount] = 1;
                    update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.XueYue_SelectTarget;
                    update[UserAction.dictUserActionRole] = "LangRen";
                    return GameActionResult.Restart;
                }
                else
                {
                    // Check for early submission
                    (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, false, langRenAlive, update);
                    if (inputValid)
                    {
                        bool allResponded = true;
                        foreach (var player in langRenAlive)
                        {
                            if (!input.ContainsKey(player.ToString()))
                            {
                                allResponded = false;
                                break;
                            }
                        }
                        if (allResponded)
                        {
                            (inputValid, input, input_others) = UserAction.GetUserResponse(game, true, langRenAlive, update);
                            var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, -1);
                            if (targets.Count > 0 && targets[0] > 0)
                            {
                                var target = targets[0];
                                // Kill the target immediately
                                var aboutToDie = Game.GetGameDictionaryProperty(game, LangRenSha.dictAboutToDie, new List<int>());
                                aboutToDie.Add(target);
                                LangRenSha.SetPlayerProperty(game, target, LieRen.dictHuntingDisabled, 1, update);
                                update[LangRenSha.dictAboutToDie] = aboutToDie;
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
    }
}
