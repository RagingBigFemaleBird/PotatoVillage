using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProcedureCore.LangRenSha
{
    public class Thief : Role
    {
        private static Dictionary<string, object> roleDict = new()
        {
            { YuYanJia.dictYuYanJiaResult, 1 },
            { LangRenSha.dictPlayerAlliance, 1 },
            { LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.Civilian | LangRenSha.PlayerFaction.God},
        };

        private static List<int> actionOrders = new()
        {
            (int)ActionConstant.Thief_OpenEyes,
            (int)ActionConstant.Thief_PickRole,
            (int)ActionConstant.Thief_ShowAttackStatus,
            (int)ActionConstant.Thief_CloseEyes
        };

        public Thief() { }

        public Dictionary<string, object> RoleDict
        {
            get { return roleDict; }
        }

        public static string Name
        {
            get { return "Thief"; }
        }

        string Role.Name => Name;

        public int Version
        {
            get { return 1; }
        }

        public List<int> ActionOrders
        {
            get { return actionOrders; }
        }

        public int ActionDuration
        {
            get { return 10; }
        }

        // Dictionary keys for Thief-specific data
        public static string dictRemovedRoles = "thief_removed_roles"; // List of removed role names
        public static string dictPreviousPick = "thief_previous_pick"; // Previous night's pick
        public static string dictThiefPlayer = "thief_player"; // The thief player's seat number

        /// <summary>
        /// Gets the number of extra roles needed when Thief is present.
        /// The game should have 3 more roles than players.
        /// </summary>
        public static int ExtraRolesNeeded => 3;

        public GameActionResult GenerateStateDiff(Game game, Dictionary<string, object> update)
        {
            if (game.StateSequenceNumber == 1)
            {
                var no = Game.GetGameDictionaryProperty(game, LangRenSha.dictNightOrders, new List<int>());
                // Add setup action (action 2) and regular actions
                no.Add((int)ActionConstant.Thief_Setup);
                no.AddRange(ActionOrders);
                update[LangRenSha.dictNightOrders] = no;
                return GameActionResult.Continue;
            }

            // Action 2: Thief setup - remove 3 players
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == (int)ActionConstant.Thief_Setup)
            {
                var day = Game.GetGameDictionaryProperty(game, LangRenSha.dictDay, 0);

                if (day == 0)
                {
                    SetupThiefRoles(game, update);
                }

                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }

            // Thief open/close eyes announcer (ActionOrders[0] = OpenEyes, ActionOrders[3] = CloseEyes)
            if (LangRenSha.AnnouncerAction(game, update, false, ActionOrders[0], ActionOrders[3], (int)HintConstant.OpenEyes, (int)HintConstant.CloseEyes, Name, 4) == GameActionResult.Restart)
            {
                return GameActionResult.Restart;
            }

            // Action 16: Thief picks role
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == ActionOrders[1])
            {
                var thiefPlayer = Game.GetGameDictionaryProperty(game, dictThiefPlayer, 0);
                var thiefAlive = thiefPlayer > 0 && LangRenSha.GetPlayerProperty(game, thiefPlayer, LangRenSha.dictAlive, 0) == 1;
                var actionDuration = Game.GetGameDictionaryProperty(game, LangRenSha.dictDurationPlayerReact, ActionDuration);

                if (!thiefAlive)
                {
                    actionDuration = new Random().Next(5, 10);
                }

                // Check if thief is still "Thief" role (hasn't picked yet or picked on previous days)
                var currentRole = thiefPlayer > 0 ? LangRenSha.GetPlayerProperty(game, thiefPlayer, LangRenSha.dictRole, "") : "";

                var removedRoles = Game.GetGameDictionaryProperty(game, dictRemovedRoles, new List<string>());
                var previousPick = Game.GetGameDictionaryProperty(game, dictPreviousPick, "");

                if (removedRoles.Count == 0)
                {
                    // No roles to pick from
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }

                // Get available roles (exclude previous pick)
                var availableRoles = removedRoles.Where(r => r != previousPick).ToList();


                if (UserAction.EndUserAction(game, update))
                {
                    (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, new List<int> { thiefPlayer }, update);

                    string pickedRole = "";
                    if (inputValid && input.ContainsKey(thiefPlayer.ToString()))
                    {
                        var targets = (List<int>)input[thiefPlayer.ToString()];
                        if (targets.Count > 0 && targets[0] > 0 && targets[0] <= availableRoles.Count)
                        {
                            pickedRole = availableRoles[targets[0] - 1];
                        }
                    }

                    // Default to first available if no valid pick
                    if (string.IsNullOrEmpty(pickedRole) && availableRoles.Count > 0)
                    {
                        pickedRole = availableRoles[0];
                    }

                    if (!string.IsNullOrEmpty(pickedRole))
                    {
                        // Update previous pick
                        update[dictPreviousPick] = pickedRole;

                        // Change thief's role to the picked role
                        LangRenSha.SetPlayerProperty(game, thiefPlayer, LangRenSha.dictRole, pickedRole, update);

                        game.Log($"Thief (player {thiefPlayer}) picked role: {pickedRole}");
                    }

                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, actionDuration, update))
                    {
                        // Create target list as indices into available roles
                        var targets = new List<int>();
                        for (int i = 1; i <= availableRoles.Count; i++)
                        {
                            targets.Add(i);
                        }

                        update[UserAction.dictUserActionTargets] = targets;
                        update[UserAction.dictUserActionUsers] = new List<int> { thiefPlayer };
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.Thief_PickRole;
                        update[UserAction.dictUserActionRole] = Name;
                        // Send available role names as info
                        update[UserAction.dictUserActionInfo] = string.Join(",", availableRoles);
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        // Check for early response
                        (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, new List<int> { thiefPlayer }, update);
                        if (inputValid && input.ContainsKey(thiefPlayer.ToString()))
                        {
                            var targets = (List<int>)input[thiefPlayer.ToString()];
                            if (targets.Count > 0 && targets[0] > 0 && targets[0] <= availableRoles.Count)
                            {
                                var pickedRole = availableRoles[targets[0] - 1];
                                update[dictPreviousPick] = pickedRole;
                                LangRenSha.SetPlayerProperty(game, thiefPlayer, LangRenSha.dictRole, pickedRole, update);
                                game.Log($"Thief (player {thiefPlayer}) picked role early: {pickedRole}");
                                UserAction.EndUserAction(game, update, true);
                                LangRenSha.AdvanceAction(game, update);
                                return GameActionResult.Restart;
                            }
                        }
                    }
                }

                return GameActionResult.NotExecuted;
            }

            // Action 17: Show attack status (if thief has LangRen available)
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == ActionOrders[2])
            {
                var thiefPlayer = Game.GetGameDictionaryProperty(game, dictThiefPlayer, 0);
                var removedRoles = Game.GetGameDictionaryProperty(game, dictRemovedRoles, new List<string>());
                var hasLangRen = removedRoles.Contains("LangRen");

                if (thiefPlayer == 0 || !hasLangRen)
                {
                    // No thief or no LangRen available, skip this action
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }

                if (UserAction.EndUserAction(game, update))
                {
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, 7, update))
                    {
                        var langRenAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == "LangRen" && (int)x[LangRenSha.dictAlive] == 1);
                        var langRenSuccession1Alive = LangRenSha.GetPlayers(game, x => x.ContainsKey(LangRen.dictSuceession) && (int)x[LangRen.dictSuceession] == 1 && (int)x[LangRenSha.dictAlive] == 1);

                        update[UserAction.dictUserActionTargets] = new List<int>();
                        update[UserAction.dictUserActionUsers] = new List<int>() { thiefPlayer };
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.LangRen_ConvertedSuccession;
                        update[UserAction.dictUserActionRole] = "LangRen";
                        if (langRenAlive.Count + langRenSuccession1Alive.Count == 0)
                        {
                            update[UserAction.dictUserActionInfo] = "Succession";
                        }
                        return GameActionResult.Restart;
                    }
                }

                return GameActionResult.NotExecuted;
            }

            return GameActionResult.NotExecuted;
        }

        /// <summary>
        /// Called during game initialization to set up the thief mechanic.
        /// Selects 3 players to remove based on constraints and stores their roles.
        /// </summary>
        public static void SetupThiefRoles(Game game, Dictionary<string, object> update)
        {
            var allPlayers = LangRenSha.GetPlayers(game, x => true);
            var playersDict = Game.GetGameDictionaryProperty(game, LangRenSha.dictPlayers, new Dictionary<string, object>());

            // Find the thief player
            var thiefPlayers = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
            if (thiefPlayers.Count == 0)
            {
                return; // No thief in game
            }

            var thiefPlayer = thiefPlayers[0];
            update[dictThiefPlayer] = thiefPlayer;

            // Categorize players by their eligibility
            var langRenPlayers = new List<int>();
            var godPlayers = new List<int>();
            var civilianPlayers = new List<int>();

            foreach (var player in allPlayers)
            {
                if (player == thiefPlayer) continue; // Don't remove the thief

                var playerDict = (Dictionary<string, object>)playersDict[player.ToString()];
                var role = (string)playerDict[LangRenSha.dictRole];
                var allegiance = playerDict.ContainsKey(LangRenSha.dictPlayerAlliance) 
                    ? Convert.ToInt32(playerDict[LangRenSha.dictPlayerAlliance]) : 1;

                var factionObj = playerDict.ContainsKey(LangRenSha.dictPlayerFaction) 
                    ? playerDict[LangRenSha.dictPlayerFaction] : null;
                LangRenSha.PlayerFaction faction = LangRenSha.PlayerFaction.Civilian;

                if (factionObj is LangRenSha.PlayerFaction pf)
                    faction = pf;
                else if (factionObj is int fi)
                    faction = (LangRenSha.PlayerFaction)fi;

                // Skip allegiance == 2 but not LangRen (these can't be selected)
                if (allegiance == 2 && role != "LangRen")
                {
                    continue;
                }

                if (role == "LangRen")
                {
                    langRenPlayers.Add(player);
                }
                else if ((faction & LangRenSha.PlayerFaction.God) != 0)
                {
                    godPlayers.Add(player);
                }
                else if ((faction & LangRenSha.PlayerFaction.Civilian) != 0 || role == "PingMin")
                {
                    civilianPlayers.Add(player);
                }
            }

            // Select 3 players based on constraints:
            // - No more than 1 LangRen
            // - No more than 2 God
            // - No more than 2 Civilian/PingMin
            var selectedPlayers = new List<int>();
            var removedRoles = new List<string>();
            bool hasLangRen = false;
            int godCount = 0;
            int civilianCount = 0;

            // Shuffle each category
            ShuffleList(langRenPlayers, game);
            ShuffleList(godPlayers, game);
            ShuffleList(civilianPlayers, game);

            // Try to select 3 players following constraints
            var allCandidates = new List<(int player, string category)>();
            
            foreach (var p in langRenPlayers) allCandidates.Add((p, "langren"));
            foreach (var p in godPlayers) allCandidates.Add((p, "god"));
            foreach (var p in civilianPlayers) allCandidates.Add((p, "civilian"));

            // Shuffle all candidates
            ShuffleList(allCandidates, game);

            foreach (var (player, category) in allCandidates)
            {
                if (selectedPlayers.Count >= 3) break;

                bool canSelect = category switch
                {
                    "langren" => !hasLangRen,
                    "god" => godCount < 2,
                    "civilian" => civilianCount < 2,
                    _ => false
                };

                if (canSelect)
                {
                    selectedPlayers.Add(player);
                    var playerDict = (Dictionary<string, object>)playersDict[player.ToString()];
                    var role = (string)playerDict[LangRenSha.dictRole];
                    removedRoles.Add(role);

                    if (category == "langren")
                    {
                        hasLangRen = true;
                    }
                    else if (category == "god")
                    {
                        godCount++;
                    }
                    else if (category == "civilian")
                    {
                        civilianCount++;
                    }
                }
            }

            if (selectedPlayers.Count < 3)
            {
                game.Log($"Warning: Thief could only select {selectedPlayers.Count} players to remove");
            }

            // Store removed roles
            update[dictRemovedRoles] = removedRoles;
            update[dictPreviousPick] = "";

            // If any LangRen was removed, thief becomes evil allegiance and gets succession
            if (hasLangRen)
            {
                LangRenSha.SetPlayerProperty(game, thiefPlayer, LangRenSha.dictPlayerAlliance, 2, update);
                LangRenSha.SetPlayerProperty(game, thiefPlayer, YuYanJia.dictYuYanJiaResult, 2, update);
                LangRenSha.SetPlayerProperty(game, thiefPlayer, LangRen.dictSuceession, 2, update);
                LangRenSha.SetPlayerProperty(game, thiefPlayer, LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.Evil, update);
            }


            // Remove selected players and adjust seat numbers
            RemovePlayersAndAdjustSeats(game, selectedPlayers, thiefPlayer, update);

            game.Log($"Thief setup complete. Removed roles: {string.Join(", ", removedRoles)}. HasLangRen: {hasLangRen}");
        }

        private static void ShuffleList<T>(List<T> list, Game game)
        {
            int n = list.Count;
            for (int i = n - 1; i > 0; i--)
            {
                int j = game.GetRandomNumber() % (i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private static void RemovePlayersAndAdjustSeats(Game game, List<int> playersToRemove, int thiefPlayer, Dictionary<string, object> update)
        {
            var playersDict = Game.GetGameDictionaryProperty(game, LangRenSha.dictPlayers, new Dictionary<string, object>());
            var newPlayersDict = new Dictionary<string, object>();

            // Sort players to remove for proper seat adjustment
            playersToRemove.Sort();

            // Get all current seat numbers
            var currentSeats = playersDict.Keys.Select(k => int.Parse(k)).OrderBy(x => x).ToList();

            // Create mapping from old seat to new seat
            var seatMapping = new Dictionary<int, int>();
            int newSeat = 1;

            foreach (var oldSeat in currentSeats)
            {
                if (!playersToRemove.Contains(oldSeat))
                {
                    seatMapping[oldSeat] = newSeat;
                    newSeat++;
                }
            }

            // Build new players dictionary with adjusted seats
            foreach (var kvp in seatMapping)
            {
                var oldSeatStr = kvp.Key.ToString();
                var newSeatStr = kvp.Value.ToString();
                newPlayersDict[newSeatStr] = playersDict[oldSeatStr];
            }

            update[LangRenSha.dictPlayers] = newPlayersDict;

            // Update thief player seat number
            if (seatMapping.ContainsKey(thiefPlayer))
            {
                update[dictThiefPlayer] = seatMapping[thiefPlayer];
            }

            game.Log($"Removed {playersToRemove.Count} players. Seat mapping: {string.Join(", ", seatMapping.Select(kvp => $"{kvp.Key}->{kvp.Value}"))}");
        }

        /// <summary>
        /// Checks if the Thief role is present in the role configuration.
        /// </summary>
        public static bool IsThiefInGame(Dictionary<string, int> roleConfiguration)
        {
            return roleConfiguration != null && roleConfiguration.ContainsKey(Name) && roleConfiguration[Name] > 0;
        }

        /// <summary>
        /// Gets the actual number of players needed when Thief is present.
        /// </summary>
        public static int GetActualPlayerCount(int totalRoles)
        {
            return totalRoles - ExtraRolesNeeded;
        }
    }
}
