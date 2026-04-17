using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProcedureCore.LangRenSha
{
    /// <summary>
    /// DingXuWangZi (定序王子) - The Sequencing Prince.
    /// Civilian-side role with no night action. After the first voteout result
    /// is displayed, may reveal itself during the reveal phase. When revealed,
    /// a skill-use announcement is broadcast and the day jumps back to the
    /// voting stage (re-vote).
    /// </summary>
    public class DingXuWangZi : Role
    {
        // Per-player property: 0/absent = not yet revealed, 1 = already revealed
        public static string dictRevealed = "dingxuwangzi_revealed";

        private static Dictionary<string, object> roleDict = new()
        {
            { YuYanJia.dictYuYanJiaResult, 1 },
            { LangRenSha.dictPlayerAlliance, 1 },
            { LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.God },
        };

        private static List<int> actionOrders = new();

        public DingXuWangZi()
        {
        }

        public Dictionary<string, object> RoleDict => roleDict;
        public string Name => "DingXuWangZi";
        public int Version => 1;
        public List<int> ActionOrders => actionOrders;
        public int ActionDuration => -1;

        /// <summary>
        /// Reveal handler invoked via LangRenSha.InterruptHandlers during the
        /// VotedOutReveal phase. When an alive DingXuWangZi reveals (target -10
        /// from the standalone Reveal button, or -100 from the hint 113 button),
        /// fire a SkillUseAnnouncement and route the day flow back to Vote1
        /// (re-vote).
        /// </summary>
        public static GameActionResult RevealSelf(Game game, int player, List<int> targets, Dictionary<string, object> update)
        {
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictSpeak, 0) != (int)SpeakConstant.VotedOutReveal)
            {
                return GameActionResult.NotExecuted;
            }
            if (!targets.Contains(-10))
            {
                return GameActionResult.NotExecuted;
            }

            var alive = LangRenSha.GetPlayers(game, x =>
                (string)x[LangRenSha.dictRole] == "DingXuWangZi" &&
                (int)x[LangRenSha.dictAlive] == 1 &&
                (!x.ContainsKey(dictRevealed) || (int)x[dictRevealed] == 0));
            if (!alive.Contains(player))
            {
                return GameActionResult.NotExecuted;
            }

            // Mark this player as revealed so they can't trigger again.
            LangRenSha.SetPlayerProperty(game, player, dictRevealed, 1, update);

            // Clear the prior vote so the re-vote starts fresh.
            update[LangRenSha.dictVoteOut] = new List<int>();
            update[LangRenSha.dictVoteInfo] = "";

            // After the skill-use announcement, jump back to Vote1 (re-vote).
            var interrupted = new Dictionary<string, object>();
            interrupted[LangRenSha.dictSpeak] = (int)SpeakConstant.Vote1;

            update[LangRenSha.dictSkillUseFrom] = player;
            update[LangRenSha.dictSkillUseTo] = new List<int>();
            update[LangRenSha.dictSkillUse] = "DingXuWangZi_Reveal";
            update[LangRenSha.dictSkillUseResult] = "2";

            update[LangRenSha.dictInterrupt] = interrupted;
            update[LangRenSha.dictSpeak] = (int)SpeakConstant.SkillUseAnnouncement;

            UserAction.EndUserAction(game, update, true);
            return GameActionResult.Restart;
        }

        public GameActionResult GenerateStateDiff(Game game, Dictionary<string, object> update)
        {
            return GameActionResult.NotExecuted;
        }
    }
}
