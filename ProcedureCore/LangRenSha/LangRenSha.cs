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
        private static List<Func<Game, int, List<int>, Dictionary<string, object>, GameActionResult>> interruptHandlers;
        public static List<Func<Game, int, List<int>, Dictionary<string, object>, GameActionResult>> InterruptHandlers
        {
            get
            {
                if (interruptHandlers == null)
                {
                    interruptHandlers = new List<Func<Game, int, List<int>, Dictionary<string, object>, GameActionResult>>();
                    interruptHandlers.Add(LangRen.RevealSelf);
                    interruptHandlers.Add(LangRenSha.WithdrawSheriff);
                }
                return interruptHandlers;
            }
        }
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
        public static string dictInterrupt = "interrupt";
        public static string dictVoteOut = "voteout";
        public static string dictSheriffVote = "sheriffvote";

        public int Version
        {
            get
            {
                return 1;
            }
        }

        public static int actionDuraionPlayerReact = 15;
        public static int actionDurationPlayerSpeak = 5;

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

        public static GameActionResult WithdrawSheriff(Game game, int player, List<int> targets, Dictionary<string, object> update)
        {
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictSpeak, 0) == 1)
            {
                if (targets.Contains(-2))
                {
                    var sheriffPlayers = Game.GetGameDictionaryProperty(game, dictSheriff, new List<int>());
                    if (sheriffPlayers.Remove(player))
                    {
                        update[LangRenSha.dictSheriff] = sheriffPlayers;
                    }
                    return GameActionResult.Continue;
                }
            }
            return GameActionResult.NotExecuted;

        }

        public static GameActionResult HandleSpeaker(Game game, Dictionary<string, object> update)
        {
            var allPlayers = LangRenSha.GetPlayers(game, x => true);
            var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
            // Sheriff volunteer
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 0)
            {

                if (Game.GetGameDictionaryProperty(game, dictDay, 0) == 0)
                {
                    if (UserAction.EndUserAction(game, update))
                    {
                        (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, allPlayers, update);
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
            // Sheriff speech
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 1)
            {
                var sheriffPlayers = Game.GetGameDictionaryProperty(game, dictSheriff, new List<int>());
                return HandleRoundTableSpeak(game, sheriffPlayers, game.GetRandomNumber() % sheriffPlayers.Count, (game.GetRandomNumber() % 2) == 1, update, 2);
            }
            // Sheriff vote
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 2)
            {
                var sheriffPlayers = Game.GetGameDictionaryProperty(game, dictSheriff, new List<int>());
                var votePlayers = new List<int>();
                votePlayers.AddRange(allPlayers);
                votePlayers.RemoveAll(x => sheriffPlayers.Contains(x));

                if (UserAction.EndUserAction(game, update))
                {
                    (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, votePlayers, update);
                    if (inputValid)
                    {
                        var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, -1);
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
            // Sheriff PK
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 3)
            {
                var pkPlayers = Game.GetGameDictionaryProperty(game, dictPk, new List<int>());
                var sheriffPlayers = Game.GetGameDictionaryProperty(game, dictSheriff, new List<int>());
                var first = sheriffPlayers[game.GetRandomNumber() % sheriffPlayers.Count];
                bool directionPlus = (game.GetRandomNumber() % 2) == 0;
                var nextPlayer = NextPlayer(pkPlayers, allPlayers.Count, first, directionPlus);
                return HandleRoundTableSpeak(game, pkPlayers, pkPlayers.IndexOf(nextPlayer), directionPlus, update, 4);
            }
            // Sheriff PK vote
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 4)
            {
                var pkPlayers = Game.GetGameDictionaryProperty(game, dictPk, new List<int>());
                var votePlayers = new List<int>();
                votePlayers.AddRange(allPlayers);
                votePlayers.RemoveAll(x => pkPlayers.Contains(x));

                if (UserAction.EndUserAction(game, update))
                {
                    (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, votePlayers, update);
                    update[dictCurrentSheriff] = 0;
                    if (inputValid)
                    {
                        var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, -1);
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
            // Dead player
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 10)
            {
                KillDeadPlayers(game, update);
                // TODO: skill
                update[dictSpeak] = 20;
                game.UseRandomNumber(update);
                return GameActionResult.Restart;
            }
            // Dead player action
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 20)
            {
                if (Game.GetGameDictionaryProperty(game, dictDay, 0) == 0)
                {
                    var interrupted = new Dictionary<string, object>();

                    interrupted[dictSpeak] = 30;
                    update[dictSpeak] = 100;
                    update[dictInterrupt] = interrupted;
                }
                else
                {
                    update[dictSpeak] = 30;
                }
                return GameActionResult.Restart;
            }
            // Day speech
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 30)
            {
                var ap = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
                var sheriff = Game.GetGameDictionaryProperty(game, dictCurrentSheriff, 0);
                int first = 0;
                if (sheriff != 0)
                {
                    first = ap.IndexOf(sheriff);
                }
                else
                {
                    first = game.GetRandomNumber() % ap.Count;
                }
                bool dir = false;
                if (sheriff == 0)
                {
                    dir = (game.GetRandomNumber() % 2) == 1;
                }
                return HandleRoundTableSpeak(game, ap, first, dir, update, 34);
            }
            // Sheriff vote
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 34)
            {
                var sheriff = Game.GetGameDictionaryProperty(game, dictCurrentSheriff, 0);
                if (sheriff != 0)
                {
                    var sheriffArray = new List<int>();
                    sheriffArray.Add(sheriff);

                    if (UserAction.EndUserAction(game, update))
                    {
                        (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, sheriffArray, update);
                        if (inputValid)
                        {
                            var targets = (List<int>)input[sheriff.ToString()];
                            update[dictSheriffVote] = targets;
                        }
                        update[dictSpeak] = 35;
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        var alivePlayersNow = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
                        if (UserAction.StartUserAction(game, actionDuraionPlayerReact, update))
                        {
                            update[UserAction.dictUserActionTargets] = alivePlayersNow;
                            update[UserAction.dictUserActionUsers] = sheriffArray;
                            update[UserAction.dictUserActionTargetsCount] = -1;
                            update[UserAction.dictUserActionTargetsHint] = 110;
                            return GameActionResult.Restart;
                        }
                    }
                    return GameActionResult.NotExecuted;
                }
                else
                {
                    update[dictSpeak] = 35;
                    return GameActionResult.Restart;
                }
            }
            // vote 1
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 35)
            {
                if (UserAction.EndUserAction(game, update))
                {
                    (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, allPlayers, update);
                    if (inputValid)
                    {
                        var sheriffTargets = Game.GetGameDictionaryProperty(game, dictSheriffVote, new List<int>());
                        int weighted = -1;
                        if (sheriffTargets.Count == 1)
                        {
                            weighted = Game.GetGameDictionaryProperty(game, dictCurrentSheriff, 0);
                        }
                        var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, weighted);
                        update[dictVoteOut] = targets;
                        if (targets.Count > 1)
                        {
                            update[dictSpeak] = 36;
                        }
                        else
                        {
                            update[dictSpeak] = 38;
                        }
                    }
                    else
                    {
                        update[dictSpeak] = 40;
                    }
                    return GameActionResult.Restart;
                }
                else
                {
                    var alivePlayersNow = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
                    if (UserAction.StartUserAction(game, actionDuraionPlayerReact, update))
                    {
                        update[UserAction.dictUserActionTargets] = alivePlayersNow;
                        update[UserAction.dictUserActionUsers] = alivePlayersNow;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = 101;
                        return GameActionResult.Restart;
                    }
                }
                return GameActionResult.NotExecuted;
            }
            // voteout speech
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 36)
            {
                var pkPlayers = Game.GetGameDictionaryProperty(game, dictVoteOut, new List<int>());
                var sheriffPlayers = Game.GetGameDictionaryProperty(game, dictSheriff, new List<int>());
                var first = sheriffPlayers[game.GetRandomNumber() % sheriffPlayers.Count];
                bool directionPlus = (game.GetRandomNumber() % 2) == 1;
                var nextPlayer = NextPlayer(pkPlayers, allPlayers.Count, first, directionPlus);
                return HandleRoundTableSpeak(game, pkPlayers, pkPlayers.IndexOf(nextPlayer), directionPlus, update, 37);
            }
            // vote 2
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 37)
            {
                if (UserAction.EndUserAction(game, update))
                {
                    (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, allPlayers, update);
                    if (inputValid)
                    {
                        int weighted = Game.GetGameDictionaryProperty(game, dictCurrentSheriff, 0);
                        var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, weighted);
                        if (targets.Count == 1)
                        {
                            update[dictVoteOut] = targets;
                            update[dictSpeak] = 38;
                        }
                        else
                        {
                            update[dictSpeak] = 40;
                            update[dictVoteOut] = new List<int>();
                        }
                    }
                    return GameActionResult.Restart;
                }
                else
                {
                    var alivePlayersNow = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
                    var voteout = Game.GetGameDictionaryProperty(game, dictVoteOut, new List<int>());
                    alivePlayersNow.RemoveAll(x => voteout.Contains(x));

                    if (UserAction.StartUserAction(game, actionDuraionPlayerReact, update))
                    {
                        update[UserAction.dictUserActionTargets] = voteout;
                        update[UserAction.dictUserActionUsers] = alivePlayersNow;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = 101;
                        return GameActionResult.Restart;
                    }
                }
                return GameActionResult.NotExecuted;
            }
            // voteout speech
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 36)
            {
                var pkPlayers = Game.GetGameDictionaryProperty(game, dictVoteOut, new List<int>());
                var sheriffPlayers = Game.GetGameDictionaryProperty(game, dictSheriff, new List<int>());
                var first = sheriffPlayers[game.GetRandomNumber() % sheriffPlayers.Count];
                bool directionPlus = (game.GetRandomNumber() % 2) == 1;
                var nextPlayer = NextPlayer(pkPlayers, allPlayers.Count, first, directionPlus);
                return HandleRoundTableSpeak(game, pkPlayers, pkPlayers.IndexOf(nextPlayer), directionPlus, update, 37);
            }
            // voted out
            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 38)
            {
                var voteout = Game.GetGameDictionaryProperty(game, dictVoteOut, new List<int>());

                if (voteout.Count == 1)
                {
                    var interrupted = new Dictionary<string, object>();

                    interrupted[dictSpeak] = 40;
                    update[dictSpeak] = 100;
                    update[dictInterrupt] = interrupted;
                }
                else
                {
                    update[dictSpeak] = 40;
                }
                return GameActionResult.Restart;
            }

            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 40)
            {
                update[dictSpeak] = 0;
                AdvanceAction(game, update);
                return GameActionResult.Restart;
            }

            if (Game.GetGameDictionaryProperty(game, dictSpeak, 0) == 100)
            {
                var dp = Game.GetGameDictionaryProperty(game, dictDeadPlayerAction, new List<int>());
                if (dp.Count > 0)
                {
                    return HandleRoundTableSpeak(game, dp, game.GetRandomNumber() % dp.Count, game.GetRandomNumber() % 2 == 1, update, -1);
                }
                else
                {
                    update[dictSpeak] = 30;
                    return GameActionResult.Restart;
                }
            }

            return GameActionResult.NotExecuted;
        }

        public static int NextPlayer(List<int> players, int totalPlayers, int startingPlayer, bool directionPlus)
        {
            int next = startingPlayer;
            do
            {
                if (directionPlus)
                {
                    next++;
                    if (next > totalPlayers)
                    {
                        next = 0;
                    }
                }
                else
                {
                    next--;
                    if (next <= 0)
                    {
                        next = totalPlayers;
                    }
                }
            } while (!players.Contains(next));
            return next;
        }
        public static GameActionResult HandleRoundTableSpeak(Game game, List<int> players, int startingPlayer, bool directionPlus, Dictionary<string, object> update, int nextSpeak)
        {
            var speakers = Game.GetGameDictionaryProperty(game, dictSpeaker, new List<int>());
            var allPlayers = LangRenSha.GetPlayers(game, x => true);
            if (UserAction.EndUserAction(game, update))
            {
                if (speakers.Count >= players.Count)
                {
                    update[dictSpeaker] = null;
                    if (nextSpeak > 0)
                    {

                        update[dictSpeak] = nextSpeak;
                    }
                    else
                    {
                        var interrupted = Game.GetGameDictionaryProperty(game, dictInterrupt, new Dictionary<string, object>());
                        foreach (var item in interrupted)
                        {
                            update[item.Key] = item.Value;
                        }
                    }
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
                        speakers.Add(players[startingPlayer]);
                    }
                    else
                    {
                        var last = speakers.Last();
                        var first = speakers.First();
                        var next = last;
                        do
                        {
                            if (directionPlus)
                            {
                                next++;
                                if (next > allPlayers.Count)
                                {
                                    next = 1;
                                }
                            }
                            else
                            {
                                next--;
                                if (next <= 0)
                                {
                                    next = allPlayers.Count;
                                }
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
                else
                {
                    (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, new List<int>(), update);
                    if (inputValid)
                    {
                        foreach (var int_input in input_others)
                        {
                            var key = int.Parse(int_input.Key);
                            var value = (List<int>)int_input.Value;
                            foreach (var handler in LangRenSha.InterruptHandlers)
                            {
                                var result = handler(game, key, value, update);
                                if (result == GameActionResult.NotExecuted)
                                {
                                    continue;
                                }
                                UserAction.EndUserAction(game, update, true);
                                return result;
                            }

                        }
                    }

                }
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
