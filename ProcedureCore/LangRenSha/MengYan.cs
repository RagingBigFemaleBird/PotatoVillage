using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcedureCore.LangRenSha
{
    /// <summary>
    /// MengYan (梦魇) - A wolf role that can disable another player's skill for the night.
    /// Alliance: LangRen (2), Succession = 1 (acts with other wolves)
    /// Acts after YingZi.
    /// On action, select a player to disable its skill by setting dictSkillTransformation to Disabled.
    /// </summary>
    public class MengYan : Role
    {
        private static Dictionary<string, object> roleDict = new()
        {
            { YuYanJia.dictYuYanJiaResult, 2 },
            { LangRenSha.dictPlayerAlliance, 2 },
            { LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.Evil },
            { LangRen.dictSuceession, 1 },
        };

        private static List<int> actionOrders = new()
        {
            (int)ActionConstant.MengYan_OpenEyes,
            (int)ActionConstant.MengYan_Act,
            (int)ActionConstant.MengYan_CloseEyes
        };

        public MengYan()
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
                return "MengYan";
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

        public static string dictLastMengYanTarget = "mengyan_last_target";

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

            // Announcer actions for open/close eyes
            if (LangRenSha.AnnouncerAction(game, update, false, ActionOrders[0], ActionOrders[2], 50, 51, Name, 4) == GameActionResult.Restart)
            {
                return GameActionResult.Restart;
            }

            // MengYan action - select a player to disable their skill
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == ActionOrders[1])
            {
                var mengYan = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
                var mengYanAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
                var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
                var actionDuration = Game.GetGameDictionaryProperty(game, LangRenSha.dictDurationPlayerReact, ActionDuration);

                if (mengYanAlive.Count == 0)
                {
                    actionDuration = new Random().Next(3, 6);
                }

                if (UserAction.EndUserAction(game, update))
                {
                    // Timeout - set skippedAct to true (not acted)
                    if (mengYanAlive.Count > 0)
                    {
                        AwkSheMengRen.SetSkippedAct(game, mengYanAlive[0], true, update);
                    }
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, actionDuration, update))
                    {
                        // Get last target - cannot target same person twice in a row
                        var lastTarget = Game.GetGameDictionaryProperty(game, dictLastMengYanTarget, 0);

                        // Build list of valid targets (alive players except self and last target)
                        var targets = new List<int>();
                        foreach (var target in alivePlayers)
                        {
                            if (mengYanAlive.Count == 0 || target != mengYanAlive[0])
                            {
                                if (target != lastTarget)
                                {
                                    targets.Add(target);
                                }
                            }
                        }

                        // Add -100 option to not use skill
                        targets.Add(-100);

                        update[UserAction.dictUserActionTargets] = targets;
                        update[UserAction.dictUserActionUsers] = mengYan;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.MengYan_Act;
                        update[UserAction.dictUserActionRole] = Name;
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, mengYanAlive, update);
                        if (inputValid)
                        {
                            var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.Input, -1);
                            if (targets.Count == 1)
                            {
                                if (targets[0] == -100)
                                {
                                    // Player chose to not use the skill - clear last target
                                    update[dictLastMengYanTarget] = 0;
                                    if (mengYanAlive.Count > 0)
                                    {
                                        AwkSheMengRen.SetSkippedAct(game, mengYanAlive[0], true, update);
                                    }
                                    UserAction.EndUserAction(game, update, true);
                                    LangRenSha.AdvanceAction(game, update);
                                    return GameActionResult.Restart;
                                }
                                if (targets[0] > 0)
                                {
                                    // Disable the target's skill by setting skillTransformation to Disabled
                                    LangRenSha.SetPlayerProperty(game, targets[0], LangRenSha.dictSkillTransformation, (int)LangRenSha.SkillTransformation.Disabled, update);
                                    // Store last target
                                    update[dictLastMengYanTarget] = targets[0];

                                    if (mengYanAlive.Count > 0)
                                    {
                                        AwkSheMengRen.SetSkippedAct(game, mengYanAlive[0], false, update);
                                    }
                                    UserAction.EndUserAction(game, update, true);
                                    LangRenSha.AdvanceAction(game, update);
                                    return GameActionResult.Restart;
                                }
                            }
                        }
                    }
                }
                return GameActionResult.NotExecuted;
            }

            return GameActionResult.NotExecuted;
        }
    }
}
