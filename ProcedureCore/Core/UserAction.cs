using ProcedureCore.LangRenSha;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
        public static string dictUserActionResponse = "user_response";
        public static string dictUserActionSelects = "user_selects";

        public GameActionResult GenerateStateDiff(Game game, Dictionary<string, object> update)
        {
            var selects = Game.GetPrivateGameDictionaryProperty(game, dictUserActionSelects, new Dictionary<string, object>());
            Game.SetPrivateGameDictionaryProperty(game, dictUserActionSelects, new Dictionary<string, object>());
            if (Game.GetGameDictionaryProperty(game, dictUserAction, 0) == 0)
            {
                return GameActionResult.NotExecuted;
            }
            if (selects.Count > 0)
            {
                var ur = Game.GetGameDictionaryProperty(game, dictUserActionResponse, new Dictionary<string, object>());
                foreach (var entry in selects)
                {
                    ur[entry.Key] = entry.Value;
                }
                update[dictUserActionResponse] = ur;
                return GameActionResult.Restart;
            }
            game.Log("User wait idle.");
            Thread.Sleep(1000);
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
                update[dictUserAction] = 0;
                return true;
            }
            return false;
        }

        public static (bool, List<int>, int) UserActionTargets(Game game, int player)
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
            var response = new Dictionary<string, object>();
            response[player.ToString()] = targets;
            Game.SetPrivateGameDictionaryProperty(game, dictUserActionSelects, response);
        }

        public static (bool, Dictionary<string, object>) GetUserResponse(Game game, bool clearResponse, List<int> users, Dictionary<string, object> update)
        {
            var ret = new Dictionary<string, object>();
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
                        foreach (var p in (List<int>)rp.Value)
                        {
                            if (targets.Contains(p))
                            {
                                l.Add(p);
                            }
                        }
                        if (l.Count > 0)
                        {
                            ret[rp.Key] = l;
                        }
                    }
                }
                if (ret.Count == 0)
                {
                    return (false, null);
                }
                return (true, ret);
            }
            return (false, null);
        }

        public enum UserInputMode
        {
            VoteMost, //Most vote counts.
            VoteMajor, //Major vote (>= half) counts.
            UniaminousVote,  //Everyone must agree.
            Input, //Simply take the input.
        }
        public static List<int> TallyUserInput(Dictionary<string, object> UserInput, int totalPlayers, UserInputMode mode)
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
                vote[target] += 1;
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
