using Microsoft.Maui.Controls;
using PotatoVillage.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace PotatoVillage
{
    public partial class GameView : ContentPage
    {
        private HubConnectionManager? connectionManager;
        private int playerId;
        private int gameId;
        private bool isOwner;
        private HashSet<int> selectedTargets = new();
        private CancellationTokenSource? countdownCts;
        private CancellationTokenSource? confirmBlinkCts; // For confirm button blinking animation
        private bool announcerEnabled = false; // Client-only setting, default off
        private bool warningBeepPlayed = false; // Track if 15-second warning beep was played
        private int serverTimeOffset = 0; // Offset between server and client clocks (server_time - client_time)
        private int lastServerTime = 0;

        // Track currently displayed target selection to avoid flickering rebuilds
        private int currentDisplayedDeadline = 0;
        private int currentDisplayedHint = -1;

        // Monotonic counter incremented every time the target-selection UI is rebuilt.
        // Each Button.Clicked closure captures the generation that was current when it
        // was created. OnTargetSelected ignores clicks whose captured generation no
        // longer matches, which prevents in-flight UI events from a previous round
        // (whose buttons have already been removed) from mutating selectedTargets and
        // causing the wrong targets to be sent to the server.
        private int displayGeneration = 0;

        // Action history tracking (client-side only)
        private List<string> actionHistory = new();
        private bool actionHistoryVisible = false;

        // User action dictionary keys
        private const string DictUserAction = "user_action";
        private const string DictUserActionUsers = "user_users";
        private const string DictUserActionTargets = "user_targets";
        private const string DictUserActionTargetsCount = "user_targets_count";
        private const string DictUserActionTargetsHint = "user_targets_hint";
        private const string DictUserActionRole = "user_role";
        private const string DictUserActionInfo = "user_info";
        private const string DictUserActionInfo2 = "user_info2";
        private const string DictUserActionInfo3 = "user_info3";
        private const string DictUserActionResponse = "user_response";
        private const string DictUserActionPauseStart = "user_pause_start";
        private const string DictServerTime = "server_time";
        private const string DictSpeaker = "speaker";

        // Property to check if announcer sounds should play
        public bool IsAnnouncerEnabled => announcerEnabled;

        // Special targets dictionary - nested by hint, then by target ID
        // First level: indexed by target hints
        // Second level: indexed by special target values (<= 0)
        private static readonly Dictionary<int, Dictionary<int, string>> SpecialTargets = new Dictionary<int, Dictionary<int, string>>()
        {
            { 1, new Dictionary<int, string> { { -100, "DoNotAttack"} } },
            { 3, new Dictionary<int, string> { { 0, "JiuRen" }, { -100, "DoNotUse"} } },
            { 18, new Dictionary<int, string> { { -100, "DoNotUse"} } },
            { 20, new Dictionary<int, string> { { -100, "DoNotUse"} } },
            { 24, new Dictionary<int, string> { { -1, "yes" }, { 0, "no" } } },
            { 26, new Dictionary<int, string> { { -1, "yes" }, { 0, "no" } } },
            { 27, new Dictionary<int, string> { { -100, "DoNotUse"} } },
            { 31, new Dictionary<int, string> { { -100, "DoNotUse"} } },
            { 32, new Dictionary<int, string> { { -100, "DoNotUse"} } },
            { 63, new Dictionary<int, string> { { -100, "DoNotUse"} } },
            { 70, new Dictionary<int, string> { { -100, "DoNotUse"} } },
            { 71, new Dictionary<int, string> { { -100, "DoNotUse"} } },
            { 80, new Dictionary<int, string> { { -100, "DoNotUse"} } },
            { 81, new Dictionary<int, string> { { -100, "DoNotUse"} } },
            { 82, new Dictionary<int, string> { { -100, "confirm"} } },
            { 100, new Dictionary<int, string> { { -1, "Volunteer" }, { 0, "Abstain" } } },
            { 102, new Dictionary<int, string> { { -1, "Done speaking" }, { 0, "Pause game" }, { -100, "OwnerVoteOverride" } } },
            { 104, new Dictionary<int, string> { { -1, "Done speaking" }, { 0, "Pause game" }, { -2, "Withdraw" }, { -101, "OwnerSheriffOverride" } } },
            { 105, new Dictionary<int, string> { { -1, "Done speaking" }, { 0, "Pause game" } } },
            { 106, new Dictionary<int, string> { { 0, "NoSheriff" } } },
            { 107, new Dictionary<int, string> { { -2, "Withdraw" }, { -101, "OwnerSheriffOverride" } } },
            { 112, new Dictionary<int, string> { { 0, "SkipVote" } } },
            { 113, new Dictionary<int, string> { { -100, "DingXuWangZi_Reveal" } } },
            { 151, new Dictionary<int, string> { { -100, "DoNotUse"} } },
            { 153, new Dictionary<int, string> { { -2, "Left" }, { -1, "Right"} } },
            { 98, new Dictionary<int, string> { { -1, "Kill" }, { -100, "DoNotKill"} } },
            { 1000, new Dictionary<int, string> { { 0, "acknowledge"} }  },
        };

        // Handlers for user actions (DisplayTargetSelection path)
        private static readonly Dictionary<int, Func<string, string, string>> UserInfoHints = new()
        {
            { 1, LangRenInfoHandler },
            { 3, NvWuInfoHandler },
            { 5, LangRenSuccessionHandler },
            { 10, ThiefPickRoleHandler },
            { 12, LangRenSuccessionHandler },
            { 14, TongLingShiResultHandler },
            { 29, ShiXiangGuiChaYanHandler },
            { 62, LangRenSuccessionHandler },
            { 6, JiaMianInfoHandler },
            { 7, YuYanJiaInfoHandler },
            { 34, YuYanJiaInfoHandler },
            { 35, XunXiangMeiYingInfoHandler },
            { 76, GiftedPoisonHandler },
            { 79, YingZiInfoHandler },
            { 82, FuChouZheAllianceInfoHandler },
            { 84, GhostBrideGroomCheckHandler },
            { 85, GhostBrideCoupleAttackHandler },
            { 86, GhostBrideWitnessCheckHandler },
            { 87, GhostBrideWitnessInfoHandler },
            { 92, GhostBrideCoupleAttackHandler },
            { 94, TufuAttackHandler },
            { 96, ShouMuRenInfoHandler },
            { 98, AwkSheMengRenJudgeActHandler },
            { 17, JiXieLangInfoHandler },
            { 18, JiXieLangActAgainHandler },
            { 19, JiXieLangActAgainInfoHandler },
            { 21, MeiYangYangInfoHandler },
            { 22, MeiYangYangCivilianInfoHandler },
            { 24, MeiYangYangSacrificeSelfHandler },
            { 25, MeiYangYangCivilianMayActHandler },
            { 26, MeiYangYangCivilianSacrificeHandler },
            { 30, ShiXiangGuiResultHandler },
            { 31, MoShuShiSwapHandler },
            { 32, GuiShuShiSwapHandler },
            { 104, SheriffSpeechHandler },
            { 105, SheriffPKHandler },
            { 151, LieRenInfoHandler },
            { 154, VoteResultInfoHandler },
            { 1000, CheckRoleInfoHandler },
            { 1003, GameWinnerHandler },
        };

        // Handlers for announcements (DisplayCurrentlyActing path, user == -1)
        private static readonly Dictionary<int, Func<string, string, string>> AnnouncementInfoHandlers = new()
        {
            { 77, MengMianRenDeathHandler },
            { 152, DeathAnnouncementHandler },
            { 1000, CheckPrivateAnnouncementHandler },
            { 1003, GameWinnerHandler },
            { 1100, SkillUseAnnouncementHandler },
        };

        private static string CheckPrivateAnnouncementHandler(string userInfo, string userInfo2)
        {
            var localization = LocalizationManager.Instance;
            // If userInfo contains specific players (comma-separated), use check_private_players
            // Otherwise use the default check_private message
            if (!string.IsNullOrEmpty(userInfo))
            {
                var txt = localization.GetString("check_private_players", "{0} please check in private");
                return txt.Replace("{0}", userInfo);
            }
            return localization.GetString("check_private", "Everyone please check in private");
        }

        private static string SkillUseAnnouncementHandler(string userInfo, string userInfo2)
        {
            // userInfo format: "from;to1,to2,...;skill;result"
            // result is "0" (failed) or "1" (succeeded)
            var parts = userInfo.Split(';');
            if (parts.Length < 4)
                return userInfo;

            var from = parts[0];
            var to = parts[1];
            var skill = parts[2];
            var result = parts[3];

            // Parse result: "0" = Failed, "1" = Succeeded
            var resultText = result == "1"
                ? LocalizationManager.Instance.GetString("skill_succeeded", "Succeeded")
                : LocalizationManager.Instance.GetString("skill_failed", "Failed");

            // Translate skill name
            var skillName = LocalizationManager.Instance.GetString(skill, skill);

            // Format: "Player {from} used {skill} on {to}: {result}"
            var txt = LocalizationManager.Instance.GetString("skill_use_announcement", "{0} {1} {2}: {3}");
            return txt.Replace("{0}", from).Replace("{1}", skillName).Replace("{2}", to).Replace("{3}", resultText);
        }

        private static string MengMianRenDeathHandler(string userInfo, string userInfo2)
        {
            // userInfo contains the player ID
            var txt = LocalizationManager.Instance.GetString("mengmianren_death", "Player {0} has died from wounds");
            return txt.Replace("{0}", userInfo);
        }

        private static string LangRenInfoHandler(string userInfo, string userInfo2)
        {
            if (!string.IsNullOrEmpty(userInfo))
            {
                var str = LocalizationManager.Instance.GetString("langren_info", "Teammates are: {0}");
                return str.Replace("{0}", userInfo);
            }
            return userInfo;
        }

        private static string LieRenInfoHandler(string userInfo, string userInfo2)
        {
            if (userInfo == "1")
            {
                return LocalizationManager.Instance.GetString("lieren_can_shoot", "Can shoot if dead.");
            }
            else
            {
                return LocalizationManager.Instance.GetString("lieren_cannot_shoot", "Shooting disabled.");
            }
        }

        private static string ThiefPickRoleHandler(string userInfo, string userInfo2)
        {
            // userInfo contains comma-separated role names, displayed as buttons
            // Just return the instruction text
            return LocalizationManager.Instance.GetString("thief_pick_role", "Pick a role to become:");
        }

        private static string TongLingShiResultHandler(string userInfo, string userInfo2)
        {
            // userInfo contains the role name - translate it
            var roleName = LocalizationManager.Instance.GetString(userInfo, userInfo);
            var txt = LocalizationManager.Instance.GetString("tonglingshi_result_info", "Prophet result: {0}");
            return txt.Replace("{0}", roleName);
        }

        private static string ShiXiangGuiChaYanHandler(string userInfo, string userInfo2)
        {
            // userInfo is "Succession" if can attack, empty otherwise
            if (userInfo == "Succession")
                return LocalizationManager.Instance.GetString("langren_succession_yes", "Can attack.");
            return LocalizationManager.Instance.GetString("langren_succession_no", "Cannot attack yet.");
        }

        private static string ShiXiangGuiResultHandler(string userInfo, string userInfo2)
        {
            // userInfo contains the role name - translate it
            var roleName = LocalizationManager.Instance.GetString(userInfo, userInfo);
            var txt = LocalizationManager.Instance.GetString("shixianggui_result_info", "ShiXiangGui result: {0}");
            return txt.Replace("{0}", roleName);
        }

        private static string GiftedPoisonHandler(string userInfo, string userInfo2)
        {
            if (userInfo != "gifted")
            {
                return LocalizationManager.Instance.GetString("not_gifted_yet", "Not gifted yet");
            }
            return LocalizationManager.Instance.GetString("gifted_use_trap", "Gifted, use cat trap:");
        }

        private static string YingZiInfoHandler(string userInfo, string userInfo2)
        {
            // Check if YingZi became ThirdParty (shadowed FuChouZhe)
            if (userInfo == "thirdparty")
            {
                return LocalizationManager.Instance.GetString("yingzi_thirdparty", "You are third party, please open eyes on Revenger's turn");
            }

            // Legacy: userInfo contains the new role name of the shadowed target
            var roleName = LocalizationManager.Instance.GetString(userInfo, userInfo);
            var txt = LocalizationManager.Instance.GetString("yingzi_role_changed", "Shadowed target's role changed to: {0}");
            return txt.Replace("{0}", roleName);
        }

        private static string FuChouZheAllianceInfoHandler(string userInfo, string userInfo2)
        {
            // userInfo is "good" or "evil"
            var allianceName = LocalizationManager.Instance.GetString(userInfo, userInfo);
            var txt = LocalizationManager.Instance.GetString("fuchouzhe_alliance_info", "Your alliance: {0}");
            return txt.Replace("{0}", allianceName);
        }

        private static string GhostBrideGroomCheckHandler(string userInfo, string userInfo2)
        {
            if (userInfo == "yes")
                return LocalizationManager.Instance.GetString("ghostbride_groom_linked", "You are linked to the Ghost Bride.");
            return LocalizationManager.Instance.GetString("ghostbride_groom_not_linked", "You are not linked.");
        }

        private static string GhostBrideWitnessCheckHandler(string userInfo, string userInfo2)
        {
            if (userInfo == "yes")
                return LocalizationManager.Instance.GetString("ghostbride_is_witness", "You are the witness.");
            return LocalizationManager.Instance.GetString("ghostbride_not_witness", "You are not witness.");
        }

        private static string GhostBrideWitnessInfoHandler(string userInfo, string userInfo2)
        {
            var localization = LocalizationManager.Instance;

            if (userInfo == "Succession")
                return localization.GetString("langren_succession_yes", "Can attack.");

            if (string.IsNullOrEmpty(userInfo))
                return localization.GetString("langren_succession_no", "Cannot attack yet.");

            var txt = localization.GetString("ghostbride_witness_info", "Bride and groom are: {0}");
            return txt.Replace("{0}", userInfo);
        }

        private static string GhostBrideCoupleAttackHandler(string userInfo, string userInfo2)
        {
            if (userInfo == "Succession")
                return LocalizationManager.Instance.GetString("langren_succession_yes", "Can attack.");
            return LocalizationManager.Instance.GetString("langren_succession_no", "Cannot attack yet.");
        }

        private static string MeiYangYangInfoHandler(string userInfo, string userInfo2)
        {
            string txt = "";
            if (userInfo2 == "yes")
                txt = LocalizationManager.Instance.GetString("hongtailang_info", "Your are poisoned and will die next night.");
            return txt + LocalizationManager.Instance.GetString("meiyangyang_cannot_sacrifice_self", "You cannot sacrifice yourself to save your sacrifice target.");
        }

        private static string MeiYangYangCivilianInfoHandler(string userInfo, string userInfo2)
        {
            string txt2 = "";
            if (userInfo2 == "yes")
                txt2 = LocalizationManager.Instance.GetString("hongtailang_info", "Your are poisoned and will die next night.");
            // userInfo contains the MeiYangYang player ID
            if (string.IsNullOrEmpty(userInfo))
                return txt2 + LocalizationManager.Instance.GetString("meiyangyang_cannot_sacrifice_self", "You cannot sacrifice yourself to save your sacrifice target.");
            var txt = LocalizationManager.Instance.GetString("meiyangyang_civilian_info", "MeiYangYang player is: {0}");
            return txt2 + txt.Replace("{0}", userInfo);
        }

        private static string MeiYangYangSacrificeSelfHandler(string userInfo, string userInfo2)
        {
            // userInfo contains the sacrifice target player ID who is about to die
            var txt = LocalizationManager.Instance.GetString("meiyangyang_sacrifice_self", "Your sacrifice target {0} is about to die. Sacrifice yourself to save them?");
            return txt.Replace("{0}", userInfo);
        }

        private static string MeiYangYangCivilianMayActHandler(string userInfo, string userInfo2)
        {
            // userInfo contains the sacrifice target player ID (the civilian being notified)
            if (userInfo == "yes")
            {
                return LocalizationManager.Instance.GetString("meiyangyang_civilian_may_act", "You may sacrifice for MeiYangYang later.");
            }
            else
            {
                return LocalizationManager.Instance.GetString("meiyangyang_civilian_may_not_act", "You cannot sacrifice for MeiYangYang later.");
            }
        }

        private static string MeiYangYangCivilianSacrificeHandler(string userInfo, string userInfo2)
        {
            // userInfo contains the MeiYangYang player ID who is about to die
            var txt = LocalizationManager.Instance.GetString("meiyangyang_civilian_sacrifice_for_meiyangyang", "MeiYangYang {0} is about to die. Sacrifice yourself to save them?");
            return txt.Replace("{0}", userInfo);
        }

        private static string GameWinnerHandler(string userInfo, string userInfo2)
        {
            // userInfo is "Good" or "Evil"
            var winnerName = LocalizationManager.Instance.GetString(userInfo, userInfo);
            var txt = LocalizationManager.Instance.GetString("game_winner", "{0} wins!");
            return txt.Replace("{0}", winnerName);
        }

        private static string XunXiangMeiYingInfoHandler(string userInfo, string userInfo2)
        {
            var localization = LocalizationManager.Instance;
            if (string.IsNullOrEmpty(userInfo))
                return "";
            var teammate = localization.GetString("xunxiangmeiying_teammate", "One of your wolf teammates: [c:green]{0}[/c]");
            return teammate.Replace("{0}", userInfo);
        }

        private static string LangRenSuccessionHandler(string userInfo, string userInfo2)
        {
            var parts = userInfo.Split(';');
            var successionInfo = parts.Length > 0 ? parts[0] : "";
            var teammatesInfo = parts.Length > 1 ? parts[1] : "";

            string ret = "";
            if (string.IsNullOrEmpty(successionInfo))
                ret += LocalizationManager.Instance.GetString("langren_succession_no", "Cannot yet attack");
            else
                ret += LocalizationManager.Instance.GetString("langren_succession_yes", "Can attack");

            if (!string.IsNullOrEmpty(teammatesInfo))
            {
                var tm = LocalizationManager.Instance.GetString("langren_info", "Teammates are: {0}");
                ret += ";" + tm.Replace("{0}", teammatesInfo);
            }
            return ret;
        }

        private static string TufuAttackHandler(string userInfo, string userInfo2)
        {
            if (userInfo == "Succession")
                return LocalizationManager.Instance.GetString("langren_succession_yes", "Can attack");
            else
                return LocalizationManager.Instance.GetString("langren_succession_no", "Cannot yet attack");
        }

        private static string ShouMuRenInfoHandler(string userInfo, string userInfo2)
        {
            var localization = LocalizationManager.Instance;
            // userInfo is "good", "evil", or empty
            if (string.IsNullOrEmpty(userInfo))
                return localization.GetString("shoumuren_info_none", "No one was voted out yesterday.");
            if (userInfo == "good")
                return localization.GetString("shoumuren_info_good", "The voted out player was good.");
            if (userInfo == "evil")
                return localization.GetString("shoumuren_info_evil", "The voted out player was evil.");
            return userInfo;
        }

        private static string AwkSheMengRenJudgeActHandler(string userInfo, string userInfo2)
        {
            var localization = LocalizationManager.Instance;
            // userInfo format: "target,acted" where acted is "1" (acted) or "0" (not acted)
            if (string.IsNullOrEmpty(userInfo))
                return localization.GetString("awkshemengren_judge_act", "Kill the guarded player?");

            var parts = userInfo.Split(',');
            if (parts.Length < 2)
                return localization.GetString("awkshemengren_judge_act", "Kill the guarded player?");

            var target = parts[0];
            var acted = parts[1] == "1";

            string infoText;
            if (acted)
            {
                var txt = localization.GetString("awkshemengren_judge_acted", "Player {0} acted during the night.");
                infoText = txt.Replace("{0}", target);
            }
            else
            {
                var txt = localization.GetString("awkshemengren_judge_not_acted", "Player {0} did not act during the night.");
                infoText = txt.Replace("{0}", target);
            }

            var actionText = localization.GetString("awkshemengren_judge_act", "Kill the guarded player?");
            return infoText + ";" + actionText;
        }

        private static string JiXieLangInfoHandler(string userInfo, string userInfo2)
        {
            // Day 0: userInfo is the mimicked role name. Day 1+: "1" = can attack, "0" = cannot
            var localization = LocalizationManager.Instance;
            if (string.IsNullOrEmpty(userInfo))
                return localization.GetString("jixielang_no_skill", "No skill learned");
            if (userInfo == "1")
                return localization.GetString("langren_succession_yes", "Can attack");
            if (userInfo == "0")
                return localization.GetString("langren_succession_no", "Cannot attack");
            // Role name (day 0)
            var roleName = localization.GetString(userInfo, userInfo);
            var txt = localization.GetString("jixielang_learned", "Learned: {0}");
            return txt.Replace("{0}", roleName);
        }

        private static string JiXieLangActAgainHandler(string userInfo, string userInfo2)
        {
            // userInfo is the mimicked role name
            if (string.IsNullOrEmpty(userInfo))
                return "";
            var localization = LocalizationManager.Instance;
            var roleName = localization.GetString(userInfo, userInfo);
            var txt = localization.GetString("jixielang_using", "Using: {0}");
            return txt.Replace("{0}", roleName);
        }

        private static string JiXieLangActAgainInfoHandler(string userInfo, string userInfo2)
        {
            // Formats:
            //   TongLingShi: role name string (e.g. "NvWu")
            //   LieRen: "LieRen,1" (can shoot) or "LieRen,0" (cannot shoot)
            //   Empty: no info to display
            if (string.IsNullOrEmpty(userInfo))
                return "";
            var localization = LocalizationManager.Instance;

            if (userInfo.StartsWith("LieRen,"))
            {
                var canShoot = userInfo.EndsWith(",1");
                return canShoot
                    ? localization.GetString("jixielang_lieren_can_shoot", "Can shoot on death")
                    : localization.GetString("jixielang_lieren_cannot_shoot", "Cannot shoot on death");
            }

            // TongLing result
            var roleName = localization.GetString(userInfo, userInfo);
            var txt = localization.GetString("jixielang_tongling_result", "TongLing: {0}");
            return txt.Replace("{0}", roleName);
        }

        private static string SheriffSpeechHandler(string userInfo, string userInfo2)
        {
            return LocalizationManager.Instance.GetString("sheriff_speech_info", "{0} volunteered.").Replace("{0}", userInfo);
        }
        private static string SheriffPKHandler(string userInfo, string userInfo2)
        {
            return LocalizationManager.Instance.GetString("sheriff_pk_info", "{0} PK.").Replace("{0}", userInfo);
        }

        private static string VoteResultInfoHandler(string userInfo, string userInfo2)
        {
            var txt = LocalizationManager.Instance.GetString("vote_result", "Vote result: {0}");
            return txt.Replace("{0}", userInfo);
        }

        private static string DeathAnnouncementHandler(string userInfo, string userInfo2)
        {
            var localization = LocalizationManager.Instance;

            // Parse the userInfo: "deadPlayers;xiongBark" or just "deadPlayers"
            var parts = userInfo.Split(';');
            var deadPlayersStr = parts.Length > 0 ? parts[0] : "";
            var xiongBarkStr = parts.Length > 1 ? parts[1] : "";

            string result;
            // Format death announcement
            if (string.IsNullOrEmpty(deadPlayersStr))
            {
                result = localization.GetString("death_announcement_none", "Last night no deaths.");
            }
            else
            {
                var txt = localization.GetString("death_announcement", "Last night death: {0}");
                result = txt.Replace("{0}", deadPlayersStr);
            }

            // Add Xiong bark info if provided
            if (!string.IsNullOrEmpty(xiongBarkStr))
            {
                if (xiongBarkStr == "1")
                {
                    // Xiong barked
                    result += ";" + localization.GetString("xiong_barked", "Bear barked!");
                }
                else if (xiongBarkStr == "2")
                {
                    // Xiong did not bark
                    result += ";" + localization.GetString("xiong_not_barked", "Bear did not bark.");
                }
            }

            return result;
        }

        private static string CheckRoleInfoHandler(string userInfo, string userInfo2)
        {
            var localization = LocalizationManager.Instance;

            // Parse comma-separated string: role,allegiance
            var parts = userInfo.Split(',');
            var role = parts.Length > 0 ? parts[0] : "";
            var allegiance = parts.Length > 1 ? parts[1] : "1";

            // Translate role name
            if (!string.IsNullOrEmpty(role))
            {
                role = localization.GetString(role);
            }

            // Translate allegiance (1 = good, 2 = evil)
            string allegianceText = localization.GetString("unknown");
            if (allegiance == "2")
                allegianceText = localization.GetString("evil");
            if (allegiance == "1")
                allegianceText = localization.GetString("good");

            var txt = localization.GetString("check_role_info", "Your role is {0}.");
            return txt.Replace("{0}", $"{role} ({allegianceText})");
        }

        private static string NvWuInfoHandler(string userInfo, string userInfo2)
        {
            if (string.IsNullOrEmpty(userInfo) || userInfo == "0")
                return LocalizationManager.Instance.GetString("nvwu_no_save", "Cannot view attack info.");
            var txt = LocalizationManager.Instance.GetString("nvwu_save", "Last night {0} was attacked.");
            return txt.Replace("{0}", userInfo);
        }

        private static string MoShuShiSwapHandler(string userInfo, string userInfo2)
        {
            var localization = LocalizationManager.Instance;
            if (string.IsNullOrWhiteSpace(userInfo))
            {
                return localization.GetString("moshushi_none_used", "No players selected yet.");
            }
            var txt = localization.GetString("moshushi_already_used", "Already used: {0}");
            return txt.Replace("{0}", userInfo);
        }

        private static string GuiShuShiSwapHandler(string userInfo, string userInfo2)
        {
            var localization = LocalizationManager.Instance;
            if (string.IsNullOrWhiteSpace(userInfo))
            {
                return localization.GetString("guishushi_none_used", "No players selected yet.");
            }
            var txt = localization.GetString("guishushi_already_used", "Already used: {0}");
            return txt.Replace("{0}", userInfo);
        }

        private static string YuYanJiaInfoHandler(string userInfo, string userInfo2)
        {
            int.TryParse(userInfo, out var result);
            var txt = LocalizationManager.Instance.GetString("yuyanjia_chayan_result", "Yuyanjia's Chayan result: {0}");
            if (result != 0)
            {
                var allegience = LocalizationManager.Instance.GetString(result == 1 ? "good" : "evil");
                return txt.Replace("{0}", allegience);
            }
            else
            {
                return txt.Replace("{0}", LocalizationManager.Instance.GetString(userInfo));
            }
        }

        private static string JiaMianInfoHandler(string userInfo, string userInfo2)
        {
            int.TryParse(userInfo, out var result);
            var txt = LocalizationManager.Instance.GetString("jiamian_info", "Your check result: {0}. Select target to flip");
            return txt.Replace("{0}", LocalizationManager.Instance.GetString("jiamian_info" + userInfo));
        }

        /// <summary>
        /// Parses text containing color codes and returns a FormattedString.
        /// Supported format: [c:ColorName]colored text[/c]
        /// Example: "Normal text [c:Red]red text[/c] more normal"
        /// Supported colors: Red, Green, Blue, Yellow, Orange, Purple, Gray, White, Black
        /// </summary>
        private static FormattedString ParseColoredText(string text)
        {
            var formattedString = new FormattedString();

            if (string.IsNullOrEmpty(text))
            {
                return formattedString;
            }

            // Pattern: [c:ColorName]text[/c]
            int currentIndex = 0;
            while (currentIndex < text.Length)
            {
                // Find the next color tag
                int tagStart = text.IndexOf("[c:", currentIndex);

                if (tagStart == -1)
                {
                    // No more color tags, add remaining text
                    if (currentIndex < text.Length)
                    {
                        formattedString.Spans.Add(new Span { Text = text.Substring(currentIndex), TextColor = Colors.Black });
                    }
                    break;
                }

                // Add text before the color tag
                if (tagStart > currentIndex)
                {
                    formattedString.Spans.Add(new Span { Text = text.Substring(currentIndex, tagStart - currentIndex), TextColor = Colors.Black });
                }

                // Find the end of the color name
                int colorEnd = text.IndexOf(']', tagStart);
                if (colorEnd == -1)
                {
                    // Malformed tag, add rest as plain text
                    formattedString.Spans.Add(new Span { Text = text.Substring(tagStart), TextColor = Colors.Black });
                    break;
                }

                // Extract color name
                string colorName = text.Substring(tagStart + 3, colorEnd - tagStart - 3);

                // Find the closing tag
                int closeTag = text.IndexOf("[/c]", colorEnd);
                if (closeTag == -1)
                {
                    // No closing tag, add rest as plain text
                    formattedString.Spans.Add(new Span { Text = text.Substring(tagStart), TextColor = Colors.Black });
                    break;
                }

                // Extract colored text
                string coloredText = text.Substring(colorEnd + 1, closeTag - colorEnd - 1);

                // Parse the color
                Color textColor = ParseColor(colorName);

                // Add the colored span
                formattedString.Spans.Add(new Span { Text = coloredText, TextColor = textColor });

                // Move past the closing tag
                currentIndex = closeTag + 4;
            }

            return formattedString;
        }

        /// <summary>
        /// Parses a color name to a Color object.
        /// </summary>
        private static Color ParseColor(string colorName)
        {
            return colorName.ToLowerInvariant() switch
            {
                "red" => Colors.Red,
                "green" => Colors.Green,
                "blue" => Colors.Blue,
                "yellow" => Colors.Yellow,
                "orange" => Colors.Orange,
                "purple" => Colors.Purple,
                "gray" or "grey" => Colors.Gray,
                "white" => Colors.White,
                "black" => Colors.Black,
                "cyan" => Colors.Cyan,
                "magenta" => Colors.Magenta,
                "pink" => Colors.Pink,
                "brown" => Colors.Brown,
                "lime" => Colors.Lime,
                "darkred" => Colors.DarkRed,
                "darkgreen" => Colors.DarkGreen,
                "darkblue" => Colors.DarkBlue,
                _ => Colors.Black // Default color
            };
        }

        /// <summary>
        /// Strips color codes from text, returning plain text for voiceover.
        /// </summary>
        private static string StripColorCodes(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Remove all [c:ColorName] and [/c] tags
            var result = System.Text.RegularExpressions.Regex.Replace(text, @"\[c:[^\]]+\]", "");
            result = result.Replace("[/c]", "");
            return result;
        }

        /// <summary>
        /// Sets the GameStatusLabel text, supporting color codes.
        /// </summary>
        private void SetGameStatusText(string text)
        {
            GameStatusLabel.FormattedText = ParseColoredText(text);
            UpdateGameStatusFontSize();
        }

        /// <summary>
        /// Sets the TargetInstructionLabel text, supporting color codes.
        /// </summary>
        private void SetTargetInstructionText(string text)
        {
            TargetInstructionLabel.FormattedText = ParseColoredText(text);
        }

        public GameView(HubConnectionManager connectionManager, int gameId, int playerId, bool isOwner = false)
        {
            InitializeComponent();
            this.connectionManager = connectionManager;
            this.gameId = gameId;
            this.playerId = playerId;
            this.isOwner = isOwner;
            PlayerIdLabel.Text = playerId.ToString();
            RevealBtn.Text = LocalizationManager.Instance.GetString("reveal");
            ConfirmButton.Text = LocalizationManager.Instance.GetString("confirm");

            // Set announcer to ON by default for game owner
            if (isOwner)
            {
                announcerEnabled = true;
                AnnouncerBtn.Text = "♪";
                AnnouncerBtn.BackgroundColor = Colors.Green;
                VoiceoverService.Instance.IsEnabled = true;
            }

            // Set dynamic font sizes based on screen size
            UpdateGameStatusFontSize();
            this.SizeChanged += OnPageSizeChanged;

            // Subscribe to game state updates
            if (connectionManager != null)
            {
                connectionManager.GameStateUpdated += UpdateGameStatus;
                connectionManager.GameEnded += OnGameEnded;
                connectionManager.SequenceMismatch += OnSequenceMismatch;
                UpdateGameStatus();
            }
        }

        private async void OnSequenceMismatch(string message)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await DisplayAlertAsync(
                    LocalizationManager.Instance.GetString("error"),
                    message,
                    LocalizationManager.Instance.GetString("yes"));
                await Navigation.PopToRootAsync();
            });
        }

        private async void OnGameEnded(string message)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await DisplayAlertAsync(
                    LocalizationManager.Instance.GetString("game_ended"),
                    message,
                    LocalizationManager.Instance.GetString("yes"));
                await Navigation.PopToRootAsync();
            });
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            // Lock to portrait when entering game view
            OrientationService.LockPortrait();

            // Subscribe to app lifecycle events for iOS background/foreground handling
            if (Window != null)
            {
                Window.Resumed += OnAppResumed;
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            // Unlock orientation when leaving game view
            OrientationService.LockPortrait();

            // Unsubscribe from app lifecycle events
            if (Window != null)
            {
                Window.Resumed -= OnAppResumed;
            }

            // Clean up event subscriptions
            if (connectionManager != null)
            {
                connectionManager.GameStateUpdated -= UpdateGameStatus;
                connectionManager.GameEnded -= OnGameEnded;
                connectionManager.SequenceMismatch -= OnSequenceMismatch;
            }

            this.SizeChanged -= OnPageSizeChanged;

            // Cancel any running countdown
            countdownCts?.Cancel();
            countdownCts = null;

            // Cancel confirm button blinking
            confirmBlinkCts?.Cancel();
            confirmBlinkCts = null;
        }

        /// <summary>
        /// Handles app resume from background (especially important for iOS).
        /// Checks connection state and reconnects if needed.
        /// </summary>
        private async void OnAppResumed(object? sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("GameView: App resumed from background");

            if (connectionManager == null)
                return;

            try
            {
                // Give the system a moment to restore network
                await Task.Delay(500);

                // Check and ensure connection is active
                bool isConnected = await connectionManager.EnsureConnectionAsync();

                if (isConnected)
                {
                    System.Diagnostics.Debug.WriteLine("GameView: Connection restored successfully");
                    // Force a UI update
                    MainThread.BeginInvokeOnMainThread(() => UpdateGameStatus());
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("GameView: Failed to restore connection");
                    // Show error and navigate back
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await DisplayAlertAsync(
                            LocalizationManager.Instance.GetString("error"),
                            LocalizationManager.Instance.GetString("connection_lost"),
                            LocalizationManager.Instance.GetString("yes"));
                        await Navigation.PopToRootAsync();
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GameView: Error handling app resume: {ex.Message}");
            }
        }

        private void OnPageSizeChanged(object? sender, EventArgs e)
        {
            UpdateGameStatusFontSize();
        }

        private void UpdateGameStatusFontSize()
        {
            // Get text content - either from FormattedText or Text
            string text;
            if (GameStatusLabel.FormattedText != null && GameStatusLabel.FormattedText.Spans.Count > 0)
            {
                text = string.Concat(GameStatusLabel.FormattedText.Spans.Select(s => s.Text ?? ""));
            }
            else
            {
                text = GameStatusLabel.Text ?? "";
            }

            // Get the container's width (Frame) minus padding (10 on each side)
            double availableWidth = GameStatusFrame.Width - 20;
            double availableHeight = GameStatusFrame.Height - 20;

            if (availableWidth <= 0)
            {
                // Fallback: calculate from screen dimensions
                // Game status column is 18/23 of screen width
                var displayInfo = DeviceDisplay.MainDisplayInfo;
                double screenWidth = displayInfo.Width / displayInfo.Density;
                availableWidth = screenWidth * 18.0 / 23.0 - 20;
            }

            if (availableHeight <= 0)
            {
                // Fallback: calculate from screen height
                var displayInfo = DeviceDisplay.MainDisplayInfo;
                double screenHeight = displayInfo.Height / displayInfo.Density;
                availableHeight = (screenHeight * 3.0 / 45.0) - 20;
            }

            if (availableWidth <= 0)
            {
                // Final fallback default
                availableWidth = 200;
            }

            if (availableHeight <= 0)
            {
                availableHeight = 100;
            }

            // Start with a base font size
            double maxFontSize = 25;

            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            // Estimate characters that fit at current font size
            double avgCharWidth = 2.2 * maxFontSize;
            int maxCharsPerLine = (int)(availableWidth / avgCharWidth);

            // Get the longest line in the text
            var lines = text.Split('\n');
            int maxLineLength = lines.Length > 0 ? lines.Max(l => l.Length) : 0;

            // If text is too long, reduce font size proportionally
            if (maxLineLength > maxCharsPerLine && maxCharsPerLine > 0)
            {
                double ratio = (double)maxCharsPerLine / maxLineLength;
                maxFontSize = Math.Max(10, maxFontSize * ratio);
            }

            double avgCharHeight = 0.6 * maxFontSize;
            int maxLines = (int)(availableHeight / avgCharHeight);

            if (lines.Count() > maxLines && maxLines > 0)
            {
                double ratio = (double)maxLines / lines.Count();
                maxFontSize = Math.Max(10, maxFontSize * ratio);
            }

            // Apply font size to label and all spans
            GameStatusLabel.FontSize = maxFontSize;
            if (GameStatusLabel.FormattedText != null)
            {
                foreach (var span in GameStatusLabel.FormattedText.Spans)
                {
                    span.FontSize = maxFontSize;
                }
            }
        }

        private int? GetInt32Value(object? obj)
        {
            if (obj == null) return null;

            if (obj is int intValue)
                return intValue;

            if (obj is JsonElement je)
            {
                return je.ValueKind == JsonValueKind.Number ? je.GetInt32() : null;
            }

            return null;
        }

        private string? GetStringValue(object? obj)
        {
            if (obj == null) return null;

            if (obj is string str)
                return str;

            if (obj is JsonElement je && je.ValueKind == JsonValueKind.String)
                return je.GetString();

            return null;
        }

        private List<int> GetInt32List(object? obj)
        {
            if (obj == null) return new();

            if (obj is List<int> intList)
                return intList;

            if (obj is JsonElement je && je.ValueKind == JsonValueKind.Array)
            {
                return je.EnumerateArray().Select(e => e.GetInt32()).ToList();
            }

            return new();
        }

        private string GetTargetHint(int hintIndex)
        {
            var localization = LocalizationManager.Instance;
            var hintKey = hintIndex switch
            {
                1 => "langren_kill",
                2 => "yuyanjia_chayan",
                3 => "nvwu_act",
                4 => "wuzhe_act",
                6 => "jiamian_chayan",
                7 => "yuyanjia_result",
                8 => "shemengren_act",
                9 => "xiong_act",
                10 => "thief_pick_role",
                11 => "langren_kill_target",
                12 => "converted_langren_succession",
                13 => "tonglingshi_chayan",
                14 => "tonglingshi_result",
                15 => "hunzi_act",
                16 => "jixielang_act",
                17 => "jixielang_info",
                18 => "jixielang_actagain",
                19 => "jixielang_actagain_info",
                20 => "meiyangyang_choose_sacrifice",
                21 => "meiyangyang_info",
                22 => "meiyangyang_info",
                23 => "meiyangyang_info",
                24 => "meiyangyang_info",
                25 => "meiyangyang_info",
                26 => "meiyangyang_info",
                29 => "shixianggui_chayan",
                30 => "shixianggui_result",
                31 => "moshushi_swap",
                32 => "guishushi_swap",
                33 => "awkyuyanjia_chayan",
                34 => "awkyuyanjia_result",
                35 => "xunxiangmeiying_act",
                50 => "open_eyes",
                51 => "close_eyes",
                52 => "lucky_one_open_eyes",
                53 => "lucky_one_close_eyes",
                54 => "converted_open_eyes",
                55 => "converted_close_eyes",
                63 => "langmeiren_act",
                64 => "awkshixianggui_act",
                65 => "awkshixianggui_check_conversion",
                70 => "shouwei_act",
                71 => "mengyan_act",
                75 => "check_mice",
                77 => "mengmianren_death",
                78 => "yingzi_act",
                79 => "yingzi_info",
                80 => "fuchouzhe_dead_shoot",
                81 => "fuchouzhe_thirdparty_kill",
                82 => "fuchouzhe_alliance",
                83 => "ghostbride_choose_groom",
                84 => "ghostbride_groom_check",
                85 => "ghostbride_couple_choose_witness",
                86 => "ghostbride_witness_check",
                87 => "ghostbride_witness",
                92 => "ghostbride_attack_status",
                93 => "liemoren_act",
                94 => "tufu_act",
                95 => "tufu_attack_status",
                96 => "shoumuren_info",
                100 => "volunteer_sheriff",
                101 => "vote_sheriff",
                102 => "round_table",
                103 => "vote_sheriff_vote",
                104 => "sheriff_speech",
                105 => "sheriff_pk",
                106 => "owner_sheriff_select",
                107 => "withdraw_or_reveal",
                110 => "sheriff_recommend_vote",
                111 => "voteout",
                112 => "owner_vote_select",
                113 => "voted_out_reveal",
                150 => "sheriff_handover",
                151 => "hunter_kill",
                152 => "death_announcement",
                153 => "sheriff_choose_direction",
                154 => "vote_result",
                155 => "voted_out",
                1000 => "check_private",
                1001 => "night_time",
                1002 => "day_time",
                1003 => "game_over",
                1020 => "put_down_device",
                _ => null
            };

            return hintKey != null ? localization.GetString(hintKey) : string.Empty;
        }

        private string GetSpecialTargetLabel(int hintIndex, int targetId)
        {
            if (SpecialTargets.TryGetValue(hintIndex, out var targetDict))
            {
                if (targetDict.TryGetValue(targetId, out var label))
                {
                    return label;
                }
            }
            return string.Empty;
        }

        private string GetPlayerRole(int playerId)
        {
            var gameDict = connectionManager?.GetGameDictionary() ?? new();
            var playersDict = GetDictionaryValue(gameDict.TryGetValue("players", out var playersObj) ? playersObj : null);

            if (playersDict == null || playersDict.Count == 0)
                return string.Empty;

            var playerKey = playerId.ToString();
            if (!playersDict.ContainsKey(playerKey))
                return string.Empty;

            var playerDict = GetDictionaryValue(playersDict[playerKey]);
            return GetStringValue(playerDict.TryGetValue("role", out var roleObj) ? roleObj : null) ?? string.Empty;
        }

        private int GetPlayerAllegiance(int playerId)
        {
            var gameDict = connectionManager?.GetGameDictionary() ?? new();
            var playersDict = GetDictionaryValue(gameDict.TryGetValue("players", out var playersObj) ? playersObj : null);

            if (playersDict == null || playersDict.Count == 0)
                return 1; // Default to good

            var playerKey = playerId.ToString();
            if (!playersDict.ContainsKey(playerKey))
                return 1; // Default to good

            var playerDict = GetDictionaryValue(playersDict[playerKey]);
            return GetInt32Value(playerDict.TryGetValue("alliance", out var allianceObj) ? allianceObj : null) ?? 1;
        }

        private bool IsPlayerDead(int playerId)
        {
            var gameDict = connectionManager?.GetGameDictionary() ?? new();
            var playersDict = GetDictionaryValue(gameDict.TryGetValue("players", out var playersObj) ? playersObj : null);

            if (playersDict == null || playersDict.Count == 0)
                return false;

            var playerKey = playerId.ToString();
            if (!playersDict.ContainsKey(playerKey))
                return false;

            var playerDict = GetDictionaryValue(playersDict[playerKey]);
            var aliveValue = GetInt32Value(playerDict.TryGetValue("alive", out var aliveObj) ? aliveObj : null);

            // Player is dead if alive == 0
            return aliveValue == 0;
        }

        private int GetCurrentSheriff()
        {
            var gameDict = connectionManager?.GetGameDictionary() ?? new();
            return GetInt32Value(gameDict.TryGetValue("current_sheriff", out var sheriffObj) ? sheriffObj : null) ?? 0;
        }

        private void UpdateCurrentSheriffLabel()
        {
            var localization = LocalizationManager.Instance;
            var currentSheriff = GetCurrentSheriff();

            if (currentSheriff > 0)
            {
                var sheriffText = localization.GetString("current_sheriff", "Sheriff: {0}");
                CurrentSheriffLabel.Text = sheriffText.Replace("{0}", currentSheriff.ToString());
                CurrentSheriffLabel.IsVisible = true;
            }
            else
            {
                CurrentSheriffLabel.Text = "";
                CurrentSheriffLabel.IsVisible = false;
            }
        }

        private Dictionary<string, object> GetDictionaryValue(object? obj)
        {
            if (obj == null) return new();

            if (obj is Dictionary<string, object> dict)
                return dict;

            if (obj is JsonElement je && je.ValueKind == JsonValueKind.Object)
            {
                var result = new Dictionary<string, object>();
                foreach (var property in je.EnumerateObject())
                {
                    result[property.Name] = property.Value;
                }
                return result;
            }

            return new();
        }

        private void UpdateGameStatus()
        {
            if (connectionManager == null) return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                var gameDict = connectionManager.GetGameDictionary();
                var localization = LocalizationManager.Instance;

                // Update game status (phase, etc.)
                var phaseValue = GetInt32Value(gameDict.TryGetValue("phase", out var phaseObj) ? phaseObj : null);
                var phaseStr = phaseValue == 0 ? localization.GetString("phase_night") :
                              phaseValue == 1 ? localization.GetString("phase_day") :
                              localization.GetString("phase_unknown");
                var dayNum = gameDict.TryGetValue("day", out var dayNum2) ? GetInt32Value(dayNum2)?.ToString() ?? "?" : "?";
                var dayString = localization.GetString("day").Replace("{0}", dayNum);
                SetGameStatusText($"{dayString} {phaseStr}\n");

                // Show/hide Reveal button based on day time (phaseValue == 1)
                RevealBtn.IsVisible = (phaseValue == 1);

                // Check if current player is dead and update player ID color
                var isPlayerDead = IsPlayerDead(playerId);
                var playerIdColor = isPlayerDead ? Colors.Red : Colors.White;
                PlayerIdLabel.TextColor = playerIdColor;

                var userAction = GetInt32Value(gameDict.TryGetValue(DictUserAction, out var uaObj) ? uaObj : null) ?? 0;
                var userUsers = GetInt32List(gameDict.TryGetValue(DictUserActionUsers, out var uuObj) ? uuObj : null);
                var userTargets = GetInt32List(gameDict.TryGetValue(DictUserActionTargets, out var utObj) ? utObj : null);
                var userTargetsCount = GetInt32Value(gameDict.TryGetValue(DictUserActionTargetsCount, out var utcObj) ? utcObj : null) ?? 0;
                var userTargetsHint = GetInt32Value(gameDict.TryGetValue(DictUserActionTargetsHint, out var uthObj) ? uthObj : null) ?? -1;
                var userRole = GetStringValue(gameDict.TryGetValue(DictUserActionRole, out var uroleObj) ? uroleObj : null) ?? "";
                var userInfo = GetStringValue(gameDict.TryGetValue(DictUserActionInfo, out var uiObj) ? uiObj : null) ?? "";
                var userInfo2 = GetStringValue(gameDict.TryGetValue(DictUserActionInfo2, out var ui2Obj) ? ui2Obj : null) ?? "";
                var userInfo3 = GetStringValue(gameDict.TryGetValue(DictUserActionInfo3, out var ui3Obj) ? ui3Obj : null) ?? "";
                var userResponse = GetDictionaryValue(gameDict.TryGetValue(DictUserActionResponse, out var urObj) ? urObj : null);

                // Track user response changes for action history
                TrackUserResponseForHistory(userResponse, userTargetsHint);

                // Calculate server-client clock offset for accurate countdown
                var serverTime = GetInt32Value(gameDict.TryGetValue(DictServerTime, out var stObj) ? stObj : null) ?? 0;
                if (serverTime != lastServerTime)
                {
                    int clientNow = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    serverTimeOffset = serverTime - clientNow;
                    lastServerTime = serverTime;
                }

                var speaking = GetInt32List(gameDict.TryGetValue(DictSpeaker, out var speakingObj) ? speakingObj : null);
                var speaker = speaking.Count > 0 ? speaking[speaking.Count - 1] : 0;

                // Check if self can act
                bool isSelfActable = userAction != 0 && userUsers.Contains(playerId);

                if (isSelfActable)
                {
                    DisplayTargetSelection(userAction, userTargets, userTargetsCount, userTargetsHint, userInfo, userInfo2, userInfo3, phaseValue == 0 ? userResponse : null);
                }
                else
                {
                    HideTargetSelection();
                }

                if (userAction != 0)
                {
                    // Pass isSelfActable to avoid double-managing the countdown
                    // When isSelfActable is true, DisplayTargetSelection already handles the countdown
                    DisplayCurrentlyActing(userAction, userUsers, userTargetsHint, userInfo, userInfo2, userRole, speaker, phaseValue ?? 0, !isSelfActable);
                }
                else
                {
                    HideCurrentlyActing();
                }

            });
        }

        private void DisplayCurrentlyActing(int deadline, List<int> actingPlayerIds, int userTargetsHint, string userInfo, string userInfo2, string userRole, int speaker = 0, int phaseValue = 0, bool manageCountdown = true)
        {
            // Only manage countdown if we're not showing target selection (which has its own countdown)
            if (manageCountdown)
            {
                countdownCts?.Cancel();
                countdownCts = new CancellationTokenSource();
            }

            if (actingPlayerIds.Contains(-1) && actingPlayerIds.Count == 1)
            {
                // This is an announcement (user == -1)
                // Check if there's a special handler for this hint
                string statusText;
                if (AnnouncementInfoHandlers.TryGetValue(userTargetsHint, out var handler))
                {
                    statusText = handler(userInfo, userInfo2);
                }
                else
                {
                    // Default handling: get hint text and replace {0} with userInfo
                    var hintText = GetTargetHint(userTargetsHint);
                    userInfo = LocalizationManager.Instance.GetString(userInfo);
                    statusText = hintText.Replace("{0}", userInfo);
                }
                SetGameStatusText(statusText);

                // Play voiceover (strip color codes for speech)
                if (IsAnnouncerEnabled)
                {
                    _ = PlayVoiceoverAsync(StripColorCodes(statusText));
                }
            }
            else if (phaseValue == 1)
            {
                // Day phase - show who should be speaking
                var localization = LocalizationManager.Instance;

                // Display speaking indicator with roles
                var speakingText = localization.GetString("speaking", "Speaking: {0}");
                if (speaker != 0)
                    SetGameStatusText(speakingText.Replace("{0}", speaker.ToString()));
                else
                    SetGameStatusText("");
            }
            else
            {
                var actingRoles = new HashSet<string>();

                if (!string.IsNullOrEmpty(userRole))
                {
                    // Use the role provided by the server
                    actingRoles.Add(LocalizationManager.Instance.GetString(userRole));
                }

                // Display the acting roles
                string actingText = actingRoles.Count > 0
                    ? string.Join(", ", actingRoles) : "";

                SetGameStatusText(actingText);
            }

            // Start countdown timer only if we're managing it
            if (manageCountdown && countdownCts != null)
            {
                StartCountdown(deadline, countdownCts.Token);
            }
        }

        private void HideCurrentlyActing()
        {
            countdownCts?.Cancel();
            countdownCts = null;
        }

        /// <summary>
        /// Plays voiceover for the given text using the VoiceoverService.
        /// Falls back to TextToSpeech if custom audio is not available.
        /// Only plays if announcer is enabled.
        /// </summary>
        private async Task PlayVoiceoverAsync(string text)
        {
            if (!announcerEnabled || string.IsNullOrEmpty(text))
                return;

            try
            {
                // Try to use custom voice clips first
                var segments = VoiceoverService.Instance.ParseText(text);

                if (segments.Count > 0)
                {
                    // Play using custom voice clips
                    await VoiceoverService.Instance.PlayAsync(text);
                }
                else
                {
                    // Fall back to TextToSpeech
                    await TextToSpeech.Default.SpeakAsync(text);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Voiceover failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Plays a warning beep sound when countdown is running low.
        /// Uses audio beep and visual feedback.
        /// </summary>
        private void PlayWarningBeep()
        {
            try
            {
                // Play audio beep
                _ = BeepService.PlayWarningBeepAsync();

                // Also trigger haptic feedback (vibration) for tactile warning on mobile
                try
                {
                    HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);
                }
                catch { /* Haptic not available on all platforms */ }

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Warning beep failed: {ex.Message}");
            }
        }

        private void DisplayTargetSelection(int userActionDeadline, List<int> availableTargets, int maxTargetCount, int hintIndex, string userInfo = "", string userInfo2 = "", string userInfo3 = "", Dictionary<string, object>? userResponse = null)
        {
            // Check if we're already displaying the same target selection
            // Only compare deadline and hint - don't rely on UI visibility state which can have race conditions
            if (currentDisplayedDeadline == userActionDeadline &&
                currentDisplayedHint == hintIndex)
            {
                // Already displaying this state, no need to rebuild
                return;
            }

            // Special case: sheriff vote. Server won't send this, as it is an interrupt.
            if (hintIndex == 104)
            {
                availableTargets.Add(-2);
            }

            // Special case: owner vote override during day speech. Server won't send this.
            if (hintIndex == 102 && isOwner && !availableTargets.Contains(-100))
            {
                availableTargets.Add(-100);
            }

            // Special case: owner sheriff override during sheriff speech. Server won't send this.
            if ((hintIndex == 104 || hintIndex == 107) && isOwner && !availableTargets.Contains(-101))
            {
                availableTargets.Add(-101);
            }

            // Track the current displayed state
            currentDisplayedDeadline = userActionDeadline;
            currentDisplayedHint = hintIndex;

            // Bump the display generation BEFORE we clear selectedTargets and the
            // button containers. Any click events that were already queued against
            // the previous layout will carry the prior generation value and be
            // rejected by OnTargetSelected, so they cannot pollute selectedTargets
            // for the new round.
            displayGeneration++;
            int generation = displayGeneration;

            // Cancel previous countdown if any
            countdownCts?.Cancel();
            countdownCts = new CancellationTokenSource();

            var localization = LocalizationManager.Instance;
            selectedTargets.Clear();
            TargetButtonsContainer.Clear();
            SpecialTargetButtonsContainer.Clear();

            // Build the instruction text
            string instructionText;
            string hintText = GetTargetHint(hintIndex);
            if (!string.IsNullOrEmpty(hintText))
            {
                instructionText = hintText;
            }
            else
            {
                // Fallback to instruction if no hint available
                instructionText = maxTargetCount == -1
                    ? localization.GetString("select_any_targets")
                    : localization.GetString("select_up_to_targets").Replace("{0}", maxTargetCount.ToString());
            }

            if (hintIndex == 1000)
            {
                var role = GetPlayerRole(playerId);
                var allegiance = GetPlayerAllegiance(playerId);
                userInfo = $"{role},{allegiance}";
            }
            if (hintIndex == 75)
            {
                if (userInfo.Contains(","))
                {
                    var split = userInfo.Split(',');
                    if (split[0] == playerId.ToString())
                    {
                        userInfo = localization.GetString("check_mice_info");
                        userInfo = userInfo.Replace("{0}", split.Length > 1 ? split[1] : "");
                    }
                    else
                    {
                        userInfo = localization.GetString("no");
                    }
                }
                else
                {
                    userInfo = userInfo == playerId.ToString() ? localization.GetString("yes") : localization.GetString("no");
                }
            }
            if (hintIndex == 65)
            {
                // AwkShiXiangGui_CheckConversion - check if player was converted
                userInfo = userInfo == playerId.ToString() ? localization.GetString("awkshixianggui_converted") : localization.GetString("awkshixianggui_not_converted");
            }
            if (hintIndex == 84 || hintIndex == 86 || hintIndex == 25)
            {
                userInfo = userInfo == playerId.ToString() ? "yes" : "no";
            }
            if (hintIndex == 22 || hintIndex == 25)
            {
                userInfo2 = userInfo2 == playerId.ToString() ? "yes" : "no";
            }
            if (hintIndex == 22)
            {
                var split = userInfo.Split(":");
                if (split.Length == 2)
                {
                    var split1 = split[0];
                    var split2 = split[1];
                    var civilians = split2.Split(",");
                    if (civilians.Contains(playerId.ToString()))
                    {
                        userInfo = split1;
                    }
                    else
                    {
                        userInfo = "";
                    }
                }
                else
                {
                    userInfo = "";
                }
            }
            var ui = UserInfoHints.TryGetValue(hintIndex, out var handler) ? handler(userInfo, userInfo2) : userInfo;

            // Check if skill is disabled (userInfo3 == "1")
            if (userInfo3 == "1")
            {
                var skillDisabledText = localization.GetString("skill_disabled", "Your skill has been disabled this night.");
                SetTargetInstructionText($"{instructionText}\n[c:Red]{skillDisabledText}[/c]");
            }
            else
            {
                // Set the full instruction text with color support.
                // Avoid a trailing newline + empty span when `ui` is empty - it
                // crashes WinUI's Label.RecalculateSpanPositions.
                var combined = string.IsNullOrEmpty(ui) ? instructionText : $"{instructionText}\n{ui}";
                SetTargetInstructionText(combined);
            }

            // Get all players from game dictionary
            var gameDict = connectionManager?.GetGameDictionary() ?? new();
            var playersDict = GetDictionaryValue(gameDict.TryGetValue("players", out var playersObj) ? playersObj : null);
            var allPlayerIds = new List<int>();

            if (playersDict != null && playersDict.Count > 0)
            {
                foreach (var key in playersDict.Keys)
                {
                    if (int.TryParse(key, out var id))
                    {
                        allPlayerIds.Add(id);
                    }
                }
                allPlayerIds.Sort();
            }

            // Parse user responses to show who chose what
            var responsesByTarget = new Dictionary<int, List<int>>();
            var ownChoices = new HashSet<int>();

            // Create special target buttons first (if they are in availableTargets)
            if (SpecialTargets.TryGetValue(hintIndex, out var specialTargetsForHint))
            {
                foreach (var specialTarget in specialTargetsForHint.Keys.OrderBy(x => x))
                {
                    // Only show special targets that are in availableTargets
                    // (owner vote override -100 was already added to availableTargets above for owners)
                    if (!availableTargets.Contains(specialTarget))
                        continue;

                    var label = GetSpecialTargetLabel(hintIndex, specialTarget);
                    if (string.IsNullOrEmpty(label))
                        label = specialTarget.ToString();
                    else
                        label = localization.GetString(label);

                    // Add response count if available
                    if (responsesByTarget.TryGetValue(specialTarget, out var respondents))
                    {
                        label += $" ({respondents.Count})";
                    }

                    var button = new Button
                    {
                        Text = label,
                        CornerRadius = 5,
                        Padding = new Thickness(10, 5),
                        Margin = new Thickness(5),
                        BackgroundColor = ownChoices.Contains(specialTarget) ? Colors.Orange : Colors.LightGray,
                        TextColor = Colors.Black,
                        IsEnabled = true,
                        Opacity = 1.0,
                        MinimumWidthRequest = 60,
                        MinimumHeightRequest = 40,
                        HeightRequest = 45,
                        FontSize = 16
                    };

                    int capturedTargetId = specialTarget;
                    int capturedGeneration = generation;
                    button.Clicked += (s, e) => OnTargetSelected(button, capturedTargetId, maxTargetCount, capturedGeneration);

                    SpecialTargetButtonsContainer.Add(button);
                }
            }

            // Create target buttons for all players
            var selectablePlayerSet = new HashSet<int>(availableTargets.Where(t => t > 0));

            if (hintIndex == 10)
            {
                // Thief pick role: targets are role indices, not player IDs - only show selectable
                foreach (var targetId in availableTargets)
                {
                    if (targetId <= 0)
                        continue;

                    var buttonText = targetId.ToString();
                    if (!string.IsNullOrEmpty(userInfo))
                    {
                        var roleNames = userInfo.Split(',');
                        if (targetId > 0 && targetId <= roleNames.Length)
                        {
                            var roleName = roleNames[targetId - 1].Trim();
                            buttonText = localization.GetString(roleName, roleName);
                        }
                    }

                    var button = new Button
                    {
                        Text = buttonText,
                        CornerRadius = 34,
                        Padding = new Thickness(0),
                        Margin = new Thickness(5),
                        BackgroundColor = ownChoices.Contains(targetId) ? Colors.Orange : Colors.LightGray,
                        TextColor = Colors.Black,
                        IsEnabled = true,
                        Opacity = 1.0,
                        WidthRequest = 58,
                        HeightRequest = 58,
                        MinimumWidthRequest = 58,
                        MinimumHeightRequest = 58,
                        FontSize = 22
                    };

                    int capturedTargetId = targetId;
                    int capturedGeneration = generation;
                    button.Clicked += (s, e) => OnTargetSelected(button, capturedTargetId, maxTargetCount, capturedGeneration);

                    TargetButtonsContainer.Add(button);
                }
            }
            else if (selectablePlayerSet.Count > 0)
            {
                // Show all players in the game; gray out those not in available targets
                foreach (var pid in allPlayerIds)
                {
                    bool isSelectable = selectablePlayerSet.Contains(pid);
                    var buttonText = pid.ToString();

                    if (isSelectable && responsesByTarget.TryGetValue(pid, out var respondents))
                    {
                        buttonText += $" ({respondents.Count})";
                    }

                    var button = new Button
                    {
                        Text = buttonText,
                        CornerRadius = 34,
                        Padding = new Thickness(0),
                        Margin = new Thickness(5),
                        BackgroundColor = isSelectable
                            ? (ownChoices.Contains(pid) ? Colors.Orange : Colors.LightGray)
                            : Colors.DarkGray,
                        TextColor = isSelectable ? Colors.Black : Colors.Gray,
                        IsEnabled = isSelectable,
                        Opacity = isSelectable ? 1.0 : 0.4,
                        WidthRequest = 58,
                        HeightRequest = 58,
                        MinimumWidthRequest = 58,
                        MinimumHeightRequest = 58,
                        FontSize = 22
                    };

                    if (isSelectable)
                    {
                        int capturedTargetId = pid;
                        int capturedGeneration = generation;
                        button.Clicked += (s, e) => OnTargetSelected(button, capturedTargetId, maxTargetCount, capturedGeneration);
                    }

                    TargetButtonsContainer.Add(button);
                }
            }

            // Show target selection containers based on content
            TargetInstructionLabel.IsVisible = true;
            bool hasSpecialTargets = SpecialTargetButtonsContainer.Children.Count > 0;
            bool hasRegularTargets = TargetButtonsContainer.Children.Count > 0;
            SpecialTargetSelectionContainer.IsVisible = hasSpecialTargets;
            TargetSelectionContainer.IsVisible = hasRegularTargets;
            ConfirmButton.IsVisible = true;
            ConfirmButton.IsEnabled = true;
            ConfirmButton.Text = LocalizationManager.Instance.GetString("confirm");

            // Update current sheriff status label
            UpdateCurrentSheriffLabel();

            // Force layout update on Android to ensure buttons are properly rendered
            TargetButtonsContainer.InvalidateMeasure();

            // Start countdown timer
            StartCountdown(userActionDeadline, countdownCts.Token);
        }

        private void OnTargetSelected(Button button, int targetId, int maxCount, int generation)
        {
            // Drop stale clicks left over from a previous layout. When the server
            // pushes a new state, DisplayTargetSelection rebuilds the buttons and
            // increments displayGeneration. Any click that the user fired against
            // the prior layout but whose handler is dispatched after the rebuild
            // will carry an out-of-date generation; processing it would inject a
            // wrong target id into selectedTargets and corrupt what is sent to the
            // server on confirm.
            if (generation != displayGeneration)
            {
                return;
            }

            if (selectedTargets.Contains(targetId))
            {
                // Deselect the target
                selectedTargets.Remove(targetId);
                button.BackgroundColor = Colors.LightGray;
                button.TextColor = Colors.Black;
            }
            else if (maxCount == -1 || selectedTargets.Count < maxCount)
            {
                // Select the target
                selectedTargets.Add(targetId);
                button.BackgroundColor = Colors.Green;
                button.TextColor = Colors.White;
            }
            else if (maxCount == 1 && selectedTargets.Count == 1)
            {
                // Special case: when maxCount is 1, auto-deselect the previous target
                // Reset only enabled (selectable) buttons; skip disabled grayed-out ones
                foreach (var child in TargetButtonsContainer.Children)
                {
                    if (child is Button btn && btn.IsEnabled)
                    {
                        btn.BackgroundColor = Colors.LightGray;
                        btn.TextColor = Colors.Black;
                    }
                }
                foreach (var child in SpecialTargetButtonsContainer.Children)
                {
                    if (child is Button btn && btn.IsEnabled)
                    {
                        btn.BackgroundColor = Colors.LightGray;
                        btn.TextColor = Colors.Black;
                    }
                }

                // Clear previous selection and select the new target
                selectedTargets.Clear();
                selectedTargets.Add(targetId);
                button.BackgroundColor = Colors.Green;
                button.TextColor = Colors.White;
            }

            // Update confirm button blinking based on selection state
            UpdateConfirmButtonBlinking();
        }

        /// <summary>
        /// Starts or stops the confirm button blinking based on whether targets are selected.
        /// </summary>
        private void UpdateConfirmButtonBlinking()
        {
            if (selectedTargets.Count > 0)
            {
                StartConfirmButtonBlinking();
            }
            else
            {
                StopConfirmButtonBlinking();
            }
        }

        /// <summary>
        /// Starts the confirm button blinking animation.
        /// </summary>
        private void StartConfirmButtonBlinking()
        {
            // Cancel any existing blink animation
            confirmBlinkCts?.Cancel();
            confirmBlinkCts = new CancellationTokenSource();
            var ct = confirmBlinkCts.Token;

            _ = Task.Run(async () =>
            {
                bool isLit = true;
                while (!ct.IsCancellationRequested)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        if (ct.IsCancellationRequested) return;

                        if (isLit)
                        {
                            // Bright/lit state - green glow
                            ConfirmButton.BackgroundColor = Color.FromArgb("#228B22"); // Forest green
                            ConfirmButton.TextColor = Colors.White;
                            ConfirmButton.Scale = 1.05;
                        }
                        else
                        {
                            // Dim state
                            ConfirmButton.BackgroundColor = Color.FromArgb("#006400"); // Dark green
                            ConfirmButton.TextColor = Colors.LightGreen;
                            ConfirmButton.Scale = 1.0;
                        }
                    });

                    isLit = !isLit;
                    await Task.Delay(500, ct).ConfigureAwait(false); // Blink every 500ms
                }
            });
        }

        /// <summary>
        /// Stops the confirm button blinking animation and resets to default state.
        /// </summary>
        private void StopConfirmButtonBlinking()
        {
            confirmBlinkCts?.Cancel();
            confirmBlinkCts = null;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Reset to default state
                ConfirmButton.BackgroundColor = Colors.Transparent;
                ConfirmButton.TextColor = Colors.White;
                ConfirmButton.Scale = 1.0;
            });
        }

        private void StartCountdown(int deadline, CancellationToken ct)
        {
            warningBeepPlayed = false; // Reset warning flag for new countdown

            _ = Task.Run(async () =>
            {
                while (!ct.IsCancellationRequested)
                {
                    int clientNow = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    // Adjust client time using server offset to account for clock skew
                    int adjustedNow = clientNow + serverTimeOffset;

                    // Check if game is paused and get current phase
                    var gameDict = connectionManager?.GetGameDictionary() ?? new();
                    var pauseStart = GetInt32Value(gameDict.TryGetValue(DictUserActionPauseStart, out var psObj) ? psObj : null) ?? 0;
                    bool isPaused = pauseStart != 0;
                    var phaseValue = GetInt32Value(gameDict.TryGetValue("phase", out var phaseObj) ? phaseObj : null) ?? 0;
                    bool isDayPhase = phaseValue == 1;

                    // Calculate effective deadline accounting for current pause
                    int effectiveDeadline = deadline;
                    if (isPaused)
                    {
                        effectiveDeadline = deadline + (adjustedNow - pauseStart);
                    }

                    int timeRemaining = effectiveDeadline - adjustedNow;

                    if (!isPaused && timeRemaining <= 0)
                    {
                        // Time expired locally - hide UI but don't reset tracking
                        // The server will send an update that will reset tracking properly
                        MainThread.BeginInvokeOnMainThread(() => HideTargetSelection(resetTracking: false));
                        break;
                    }

                    // Play warning beep when countdown reaches 15 seconds (only during day phase)
                    if (!isPaused && isDayPhase && timeRemaining == 15 && !warningBeepPlayed && IsAnnouncerEnabled)
                    {
                        warningBeepPlayed = true;
                        MainThread.BeginInvokeOnMainThread(() => PlayWarningBeep());
                    }

                    // Special beeping for LangRen_Kill (hint index 1): beep from 15 seconds to 5 seconds each second
                    // Use the global hint from game dictionary so all players hear the beep, not just LangRen
                    var globalHint = GetInt32Value(gameDict.TryGetValue(DictUserActionTargetsHint, out var hintObj) ? hintObj : null) ?? 0;
                    if (!isPaused && globalHint == 1 && timeRemaining >= 5 && timeRemaining <= 15 && IsAnnouncerEnabled)
                    {
                        MainThread.BeginInvokeOnMainThread(() => PlayWarningBeep());
                    }

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (isPaused)
                        {
                            CountdownLabel.Text = LocalizationManager.Instance.GetString("game_paused", "? Paused");
                        }
                        else
                        {
                            CountdownLabel.Text = $"{timeRemaining}";
                        }
                    });

                    await Task.Delay(1000, ct);
                }
            }, ct);
        }

        private void HideTargetSelection(bool resetTracking = true)
        {
            // Only reset tracking variables when the server indicates the action is complete
            // Don't reset when hiding due to local countdown expiry (server might still have same deadline)
            if (resetTracking)
            {
                currentDisplayedDeadline = 0;
                currentDisplayedHint = -1;
            }

            // Invalidate any in-flight click handlers so they cannot mutate
            // selectedTargets after the UI has been torn down.
            displayGeneration++;

            countdownCts?.Cancel();
            countdownCts = null;
            selectedTargets.Clear();

            // Stop confirm button blinking
            StopConfirmButtonBlinking();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                TargetInstructionLabel.IsVisible = false;
                SpecialTargetSelectionContainer.IsVisible = false;
                TargetSelectionContainer.IsVisible = false;
                ConfirmButton.IsVisible = false;
                CountdownLabel.Text = "";
                SpecialTargetButtonsContainer.Clear();
                TargetButtonsContainer.Clear();
                CurrentSheriffLabel.Text = "";
                CurrentSheriffLabel.IsVisible = false;
            });
        }

        private async void OnConfirmClicked(object? sender, EventArgs e)
        {
            if (connectionManager == null || selectedTargets.Count == 0) return;

            var localization = LocalizationManager.Instance;
            try
            {
                // Stop blinking and show confirmed state
                StopConfirmButtonBlinking();
                ConfirmButton.Text = "✓";
                ConfirmButton.BackgroundColor = Color.FromArgb("#228B22"); // Forest green - confirmed

                var targetsList = selectedTargets.ToList();

                // Record the action as "Selected" (client-side click)
                RecordPlayerAction(currentDisplayedHint, targetsList, isConfirmed: false);

                // Send the selected targets to the server via SignalR
                // Don't hide the selection - let server state drive the UI
                // This prevents countdown timer from resetting when server sends update
                await connectionManager.SendTargetSelectionAsync(gameId, playerId, targetsList);
            }
            catch (Exception ex)
            {
                // Re-enable on error
                ConfirmButton.IsEnabled = true;
                ConfirmButton.Text = localization.GetString("confirm");
                ConfirmButton.BackgroundColor = Colors.Transparent;

                await DisplayAlertAsync(
                    localization.GetString("error"),
                    localization.GetString("failed_send_selection") + ": " + ex.Message,
                    localization.GetString("yes"));
            }
        }

        private async void OnDisconnectClicked(object? sender, EventArgs e)
        {
            var localization = LocalizationManager.Instance;
            bool confirm = await DisplayAlertAsync(
                localization.GetString("disconnect"),
                localization.GetString("disconnect_confirm"),
                localization.GetString("yes"),
                localization.GetString("no"));
            if (confirm)
            {
                if (connectionManager != null)
                {
                    connectionManager.GameStateUpdated -= UpdateGameStatus;

                    // Notify server that we're leaving
                    await connectionManager.LeaveGameAsync(gameId);
                    await connectionManager.Disconnect();
                }
                await Navigation.PopAsync();
            }
        }

        private async void OnRevealClicked(object? sender, EventArgs e)
        {
            if (connectionManager == null)
                return;

            var localization = LocalizationManager.Instance;

            // Show confirmation dialog
            bool confirm = await DisplayAlertAsync(
                localization.GetString("reveal"),
                localization.GetString("reveal_confirm"),
                localization.GetString("yes"),
                localization.GetString("no"));

            if (!confirm)
                return;

            try
            {
                // Send action -10 to the server
                await connectionManager.SendTargetSelectionAsync(gameId, playerId, new List<int> { -10 });
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync(
                    localization.GetString("error"),
                    localization.GetString("failed_send_selection") + ": " + ex.Message,
                    localization.GetString("yes"));
            }
        }

        private void OnAnnouncerToggleClicked(object? sender, EventArgs e)
        {
            announcerEnabled = !announcerEnabled;

            // Update button appearance based on state
            if (announcerEnabled)
            {
                AnnouncerBtn.Text = "♪";
                AnnouncerBtn.BackgroundColor = Colors.Green;
                VoiceoverService.Instance.IsEnabled = true;
            }
            else
            {
                AnnouncerBtn.Text = "×";
                AnnouncerBtn.BackgroundColor = Colors.LightGray;
                VoiceoverService.Instance.IsEnabled = false;
            }
        }

        private void OnPastActionsClicked(object? sender, EventArgs e)
        {
            actionHistoryVisible = !actionHistoryVisible;
            ActionHistoryContainer.IsVisible = actionHistoryVisible;

            // Update button appearance
            if (actionHistoryVisible)
            {
                PastActionsBtn.BackgroundColor = Colors.Green;
                UpdateActionHistoryDisplay();
            }
            else
            {
                PastActionsBtn.BackgroundColor = Colors.LightGray;
            }
        }

        /// <summary>
        /// Tracks user_response dictionary changes from server and records actions for the current player.
        /// This ensures action history is captured even on reconnection or when local tracking misses updates.
        /// Records as "Confirmed" to distinguish from local "Selected" actions.
        /// </summary>
        private void TrackUserResponseForHistory(Dictionary<string, object> userResponse, int hintIndex)
        {
            if (userResponse == null || userResponse.Count == 0)
            {
                return;
            }

            // Check if there's a response for the current player
            var playerKey = playerId.ToString();
            if (!userResponse.TryGetValue(playerKey, out var responseObj))
            {
                return;
            }

            // Get the response as a list of targets
            var targets = GetInt32List(responseObj);
            if (targets == null || targets.Count == 0)
            {
                return;
            }

            // Record the server-confirmed response
            RecordPlayerAction(hintIndex, targets, isConfirmed: true);
        }

        /// <summary>
        /// Records a player action with timestamp.
        /// Translates special targets using SpecialTargets dictionary.
        /// </summary>
        /// <param name="hintIndex">The hint index for the action</param>
        /// <param name="targets">The selected targets</param>
        /// <param name="isConfirmed">True if this is a server-confirmed action, false if client-selected</param>
        private void RecordPlayerAction(int hintIndex, List<int> targets, bool isConfirmed = false)
        {
            if (targets.Count == 0) return;

            var localization = LocalizationManager.Instance;
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var actionText = isConfirmed
                ? localization.GetString("action_confirmed", "Confirmed")
                : localization.GetString("action_selected", "Selected");

            foreach (var target in targets)
            {
                string targetLabel;

                // Try to translate using SpecialTargets
                if (target <= 0 && SpecialTargets.TryGetValue(hintIndex, out var specialTargetsForHint))
                {
                    if (specialTargetsForHint.TryGetValue(target, out var label))
                    {
                        targetLabel = localization.GetString(label, label);
                    }
                    else
                    {
                        targetLabel = target.ToString();
                    }
                }
                else
                {
                    // Regular player target
                    targetLabel = target.ToString();
                }

                var entry = $"{timestamp} {actionText} {targetLabel}";
                actionHistory.Add(entry);
            }

            // Update display if visible
            if (actionHistoryVisible)
            {
                UpdateActionHistoryDisplay();
            }
        }

        /// <summary>
        /// Updates the action history display label.
        /// </summary>
        private void UpdateActionHistoryDisplay()
        {
            if (actionHistory.Count == 0)
            {
                ActionHistoryLabel.Text = LocalizationManager.Instance.GetString("past_actions", "Past Actions") + ": --";
            }
            else
            {
                // Show most recent actions first (reversed order)
                var reversedHistory = actionHistory.AsEnumerable().Reverse().Take(50).ToList();
                ActionHistoryLabel.Text = string.Join("\n", reversedHistory);
            }
        }
    }

    public class PlayerDisplay
    {
        public string PlayerInfo { get; set; } = "";
        public string Details { get; set; } = "";
    }
}
