using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Media;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

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
            players.Add((4, new LangRen()));
            players.Add((5, new NvWu()));
            players.Add((6, new WuZhe()));
            players.Add((7, new JiaMian()));
        }

        public static string dictPlayers = "players";
        public static string dictRole = "role";
        public static string dictRoleVersion = "role_version";
        public static string dictNightOrders = "night_orders";
        public static string dictAlive = "alive";
        public static string dictDay = "day";
        public static string dictPhase = "phase";
        public static string dictAction = "action";
        public static string dictSpeak = "speak";
        public static string dictSpeaker = "speaker";
        public static string dictAboutToDie = "about_to_die";
        public static string dictDeadPlayerAction = "dead_player";
        public static string dictPlayerAlliance = "alliance";
        public static string dictSheriff = "sheriff";
        public static string dictCurrentSheriff = "current_sheriff";
        public static string dictPk = "pk";

        public int Version
        {
            get
            {
                return 1;
            }
        }

        public static int actionDuraionPlayerReact = 10;
        public static int actionDurationPlayerSpeak = 2;

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
                update[dictSpeak] = 0;
                return GameActionResult.Restart;
            }
            if (game.StateSequenceNumber >= 2)
            {                
                if (Game.GetGameDictionaryProperty(game, dictAction, 0) == 0 && Game.GetGameDictionaryProperty(game, dictPhase, 0) == 0)
                {
                    AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                else if (Game.GetGameDictionaryProperty(game, dictPhase, 0) == 1)
                {
                    return HandleSpeaker(game, update);
                }
            }
            return GameActionResult.NotExecuted;
        }

        public static GameActionResult HandleSpeaker(Game game, Dictionary<string, object> update)
        {
            var allPlayers = LangRenSha.GetPlayers(game, x => true);
            var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 0)
            {

                if (Game.GetGameDictionaryProperty(game, dictDay, 0) == 0)
                {
                    if (UserAction.EndUserAction(game, update))
                    {
                        (var inputValid, var input) = UserAction.GetUserResponse(game, true, allPlayers, update);
                        var sheriffPlayers = new List<int>();
                        if (inputValid)
                        {
                            foreach (var player in allPlayers)
                            {
                                if (input.ContainsKey(player.ToString()) && ((List<int>)input[player.ToString()]).Contains(-1))
                                {
                                    sheriffPlayers.Add(player);
                                }
                            }
                        }
                        update[dictSpeaker] = null;
                        update[dictSheriff] = sheriffPlayers;
                        if (sheriffPlayers.Count == allPlayers.Count || sheriffPlayers.Count == 0)
                        {
                            update[dictSpeak] = 10;
                        }
                        else if (sheriffPlayers.Count == 1)
                        {
                            update[dictSpeak] = 10;
                            update[dictCurrentSheriff] = sheriffPlayers[0];
                        }
                        else
                        {
                            update[dictSpeak] = 1;
                        }
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        if (UserAction.StartUserAction(game, actionDuraionPlayerReact, update))
                        {
                            update[UserAction.dictUserActionTargets] = new List<int> { -1, 0 };
                            update[UserAction.dictUserActionUsers] = allPlayers;
                            update[UserAction.dictUserActionTargetsCount] = 1;
                            update[UserAction.dictUserActionTargetsHint] = 100;
                            return GameActionResult.Restart;
                        }
                    }
                    return GameActionResult.NotExecuted;
                }
                else
                {
                    update[dictSpeak] = 10;
                    return GameActionResult.Restart;
                }

            }
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 1)
            {
                var sheriffPlayers = Game.GetGameDictionaryProperty(game, dictSheriff, new List<int>());
                return HandleRoundTableSpeak(game, sheriffPlayers, update, 2);
            }
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 2)
            {
                var sheriffPlayers = Game.GetGameDictionaryProperty(game, dictSheriff, new List<int>());
                var votePlayers = new List<int>();
                votePlayers.AddRange(allPlayers);
                votePlayers.RemoveAll(x => sheriffPlayers.Contains(x));

                if (UserAction.EndUserAction(game, update))
                {
                    (var inputValid, var input) = UserAction.GetUserResponse(game, true, votePlayers, update);
                    if (inputValid)
                    {
                        var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost);
                        if (targets.Count > 1)
                        {
                            update[dictSpeak] = 3;
                            update[dictPk] = targets;
                        }
                        else
                        {
                            update[dictSpeak] = 10;
                            update[dictCurrentSheriff] = targets[0];
                        }
                    }
                    else
                    {
                        update[dictSpeak] = 10;
                        update[dictCurrentSheriff] = 0;
                    }
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, actionDuraionPlayerReact, update))
                    {
                        update[UserAction.dictUserActionTargets] = sheriffPlayers;
                        update[UserAction.dictUserActionUsers] = votePlayers;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = 102;
                        return GameActionResult.Restart;
                    }
                }
                return GameActionResult.NotExecuted;

            }

            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 3)
            {
                var pkPlayers = Game.GetGameDictionaryProperty(game, dictPk, new List<int>());
                return HandleRoundTableSpeak(game, pkPlayers, update, 4);
            }

            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 4)
            {
                var pkPlayers = Game.GetGameDictionaryProperty(game, dictPk, new List<int>());
                var votePlayers = new List<int>();
                votePlayers.AddRange(pkPlayers);
                votePlayers.RemoveAll(x => pkPlayers.Contains(x));

                if (UserAction.EndUserAction(game, update))
                {
                    (var inputValid, var input) = UserAction.GetUserResponse(game, true, votePlayers, update);
                    update[dictCurrentSheriff] = 0;
                    if (inputValid)
                    {
                        var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost);
                        if (inputValid)
                        {
                            if (targets.Count == 1)
                            {
                                update[dictCurrentSheriff] = targets[0];
                            }
                        }
                    }
                    update[dictPk] = null;
                    update[dictSpeak] = 10;
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, actionDuraionPlayerReact, update))
                    {
                        update[UserAction.dictUserActionTargets] = pkPlayers;
                        update[UserAction.dictUserActionUsers] = votePlayers;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = 102;
                        return GameActionResult.Restart;
                    }
                }
                return GameActionResult.NotExecuted;

            }
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 10)
            {
                KillDeadPlayers(game, update);
                // TODO: skill
                update[dictSpeak] = 20;
                return GameActionResult.Restart;
            }

            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 20)
            {
                if (Game.GetGameDictionaryProperty(game, dictDay, 0) == 0)
                {
                    var dp = Game.GetGameDictionaryProperty(game, dictDeadPlayerAction, new List<int>());
                    if (dp.Count > 0)
                    {
                        return HandleRoundTableSpeak(game, dp, update, 30);
                    }
                    else
                    {
                        update[dictSpeak] = 30;
                    }
                }
                else
                {
                    update[dictSpeak] = 30;
                }
                return GameActionResult.Restart;
            }

            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 30)
            {
                var ap = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
                return HandleRoundTableSpeak(game, alivePlayers, update, 40);
            }

            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) >= 40)
            {
                update[dictSpeak] = 0;
                AdvanceAction(game, update);
                return GameActionResult.Restart;
            }

            return GameActionResult.NotExecuted;
        }

        public static GameActionResult HandleRoundTableSpeak(Game game, List<int> players, Dictionary<string, object> update, int nextSpeak)
        {
            var speakers = Game.GetGameDictionaryProperty(game, dictSpeaker, new List<int>());
            var allPlayers = LangRenSha.GetPlayers(game, x => true);
            if (UserAction.EndUserAction(game, update))
            {
                if (speakers.Count == players.Count)
                {
                    update[dictSpeaker] = null;
                    update[dictSpeak] = nextSpeak;
                    return GameActionResult.Restart;
                }
                return GameActionResult.Restart;
            }
            else
            {
                if (UserAction.StartUserAction(game, actionDurationPlayerSpeak, update))
                {
                    if (speakers.Count == 0)
                    {
                        // TODO: random
                        speakers.Add(players[0]);
                    }
                    else
                    {
                        var last = speakers.Last();
                        var first = speakers.First();
                        // TODO: ccw or cw
                        var next = last;
                        do
                        {
                            next++;
                            if (next > allPlayers.Count)
                            {
                                next = 1;
                            }
                            if (players.Contains(next))
                            {
                                break;
                            }
                        } while (next != last);
                        speakers.Add(next);
                    }
                    update[dictSpeaker] = speakers;
                    update[UserAction.dictUserActionTargets] = new List<int> { -1, 0 };
                    update[UserAction.dictUserActionUsers] = allPlayers;
                    update[UserAction.dictUserActionTargetsCount] = 1;
                    update[UserAction.dictUserActionTargetsHint] = 101;
                    return GameActionResult.Restart;
                }
                // TODO: pass and other skills.
            }

            return GameActionResult.NotExecuted;
        }

        public static void AdvanceAction(Game game, Dictionary<string, object> update)
        {
            if (Game.GetGameDictionaryProperty(game, dictPhase, 0) == 1)
            {
                update[dictPhase] = 0;
                update[dictDay] = Game.GetGameDictionaryProperty(game, dictDay, 0) + 1;
            }
            else
            {
                var current = Game.GetGameDictionaryProperty(game, dictAction, 0);
                var nightOrders = GetNightOrders(game);
                int next = nightOrders.Where(n => n > current).OrderBy(n => n).FirstOrDefault();
                if (next == 0)
                {
                    update[dictAction] = 0;
                    if (Game.GetGameDictionaryProperty(game, dictPhase, 0) == 0)
                    {
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
            return Game.GetGameDictionaryProperty(game, dictNightOrders, new List<int>());
        }

        public static List<int> GetPlayers(Game game, Func<Dictionary<string, object>, bool> conditional)
        {
            var ret = new List<int>();
            var players = Game.GetGameDictionaryProperty(game, dictPlayers, new Dictionary<string, object>());
            foreach (var player in players)
            {
                if (conditional((Dictionary<string, object>)players[player.Key.ToString()]))
                {
                    ret.Add(int.Parse(player.Key));
                }
            }
            return ret;
        }

        public static void KillDeadPlayers(Game game, Dictionary<string, object> update)
        {
            var aboutToDie = Game.GetGameDictionaryProperty(game, dictAboutToDie, new List<int>());
            var players = Game.GetGameDictionaryProperty(game, dictPlayers, new Dictionary<string, object>());
            foreach (var player in aboutToDie)
            {
                ((Dictionary<string, object>)players[player.ToString()])[dictAlive] = 0;
            }
            if (players.Count > 0)
            {
                update[dictPlayers] = players;
            }
            update[LangRen.dictAttackTarget] = null;
            update[dictDeadPlayerAction] = aboutToDie;
            update[dictAboutToDie] = null;
        }

        public static bool MarkPlayerAboutToDie(Game game, int target, Dictionary<string, object> update)
        {
            var aboutToDie = Game.GetGameDictionaryProperty(game, dictAboutToDie, new List<int>());
            if (aboutToDie.Contains(target))
            {
                return false;
            }

            aboutToDie.Add(target);
            update[dictAboutToDie] = aboutToDie;
            return true;
        }

        public static T GetPlayerProperty<T>(Game game, int player, string key, T defaultValue)
        {
            var players = Game.GetGameDictionaryProperty(game, dictPlayers, new Dictionary<string, object>());
            if (!((Dictionary<string, object>)players[player.ToString()]).ContainsKey(key))
            {
                return defaultValue;
            }
            return (T)((Dictionary<string, object>)players[player.ToString()])[key];
        }

        public static void SetPlayerProperty<T>(Game game, int player, string key, T value, Dictionary<string, object> update)
        {
            var players = Game.GetGameDictionaryProperty(game, dictPlayers, new Dictionary<string, object>());

            ((Dictionary<string, object>)players[player.ToString()])[key] = value;

            update[LangRenSha.dictPlayers] = players;
        }

    }
}
