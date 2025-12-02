using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading.Tasks;

namespace ProcedureCore.LangRenSha
{
    public class LangRenSha : GameAction
    {
        private List<(int, Role)> players; 
        public LangRenSha()
        {
            players = new List<(int, Role)>();
            players.Add((1, new YuYanJia()));
            players.Add((2, new LangRen()));
            players.Add((3, new LangRen()));
            players.Add((4, new PingMin()));
            players.Add((5, new NvWu()));
        }

        public static string dictPlayers = "players";
        public static string dictRole = "role";
        public static string dictRoleVersion = "role_version";
        public static string dictNightOrders = "night_orders";
        public static string dictAlive = "alive";
        public static string dictDay = "day";
        public static string dictPhase = "phase";
        public static string dictAction = "action";
        public static string dictAboutToDie = "about_to_die";

        public int Version
        {
            get
            {
                return 1;
            }
        }

        public GameActionResult GenerateStateDiff(Game game, Dictionary<string, object> update)
        {
            // Game init
            if (game.StateSequenceNumber == 0)
            {
                // Add people
                update[dictPlayers] = new Dictionary<string, object>();
                foreach (var player in players)
                {
                    var (position, role) = player;
                    ((Dictionary<string, object>)update[dictPlayers])[position.ToString()] = new Dictionary<string, object>();
                    ((Dictionary<string, object>)((Dictionary<string, object>)update[dictPlayers])[position.ToString()])[dictRole] = role.Name;
                    ((Dictionary<string, object>)((Dictionary<string, object>)update[dictPlayers])[position.ToString()])[dictRoleVersion] = role.Version;
                    ((Dictionary<string, object>)((Dictionary<string, object>)update[dictPlayers])[position.ToString()])[dictAlive] = 1;
                    foreach (var rd in role.RoleDict)
                    {
                        ((Dictionary<string, object>)((Dictionary<string, object>)update[dictPlayers])[position.ToString()])[rd.Key] = role.RoleDict[rd.Key];
                    }
                }
                update[dictNightOrders] = new List<int>();
                update[dictDay] = 0;
                update[dictPhase] = 0;
                update[dictAction] = 0;
                return GameActionResult.Restart;
            }
            if (game.StateSequenceNumber >= 2)
            {
                
                if ((int)((Dictionary<string, object>)game.StateDictionary)[dictAction] == 0)
                {
                    AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
            }
            return GameActionResult.NotExecuted;
        }

        public static void AdvanceAction(Game game, Dictionary<string, object> update)
        {
            if ((int)game.StateDictionary[dictPhase] == 1)
            {
                update[dictPhase] = 0;
                update[dictDay] = (int)game.StateDictionary[dictDay] + 1;
            }
            else
            {
                var current = (int)game.StateDictionary[dictAction];
                var nightOrders = GetNightOrders(game);
                int next = nightOrders.Where(n => n > current).OrderBy(n => n).FirstOrDefault();
                if (next == 0)
                {
                    update[dictAction] = 0;
                    if ((int)game.StateDictionary[dictPhase] == 0)
                    {
                        KillDeadPlayers(game, update);
                        update[dictPhase] = 1;
                    }
                }
                else
                {
                    update[dictAction] = next;
                }
            }

        }
        public static List<int> GetNightOrders(Game game)
        {
            return (List<int>)((Dictionary<string, object>)game.StateDictionary)[dictNightOrders];
        }

        public static List<int> GetPlayers(Game game, Func<Dictionary<string, object>, bool> conditional)
        {
            var ret = new List<int>();
            var players = (Dictionary<string, object>)game.StateDictionary[dictPlayers];
            foreach (var player in players)
            {
                if (conditional((Dictionary<string, object>)((Dictionary<string, object>)game.StateDictionary[dictPlayers])[player.Key.ToString()]))
                {
                    ret.Add(int.Parse(player.Key));
                }
            }
            return ret;
        }

        public static void KillDeadPlayers(Game game, Dictionary<string, object> update)
        {
            List<int> aboutToDie = new List<int>();
            if (game.StateDictionary.ContainsKey(dictAboutToDie))
            {
                aboutToDie = (List<int>)game.StateDictionary[dictAboutToDie];
            }
            var players = (Dictionary<string, object>)game.StateDictionary[dictPlayers];
            foreach (var player in aboutToDie)
            {
                ((Dictionary<string, object>)((Dictionary<string, object>)game.StateDictionary[dictPlayers])[player.ToString()])[dictAlive] = 0;
            }
            if (players.Count > 0)
            {
                update[dictPlayers] = players;
            }
            update[dictAboutToDie] = null;
        }

        public static bool MarkPlayerAboutToDie(Game game, int target, Dictionary<string, object> update)
        {
            List<int> aboutToDie = new List<int>();
            if (game.StateDictionary.ContainsKey(dictAboutToDie))
            {
                aboutToDie = (List<int>)game.StateDictionary[dictAboutToDie];
                if (aboutToDie.Contains(target))
                {
                    return false;
                }
            }
            aboutToDie.Add(target);
            update[dictAboutToDie] = aboutToDie;
            return true;
        }

        public static List<int> GetListIntGameDictionaryProperty(Game game, string key)
        {
            var list = new List<int>();
            if (game.StateDictionary.ContainsKey(key))
            {
                list = (List<int>)game.StateDictionary[key];
            }
            return list;
        }

        public static T GetPlayerProperty<T>(Game game, int player, string key, T defaultValue)
        {
            var players = (Dictionary<string, object>)(game.StateDictionary[LangRenSha.dictPlayers]);
            if (!((Dictionary<string, object>)players[player.ToString()]).ContainsKey(key))
            {
                return defaultValue;
            }
            return (T)((Dictionary<string, object>)players[player.ToString()])[key];
        }

        public static void SetPlayerProperty<T>(Game game, int player, string key, T value, Dictionary<string, object> update)
        {
            var players = (Dictionary<string, object>)(game.StateDictionary[LangRenSha.dictPlayers]);

            ((Dictionary<string, object>)players[player.ToString()])[key] = value;

            update[LangRenSha.dictPlayers] = players;
        }

    }
}
