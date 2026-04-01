using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProcedureCore.LangRenSha
{
    /// <summary>
    /// HongTaiLang (红太狼) - A special wolf role that can "pour boiling water" (泼开水) on a target.
    /// On Day 1+, can choose a target to delay poison. The skill can only be used once.
    /// If target is MeiYangYang, they die immediately with sacrifice used.
    /// Otherwise, target dies on the following night with hunter gun disabled.
    /// </summary>
    public class HongTaiLang : Role
    {
        public static string dictPoisonTarget = "hongtailang_poison_target";
        public static string dictPoisonUsed = "hongtailang_poison_used";
        public static string dictDelayedPoisonTarget = "hongtailang_delayed_poison"; // Target to die next night

        private static Dictionary<string, object> roleDict = new()
        {
            { YuYanJia.dictYuYanJiaResult, 2 }, // Shows as evil to seer
            { LangRenSha.dictPlayerAlliance, 2 }, // Evil alliance
            { LangRen.dictSuceession, 1 }, // Can inherit wolf kill
            { LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.Evil },
        };

        private static List<int> actionOrders = new()
        {
            (int)ActionConstant.HongTaiLang_OpenEyes,
            (int)ActionConstant.HongTaiLang_ChooseTarget,
            (int)ActionConstant.HongTaiLang_CloseEyes,
            (int)ActionConstant.HongTaiLang_KillTarget,
        };

        public HongTaiLang()
        {
        }

        public Dictionary<string, object> RoleDict => roleDict;

        public string Name => "HongTaiLang";

        public int Version => 1;

        public List<int> ActionOrders => actionOrders;

        public int ActionDuration => 20;

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

            // HongTaiLang open/close eyes announcements
            if (LangRenSha.AnnouncerAction(game, update, false, (int)ActionConstant.HongTaiLang_OpenEyes, (int)ActionConstant.HongTaiLang_CloseEyes, 50, 51, Name, 4) == GameActionResult.Restart)
            {
                return GameActionResult.Restart;
            }

            // Action 115: HongTaiLang chooses target
            if (currentAction == (int)ActionConstant.HongTaiLang_ChooseTarget)
            {
                return HandleChooseTarget(game, update, dayNumber);
            }

            // Action 222: Kill delayed poison target
            if (currentAction == (int)ActionConstant.HongTaiLang_KillTarget)
            {
                return HandleKillTarget(game, update, dayNumber);
            }

            return GameActionResult.NotExecuted;
        }

        private GameActionResult HandleChooseTarget(Game game, Dictionary<string, object> update, int dayNumber)
        {
            var hongTaiLang = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
            var hongTaiLangAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
            var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);

            var actionDuration = Game.GetGameDictionaryProperty(game, LangRenSha.dictDurationLangRen, ActionDuration);
            var poisonUsed = hongTaiLangAlive.Count > 0 ? LangRenSha.GetPlayerProperty(game, hongTaiLangAlive[0], dictPoisonUsed, 0) == 1 : true;


            if (poisonUsed || hongTaiLangAlive.Count == 0)
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
                var validTargets = new List<int>(alivePlayers);
                // Remove self from targets
                if (hongTaiLangAlive.Count > 0)
                {
                    validTargets.Remove(hongTaiLangAlive[0]);
                }
                validTargets.Add(-100); // Skip option

                update[UserAction.dictUserActionTargets] = (poisonUsed || hongTaiLangAlive.Count == 0) ? new List<int> { -100 } : validTargets;
                update[UserAction.dictUserActionUsers] = hongTaiLang;
                update[UserAction.dictUserActionTargetsCount] = 1;
                update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.HongTaiLang_ChooseTarget;
                update[UserAction.dictUserActionRole] = Name;
                return GameActionResult.Restart;
            }

            // Check for early completion
            (var inputValidEarly, var inputEarly, var input_othersEarly) = UserAction.GetUserResponse(game, true, hongTaiLangAlive, update);
            if (inputValidEarly && !poisonUsed)
            {
                var targets = UserAction.TallyUserInput(inputEarly, 0, UserAction.UserInputMode.VoteMost, -1);
                if (targets.Count > 0 && hongTaiLangAlive.Count > 0)
                {
                    var target = targets[0];
                    if (target > 0)
                    {
                        var hongTaiLangPlayer = hongTaiLangAlive[0];

                        update[dictDelayedPoisonTarget] = target;
                        LangRenSha.SetPlayerProperty(game, hongTaiLangPlayer, dictPoisonUsed, 1, update);
                    }

                    UserAction.EndUserAction(game, update, true);
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
            }

            return GameActionResult.NotExecuted;
        }

        private GameActionResult HandleKillTarget(Game game, Dictionary<string, object> update, int dayNumber)
        {
            var delayedTarget = Game.GetGameDictionaryProperty(game, dictDelayedPoisonTarget, 0);
            var currentTarget = Game.GetGameDictionaryProperty(game, dictPoisonTarget, 0);

            if (delayedTarget > 0)
            {
                var isMeiYangYang = LangRenSha.GetPlayerProperty(game, delayedTarget, LangRenSha.dictRole, "") == "MeiYangYang";
                if (isMeiYangYang)
                {
                    update[dictDelayedPoisonTarget] = 0;
                    LangRenSha.MarkPlayerAboutToDie(game, delayedTarget, update);
                    LangRenSha.SetPlayerProperty(game, delayedTarget, MeiYangYang.dictSacrificeUsed, 1, update);
                }
                else
                {
                    // Move the delayed target to current poison target for killing tomorrow
                    update[dictPoisonTarget] = delayedTarget;
                }
            }
            // If there's a delayed poison target from last night, kill them now
            if (currentTarget > 0)
            {
                var targetAlive = LangRenSha.GetPlayerProperty(game, currentTarget, LangRenSha.dictAlive, 0);
                if (targetAlive == 1)
                {
                    LangRenSha.MarkPlayerAboutToDie(game, currentTarget, update);
                    // Disable hunter gun for the poisoned target
                    LangRenSha.SetPlayerProperty(game, delayedTarget, LieRen.dictHuntingDisabled, 1, update);
                }
                // Clear the delayed poison target
                update[dictPoisonTarget] = 0;
                update[dictDelayedPoisonTarget] = 0;
            }

            LangRenSha.AdvanceAction(game, update);
            return GameActionResult.Restart;
        }

        /// <summary>
        /// Gets the current poisoned player (for display in MeiYangYang civilian info)
        /// </summary>
        public static int GetPoisonedPlayer(Game game)
        {
            return Game.GetGameDictionaryProperty(game, dictDelayedPoisonTarget, 0);
        }
    }
}
