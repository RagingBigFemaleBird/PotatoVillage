using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProcedureCore.LangRenSha
{
    /// <summary>
    /// MeiYangYang (美羊羊) - A god role that can choose a civilian as sacrifice.
    /// On Day 1+, can choose a target as sacrifice. Nothing happens if the target is not a civilian.
    /// Otherwise, if the sacrifice target is about to die, MeiYangYang can choose to die instead;
    /// if MeiYangYang is about to die, the sacrifice target can choose to die instead.
    /// </summary>
    public class MeiYangYang : Role
    {
        public static string dictSacrificeTarget = "meiyangyang_sacrifice_target";
        public static string dictSacrificeUsed = "meiyangyang_sacrifice_used";

        private static Dictionary<string, object> roleDict = new()
        {
            { YuYanJia.dictYuYanJiaResult, 1 },
            { LangRenSha.dictPlayerAlliance, 1 },
            { LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.God },
        };

        private static List<int> actionOrders = new()
        {
            (int)ActionConstant.MeiYangYang_OpenEyes,
            (int)ActionConstant.MeiYangYang_ChooseSacrifice,
            (int)ActionConstant.MeiYangYang_Info,
            (int)ActionConstant.MeiYangYang_CloseEyes,
            (int)ActionConstant.MeiYangYang_CivilianOpenEyes,
            (int)ActionConstant.MeiYangYang_CivilianInfo,
            (int)ActionConstant.MeiYangYang_CivilianCloseEyes,
            (int)ActionConstant.MeiYangYang_SacrificeOpenEyes,
            (int)ActionConstant.MeiYangYang_SacrificeAction,
            (int)ActionConstant.MeiYangYang_SacrificeCloseEyes,
        };

        public MeiYangYang()
        {
        }

        public Dictionary<string, object> RoleDict => roleDict;

        public string Name => "MeiYangYang";

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

            // MeiYangYang open/close eyes announcements
            if (LangRenSha.AnnouncerAction(game, update, true, (int)ActionConstant.MeiYangYang_OpenEyes, (int)ActionConstant.MeiYangYang_CloseEyes, 50, 51, Name, 4) == GameActionResult.Restart)
            {
                return GameActionResult.Restart;
            }

            // Action 241: MeiYangYang chooses sacrifice target (Day 1+ only)
            if (currentAction == (int)ActionConstant.MeiYangYang_ChooseSacrifice)
            {
                return HandleChooseSacrifice(game, update, dayNumber);
            }

            // Action 242: MeiYangYang info - if sacrifice target is about to die, ask if willing to sacrifice
            if (currentAction == (int)ActionConstant.MeiYangYang_Info)
            {
                return HandleMeiYangYangInfo(game, update, dayNumber);
            }

            // Civilian open/close eyes announcements
            if (LangRenSha.AnnouncerAction(game, update, false, (int)ActionConstant.MeiYangYang_CivilianOpenEyes, (int)ActionConstant.MeiYangYang_CivilianCloseEyes, 1000, 1001, "", 4) == GameActionResult.Restart)
            {
                return GameActionResult.Restart;
            }

            // Action 245: Civilian info
            if (currentAction == (int)ActionConstant.MeiYangYang_CivilianInfo)
            {
                return HandleCivilianInfo(game, update, dayNumber);
            }

            // Sacrifice open/close eyes announcements (Day 1+ only)
            if (LangRenSha.AnnouncerAction(game, update, true, (int)ActionConstant.MeiYangYang_SacrificeOpenEyes, (int)ActionConstant.MeiYangYang_SacrificeCloseEyes, 50, 51, "Sacrifice", 4) == GameActionResult.Restart)
            {
                return GameActionResult.Restart;
            }

            // Action 248: Sacrifice target action (Day 1+ only)
            if (currentAction == (int)ActionConstant.MeiYangYang_SacrificeAction)
            {
                return HandleSacrificeAction(game, update, dayNumber);
            }

            return GameActionResult.NotExecuted;
        }

        private GameActionResult HandleChooseSacrifice(Game game, Dictionary<string, object> update, int dayNumber)
        {
            var meiYangYang = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
            var meiYangYangAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
            var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);

            var actionDuration = Game.GetGameDictionaryProperty(game, LangRenSha.dictDurationPlayerReact, ActionDuration);
            var sacrificeUsed = meiYangYangAlive.Count > 0 ? LangRenSha.GetPlayerProperty(game, meiYangYangAlive[0], dictSacrificeUsed, 0) == 1 : true;

            // Day 0: Skip this action (no choosing)
            if (dayNumber == 0)
            {
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }

            if (sacrificeUsed)
            {
                actionDuration = new Random().Next(3, 6);
            }

            if (UserAction.EndUserAction(game, update))
            {
                (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, meiYangYangAlive, update);
                if (inputValid && meiYangYangAlive.Count > 0)
                {
                    var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, -1);
                    if (targets.Count > 0 && targets[0] > 0)
                    {
                        var target = targets[0];
                        var meiYangYangPlayer = meiYangYangAlive[0];

                        // Check if target is a civilian
                        var targetFaction = LangRenSha.GetPlayerProperty(game, target, LangRenSha.dictPlayerFaction, 0);
                        if ((targetFaction & (int)LangRenSha.PlayerFaction.Civilian) != 0)
                        {
                            // Store the sacrifice target
                            update[dictSacrificeTarget] = target;
                        }
                    }
                }
                else
                {
                    update[dictSacrificeTarget] = 0;
                }
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }

            if (UserAction.StartUserAction(game, actionDuration, update))
            {
                // Include -100 for skip option, and civilians + self (to select self)
                var validTargets = new List<int>(alivePlayers);
                validTargets.Add(-100); // Skip option

                update[UserAction.dictUserActionTargets] = sacrificeUsed ? new List<int> { -100 } : validTargets;
                update[UserAction.dictUserActionUsers] = meiYangYang;
                update[UserAction.dictUserActionTargetsCount] = 1;
                update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.MeiYangYang_ChooseSacrifice;
                update[UserAction.dictUserActionRole] = Name;
                return GameActionResult.Restart;
            }

            // Check for early completion
            (var inputValidEarly, var inputEarly, var input_othersEarly) = UserAction.GetUserResponse(game, true, meiYangYangAlive, update);
            if (inputValidEarly)
            {
                var targets = UserAction.TallyUserInput(inputEarly, 0, UserAction.UserInputMode.VoteMost, -1);
                if (targets.Count > 0 && meiYangYangAlive.Count > 0)
                {
                    var target = targets[0];
                    if (target > 0)
                    {
                        var meiYangYangPlayer = meiYangYangAlive[0];

                        // Check if target is a civilian
                        var targetFaction = LangRenSha.GetPlayerProperty(game, target, LangRenSha.dictPlayerFaction, 0);
                        if ((targetFaction & (int)LangRenSha.PlayerFaction.Civilian) != 0)
                        {
                            update[dictSacrificeTarget] = target;
                        }
                    }
                    else
                    {
                        update[dictSacrificeTarget] = 0;
                    }

                    UserAction.EndUserAction(game, update, true);
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
            }

            return GameActionResult.NotExecuted;
        }

        private GameActionResult HandleMeiYangYangInfo(Game game, Dictionary<string, object> update, int dayNumber)
        {
            var meiYangYang = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
            var meiYangYangAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
            var aboutToDie = Game.GetGameDictionaryProperty(game, LangRenSha.dictAboutToDie, new List<int>());

            // Day 0: No action
            if (dayNumber == 0)
            {
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }

            var sacrificeTarget = Game.GetGameDictionaryProperty(game, dictSacrificeTarget, 0);

            var actionDuration = Game.GetGameDictionaryProperty(game, LangRenSha.dictDurationPlayerReact, ActionDuration);

            if (meiYangYangAlive.Count == 0)
            {
                actionDuration = new Random().Next(3, 6);
            }

            // Check if sacrifice target is about to die and is a civilian
            bool canSacrifice = false;
            if (sacrificeTarget > 0 && aboutToDie.Contains(sacrificeTarget) && meiYangYangAlive.Count > 0)
            {
                var targetFaction = LangRenSha.GetPlayerProperty(game, sacrificeTarget, LangRenSha.dictPlayerFaction, 0);
                if ((targetFaction & (int)LangRenSha.PlayerFaction.Civilian) != 0)
                {
                    canSacrifice = true;
                }
            }
            if (!canSacrifice || meiYangYangAlive.Count == 0)
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
                if (canSacrifice)
                {
                    // Show yes/no option to sacrifice
                    update[UserAction.dictUserActionTargets] = new List<int> { -1, 0 }; // -1 = Yes, 0 = No
                    update[UserAction.dictUserActionUsers] = meiYangYang;
                    update[UserAction.dictUserActionTargetsCount] = 1;
                    update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.MeiYangYang_SacrificeSelf;
                    update[UserAction.dictUserActionRole] = Name;
                    update[UserAction.dictUserActionInfo] = sacrificeTarget.ToString();
                }
                else
                {
                    // Just display info, no action needed
                    update[UserAction.dictUserActionTargets] = new List<int> { 0 };
                    update[UserAction.dictUserActionUsers] = meiYangYang;
                    update[UserAction.dictUserActionTargetsCount] = 1;
                    update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.MeiYangYang_Info;
                    update[UserAction.dictUserActionRole] = Name;
                    update[UserAction.dictUserActionInfo] = sacrificeTarget > 0 ? sacrificeTarget.ToString() : "";
                }
                return GameActionResult.Restart;
            }

            // Check for early completion
            if (canSacrifice)
            {
                (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, meiYangYangAlive, update);
                if (inputValid)
                {
                    var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, -1);
                    if (targets.Count > 0)
                    {
                        if (targets[0] == -1 && meiYangYangAlive.Count > 0)
                        {
                            // MeiYangYang sacrifices self for the target
                            var meiYangYangPlayer = meiYangYangAlive[0];

                            aboutToDie.Remove(sacrificeTarget);
                            if (!aboutToDie.Contains(meiYangYangPlayer))
                            {
                                aboutToDie.Add(meiYangYangPlayer);
                            }
                            update[LangRenSha.dictAboutToDie] = aboutToDie;

                            update[dictSacrificeTarget] = 0;
                            LangRenSha.SetPlayerProperty(game, meiYangYangPlayer, dictSacrificeUsed, 1, update);
                        }

                        UserAction.EndUserAction(game, update, true);
                        LangRenSha.AdvanceAction(game, update);
                        return GameActionResult.Restart;
                    }
                }
            }

            return GameActionResult.NotExecuted;
        }

        private GameActionResult HandleCivilianInfo(Game game, Dictionary<string, object> update, int dayNumber)
        {
            var meiYangYang = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
            var meiYangYangAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
            var aboutToDie = Game.GetGameDictionaryProperty(game, LangRenSha.dictAboutToDie, new List<int>());
            var sacrificeTarget = Game.GetGameDictionaryProperty(game, dictSacrificeTarget, 0);
            var allPlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);

            // Get all civilians
            var civilians = LangRenSha.GetPlayers(game, x => 
                (int)x[LangRenSha.dictAlive] == 1 && 
                x.ContainsKey(LangRenSha.dictPlayerFaction) &&
                ((int)x[LangRenSha.dictPlayerFaction] & (int)LangRenSha.PlayerFaction.Civilian) != 0);

            var actionDuration = 7;

            if (UserAction.EndUserAction(game, update))
            {
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }

            if (UserAction.StartUserAction(game, actionDuration, update))
            {
                update[UserAction.dictUserActionTargets] = new List<int> { 0 };
                update[UserAction.dictUserActionUsers] = allPlayers;
                update[UserAction.dictUserActionTargetsCount] = 1;
                update[UserAction.dictUserActionRole] = "Civilian";

                // Get poisoned player info from HongTaiLang
                var poisonedPlayer = HongTaiLang.GetPoisonedPlayer(game);
                update[UserAction.dictUserActionInfo2] = poisonedPlayer > 0 ? poisonedPlayer.ToString() : "";

                if (dayNumber == 0)
                {
                    // Day 0: Display who is the MeiYangYang player
                    var meiYangYangPlayer = meiYangYang.Count > 0 ? meiYangYang[0] : 0;
                    update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.MeiYangYang_CivilianInfo;
                    update[UserAction.dictUserActionInfo] = $"{meiYangYangPlayer}:" + string.Join(",", civilians);
                }
                else
                {
                    // Day 1+: Check if MeiYangYang is in about to die list and sacrifice is self
                    var meiYangYangPlayer = meiYangYangAlive.Count > 0 ? meiYangYangAlive[0] : 0;
                    bool meiYangYangAboutToDie = meiYangYangPlayer > 0 && aboutToDie.Contains(meiYangYangPlayer);

                    if (meiYangYangAboutToDie)
                    {
                        // Show info that civilian may act on sacrifice later
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.MeiYangYang_CivilianMayAct;
                        update[UserAction.dictUserActionInfo] = sacrificeTarget.ToString();
                    }
                    else
                    {
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.MeiYangYang_CivilianInfo;
                        update[UserAction.dictUserActionInfo] = "";
                    }
                }
                return GameActionResult.Restart;
            }

            return GameActionResult.NotExecuted;
        }

        private GameActionResult HandleSacrificeAction(Game game, Dictionary<string, object> update, int dayNumber)
        {
            // Day 0: Skip this action
            if (dayNumber == 0)
            {
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }

            var meiYangYang = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
            var meiYangYangAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
            var aboutToDie = Game.GetGameDictionaryProperty(game, LangRenSha.dictAboutToDie, new List<int>());
            var sacrificeTarget = Game.GetGameDictionaryProperty(game, dictSacrificeTarget, 0);

            var meiYangYangPlayer = meiYangYangAlive.Count > 0 ? meiYangYangAlive[0] : (meiYangYang.Count > 0 ? meiYangYang[0] : 0);

            // Check if MeiYangYang chose self as sacrifice target and is about to die
            bool meiYangYangAboutToDie = meiYangYangPlayer > 0 && aboutToDie.Contains(meiYangYangPlayer);

            var actionDuration = 10;
            if (!meiYangYangAboutToDie || meiYangYangAlive.Count == 0)
            {
                actionDuration = new Random().Next(6, 8);
            }


            if (UserAction.EndUserAction(game, update))
            {
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }
            var targetList = sacrificeTarget > 0 ? new List<int> { sacrificeTarget } : new List<int>();
            if (UserAction.StartUserAction(game, actionDuration, update))
            {
                // Show yes/no option to civilian
                update[UserAction.dictUserActionTargets] = new List<int> { -1, 0 }; // -1 = Yes, 0 = No
                update[UserAction.dictUserActionUsers] = targetList;
                update[UserAction.dictUserActionTargetsCount] = 1;
                update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.MeiYangYang_CivilianSacrificeForMeiYangYang;
                update[UserAction.dictUserActionRole] = "Sacrifice";
                update[UserAction.dictUserActionInfo] = meiYangYangPlayer.ToString();
                return GameActionResult.Restart;
            }

            // Check for early completion
            (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, targetList, update);
            if (inputValid)
            {
                var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, -1);
                if (targets.Count > 0)
                {
                    if (targets[0] == -1)
                    {
                        aboutToDie.Remove(meiYangYangPlayer);
                        if (!aboutToDie.Contains(sacrificeTarget))
                        {
                            aboutToDie.Add(sacrificeTarget);
                        }
                        update[LangRenSha.dictAboutToDie] = aboutToDie;
                        LangRenSha.SetPlayerProperty(game, meiYangYangPlayer, dictSacrificeUsed, 1, update);
                    }

                    UserAction.EndUserAction(game, update, true);
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
            }

            return GameActionResult.NotExecuted;
        }
    }
}
