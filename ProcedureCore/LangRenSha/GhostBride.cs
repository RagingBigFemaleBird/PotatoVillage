using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProcedureCore.LangRenSha
{
    /// <summary>
    /// GhostBride (鬼魂新娘) - A third party role.
    /// On Day 0, chooses a Groom to link (both die together).
    /// The couple then chooses a Witness who knows their identity.
    /// Win condition: Survive as a couple.
    /// </summary>
    public class GhostBride : Role
    {
        public static string dictLinkedTo = "LinkedTo";
        public static string dictWitness = "witness";
        public static string dictBridePlayer = "bride_player";
        public static string dictGroomPlayer = "groom_player";

        private static Dictionary<string, object> roleDict = new()
        {
            { YuYanJia.dictYuYanJiaResult, 1 },
            { LangRenSha.dictPlayerAlliance, 3 },
            { LangRen.dictSuceession, 2 },
            { LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.ThirdParty },
        };

        private static List<int> actionOrders = new()
        {
            (int)ActionConstant.GhostBride_OpenEyes,
            (int)ActionConstant.GhostBride_ChooseGroom,
            (int)ActionConstant.GhostBride_CloseEyes,
            (int)ActionConstant.GhostBride_GroomOpenEyes,
            (int)ActionConstant.GhostBride_GroomCheckLinked,
            (int)ActionConstant.GhostBride_GroomCloseEyes,
            (int)ActionConstant.GhostBride_CoupleOpenEyes,
            (int)ActionConstant.GhostBride_CoupleChooseWitness,
            (int)ActionConstant.GhostBride_CoupleCloseEyes,
            (int)ActionConstant.GhostBride_WitnessLuckyOpenEyes,
            (int)ActionConstant.GhostBride_WitnessCheckLinked,
            (int)ActionConstant.GhostBride_WitnessLuckyCloseEyes,
            (int)ActionConstant.GhostBride_WitnessOpenEyes,
            (int)ActionConstant.GhostBride_WitnessInfo,
            (int)ActionConstant.GhostBride_WitnessCloseEyes,
        };

        public GhostBride()
        {
        }

        public Dictionary<string, object> RoleDict => roleDict;

        public string Name => "GhostBride";

        public int Version => 1;

        public List<int> ActionOrders => actionOrders;

        public int ActionDuration => 30;

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

            var currentAction = Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0);
            var dayNumber = Game.GetGameDictionaryProperty(game, LangRenSha.dictDay, 0);

            // Day 0 only actions: 29-34, 38-40
            bool isDay0OnlyAction = currentAction >= (int)ActionConstant.GhostBride_OpenEyes && 
                                    currentAction <= (int)ActionConstant.GhostBride_GroomCloseEyes ||
                                    currentAction >= (int)ActionConstant.GhostBride_WitnessLuckyOpenEyes && 
                                    currentAction <= (int)ActionConstant.GhostBride_WitnessLuckyCloseEyes;

            if (isDay0OnlyAction && dayNumber > 0)
            {
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }

            // Bride open/close eyes announcements (Day 0)
            if (LangRenSha.AnnouncerAction(game, update, false, (int)ActionConstant.GhostBride_OpenEyes, (int)ActionConstant.GhostBride_CloseEyes, 50, 51, Name, 4) == GameActionResult.Restart)
            {
                return GameActionResult.Restart;
            }

            // Action 30: Bride chooses Groom (Day 0)
            if (currentAction == (int)ActionConstant.GhostBride_ChooseGroom)
            {
                return HandleChooseGroom(game, update);
            }

            // Groom open/close eyes announcements (Day 0)
            if (LangRenSha.AnnouncerAction(game, update, false, (int)ActionConstant.GhostBride_GroomOpenEyes, (int)ActionConstant.GhostBride_GroomCloseEyes, 1000, 1001, "", 4) == GameActionResult.Restart)
            {
                return GameActionResult.Restart;
            }

            // Action 33: Groom checks if linked (Day 0)
            if (currentAction == (int)ActionConstant.GhostBride_GroomCheckLinked)
            {
                return HandleGroomCheckLinked(game, update);
            }

            // Couple open/close eyes announcements (Day 0)
            if (LangRenSha.AnnouncerAction(game, update, false, (int)ActionConstant.GhostBride_CoupleOpenEyes, (int)ActionConstant.GhostBride_CoupleCloseEyes, 50, 51, "BrideGroom", 4) == GameActionResult.Restart)
            {
                return GameActionResult.Restart;
            }

            // Action 36: Couple chooses Witness (Day 0) / Shows attack status (Day 1+)
            if (currentAction == (int)ActionConstant.GhostBride_CoupleChooseWitness)
            {
                return HandleCoupleChooseWitness(game, update, dayNumber);
            }

            // Witness open/close eyes announcements (Day 0)
            if (LangRenSha.AnnouncerAction(game, update, false, (int)ActionConstant.GhostBride_WitnessLuckyOpenEyes, (int)ActionConstant.GhostBride_WitnessLuckyCloseEyes, 1000, 1001, "", 4) == GameActionResult.Restart)
            {
                return GameActionResult.Restart;
            }

            // Action 39: Witness checks if they're witness (Day 0)
            if (currentAction == (int)ActionConstant.GhostBride_WitnessCheckLinked)
            {
                return HandleWitnessCheckLinked(game, update);
            }

            // Witness open/close eyes announcements
            if (LangRenSha.AnnouncerAction(game, update, false, (int)ActionConstant.GhostBride_WitnessOpenEyes, (int)ActionConstant.GhostBride_WitnessCloseEyes, 50, 51, "Witness", 4) == GameActionResult.Restart)
            {
                return GameActionResult.Restart;
            }

            // Action 42: Witness info (Day 0: who's bride/groom, Day 1+: attack status)
            if (currentAction == (int)ActionConstant.GhostBride_WitnessInfo)
            {
                return HandleWitnessInfo(game, update, dayNumber);
            }

            return GameActionResult.NotExecuted;
        }

        private GameActionResult HandleChooseGroom(Game game, Dictionary<string, object> update)
        {
            var bride = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
            var brideAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
            var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
            
            // Exclude the bride from targets
            var validTargets = alivePlayers.Where(p => !bride.Contains(p)).ToList();

            var actionDuration = Game.GetGameDictionaryProperty(game, LangRenSha.dictDurationLangRen, ActionDuration);
            if (brideAlive.Count == 0)
            {
                actionDuration = new Random().Next(3, 6);
            }

            if (UserAction.EndUserAction(game, update))
            {
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }

            if (UserAction.StartUserAction(game, actionDuration, update))
            {
                update[UserAction.dictUserActionTargets] = validTargets;
                update[UserAction.dictUserActionUsers] = bride;
                update[UserAction.dictUserActionTargetsCount] = 1;
                update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.GhostBride_ChooseGroom;
                update[UserAction.dictUserActionRole] = Name;
                return GameActionResult.Restart;
            }

            (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, brideAlive, update);
            if (inputValid)
            {
                var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, -1);
                if (targets.Count == 0)
                {
                    return GameActionResult.NotExecuted;
                }

                var groomTarget = targets[0];
                if (groomTarget > 0 && brideAlive.Count > 0)
                {
                    var bridePlayer = brideAlive[0];
                    
                    // Link bride to groom
                    LangRenSha.SetPlayerProperty(game, bridePlayer, dictLinkedTo, groomTarget, update);
                    LangRenSha.SetPlayerProperty(game, bridePlayer, dictGroomPlayer, groomTarget, update);
                    LangRenSha.SetPlayerProperty(game, bridePlayer, dictBridePlayer, bridePlayer, update);
                    
                    // Link groom to bride
                    LangRenSha.SetPlayerProperty(game, groomTarget, dictLinkedTo, bridePlayer, update);
                    LangRenSha.SetPlayerProperty(game, groomTarget, dictBridePlayer, bridePlayer, update);
                    LangRenSha.SetPlayerProperty(game, groomTarget, dictGroomPlayer, groomTarget, update);
                    
                    // Change groom's faction to ThirdParty and succession to 2
                    LangRenSha.SetPlayerProperty(game, groomTarget, LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.ThirdParty, update);
                    if (LangRenSha.GetPlayerProperty(game, groomTarget, LangRen.dictSuceession, 0) == 0 && LangRenSha.GetPlayerProperty(game, groomTarget, LangRenSha.dictRole, "") != "LangRen")
                    {
                        LangRenSha.SetPlayerProperty(game, groomTarget, LangRen.dictSuceession, 2, update);
                    }
                    LangRenSha.SetPlayerProperty(game, groomTarget, LangRenSha.dictPlayerAlliance, 3, update);
                }

                UserAction.EndUserAction(game, update, true);
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }

            return GameActionResult.NotExecuted;
        }

        private GameActionResult HandleGroomCheckLinked(Game game, Dictionary<string, object> update)
        {
            var bride = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
            int groomPlayer = 0;
            if (bride.Count > 0)
            {
                groomPlayer = LangRenSha.GetPlayerProperty(game, bride[0], dictGroomPlayer, 0);
            }

            if (UserAction.EndUserAction(game, update))
            {
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }
            else
            {
                if (UserAction.StartUserAction(game, 5, update))
                {
                    var allPlayers = LangRenSha.GetPlayers(game, x => true);
                    update[UserAction.dictUserActionTargets] = new List<int>();
                    update[UserAction.dictUserActionUsers] = allPlayers;
                    update[UserAction.dictUserActionTargetsCount] = 0;
                    update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.GhostBride_GroomCheckLinked;
                    update[UserAction.dictUserActionInfo] = groomPlayer.ToString();
                    return GameActionResult.Restart;
                }
            }

            return GameActionResult.NotExecuted;
        }

        private GameActionResult HandleCoupleChooseWitness(Game game, Dictionary<string, object> update, int dayNumber)
        {
            var bride = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
            var coupleAlive = new List<int>();
            var couple = new List<int>();
            
            if (bride.Count > 0)
            {
                var bridePlayer = bride[0];
                var groomPlayer = LangRenSha.GetPlayerProperty(game, bridePlayer, dictGroomPlayer, 0);
                couple.Add(bridePlayer);
                if (groomPlayer > 0) couple.Add(groomPlayer);
                
                if (LangRenSha.GetPlayerProperty(game, bridePlayer, LangRenSha.dictAlive, 0) == 1)
                    coupleAlive.Add(bridePlayer);
                if (groomPlayer > 0 && LangRenSha.GetPlayerProperty(game, groomPlayer, LangRenSha.dictAlive, 0) == 1)
                    coupleAlive.Add(groomPlayer);
            }

            var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
            var validTargets = alivePlayers.Where(p => !couple.Contains(p)).ToList();

            var actionDuration = Game.GetGameDictionaryProperty(game, LangRenSha.dictDurationLangRen, ActionDuration);
            if (coupleAlive.Count == 0)
            {
                actionDuration = new Random().Next(3, 6);
            }
            if (dayNumber > 0)
            {
                actionDuration = 8;
            }

            if (UserAction.EndUserAction(game, update))
            {
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }

            if (UserAction.StartUserAction(game, actionDuration, update))
            {
                if (dayNumber == 0)
                {
                    // Day 0: Choose witness
                    update[UserAction.dictUserActionTargets] = validTargets;
                    update[UserAction.dictUserActionTargetsCount] = 1;
                    update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.GhostBride_CoupleChooseWitness;
                }
                else
                {
                    // Day 1+: Show attack status
                    update[UserAction.dictUserActionTargets] = new List<int> { 0 };
                    update[UserAction.dictUserActionTargetsCount] = 1;
                    update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.GhostBride_AttackStatus;

                    var langRenAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == "LangRen" && (int)x[LangRenSha.dictAlive] == 1);
                    var langRenSuccession1Alive = LangRenSha.GetPlayers(game, x => x.ContainsKey(LangRen.dictSuceession) && (int)x[LangRen.dictSuceession] == 1 && (int)x[LangRenSha.dictAlive] == 1);
                    bool canAttack = langRenAlive.Count == 0 && langRenSuccession1Alive.Count == 0;
                    update[UserAction.dictUserActionInfo] = canAttack ? "Succession" : "";
                }
                
                update[UserAction.dictUserActionUsers] = couple;
                update[UserAction.dictUserActionRole] = Name;
                return GameActionResult.Restart;
            }

            (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, coupleAlive, update);
            if (inputValid)
            {
                if (dayNumber == 0)
                {
                    var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, -1);
                    if (targets.Count > 0 && targets[0] > 0 && bride.Count > 0)
                    {
                        var witnessPlayer = targets[0];
                        var bridePlayer = bride[0];
                        var groomPlayer = LangRenSha.GetPlayerProperty(game, bridePlayer, dictGroomPlayer, 0);
                        
                        // Store witness info
                        LangRenSha.SetPlayerProperty(game, witnessPlayer, dictWitness, 1, update);
                        LangRenSha.SetPlayerProperty(game, witnessPlayer, dictBridePlayer, bridePlayer, update);
                        LangRenSha.SetPlayerProperty(game, witnessPlayer, dictGroomPlayer, groomPlayer, update);
                        LangRenSha.SetPlayerProperty(game, witnessPlayer, LangRenSha.dictPlayerAlliance, 3, update);
                        LangRenSha.SetPlayerProperty(game, witnessPlayer, LangRenSha.dictPlayerFaction, (int)LangRenSha.PlayerFaction.ThirdParty, update);
                        if (LangRenSha.GetPlayerProperty(game, witnessPlayer, LangRen.dictSuceession, 0) == 0 && LangRenSha.GetPlayerProperty(game, witnessPlayer, LangRenSha.dictRole, "") != "LangRen")
                        {
                            LangRenSha.SetPlayerProperty(game, witnessPlayer, LangRen.dictSuceession, 3, update);
                        }

                        // Store witness reference in couple
                        LangRenSha.SetPlayerProperty(game, bridePlayer, dictWitness, witnessPlayer, update);
                        if (groomPlayer > 0)
                        {
                            LangRenSha.SetPlayerProperty(game, groomPlayer, dictWitness, witnessPlayer, update);
                        }
                    }
                }

                UserAction.EndUserAction(game, update, true);
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }

            return GameActionResult.NotExecuted;
        }

        private GameActionResult HandleWitnessCheckLinked(Game game, Dictionary<string, object> update)
        {
            var witnessPlayers = LangRenSha.GetPlayers(game, x => 
                x.ContainsKey(dictWitness) && (int)x[dictWitness] == 1);

            if (UserAction.EndUserAction(game, update))
            {
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }
            else
            {
                if (UserAction.StartUserAction(game, 5, update))
                {
                    var allPlayers = LangRenSha.GetPlayers(game, x => true);
                    update[UserAction.dictUserActionTargets] = new List<int>();
                    update[UserAction.dictUserActionUsers] = allPlayers;
                    update[UserAction.dictUserActionTargetsCount] = 0;
                    update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.GhostBride_WitnessCheckLinked;
                    update[UserAction.dictUserActionInfo] = witnessPlayers.Count > 0 ? witnessPlayers[0].ToString() : "";
                    update[UserAction.dictUserActionRole] = "Witness";
                    return GameActionResult.Restart;
                }
            }

            return GameActionResult.NotExecuted;
        }

        private GameActionResult HandleWitnessInfo(Game game, Dictionary<string, object> update, int dayNumber)
        {
            var witnessPlayers = LangRenSha.GetPlayers(game, x => 
                x.ContainsKey(dictWitness) && (int)x[dictWitness] == 1 && (int)x[LangRenSha.dictAlive] == 1);

            var actionDuration = 5;
            if (witnessPlayers.Count == 0)
            {
                actionDuration = new Random().Next(3, 6);
            }

            if (UserAction.EndUserAction(game, update))
            {
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }

            if (UserAction.StartUserAction(game, actionDuration, update))
            {
                update[UserAction.dictUserActionTargets] = new List<int> { 0 };
                update[UserAction.dictUserActionUsers] = witnessPlayers;
                update[UserAction.dictUserActionTargetsCount] = 1;
                update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.GhostBride_WitnessInfo;
                update[UserAction.dictUserActionRole] = "Witness";

                if (dayNumber == 0 && witnessPlayers.Count > 0)
                {
                    // Day 0: Show who's bride and groom
                    var witness = witnessPlayers[0];
                    var bridePlayer = LangRenSha.GetPlayerProperty(game, witness, dictBridePlayer, 0);
                    var groomPlayer = LangRenSha.GetPlayerProperty(game, witness, dictGroomPlayer, 0);
                    update[UserAction.dictUserActionInfo] = $"{bridePlayer}, {groomPlayer}";
                }
                else
                {
                    var witnessPlayer = witnessPlayers.Count > 0 ? witnessPlayers[0] : 0;
                    int succession = 0;
                    if (witnessPlayer > 0)
                    {
                        succession = LangRenSha.GetPlayerProperty(game, witnessPlayer, LangRen.dictSuceession, 0);
                    }
                    bool canAttack = true;
                    var langRenAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == "LangRen" && (int)x[LangRenSha.dictAlive] == 1);
                    var langRenSuccession1Alive = LangRenSha.GetPlayers(game, x => x.ContainsKey(LangRen.dictSuceession) && (int)x[LangRen.dictSuceession] == 1 && (int)x[LangRenSha.dictAlive] == 1);
                    var langRenSuccession2Alive = LangRenSha.GetPlayers(game, x => x.ContainsKey(LangRen.dictSuceession) && (int)x[LangRen.dictSuceession] == 2 && (int)x[LangRenSha.dictAlive] == 1);
                    var langRenSuccession3Alive = LangRenSha.GetPlayers(game, x => x.ContainsKey(LangRen.dictSuceession) && (int)x[LangRen.dictSuceession] == 3 && (int)x[LangRenSha.dictAlive] == 1);
                    if (succession == 2)
                    {
                        canAttack = langRenAlive.Count == 0 && langRenSuccession1Alive.Count == 0;
                    }
                    else if (succession == 3)
                    {
                        canAttack = langRenAlive.Count == 0 && langRenSuccession1Alive.Count == 0 && langRenSuccession2Alive.Count == 0;
                    }
                    else if (succession == 4)
                    {
                        canAttack = langRenAlive.Count == 0 && langRenSuccession1Alive.Count == 0 && langRenSuccession2Alive.Count == 0 && langRenSuccession3Alive.Count == 0;
                    }
                    // Day 1+: Show attack status
                    var bride = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);

                    update[UserAction.dictUserActionInfo] = canAttack ? "Succession": "";
                }
                
                return GameActionResult.Restart;
            }

            (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, witnessPlayers, update);
            if (inputValid)
            {
                UserAction.EndUserAction(game, update, true);
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }

            return GameActionResult.NotExecuted;
        }

        /// <summary>
        /// Death handler for GhostBride couple - if one dies, the other dies too (cannot shoot gun).
        /// This handles day-time deaths (vote out, etc). Night deaths are handled by ChainKill.
        /// </summary>
        public static (bool, GameActionResult) HandleGhostBrideDeathSkill(Game game, int deadPlayer, Dictionary<string, object> update)
        {
            // Check if the dead player is linked to someone (part of GhostBride couple)
            var linkedTo = LangRenSha.GetPlayerProperty(game, deadPlayer, dictLinkedTo, 0);

            if (linkedTo <= 0)
            {
                // Not part of a couple, don't handle
                return (false, GameActionResult.NotExecuted);
            }

            // Check if the linked player is still alive
            var linkedAlive = LangRenSha.GetPlayerProperty(game, linkedTo, LangRenSha.dictAlive, 0);
            if (linkedAlive != 1)
            {
                // Linked player already dead, nothing to do
                return (false, GameActionResult.NotExecuted);
            }

            var currentAboutToDie = Game.GetGameDictionaryProperty(game, LangRenSha.dictAboutToDie, new List<int>());
            LangRenSha.MarkPlayerAboutToDie(game, linkedTo, update);
            LangRenSha.SetPlayerProperty(game, linkedTo, LieRen.dictHuntingDisabled, 1, update);

            // Mark this hunter's skill as processed before interrupting
            var skillsProcessed = Game.GetGameDictionaryProperty(game, LangRenSha.dictDeadSkillsProcessed, new List<int>());
            if (!skillsProcessed.Contains(deadPlayer))
            {
                skillsProcessed.Add(deadPlayer);
                update[LangRenSha.dictDeadSkillsProcessed] = skillsProcessed;
            }

            // Set up interrupt to go through death handling (state 97)
            var currentInterrupt = Game.GetGameDictionaryProperty(game, LangRenSha.dictInterrupt, new Dictionary<string, object>());
            var currentDeadPlayers = Game.GetGameDictionaryProperty(game, LangRenSha.dictDeadPlayerAction, new List<int>());

            var newInterrupt = new Dictionary<string, object>();
            newInterrupt[LangRenSha.dictSpeak] = 98; // Return to continue processing dead player skills
            newInterrupt[LangRenSha.dictInterrupt] = currentInterrupt;

            // Save death-related fields for restoration
            newInterrupt[LangRenSha.dictDeadPlayerAction] = currentDeadPlayers;
            newInterrupt[LangRenSha.dictDeadSkillsProcessed] = skillsProcessed;
            newInterrupt[LangRenSha.dictAboutToDie] = currentAboutToDie;

            update[LangRenSha.dictInterrupt] = newInterrupt;
            update[LangRenSha.dictSpeak] = 97;
            UserAction.EndUserAction(game, update, true);

            return (true, GameActionResult.Restart);
        }
    }
}
