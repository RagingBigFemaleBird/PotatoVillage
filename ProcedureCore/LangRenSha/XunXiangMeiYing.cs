using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcedureCore.LangRenSha
{
    /// <summary>
    /// XunXiangMeiYing (寻香魅影 - Phantom of Fragrance).
    /// A LangRen succession-2 role (acts as a fallback wolf if all LangRen and
    /// succession-1 wolves are dead). Acts before JiaMian.
    ///
    /// At night:
    ///   * Knows the seat number of ONE randomly chosen LangRen teammate
    ///     (informed via the action UserInfo, similar to DaMao).
    ///   * Selects two players (self allowed, no other restriction) and links
    ///     them together. If either dies in any way, the other dies too with
    ///     death skills (e.g. hunter shoot) disabled.
    ///
    /// The bidirectional link reuses <see cref="GhostBride.dictLinkedTo"/> so
    /// that the existing chain-death handling in <c>LangRenSha.ChainKill</c>
    /// (which already walks <c>dictLinkedTo</c> and disables hunting on the
    /// linked target) does not need any modification.
    /// </summary>
    public class XunXiangMeiYing : Role
    {
        private static Dictionary<string, object> roleDict = new()
        {
            { YuYanJia.dictYuYanJiaResult, 2 },
            { LangRenSha.dictPlayerAlliance, 2 },
            { LangRen.dictSuceession, 2 },
            { LangRenSha.dictPlayerFaction, LangRenSha.PlayerFaction.Evil },
        };

        private static List<int> actionOrders = new()
        {
            (int)ActionConstant.XunXiangMeiYing_OpenEyes,
            (int)ActionConstant.XunXiangMeiYing_Act,
            (int)ActionConstant.XunXiangMeiYing_CloseEyes,
        };

        public XunXiangMeiYing() { }

        public Dictionary<string, object> RoleDict => roleDict;
        public string Name => "XunXiangMeiYing";
        public int Version => 1;
        public List<int> ActionOrders => actionOrders;
        public int ActionDuration => 20;

        public static string dictLinkUsed = "xunxiangmeiying_link_used";

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

            // Open/close eyes announcer
            if (LangRenSha.AnnouncerAction(game, update, false, ActionOrders[0], ActionOrders[2], 50, 51, Name, 4) == GameActionResult.Restart)
            {
                return GameActionResult.Restart;
            }

            // Act phase: pick two players to bidirectionally link.
            if (Game.GetGameDictionaryProperty(game, LangRenSha.dictAction, 0) == ActionOrders[1])
            {
                var xxmy = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
                var xxmyAlive = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name && (int)x[LangRenSha.dictAlive] == 1);
                var xxmyPlayer = xxmyAlive.Count > 0 ? xxmyAlive[0] : 0;
                var langRenTeammates = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == "LangRen");
                var linkUsed = xxmyAlive.Count > 0 ? LangRenSha.GetPlayerProperty(game, xxmyPlayer, dictLinkUsed, 0) == 1 : false;

                var actionDuration = Game.GetGameDictionaryProperty(game, LangRenSha.dictDurationPlayerReact, ActionDuration);
                if (xxmyAlive.Count == 0 || linkUsed)
                {
                    actionDuration = new Random().Next(3, 6);
                }

                // Self is allowed - no restriction.
                var alivePlayers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);

                if (UserAction.EndUserAction(game, update))
                {
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, actionDuration, update))
                    {
                        update[UserAction.dictUserActionTargets] = xxmyAlive.Count > 0 ? alivePlayers : new List<int>();
                        update[UserAction.dictUserActionUsers] = xxmy;
                        update[UserAction.dictUserActionTargetsCount] = 2;
                        update[UserAction.dictUserActionTargetsHint] = (int)HintConstant.XunXiangMeiYing_Act;
                        update[UserAction.dictUserActionRole] = Name;

                        // Pick a random LangRen teammate to reveal (only one).
                        var info = "";
                        if (langRenTeammates.Count > 0)
                        {
                            var pick = langRenTeammates[new Random().Next(langRenTeammates.Count)];
                            info = pick.ToString();
                        }
                        update[UserAction.dictUserActionInfo] = info;
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        (var inputValid, var input, var input_others) = UserAction.GetUserResponse(game, true, linkUsed ? new List<int>() : xxmyAlive, update);
                        if (inputValid)
                        {
                            // Single user, so Input mode returns the targets list as-is.
                            var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.Input, -1);
                            var picked = targets.Where(t => t > 0).Distinct().Take(2).ToList();
                            if (picked.Count == 2)
                            {
                                // Bidirectional link via the already-supported GhostBride.dictLinkedTo
                                // key. ChainKill already chases this and disables hunting on the
                                // linked target, so no death-handling changes are needed.
                                LangRenSha.SetPlayerProperty(game, picked[0], GhostBride.dictLinkedTo, picked[1], update);
                                LangRenSha.SetPlayerProperty(game, picked[1], GhostBride.dictLinkedTo, picked[0], update);
                                // Tag both linked players with this XXMY so that when the link
                                // triggers a chain death, HandleLinkedDeathSkill can flip our
                                // dictLinkUsed flag on the originating XXMY (one-shot skill).
                                LangRenSha.SetPlayerProperty(game, picked[0], GhostBride.dictLinkedFrom, xxmyPlayer, update);
                                LangRenSha.SetPlayerProperty(game, picked[1], GhostBride.dictLinkedFrom, xxmyPlayer, update);
                                UserAction.EndUserAction(game, update, true);
                                LangRenSha.AdvanceAction(game, update);
                                return GameActionResult.Restart;
                            }
                        }
                    }
                }
                return GameActionResult.NotExecuted;
            }

            return GameActionResult.NotExecuted;
        }
    }
}
