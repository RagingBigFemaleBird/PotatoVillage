using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcedureCore.LangRenSha
{
    public class LieRen : Role
    {
        private static Dictionary<string, object> roleDict = new()
            {
                { YuYanJia.dictYuYanJiaResult, 1 },
                { LangRenSha.dictPlayerAlliance, 1 },
                { LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.God },
            };
        private static List<int> actionOrders = new()
            { 170, 171, 172 };

        public LieRen()
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
                return "LieRen";
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
                return 10;
            }
        }

        public static string dictHuntingDisabled = "hunter_disabled";

        public GameActionResult GenerateStateDiff(Game game, Dictionary<string, object> update)
        {
            if (game.StateSequenceNumber == 1)
            {
                // Only add actions if LaoShu is present in the game
                var laoShuPresent = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == "LaoShu").Count > 0;
                if (laoShuPresent)
                {
                    var addSelf = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
                    if (addSelf.Count > 0)
                    {
                        var no = Game.GetGameDictionaryProperty(game, LangRenSha.dictNightOrders, new List<int>());
                        no.AddRange(ActionOrders);
                        update[LangRenSha.dictNightOrders] = no;
                    }
                }
                return GameActionResult.Continue;
            }

            // Actions 170 and 172 are announcer actions
            if (LangRenSha.AnnouncerAction(game, update, false, ActionOrders[0], ActionOrders[2], 50, 51, Name, 4) == GameActionResult.Restart)
            {
                return GameActionResult.Restart;
            }

            // Action 171: LieRen can shoot at night if tagged by mice
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == ActionOrders[1])
            {
                var lieRen = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
                var lieRenAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);

                // Check if LieRen is tagged by mice
                var miceTag = Game.GetGameDictionaryProperty(game, LaoShu.dictMiceTag, 0);
                var lieRenPlayer = lieRenAlive.Count == 0 ? 0 : lieRenAlive[0];
                var isTagged = lieRenAlive.Count > 0 && miceTag == lieRenPlayer;
                var nightShootUsed = lieRenAlive.Count == 0 || LangRenSha.GetPlayerProperty(game, lieRenPlayer, dictHuntingDisabled, 0) == 1;

                var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
                alivePlayers.Remove(lieRenPlayer); // Cannot shoot self

                if (UserAction.EndUserAction(game, update))
                {
                    // Time's up - get final response and process
                    (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, isTagged ? lieRenAlive : new List<int>(), update);
                    if (inputValid)
                    {
                        var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, -1);
                        if (targets.Count > 0 && targets[0] > 0)
                        {
                            // Shoot the target
                            LangRenSha.MarkPlayerAboutToDie(game, targets[0], update);
                            LangRenSha.SetPlayerProperty(game, lieRenPlayer, dictHuntingDisabled, 1, update);
                        }
                    }
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, ActionDuration, update))
                    {
                        // Action just started - setup targets and users
                        update[UserAction.dictUserActionTargets] = alivePlayers;
                        update[UserAction.dictUserActionUsers] = lieRen;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = 151; // Hunter kill hint
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        // Action in progress - check for early completion
                        (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, isTagged ? lieRenAlive : new List<int>(), update);
                        if (inputValid)
                        {
                            (inputValid, input, input_others) = UserAction.GetUserResponse(game, true, lieRenAlive, update);
                            var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, -1);
                            if (targets.Count == 0)
                            {
                                return GameActionResult.NotExecuted;
                            }
                            LangRenSha.MarkPlayerAboutToDie(game, targets[0], update);
                            LangRenSha.SetPlayerProperty(game, lieRenPlayer, dictHuntingDisabled, 1, update);
                            UserAction.EndUserAction(game, update, true);
                            LangRenSha.AdvanceAction(game, update);
                            return GameActionResult.Restart;
                        }
                    }
                }
                return GameActionResult.NotExecuted;
            }

            return GameActionResult.NotExecuted;
        }

        /// <summary>
        /// Death skill handler - allows hunter to kill someone when they die
        /// </summary>
        public static (bool, GameActionResult) HandleHunterDeathSkill(Game game, int deadPlayer, Dictionary<string, object> update)
        {
            var isLieRen = LangRenSha.GetPlayerProperty(game, deadPlayer, LangRenSha.dictRole, "") == "LieRen";
            var disabled = LangRenSha.GetPlayerProperty(game, deadPlayer, dictHuntingDisabled, 0) == 1;
            if (!isLieRen || disabled)
            {
                return (false, GameActionResult.NotExecuted);
            }

            // Prompt hunter to select a target to kill
            if (UserAction.EndUserAction(game, update))
            {
                // No valid target selected, just mark as processed and continue
                return (true, GameActionResult.Restart);
            }
            else
            {
                // Start user action for hunter to select target
                if (UserAction.StartUserAction(game, 15, update))
                {
                    var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
                    alivePlayers.Remove(deadPlayer); // Hunter cannot kill themselves

                    update[UserAction.dictUserActionTargets] = alivePlayers;
                    update[UserAction.dictUserActionUsers] = new List<int> { deadPlayer };
                    update[UserAction.dictUserActionTargetsCount] = 1;
                    update[UserAction.dictUserActionTargetsHint] = 151; // Hunter kill hint
                    update[UserAction.dictUserActionInfo] = "LieRen";
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
                            LangRenSha.MarkPlayerAboutToDie(game, target, update);
                            LangRenSha.SetPlayerProperty(game, deadPlayer, dictHuntingDisabled, 1, update);

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
                    else
                    {
                        return (true, GameActionResult.NotExecuted);
                    }
                    return (true, GameActionResult.Restart);
                }
            }

        }

    }
}
