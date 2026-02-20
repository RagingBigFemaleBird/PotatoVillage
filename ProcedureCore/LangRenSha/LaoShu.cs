using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcedureCore.LangRenSha
{
    public class LaoShu : Role
    {
        public static string dictMiceTag = "mice_tag";
        public static string dictMiceLives = "mice_lives";
        public static string dictMiceTagCount = "mice_tag_count";
        public static string dictMiceGifted = "mice_gifted";

        private static Dictionary<string, object> roleDict = new()
            {
                { YuYanJia.dictYuYanJiaResult, 1 },
                { LangRenSha.dictPlayerAlliance, 1 },
                { dictMiceLives, 2 },
                { LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.God },
            };
        private static List<int> actionOrders = new()
            { 30, 31, 32, 33, 34, 35, 36, 37, 38 };

        public LaoShu()
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
                return "LaoShu";
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

            // Actions 30 and 32 are announcer actions for Mice to open/close eyes
            if (LangRenSha.AnnouncerAction(game, update, false, ActionOrders[0], ActionOrders[2], 50, 51, Name, 4) == GameActionResult.Restart)
            {
                return GameActionResult.Restart;
            }

            // Action 31: Mice player tags a person
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == ActionOrders[1])
            {
                var mice = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
                var miceAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
                
                var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
                var miceTag = Game.GetGameDictionaryProperty(game, dictMiceTag, 0);
                if (miceTag > 0)
                {
                    alivePlayers.Remove(miceTag);
                }

                if (UserAction.EndUserAction(game, update))
                {
                    (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, miceAlive, update);
                    int target = 0;
                    if (miceAlive.Count > 0)
                    {
                        target = miceAlive[0];
                    }
                    if (inputValid)
                    {
                        var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, -1);
                        if (targets.Count > 0)
                            target = targets[0];
                    }

                    // Check if target is cat marked
                    var catMark = Game.GetGameDictionaryProperty(game, DaMao.dictCatMark, 0);
                    update[dictMiceTag] = 0;
                    if (catMark == target && target > 0)
                    {
                        // Tagging fails and mice player dies
                        LangRenSha.MarkPlayerAboutToDie(game, mice[0], update);
                    }
                    else
                    {
                        if (target > 0)
                        {
                            update[dictMiceTag] = target;
                            // Track tag count and check for gifted status
                            UpdateTagCount(game, target, update);
                        }
                    }
                    update[DaMao.dictCatMark] = 0;
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                else
                {
                    var actionDuration = Game.GetGameDictionaryProperty(game, LangRenSha.dictDurationPlayerReact + 5, ActionDuration);

                    if (UserAction.StartUserAction(game, ActionDuration, update))
                    {
                        update[UserAction.dictUserActionTargets] = alivePlayers;
                        update[UserAction.dictUserActionUsers] = mice;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = 72; // Hint for Mice tagging
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, miceAlive, update);
                        if (inputValid)
                        {
                            var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, -1);
                            if (targets.Count == 0)
                            {
                                return GameActionResult.NotExecuted;
                            }
                            var target = targets[0];

                            // Check if target is cat marked
                            var catMark = Game.GetGameDictionaryProperty(game, DaMao.dictCatMark, 0);
                            update[dictMiceTag] = 0;
                            if (catMark == target && mice.Count > 0)
                            {
                                // Tagging fails and mice player dies
                                LangRenSha.MarkPlayerAboutToDie(game, mice[0], update);
                            }
                            else
                            {
                                var isLangRen = target > 0 && mice.Count > 0 && LangRenSha.GetPlayerProperty(game, target, LangRenSha.dictPlayerAlliance, 0) == 2;
                                if (isLangRen)
                                {
                                    var lives = LangRenSha.GetPlayerProperty(game, mice[0], dictMiceLives, 2);
                                    lives -= 1;
                                    LangRenSha.SetPlayerProperty(game, mice[0], dictMiceLives, lives, update);
                                    if (lives <= 0)
                                    {
                                        LangRenSha.MarkPlayerAboutToDie(game, mice[0], update);
                                    }
                                }
                                update[dictMiceTag] = target;
                                // Track tag count and check for gifted status
                                UpdateTagCount(game, target, update);
                            }
                            update[DaMao.dictCatMark] = 0;
                            UserAction.EndUserAction(game, update, true);
                            LangRenSha.AdvanceAction(game, update);
                            return GameActionResult.Restart;
                        }

                    }

                }
            }

            // Actions 33 and 35 are announcer actions for everyone to open/close eyes to check mice information
            if (LangRenSha.AnnouncerAction(game, update, false, ActionOrders[3], ActionOrders[5], 1000, 1001, "MiceCheck", 4) == GameActionResult.Restart)
            {
                return GameActionResult.Restart;
            }

            // Action 34: Announcer action for everyone to check if they are tagged
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == ActionOrders[4])
            {
                if (UserAction.EndUserAction(game, update))
                {
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, 5, update))
                    {
                        var tagged = Game.GetGameDictionaryProperty(game, dictMiceTag, 0);
                        var allPlayers = LangRenSha.GetPlayers(game, x => true);
                        update[UserAction.dictUserActionTargets] = new List<int>();
                        update[UserAction.dictUserActionUsers] = allPlayers;
                        update[UserAction.dictUserActionTargetsCount] = 0;
                        update[UserAction.dictUserActionTargetsHint] = 75; // Hint for checking tag status
                        var isLangRen = tagged > 0 && LangRenSha.GetPlayerProperty(game, tagged, LangRenSha.dictPlayerAlliance, 0) == 2;
                        var miceAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
                        var laoshuPlayer = miceAlive.Count > 0 ? miceAlive[0] : 0;
                        update[UserAction.dictUserActionInfo] = isLangRen ? tagged.ToString() + "," + laoshuPlayer.ToString() : tagged.ToString();
                        return GameActionResult.Restart;
                    }
                }
            }

            // Actions 36 and 38 are announcer actions lucky one
            if (LangRenSha.AnnouncerAction(game, update, true, ActionOrders[6], ActionOrders[8], 52, 53, "LuckyCheck", 4) == GameActionResult.Restart)
            {
                return GameActionResult.Restart;
            }

            // Action 36: Gifted player can use poison on bad players (only on day 1 and later)
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == ActionOrders[7])
            {
                var currentDay = Game.GetGameDictionaryProperty(game, LangRenSha.dictDay, 0);

                // Skip on day 0
                if (currentDay < 1)
                {
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }

                var tagged = Game.GetGameDictionaryProperty(game, dictMiceTag, 0);

                var isGifted = tagged > 0 && LangRenSha.GetPlayerProperty(game, tagged, dictMiceGifted, 0) == 1;

                var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);

                if (UserAction.EndUserAction(game, update))
                {
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, 15, update))
                    {
                        // Action just started - setup for gifted player to select target
                        update[UserAction.dictUserActionTargets] = alivePlayers;
                        update[UserAction.dictUserActionUsers] = new List<int> { tagged };
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = 76; // Hint for gifted poison
                        update[UserAction.dictUserActionInfo] = isGifted ? "gifted" : "";
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        // Action in progress - check for early completion
                        (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, isGifted ? new List<int> { tagged } : new List<int>(), update);
                        if (inputValid)
                        {
                            var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, -1);
                            if (targets.Count == 0)
                            {
                                return GameActionResult.NotExecuted;
                            }
                            var target = targets[0];
                            // Poison only works on bad players (alliance = 2)
                            var targetAlliance = LangRenSha.GetPlayerProperty(game, target, LangRenSha.dictPlayerAlliance, 0);
                            if (targetAlliance == 2)
                            {
                                LangRenSha.MarkPlayerAboutToDie(game, target, update);
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
        /// Updates the tag count for a player and marks them as gifted if they are a PingMin tagged twice
        /// </summary>
        private static void UpdateTagCount(Game game, int target, Dictionary<string, object> update)
        {
            var currentCount = LangRenSha.GetPlayerProperty(game, target, dictMiceTagCount, 0);
            currentCount++;
            LangRenSha.SetPlayerProperty(game, target, dictMiceTagCount, currentCount, update);

            // Check if target is PingMin and has been tagged twice
            if (currentCount >= 2)
            {
                var targetRole = LangRenSha.GetPlayerProperty(game, target, LangRenSha.dictRole, "");
                if (targetRole == "PingMin")
                {
                    LangRenSha.SetPlayerProperty(game, target, dictMiceGifted, 0, update);
                }
            }
        }
    }
}
