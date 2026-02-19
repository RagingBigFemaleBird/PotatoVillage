using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Concurrent;

namespace ProcedureCore.Core
{
    public class Game
    {
        public Game(Action<Game, Dictionary<string, object>> stateUpdateCallback)
        {
            StateDictionary = new Dictionary<string, object>();
            PrivateStateDictionary = new Dictionary<string, object>();
            StateJournal = new List<Dictionary<string, object>>();
            StateSequenceNumber = 0;
            Actions = new List<GameAction>();
            StateDictionary[dictStateSequence] = StateSequenceNumber;
            StateDictionary[UserAction.dictUserAction] = 0;
            StateDictionary[dictRandomSeed] = new System.Random().Next();
            Actions.Add(new UserAction());
            PlayerToId = new ConcurrentDictionary<string, int>();
            IdToPlayer = new ConcurrentDictionary<int, string>();
            StateUpdateCallback = stateUpdateCallback;
        }

        private Action<Game, Dictionary<string, object>> StateUpdateCallback;
        protected Dictionary<string, object> StateDictionary { get; set; }
        protected Dictionary<string, object> PrivateStateDictionary { get; set; }

        public readonly object stateLock = new object();
        public List<Dictionary<string, object>> StateJournal { get; private set; }

        public static string dictStateSequence = "sequence";
        public static string dictRandomSeed = "random";
        public int StateSequenceNumber { get; set; }

        private AutoResetEvent userWait = new(false);

        public List<GameAction> Actions { get; private set; }
        public int TotalPlayers { get; set; }
        public ConcurrentDictionary<string, int> PlayerToId { get; private set; }
        public ConcurrentDictionary<int, string> IdToPlayer { get; private set; }
        public ConcurrentDictionary<int, string> PlayerNicknames { get; private set; } = new();
        public Dictionary<string, int> RoleConfiguration { get; set; } = new();
        public bool GameStarted { get; set; } = false;

        public int StateUpdate(Dictionary<string, object> stateDiff)
        {
            lock (stateLock)
            {
                foreach (var diff in stateDiff)
                {
                    if (diff.Value == null)
                    {
                        if (StateDictionary.ContainsKey(diff.Key))
                        {
                            StateDictionary.Remove(diff.Key);
                        }
                        else
                        {
                            // log exception
                        }
                    }
                    else
                    {
                        StateDictionary[diff.Key] = diff.Value;
                    }
                }
            }
            LogDict("Game state update:", stateDiff);
            StateUpdateCallback(this, stateDiff);
            //LogDict("Current game state:", StateDictionary);
            return 0;
        }

        public GameActionResult InitiateAction(GameAction action, Dictionary<string, object> update)
        {
            lock (stateLock)
            {
                var result = action.GenerateStateDiff(this, update);
                return result;
            }
        }

        public void ActionLoop()
        {
            while (true)
            {
                var update = new Dictionary<string, object>();
                bool doUpdate = false;
                foreach (var action in Actions)
                {
                    var result = InitiateAction(action, update);
                    if (result != GameActionResult.NotExecuted)
                    {
                        doUpdate = true;
                    }
                    if (result == GameActionResult.Restart)
                    {
                        break;
                    }
                }
                if (doUpdate)
                {
                    lock (stateLock)
                    {
                        StateSequenceNumber++;
                        update[dictStateSequence] = StateSequenceNumber;
                        StateJournal.Add(update);
                        StateUpdate(update);
                    }
                }
            }
        }

        public Dictionary<string, object> GetGameDictionary()
        {
            return StateDictionary;
        }

        public static T GetGameDictionaryProperty<T>(Game game, string key, T defaultValue)
        {
            if (game.StateDictionary.ContainsKey(key))
            {
                return (T)game.StateDictionary[key];
            }
            return defaultValue;
        }

        public static T GetPrivateGameDictionaryProperty<T>(Game game, string key, T defaultValue)
        {
            if (game.PrivateStateDictionary.ContainsKey(key))
            {
                return (T)game.PrivateStateDictionary[key];
            }
            return defaultValue;
        }

        public static void SetPrivateGameDictionaryProperty<T>(Game game, string key, T value)
        {
            game.PrivateStateDictionary[key] = value;
        }

        public int GetRandomNumber()
        {
            return (int)StateDictionary[dictRandomSeed];
        }
        public int UseRandomNumber(Dictionary<string, object> update)
        {
            var ret = (int)StateDictionary[dictRandomSeed];
            StateDictionary[dictRandomSeed] = new System.Random(ret).Next();
            return ret;
        }

        public void LogDict(string reason, Dictionary<string, object> dict)
        {
            string jsonString = JsonSerializer.Serialize(dict);
            Console.WriteLine(reason + " " + jsonString);
        }

        public void Log(string log)
        {
            Console.WriteLine(log);
        }

        public void UserWait(int utcSeconds)
        {
            var now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (utcSeconds <= now)
            {
                return;
            }
            userWait.WaitOne((utcSeconds - now) * 1000);
        }

        public void UserWakeup()
        {
            userWait.Set();
        }
    }
}
