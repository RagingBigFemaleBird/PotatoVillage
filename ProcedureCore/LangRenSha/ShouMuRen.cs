using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProcedureCore.LangRenSha
{
    /// <summary>
    /// ShouMuRen (守墓人 - Gravekeeper) - A god role that receives info about voted out players.
    /// Day 1+: Wakes up to receive info on whether the person voted out the day before is good or evil.
    /// No info given if no person was voted out.
    /// </summary>
    public class ShouMuRen : Role
    {
        private static Dictionary<string, object> roleDict = new()
        {
            { YuYanJia.dictYuYanJiaResult, 1 },
            { LangRenSha.dictPlayerAlliance, 1 },
            { LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.God },
        };

        private static List<int> actionOrders = new()
        {
            (int)ActionConstant.ShouMuRen_OpenEyes,
            (int)ActionConstant.ShouMuRen_Info,
            (int)ActionConstant.ShouMuRen_CloseEyes
        };

        public ShouMuRen()
        {
        }

        public Dictionary<string, object> RoleDict => roleDict;

        public string Name => "ShouMuRen";

        public int Version => 1;

        public List<int> ActionOrders => actionOrders;

        public int ActionDuration => 10;

        // Dictionary key for tracking voted out player from the previous day
        public static string dictLastVotedOutPlayer = "last_voted_out_player";

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

            var day = Game.GetGameDictionaryProperty(game, LangRenSha.dictDay, 0);

            // Announcer actions for open/close eyes (uses generic 50/51 hints)
            // Skip on Day 0 as there's no voted out player yet
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == ActionOrders[0] ||
                Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == ActionOrders[2])
            {
                if (day == 0)
                {
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                if (LangRenSha.AnnouncerAction(game, update, false, ActionOrders[0], ActionOrders[2], 50, 51, Name, 4) == GameActionResult.Restart)
                {
                    return GameActionResult.Restart;
                }
            }

            // ShouMuRen Info - show whether voted out player was good or evil
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == ActionOrders[1])
            {
                // Skip on Day 0 - no voted out player yet
                if (day == 0)
                {
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                return HandleShouMuRenInfo(game, update);
            }

            return GameActionResult.NotExecuted;
        }

        private GameActionResult HandleShouMuRenInfo(Game game, Dictionary<string, object> update)
        {
            var shouMuRen = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
            var shouMuRenAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);

            var actionDuration = ActionDuration;
            if (shouMuRenAlive.Count == 0)
            {
                actionDuration = new Random().Next(3, 6);
            }

            // Get the voted out player from the previous day
            var votedOutPlayer = Game.GetGameDictionaryProperty(game, dictLastVotedOutPlayer, 0);

            // Determine info to show
            string infoToShow = "";
            if (votedOutPlayer > 0 && shouMuRenAlive.Count > 0)
            {
                // Check alliance of the voted out player (1 = good, 2 = evil)
                var alliance = LangRenSha.GetPlayerProperty(game, votedOutPlayer, LangRenSha.dictPlayerAlliance, 1);
                infoToShow = alliance == 2 ? "evil" : "good";
            }

            if (UserAction.EndUserAction(game, update))
            {
                LangRenSha.AdvanceAction(game, update);
                return GameActionResult.Restart;
            }
            else
            {
                if (UserAction.StartUserAction(game, actionDuration, update))
                {
                    update[UserAction.dictUserActionTargets] = new List<int>();
                    update[UserAction.dictUserActionUsers] = shouMuRen;
                    update[UserAction.dictUserActionTargetsCount] = 0;
                    update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.ShouMuRen_Info;
                    update[UserAction.dictUserActionInfo] = infoToShow;
                    update[UserAction.dictUserActionRole] = Name;
                    return GameActionResult.Restart;
                }
            }
            return GameActionResult.NotExecuted;
        }
    }
}
