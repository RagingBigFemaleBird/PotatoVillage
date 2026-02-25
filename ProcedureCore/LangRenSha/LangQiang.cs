using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcedureCore.LangRenSha
{
    /// <summary>
    /// LangQiang (狼枪 - Werewolf Hunter) role
    /// Same as LieRen (Hunter) but on the werewolf side.
    /// </summary>
    public class LangQiang : Role
    {
        private static Dictionary<string, object> roleDict = new()
            {
                { YuYanJia.dictYuYanJiaResult, 2 },
                { LangRenSha.dictPlayerAlliance, 2 },
                { LangRen.dictSuceession, 1  },
                { LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.Evil },
            };
        private static List<int> actionOrders = new();

        public LangQiang()
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
                return "LangQiang";
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


        public GameActionResult GenerateStateDiff(Game game, Dictionary<string, object> update)
        {
            return GameActionResult.NotExecuted;
        }

        /// <summary>
        /// Death skill handler - allows LangQiang to kill someone when they die
        /// </summary>
        public static (bool, GameActionResult) HandleLangQiangDeathSkill(Game game, int deadPlayer, Dictionary<string, object> update)
        {
            var isLangQiang = LangRenSha.GetPlayerProperty(game, deadPlayer, LangRenSha.dictRole, "") == "LangQiang";
            var disabled = LangRenSha.GetPlayerProperty(game, deadPlayer, LieRen.dictHuntingDisabled, 0) == 1;
            if (!isLangQiang || disabled)
            {
                return (false, GameActionResult.NotExecuted);
            }

            // Prompt LangQiang to select a target to kill
            if (UserAction.EndUserAction(game, update))
            {
                // No valid target selected, just mark as processed and continue
                return (true, GameActionResult.Restart);
            }
            else
            {
                // Start user action for LangQiang to select target
                if (UserAction.StartUserAction(game, 15, update))
                {
                    var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
                    alivePlayers.Remove(deadPlayer); // LangQiang cannot kill themselves

                    update[UserAction.dictUserActionTargets] = alivePlayers;
                    update[UserAction.dictUserActionUsers] = new List<int> { deadPlayer };
                    update[UserAction.dictUserActionTargetsCount] = 1;
                    update[UserAction.dictUserActionTargetsHint] = 151; // Hunter kill hint (same as LieRen)
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
                            LangRenSha.SetPlayerProperty(game, deadPlayer, LieRen.dictHuntingDisabled, 1, update);

                            // Mark this LangQiang's skill as processed before interrupting
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
