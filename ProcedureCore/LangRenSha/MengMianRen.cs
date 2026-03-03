using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProcedureCore.LangRenSha
{
    /// <summary>
    /// MengMianRen (蒙面人 - Masked Person) role.
    /// At the end of night, if this player is about to die, they survive but become wounded.
    /// After speaking during the day, if wounded, the player dies.
    /// </summary>
    public class MengMianRen : Role
    {
        private static Dictionary<string, object> roleDict = new()
        {
            { YuYanJia.dictYuYanJiaResult, 1 },
            { LangRenSha.dictPlayerAlliance, 1 },
            { LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.God },
        };

        private static List<int> actionOrders = new()
        {
            (int)ActionConstant.MengMianRen_Act
        };

        public MengMianRen() { }

        public Dictionary<string, object> RoleDict
        {
            get { return roleDict; }
        }

        public string Name
        {
            get { return "MengMianRen"; }
        }

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
            get { return 1; }
        }

        // Dictionary key for wounded state
        public static string dictIsWounded = "mengmianren_wounded";
        public static string dictMengMianRenDeadPlayer = "mengmianren_dead_player";

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

            // Action: MengMianRen checks if about to die and becomes wounded instead
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == ActionOrders[0])
            {
                var mengMianRenPlayers = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
                var aboutToDie = Game.GetGameDictionaryProperty(game, LangRenSha.dictAboutToDie, new List<int>());

                foreach (var player in mengMianRenPlayers)
                {
                    // Check if this MengMianRen is about to die
                    if (aboutToDie.Contains(player))
                    {
                        // Check if already wounded (can only survive once)
                        var isAlreadyWounded = LangRenSha.GetPlayerProperty(game, player, dictIsWounded, 0);
                        if (isAlreadyWounded == 0)
                        {
                            // Remove from about to die list
                            aboutToDie.Remove(player);
                            update[LangRenSha.dictAboutToDie] = aboutToDie;

                            // Set wounded state
                            LangRenSha.SetPlayerProperty(game, player, dictIsWounded, 1, update);

                            game.Log($"MengMianRen (player {player}) survived attack and became wounded");
                        }
                    }
                }

                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }

            return GameActionResult.NotExecuted;
        }

        /// <summary>
        /// AfterSpeak handler for MengMianRen.
        /// Called after a player finishes speaking during the day.
        /// If the player is a wounded MengMianRen, use interrupt system to handle death.
        /// </summary>
        public static (bool handled, GameActionResult result) HandleAfterSpeak(Game game, int player, Dictionary<string, object> update)
        {
            var isWounded = LangRenSha.GetPlayerProperty(game, player, dictIsWounded, 0);
            if (isWounded != 1)
            {
                return (false, GameActionResult.NotExecuted);
            }

            // Check if player is still alive
            var isAlive = LangRenSha.GetPlayerProperty(game, player, LangRenSha.dictAlive, 1);
            if (isAlive != 1)
            {
                return (false, GameActionResult.NotExecuted);
            }

            // Kill the player
            LangRenSha.SetPlayerProperty(game, player, LangRenSha.dictAlive, 0, update);

            // Add to dead players list for processing
            var deadPlayers = Game.GetGameDictionaryProperty(game, LangRenSha.dictDeadPlayerAction, new List<int>());
            if (!deadPlayers.Contains(player))
            {
                deadPlayers.Add(player);
                update[LangRenSha.dictDeadPlayerAction] = deadPlayers;
            }

            game.Log($"MengMianRen (player {player}) died after speaking due to wound");

            // Save current speak state and interrupt to MengMianRen death announcement
            var currentSpeak = Game.GetGameDictionaryProperty(game, LangRenSha.dictSpeak, 0);
            var interrupted = new Dictionary<string, object>();
            interrupted[LangRenSha.dictSpeak] = currentSpeak;
            update[LangRenSha.dictInterrupt] = interrupted;
            update[LangRenSha.dictSpeak] = (int)SpeakConstant.MengMianRenDeath;
            update[dictMengMianRenDeadPlayer] = player;

            return (true, GameActionResult.Restart);
        }

        /// <summary>
        /// Death handler for MengMianRen.
        /// If not wounded, revive by setting alive=1 and removing from deadPlayerAction.
        /// </summary>
        public static (bool handled, GameActionResult result) HandleDeathSkill(Game game, int player, Dictionary<string, object> update)
        {
            // Check if this is a MengMianRen
            var role = LangRenSha.GetPlayerProperty(game, player, LangRenSha.dictRole, "");
            if (role != "MengMianRen")
            {
                return (false, GameActionResult.NotExecuted);
            }

            // Check if wounded - if wounded, they stay dead
            var isWounded = LangRenSha.GetPlayerProperty(game, player, dictIsWounded, 0);
            if (isWounded == 1)
            {
                return (false, GameActionResult.NotExecuted);
            }

            // Not wounded - revive the player
            LangRenSha.SetPlayerProperty(game, player, LangRenSha.dictAlive, 1, update);

            // Remove from dead players list
            var deadPlayers = Game.GetGameDictionaryProperty(game, LangRenSha.dictDeadPlayerAction, new List<int>());
            if (deadPlayers.Contains(player))
            {
                deadPlayers.Remove(player);
                update[LangRenSha.dictDeadPlayerAction] = deadPlayers;
            }

            // Set wounded state so they can only survive once
            LangRenSha.SetPlayerProperty(game, player, dictIsWounded, 1, update);

            game.Log($"MengMianRen (player {player}) revived and became wounded");

            return (true, GameActionResult.Restart);
        }
    }
}
