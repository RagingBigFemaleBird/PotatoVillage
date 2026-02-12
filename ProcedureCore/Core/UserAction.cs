using ProcedureCore.LangRenSha;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProcedureCore.Core
{
    public class UserAction : GameAction
    {
        public UserAction() { }

        public int Version
        {
            get
            {
                return 1;
            }
        }

        public static string dictUserAction = "user_action";
        public static string dictUserActionUsers = "user_users";
        public static string dictUserActionTargets = "user_targets";
        public static string dictUserActionTargetsCount = "user_targets_count";
        public static string dictUserActionTargetsHint = "user_targets_hint";
        public static string dictUserActionInfo = "user_info";
        public static string dictUserActionResponse = "user_response";
        public static string dictUserActionSelects = "user_selects";
        public static string dictUserActionSelectsUpdate = "user_selects_update";
        public static object uarLock = new object();

        public GameActionResult GenerateStateDiff(Game game, Dictionary<string, object> update)
        {
            if (Game.GetGameDictionaryProperty(game, dictUserAction, 0) == 0)
            {
                return GameActionResult.NotExecuted;
            }
            bool doWait = false;
            lock (uarLock)
            {
                var uasu = Game.GetPrivateGameDictionaryProperty(game, dictUserActionSelectsUpdate, new Dictionary<string, object>());
                if (uasu.Count > 0)
                {
                    var uas = Game.GetPrivateGameDictionaryProperty(game, dictUserActionSelects, new Dictionary<string, object>());
                    foreach (var entry in uasu)
                    {
                        if (uas.ContainsKey(entry.Key))
                        {
                            uas.Remove(entry.Key);
                        }
                    }
                    Game.SetPrivateGameDictionaryProperty(game, dictUserActionSelects, uas);
                    Game.SetPrivateGameDictionaryProperty(game, dictUserActionSelectsUpdate, new Dictionary<string, object>());
                    return GameActionResult.NotExecuted;
                }
                var selects = Game.GetPrivateGameDictionaryProperty(game, dictUserActionSelects, new Dictionary<string, object>());
                if (selects.Count == 0)
                {
                    doWait = true;
                }
            }
            if (doWait)
            {
                game.Log("User wait idle.");
                game.UserWait(Game.GetGameDictionaryProperty(game, dictUserAction, 0));
            }
            lock (uarLock)
            {
                var selects = Game.GetPrivateGameDictionaryProperty(game, dictUserActionSelects, new Dictionary<string, object>());
                if (selects.Count > 0)
                {
                    var uasu = new Dictionary<string, object>();
                    var ur = Game.GetGameDictionaryProperty(game, dictUserActionResponse, new Dictionary<string, object>());
                    foreach (var entry in selects)
                    {
                        uasu[entry.Key] = entry.Value;
                        ur[entry.Key] = entry.Value;
                    }
                    Game.SetPrivateGameDictionaryProperty(game, dictUserActionSelectsUpdate, uasu);
                    update[dictUserActionResponse] = ur;
                    return GameActionResult.Restart;
                }
            }
            return GameActionResult.NotExecuted;
        }

        public static bool StartUserAction(Game game, int duration_seconds, Dictionary<string, object> update)
        {
            int now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (Game.GetGameDictionaryProperty(game, dictUserAction, 0) == 0)
            {
                update[dictUserAction] = now + duration_seconds;
                return true;
            }
            return false;
        }

        public static bool EndUserAction(Game game, Dictionary<string, object> update, bool force = false)
        {
            int now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var ua = Game.GetGameDictionaryProperty(game, dictUserAction, 0);
            if (ua != 0 && (ua <= now || force))
            {
                update[dictUserActionInfo] = null;
                update[dictUserAction] = 0;
                return true;
            }
            return false;
        }

        public static (bool, List<int>?, int) UserActionTargets(Game game, int player)
        {
            if (Game.GetGameDictionaryProperty(game, Game.dictStateSequence, 0) <= 2)
            {
                return (false, null, 0);
            }
            var ret = new List<int>();
            var users = Game.GetGameDictionaryProperty(game, dictUserActionUsers, new List<int>());
            var targets = Game.GetGameDictionaryProperty(game, dictUserActionTargets, new List<int>());
            var targets_count = Game.GetGameDictionaryProperty(game, dictUserActionTargetsCount, 0);

            if (users.Contains(player))
            {
                return (true, targets, targets_count);
            }
            return (false, null, 0);
        }

        public static void UserActionRespond(Game game, int player, List<int> targets)
        {
            lock (uarLock)
            {
                var response = Game.GetPrivateGameDictionaryProperty(game, dictUserActionSelects, new Dictionary<string, object>());
                response[player.ToString()] = targets;
                Game.SetPrivateGameDictionaryProperty(game, dictUserActionSelects, response);
                game.UserWakeup();
            }
        }

        public static (bool, Dictionary<string, object>, Dictionary<string, object>) GetUserResponse(Game game, bool clearResponse, List<int> users, Dictionary<string, object> update)
        {
            var ret = new Dictionary<string, object>();
            var ret_others = new Dictionary<string, object>();
            users = Game.GetGameDictionaryProperty(game, dictUserActionUsers, new List<int>());
            var targets = Game.GetGameDictionaryProperty(game, dictUserActionTargets, new List<int>());
            var uar = Game.GetGameDictionaryProperty(game,dictUserActionResponse, new Dictionary<string, object>());
            if (uar.Count > 0)
            {
                if (clearResponse)
                {
                    update[dictUserActionResponse] = null;
                }
                foreach (var rp in uar)
                {
                    if (users.Contains(int.Parse(rp.Key)))
                    {
                        var l = new List<int>();
                        var m = new List<int>();
                        foreach (var p in (List<int>)rp.Value)
                        {
                            if (targets.Contains(p))
                            {
                                l.Add(p);
                            }
                            else
                            {
                                m.Add(p);
                            }
                        }
                        if (l.Count > 0)
                        {
                            ret[rp.Key] = l;
                        }
                        if (m.Count > 0)
                        {
                            ret_others[rp.Key] = m;
                        }
                    }
                    else
                    {
                        ret_others[rp.Key] = rp.Value;
                    }
                }
                if (ret.Count == 0 && ret_others.Count == 0)
                {
                    return (false, new Dictionary<string, object>(), new Dictionary<string, object>());
                }
                return (true, ret, ret_others);
            }
            return (false, new Dictionary<string, object>(), new Dictionary<string, object>());
        }

        public enum UserInputMode
        {
            VoteMost, //Most vote counts.
            VoteMajor, //Major vote (>= half) counts.
            UniaminousVote,  //Everyone must agree.
            Input, //Simply take the input.
        }
        public static List<int> TallyUserInput(Dictionary<string, object> UserInput, int totalPlayers, UserInputMode mode, int halfVoteWeightedPlayer)
        {
            var ret = new List<int>();
            var vote = new Dictionary<int, int>();
            foreach (var entry in UserInput)
            {
                var player = int.Parse(entry.Key);
                var targets = (List<int>)entry.Value;
                var target = targets[0];
                if (mode == UserInputMode.Input)
                {
                    return targets;
                }
                if (!vote.ContainsKey(target))
                {
                    vote[target] = 0;
                }
                vote[target] += 2;
                if (player == halfVoteWeightedPlayer)
                {
                    vote[target] += 1;
                }

            }
            var result = vote.OrderByDescending(pair => pair.Value);
            var max = result.FirstOrDefault();
            var maxPlayer = max.Key;
            var maxCount = max.Value;
            if (mode == UserInputMode.VoteMost)
            {
                foreach (var voteResult in result)
                {
                    if (voteResult.Value == maxCount)
                    {
                        ret.Add(voteResult.Key);
                    }
                }
                return ret;
            }
            return ret;
        }
    }
}
