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
            { (int)ActionConstant.LieRen_OpenEyes, (int)ActionConstant.LieRen_Act, (int)ActionConstant.LieRen_CloseEyes };

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
                var addSelf = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
                if (addSelf.Count > 0)
                {
                    var no = Game.GetGameDictionaryProperty(game, LangRenSha.dictNightOrders, new List<int>());
                    no.AddRange(ActionOrders);
                    update[LangRenSha.dictNightOrders] = no;
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

                // Check if skill is disabled by MengYan
                var skillDisabled = lieRenPlayer > 0 && LangRenSha.GetPlayerProperty(game, lieRenPlayer, LangRenSha.dictSkillTransformation, 0) == (int)LangRenSha.SkillTransformation.Disabled;

                var actionDuration = Game.GetGameDictionaryProperty(game, LangRenSha.dictDurationPlayerReact, ActionDuration);
                if (lieRenAlive.Count == 0 || !isTagged || nightShootUsed)
                {
                    actionDuration = new Random().Next(3, 6);
                }

                var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
                alivePlayers.Remove(lieRenPlayer); // Cannot shoot self
                alivePlayers.Add(-100);

                if (UserAction.EndUserAction(game, update))
                {
                    // Time's up - get final response and process
                    (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, (isTagged && !nightShootUsed) ? lieRenAlive : new List<int>(), update);
                    if (inputValid)
                    {
                        var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, -1);
                        if (targets.Count > 0 && targets[0] > 0)
                        {
                            // Shoot the target
                            var laoShu = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == "LaoShu");
                            var laoShuPlayer = laoShu.Count > 0 ? laoShu[0] : -1;

                            if (targets[0] != laoShuPlayer)
                            {
                                LangRenSha.MarkPlayerAboutToDie(game, targets[0], update);
                            }
                            LangRenSha.SetPlayerProperty(game, lieRenPlayer, dictHuntingDisabled, 1, update);
                        }
                    }
                    // On action completion: if aboutToDie and marked by MengYan, set huntingDisabled; otherwise reset skillDisabled
                    if (lieRenPlayer > 0 && skillDisabled)
                    {
                        var aboutToDie = Game.GetGameDictionaryProperty(game, LangRenSha.dictAboutToDie, new List<int>());
                        if (aboutToDie.Contains(lieRenPlayer))
                        {
                            LangRenSha.SetPlayerProperty(game, lieRenPlayer, dictHuntingDisabled, 1, update);
                        }
                        LangRenSha.SetPlayerProperty(game, lieRenPlayer, LangRenSha.dictSkillTransformation, (int)LangRenSha.SkillTransformation.None, update);
                    }
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                else
                {

                    if (UserAction.StartUserAction(game, actionDuration, update))
                    {
                        // Action just started - setup targets and users
                        if (skillDisabled)
                        {
                            // Skill disabled by MengYan - show "Unable to shoot" message
                            update[UserAction.dictUserActionTargets] = new List<int>();
                            update[UserAction.dictUserActionInfo3] = "1"; // Indicate skill is disabled
                        }
                        else
                        {
                            update[UserAction.dictUserActionTargets] = (isTagged && !nightShootUsed) ? alivePlayers : new List<int>();
                        }
                        update[UserAction.dictUserActionUsers] = lieRen;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.HunterKill;
                        update[UserAction.dictUserActionRole] = Name;
                        update[UserAction.dictUserActionInfo] = (nightShootUsed || skillDisabled) ? "0" : "1";
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        // Action in progress - check for early completion
                        (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, (isTagged && !nightShootUsed) ? lieRenAlive : new List<int>(), update);
                        if (inputValid)
                        {
                            if (!(isTagged && !nightShootUsed))
                            {
                                UserAction.EndUserAction(game, update, true);
                                LangRenSha.AdvanceAction(game, update);
                                return GameActionResult.Restart;
                            }
                            var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, -1);
                            if (targets.Count == 0)
                            {
                                return GameActionResult.NotExecuted;
                            }
                            if (targets[0] > 0)
                            {
                                var laoShu = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == "LaoShu");
                                var laoShuPlayer = laoShu.Count > 0 ? laoShu[0] : -1;

                                if (targets[0] != laoShuPlayer)
                                {

                                    LangRenSha.MarkPlayerAboutToDie(game, targets[0], update);
                                }
                                LangRenSha.SetPlayerProperty(game, lieRenPlayer, dictHuntingDisabled, 1, update);
                            }
                            // On action completion: if aboutToDie and marked by MengYan, set huntingDisabled; otherwise reset skillDisabled
                            if (lieRenPlayer > 0 && skillDisabled)
                            {
                                var aboutToDie = Game.GetGameDictionaryProperty(game, LangRenSha.dictAboutToDie, new List<int>());
                                if (aboutToDie.Contains(lieRenPlayer))
                                {
                                    LangRenSha.SetPlayerProperty(game, lieRenPlayer, dictHuntingDisabled, 1, update);
                                }
                                LangRenSha.SetPlayerProperty(game, lieRenPlayer, LangRenSha.dictSkillTransformation, (int)LangRenSha.SkillTransformation.None, update);
                            }
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
                return (false, GameActionResult.Restart);
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
                    update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.HunterKill;
                    update[UserAction.dictUserActionRole] = "LieRen";
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
                            var target = targets[0];
                            LangRenSha.MarkPlayerAboutToDie(game, target, update);
                            LangRenSha.SetPlayerProperty(game, deadPlayer, dictHuntingDisabled, 1, update);

                            // Skill-processed tracking is owned by HandleDeadPlayerSkills via
                            // its per-player handler index, so we no longer mark this player
                            // as processed here. Doing so would prevent any subsequent death
                            // handlers (linked, etc.) from running on the same player.
                            LangRenSha.SetupSkillUseInterrupt(game, deadPlayer, new List<int> { target }, "hunted", "1", update);
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
