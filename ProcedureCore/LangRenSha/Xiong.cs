using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcedureCore.LangRenSha
{
    public class Xiong : Role
    {
        private static Dictionary<string, object> roleDict = new()
        {
            { YuYanJia.dictYuYanJiaResult, 1 },
            { LangRenSha.dictPlayerAlliance, 1 },
            { LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.God },
        };

        private static List<int> actionOrders = new() { (int)ActionConstant.Xiong_OpenEyes, (int)ActionConstant.Xiong_Act, (int)ActionConstant.Xiong_CloseEyes, (int)ActionConstant.Xiong_BarkCheck };

        public Xiong()
        {
        }

        public Dictionary<string, object> RoleDict
        {
            get { return roleDict; }
        }

        public string Name
        {
            get { return "Xiong"; }
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
            get { return 15; }
        }

        // Dictionary keys for Xiong state
        public static string dictXiongLink = "xiong_link";           // Player property: linked target
        public static string dictXiongBark = "xiong_bark";           // Global: did Xiong bark today? (1 = barked, 2 = not barked)

        public GameActionResult GenerateStateDiff(Game game, Dictionary<string, object> update)
        {
            if (game.StateSequenceNumber == 1)
            {
                // Add Xiong's actions to night orders
                var addSelf = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
                if (addSelf.Count > 0)
                {
                    var no = Game.GetGameDictionaryProperty(game, LangRenSha.dictNightOrders, new List<int>());
                    no.AddRange(ActionOrders);
                    update[LangRenSha.dictNightOrders] = no;
                }
                return GameActionResult.Continue;
            }

            // Actions 180 and 182 are announcer actions (open eyes, close eyes)
            if (LangRenSha.AnnouncerAction(game, update, false, ActionOrders[0], ActionOrders[2], 50, 51, Name, 4) == GameActionResult.Restart)
            {
                return GameActionResult.Restart;
            }

            // Action 161: Xiong selects a target to link (only if evil)
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == ActionOrders[1])
            {
                var xiong = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
                var xiongAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
                var xiongPlayer = xiongAlive.Count > 0 ? xiongAlive[0] : 0;

                // Check if Xiong is evil (alliance = 2)
                var isEvil = xiongPlayer > 0 && LangRenSha.GetPlayerProperty(game, xiongPlayer, LangRenSha.dictPlayerAlliance, 1) == 2;

                var actionDuration = Game.GetGameDictionaryProperty(game, LangRenSha.dictDurationPlayerReact, ActionDuration);
                if (xiongAlive.Count == 0 || !isEvil)
                {
                    // Dead or not evil - random short duration
                    actionDuration = new Random().Next(3, 6);
                }

                var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
                var lastLink = xiongPlayer > 0 ? LangRenSha.GetPlayerProperty(game, xiongPlayer, dictXiongLink, 0) : 0;
                if (lastLink > 0)
                {
                    alivePlayers.Remove(lastLink); // Cannot link same target as last time
                }

                alivePlayers.Remove(xiongPlayer); // Cannot link self

                if (UserAction.EndUserAction(game, update))
                {
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, actionDuration, update))
                    {
                        // Action just started - setup targets and users
                        // Only show targets if Xiong is evil
                        update[UserAction.dictUserActionTargets] = (isEvil && xiongAlive.Count > 0) ? alivePlayers : new List<int>();
                        update[UserAction.dictUserActionUsers] = xiong;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.Xiong_Act;
                        update[UserAction.dictUserActionRole] = Name;
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        // Action in progress - check for early completion
                        (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, xiongAlive, update);
                        if (inputValid)
                        {
                            var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost, -1);
                            if (targets.Count > 0 && targets[0] > 0 && isEvil)
                            {
                                // Store the linked target as player property
                                LangRenSha.SetPlayerProperty(game, xiongPlayer, dictXiongLink, targets[0], update);

                                UserAction.EndUserAction(game, update, true);
                                LangRenSha.AdvanceAction(game, update);
                                return GameActionResult.Restart;
                            }
                        }
                    }
                }
                return GameActionResult.NotExecuted;
            }

            // Action 230: Xiong bark check - runs after attack resolution when AboutToDie is set
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == (int)ActionConstant.Xiong_BarkCheck)
            {
                var xiongAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
                var xiongPlayer = xiongAlive.Count > 0 ? xiongAlive[0] : 0;

                // Check for barking (accounts for AboutToDie)
                CheckAndSetBark(game, xiongPlayer, update);

                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }

            return GameActionResult.NotExecuted;
        }

        /// <summary>
        /// Check if Xiong should bark and set the bark flag.
        /// Xiong barks if:
        /// 1. Xiong is alive
        /// 2. Xiong is NOT evil (alliance != 2)
        /// 3. Either left or right alive neighbor is evil (alliance = 2)
        /// </summary>
        private void CheckAndSetBark(Game game, int xiongPlayer, Dictionary<string, object> update)
        {
            if (xiongPlayer <= 0)
            {
                update[dictXiongBark] = 2;
                return;
            }

            // Check if Xiong is alive
            var isAlive = LangRenSha.GetPlayerProperty(game, xiongPlayer, LangRenSha.dictAlive, 0) == 1;
            if (!isAlive)
            {
                update[dictXiongBark] = 2;
                return;
            }

            var aboutToDie = Game.GetGameDictionaryProperty(game, LangRenSha.dictAboutToDie, new List<int>());
            if (aboutToDie.Contains(xiongPlayer))
            {
                // Xiong is about to die, doesn't bark
                update[dictXiongBark] = 2;
                return;
            }

            // Check if Xiong is evil
            var xiongAlliance = LangRenSha.GetPlayerProperty(game, xiongPlayer, LangRenSha.dictPlayerAlliance, 1);
            if (xiongAlliance == 2)
            {
                // Xiong is evil, doesn't bark
                update[dictXiongBark] = 2;
                return;
            }

            // Get all alive players sorted by player number
            var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
            alivePlayers.Sort();


            // Find Xiong's position in the alive players circle
            int xiongIndex = alivePlayers.IndexOf(xiongPlayer);
            if (xiongIndex < 0)
            {
                update[dictXiongBark] = 2;
                return;
            }

            // Get left and right neighbors (circular)
            int leftIndex = (xiongIndex - 1 + alivePlayers.Count) % alivePlayers.Count;
            int rightIndex = (xiongIndex + 1) % alivePlayers.Count;

            int leftNeighbor = alivePlayers[leftIndex];
            int rightNeighbor = alivePlayers[rightIndex];

            // Check if either neighbor is evil
            var leftAlliance = LangRenSha.GetPlayerProperty(game, leftNeighbor, LangRenSha.dictPlayerAlliance, 1);
            var rightAlliance = LangRenSha.GetPlayerProperty(game, rightNeighbor, LangRenSha.dictPlayerAlliance, 1);

            bool shouldBark = (leftAlliance == 2 || rightAlliance == 2);
            update[dictXiongBark] = shouldBark ? 1 : 2;
        }

        /// <summary>
        /// Death handler for Xiong - when Xiong dies, the linked player also dies.
        /// The linked player cannot use death skills (e.g., hunter cannot shoot).
        /// </summary>
        /// <param name="game">The game instance</param>
        /// <param name="deadPlayer">The player who just died</param>
        /// <param name="update">The update dictionary</param>
        public static (bool, GameActionResult) HandleXiongDeathSkill(Game game, int deadPlayer, Dictionary<string, object> update)
        {
            // Check if the dead player is Xiong
            var isXiong = LangRenSha.GetPlayerProperty(game, deadPlayer, LangRenSha.dictRole, "") == "Xiong";
            if (!isXiong)
            {
                return (false, GameActionResult.NotExecuted);
            }

            // Get the linked target
            var linkedTarget = LangRenSha.GetPlayerProperty(game, deadPlayer, dictXiongLink, 0);
            if (linkedTarget <= 0)
            {
                return (false, GameActionResult.NotExecuted);
            }

            // Check if linked target is still alive
            var targetAlive = LangRenSha.GetPlayerProperty(game, linkedTarget, LangRenSha.dictAlive, 0) == 1;
            if (!targetAlive)
            {
                return (false, GameActionResult.NotExecuted);
            }

            // Check if linked target is already about to die
            var aboutToDie = Game.GetGameDictionaryProperty(game, LangRenSha.dictAboutToDie, new List<int>());
            if (aboutToDie.Contains(linkedTarget))
            {
                return (false, GameActionResult.NotExecuted);
            }

            // Mark the linked target as about to die
            LangRenSha.MarkPlayerAboutToDie(game, linkedTarget, update);

            // Disable death skills for the linked target (e.g., hunter cannot shoot)
            LangRenSha.SetPlayerProperty(game, linkedTarget, LieRen.dictHuntingDisabled, 1, update);

            var currentAboutToDie = Game.GetGameDictionaryProperty(game, LangRenSha.dictAboutToDie, new List<int>());

            var skillsProcessed = Game.GetGameDictionaryProperty(game, LangRenSha.dictDeadSkillsProcessed, new List<int>());
            if (!skillsProcessed.Contains(deadPlayer))
            {
                skillsProcessed.Add(deadPlayer);
                update[LangRenSha.dictDeadSkillsProcessed] = skillsProcessed;
            }

            // Set up interrupt to go through death handling (state 97)
            var currentInterrupt = Game.GetGameDictionaryProperty(game, LangRenSha.dictInterrupt, new Dictionary<string, object>());
            var currentDeadPlayers = Game.GetGameDictionaryProperty(game, LangRenSha.dictDeadPlayerAction, new List<int>());

            var newInterrupt = new Dictionary<string, object>();
            newInterrupt[LangRenSha.dictSpeak] = 98; // Return to continue processing dead player skills
            newInterrupt[LangRenSha.dictInterrupt] = currentInterrupt;

            // Save death-related fields for restoration
            newInterrupt[LangRenSha.dictDeadPlayerAction] = currentDeadPlayers;
            newInterrupt[LangRenSha.dictDeadSkillsProcessed] = skillsProcessed;
            newInterrupt[LangRenSha.dictAboutToDie] = currentAboutToDie;

            update[LangRenSha.dictInterrupt] = newInterrupt;
            update[LangRenSha.dictSpeak] = 97;
            UserAction.EndUserAction(game, update, true);


            return (true, GameActionResult.Restart);
        }

    }
}
