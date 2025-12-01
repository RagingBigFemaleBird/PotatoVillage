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
            Dictionary<string, object> selects = null;
            if (game.StateDictionary.ContainsKey(dictUserActionSelects))
            {
                selects = (Dictionary<string, object>)game.StateDictionary[dictUserActionSelects];
                game.StateDictionary.Remove(dictUserActionSelects);
            }
            if ((int)game.StateDictionary[dictUserAction] == 0)
            {
                return GameActionResult.NotExecuted;
            }
            if (selects != null)
            {
                if (game.StateDictionary.ContainsKey(dictUserActionResponse))
                {
                    update[dictUserActionResponse] = game.StateDictionary[dictUserActionResponse];
                }
                else
                {
                    update[dictUserActionResponse] = new Dictionary<string, object>();
                }
                    foreach (var entry in selects)
                    {
                        ((Dictionary<string, object>)update[dictUserActionResponse])[entry.Key] = entry.Value;
                    }
                return GameActionResult.Restart;
            }
            game.Log("User wait idle.");
            Thread.Sleep(1000);
            return GameActionResult.NotExecuted;
        }

        public static bool StartUserAction(Game game, int duration_seconds, Dictionary<string, object> update)
        {
            int now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if ((int)game.StateDictionary[dictUserAction] == 0)
            {
                update[dictUserAction] = now + duration_seconds;
                return true;
            }
            return false;
        }

        public static bool EndUserAction(Game game, Dictionary<string, object> update, bool force = false)
        {
            int now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if ((int)game.StateDictionary[dictUserAction] != 0 && ((int)game.StateDictionary[dictUserAction] <= now || force))
            {
                update[dictUserAction] = 0;
                return true;
            }
            return false;
        }

        public static (bool, List<int>, int) UserActionTargets(Game game, int player)
        {
            if ((int)game.StateDictionary[Game.dictStateSequence] <= 2)
            {
                return (false, null, 0);
            }
            var ret = new List<int>();
            var users = game.StateDictionary.ContainsKey(dictUserActionUsers) ? (List<int>)game.StateDictionary[dictUserActionUsers] : new List<int>();
            var targets = game.StateDictionary.ContainsKey(dictUserActionTargets) ? (List<int>)game.StateDictionary[dictUserActionTargets] : null;
            var targets_count = game.StateDictionary.ContainsKey(dictUserActionTargetsCount) ? (int)game.StateDictionary[dictUserActionTargetsCount] : 0;

            if (users.Contains(player))
            {
                return (true, targets, targets_count);
            }
            return (false, null, 0);
        }

        public static void UserActionRespond(Game game, int player, List<int> targets)
        {
            lock (game.stateLock)
            {
                game.StateDictionary[dictUserActionSelects] = new Dictionary<string, object>();
                ((Dictionary<string, object>)game.StateDictionary[dictUserActionSelects])[player.ToString()] = targets;
            }
        }

        public static (bool, Dictionary<string, object>) GetUserResponse(Game game, bool clearResponse, Dictionary<string, object> update)
        {
            Dictionary<string, object> ret = null;
            if (game.StateDictionary.ContainsKey(dictUserActionResponse))
            {
                if (clearResponse)
                {
                    update[dictUserActionResponse] = null;
                }
                ret = (Dictionary<string, object>)game.StateDictionary[dictUserActionResponse];
                return (true, ret);
            }
            return (false, ret);
        }

        public enum UserInputMode
        {
            VoteMost, //Most vote counts.
            VoteMajor, //Major vote (>= half) counts.
            UniaminousVote,  //Everyone must agree.
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
