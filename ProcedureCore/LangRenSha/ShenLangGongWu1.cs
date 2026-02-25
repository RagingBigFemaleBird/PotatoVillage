using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcedureCore.LangRenSha
{
    /// <summary>
    /// ShenLangGongWu1 (神狼共舞1 - God Wolf Dance 1) role
    /// A special game mechanic role that on day 0, randomly picks a God faction player 
    /// and changes their allegiance to evil.
    /// </summary>
    public class ShenLangGongWu1 : Role
    {
        private static Dictionary<string, object> roleDict = new()
        {
            { YuYanJia.dictYuYanJiaResult, 1 },
            { LangRenSha.dictPlayerAlliance, 1 },
            { LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.ThirdParty },
        };

        private static List<int> actionOrders = new() { 1 };

        public ShenLangGongWu1()
        {
        }

        public Dictionary<string, object> RoleDict
        {
            get { return roleDict; }
        }

        public string Name
        {
            get { return "ShenLangGongWu1"; }
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
            get { return 3; }
        }

        public GameActionResult GenerateStateDiff(Game game, Dictionary<string, object> update)
        {
            if (game.StateSequenceNumber == 1)
            {
                var no = Game.GetGameDictionaryProperty(game, LangRenSha.dictNightOrders, new List<int>());
                no.AddRange(ActionOrders);
                update[LangRenSha.dictNightOrders] = no;
                return GameActionResult.Continue;
            }

            // Action 1: On day 0, randomly convert a God faction player to evil allegiance
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == ActionOrders[0])
            {
                var day = Game.GetGameDictionaryProperty(game, LangRenSha.dictDay, 0);

                // Only execute on day 0
                if (day == 0)
                {
                    // Find all God faction players
                    var godPlayers = LangRenSha.GetPlayers(game, x =>
                    {
                        var factionObj = x.ContainsKey(LangRenSha.dictPlayerFaction) ? x[LangRenSha.dictPlayerFaction] : null;
                        LangRenSha.PlayerFaction faction;

                        if (factionObj is LangRenSha.PlayerFaction pf)
                        {
                            faction = pf;
                        }
                        else if (factionObj is int factionInt)
                        {
                            faction = (LangRenSha.PlayerFaction)factionInt;
                        }
                        else
                        {
                            return false;
                        }

                        return (faction & LangRenSha.PlayerFaction.God) != 0;
                    });

                    // Randomly pick one God player and change their allegiance to evil
                    if (godPlayers.Count > 0)
                    {
                        var randomIndex = game.GetRandomNumber() % godPlayers.Count;
                        var targetPlayer = godPlayers[randomIndex];

                        // Change allegiance to evil (2)
                        LangRenSha.SetPlayerProperty(game, targetPlayer, LangRenSha.dictPlayerAlliance, 2, update);
                        LangRenSha.SetPlayerProperty(game, targetPlayer, YuYanJia.dictYuYanJiaResult, 2, update);
                        LangRenSha.SetPlayerProperty(game, targetPlayer, LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.Evil, update);
                        game.Log($"ShenLangGongWu1: Player {targetPlayer} allegiance changed to evil");
                    }
                }

                // Advance to next action
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }

            return GameActionResult.NotExecuted;
        }
    }
}
