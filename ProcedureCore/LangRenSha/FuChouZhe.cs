using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcedureCore.LangRenSha
{
    /// <summary>
    /// FuChouZhe (Revenger/复仇者) - A god role similar to LieRen (Hunter).
    /// Can shoot someone at night if dead (unlike LieRen who shoots during day).
    /// Can also kill someone at night if faction is ThirdParty.
    /// Becomes ThirdParty if shadowed by YingZi (Shadow).
    /// </summary>
    public class FuChouZhe : Role
    {
        private static Dictionary<string, object> roleDict = new()
        {
            { YuYanJia.dictYuYanJiaResult, 1 },
            { LangRenSha.dictPlayerAlliance, 0 },
            { LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.Evil },
        };

        private static List<int> actionOrders = new()
        {
            (int)ActionConstant.FuChouZhe_OpenEyes,
            (int)ActionConstant.FuChouZhe_ThirdPartyKill,
            (int)ActionConstant.FuChouZhe_DeadShoot,
            (int)ActionConstant.FuChouZhe_CloseEyes
        };

        public FuChouZhe()
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
                return "FuChouZhe";
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

        // Global dictionary key for FuChouZhe player (use global because role can change via YingZi)
        public static string dictRevengeUsed = "revenge_used";

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

            // Announcer actions for open/close eyes (uses generic 50/51 hints)
            if (LangRenSha.AnnouncerAction(game, update, false, ActionOrders[0], ActionOrders[3], 50, 51, Name, 4) == GameActionResult.Restart)
            {
                return GameActionResult.Restart;
            }

            // FuChouZhe ThirdPartyKill - kill someone if faction is ThirdParty
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == ActionOrders[1])
            {
                return HandleThirdPartyKill(game, update);
            }

            // FuChouZhe DeadShoot - shoot someone if dead (at night)
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == ActionOrders[2])
            {
                return HandleDeadShoot(game, update);
            }

            return GameActionResult.NotExecuted;
        }

        private GameActionResult HandleDeadShoot(Game game, Dictionary<string, object> update)
        {
            var fuChouZhe = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
            var fuChouZheAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
            var fuChouZhePlayer = fuChouZhe.Count > 0 ? fuChouZhe[0] : 0;

            var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
            var actionDuration = Game.GetGameDictionaryProperty(game, LangRenSha.dictDurationPlayerReact, ActionDuration);

            if (fuChouZheAlive.Count == 0)
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
                    var aboutToDie = Game.GetGameDictionaryProperty(game, LangRenSha.dictAboutToDie, new List<int>()).Contains(fuChouZhePlayer);
                    // Can shoot any alive player, or -100 to not shoot
                    var targets = new List<int>();
                    if (aboutToDie)
                    {
                        targets.AddRange(alivePlayers);
                        targets.Remove(fuChouZhePlayer); // Can't shoot self
                    }
                    targets.Add(-100);

                    update[UserAction.dictUserActionTargets] = targets;
                    update[UserAction.dictUserActionUsers] = fuChouZhe;
                    update[UserAction.dictUserActionTargetsCount] = 1;
                    update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.FuChouZhe_DeadShoot;
                    update[UserAction.dictUserActionRole] = Name;
                    return GameActionResult.Restart;
                }
                else
                {
                    (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, fuChouZheAlive, update);
                    if (inputValid)
                    {
                        var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.Input, -1);
                        if (targets.Count == 1 && targets[0] > 0)
                        {
                            var alliance = LangRenSha.GetPlayerProperty(game, fuChouZhePlayer, LangRenSha.dictPlayerAlliance, 0);
                            var targetAlliance = LangRenSha.GetPlayerProperty(game, targets[0], LangRenSha.dictPlayerAlliance, 0);
                            if (alliance != targetAlliance)
                            {
                                // Mark target to die
                                LangRenSha.MarkPlayerAboutToDie(game, targets[0], update);
                            }
                            LangRenSha.SetPlayerProperty(game, fuChouZhePlayer, dictRevengeUsed, 1, update);

                            UserAction.EndUserAction(game, update, true);
                            LangRenSha.AdvanceAction(game, update);
                            return GameActionResult.Restart;
                        }
                        else if (targets.Count == 1 && targets[0] == -100)
                        {
                            // Chose not to shoot
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
        /// Death skill handler - allows revenger to kill someone when they die
        /// </summary>
        public static (bool, GameActionResult) HandleRevengerDeathSkill(Game game, int deadPlayer, Dictionary<string, object> update)
        {
            var isFuChouZhe = LangRenSha.GetPlayerProperty(game, deadPlayer, LangRenSha.dictRole, "") == "FuChouZhe";
            var disabled = LangRenSha.GetPlayerProperty(game, deadPlayer, dictRevengeUsed, 0) == 1;
            if (!isFuChouZhe || disabled)
            {
                return (false, GameActionResult.NotExecuted);
            }

            // Prompt revenger to select a target to kill
            if (UserAction.EndUserAction(game, update))
            {
                // No valid target selected, just mark as processed and continue
                return (true, GameActionResult.Restart);
            }
            else
            {
                // Start user action for revenger to select target
                if (UserAction.StartUserAction(game, 15, update))
                {
                    var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
                    alivePlayers.Remove(deadPlayer); // Hunter cannot kill themselves

                    update[UserAction.dictUserActionTargets] = alivePlayers;
                    update[UserAction.dictUserActionUsers] = new List<int> { deadPlayer };
                    update[UserAction.dictUserActionTargetsCount] = 1;
                    update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.FuChouZhe_DeadShoot;
                    update[UserAction.dictUserActionRole] = "FuChouZhe";
                    update[UserAction.dictUserActionInfo] = "1";
                    return (true, GameActionResult.Restart);
                }
                else
                {
                    (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, new List<int> { deadPlayer }, update);
                    if (inputValid && input.ContainsKey(deadPlayer.ToString()))
                    {
                        var targets = (List<int>)input[deadPlayer.ToString()];
                        if (targets.Count > 0 && targets[0] > 0)
                        {
                            var currentAboutToDie = Game.GetGameDictionaryProperty(game, LangRenSha.dictAboutToDie, new List<int>());
                            var target = targets[0];
                            var alliance = LangRenSha.GetPlayerProperty(game, deadPlayer, LangRenSha.dictPlayerAlliance, 0);
                            var targetAlliance = LangRenSha.GetPlayerProperty(game, target, LangRenSha.dictPlayerAlliance, 0);
                            var revengeSucceeded = alliance != targetAlliance;
                            if (revengeSucceeded)
                            {
                                LangRenSha.MarkPlayerAboutToDie(game, target, update);
                            }
                            LangRenSha.SetPlayerProperty(game, deadPlayer, dictRevengeUsed, 1, update);

                            // Mark this revenger's skill as processed before interrupting
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
                            update[LangRenSha.dictSkillUse] = "revenged";
                            update[LangRenSha.dictSkillUseResult] = revengeSucceeded ? "1" : "0"; // Success depends on alliance

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

        private GameActionResult HandleThirdPartyKill(Game game, Dictionary<string, object> update)
        {
            // Get FuChouZhe player from global dict
            var fuChouZhe = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
            var fuChouZheAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
            var yingZi = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == "YingZi");
            var yingZiAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == "YingZi" && (int)x[LangRenSha.dictAlive] == 1);
            var combined = fuChouZhe.Union(yingZi).ToList();
            var combinedAlive = fuChouZheAlive.Union(yingZiAlive).ToList();
            var fuChouZhePlayer = fuChouZhe.Count > 0 ? fuChouZhe[0] : 0;
            var day0 = Game.GetGameDictionaryProperty(game, LangRenSha.dictDay, 0) == 0;

            var isFuChouZheAlive = LangRenSha.GetPlayerProperty(game, fuChouZhePlayer, LangRenSha.dictAlive, 0) == 1;
            var faction = LangRenSha.GetPlayerProperty(game, fuChouZhePlayer, LangRenSha.dictPlayerFaction, (int)LangRenSha.PlayerFaction.God);
            var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
            var actionDuration = Game.GetGameDictionaryProperty(game, LangRenSha.dictDurationPlayerReact, ActionDuration);
            var isThirdParty = faction == (int)LangRenSha.PlayerFaction.ThirdParty;

            if (isThirdParty)
                actionDuration = Game.GetGameDictionaryProperty(game, LangRenSha.dictDurationLangRen, 60);

            if (UserAction.EndUserAction(game, update))
            {
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }
            else
            {
                if (UserAction.StartUserAction(game, actionDuration, update))
                {
                    var targets = new List<int>();
                    int hintToUse;
                    string infoToShow = "";

                    if (isThirdParty)
                    {
                        // ThirdParty mode: can kill any alive player
                        if (!day0)
                        {
                            targets.AddRange(alivePlayers);
                        }
                        hintToUse = (int)HintConstant.FuChouZhe_ThirdPartyKill;
                    }
                    else
                    {
                        // Not ThirdParty: show alliance info
                        var alliance = LangRenSha.GetPlayerProperty(game, fuChouZhePlayer, LangRenSha.dictPlayerAlliance, 1);
                        infoToShow = alliance == 1 ? "good" : "evil";
                        hintToUse = (int)HintConstant.FuChouZhe_AllianceInfo;
                    }
                    targets.Add(-100);

                    update[UserAction.dictUserActionTargets] = targets;
                    update[UserAction.dictUserActionUsers] = isThirdParty ? combinedAlive : fuChouZheAlive;
                    update[UserAction.dictUserActionTargetsCount] = 1;
                    update[UserAction.dictUserActionTargetsHint] = hintToUse;
                    update[UserAction.dictUserActionInfo] = infoToShow;
                    update[UserAction.dictUserActionRole] = Name;
                    return GameActionResult.Restart;
                }
                else
                {
                    (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, isThirdParty ? combinedAlive : fuChouZheAlive, update);
                    if (inputValid)
                    {
                        var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.Input, -1);
                        if (targets.Count == 1 && targets[0] > 0)
                        {
                            // Mark target to die (only valid in ThirdParty mode)
                            LangRenSha.MarkPlayerAboutToDie(game, targets[0], update);

                            UserAction.EndUserAction(game, update, true);
                            LangRenSha.AdvanceAction(game, update);
                            return GameActionResult.Restart;
                        }
                        else if (targets.Count == 1 && targets[0] == -100)
                        {
                            // Chose not to kill or acknowledged alliance info
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
