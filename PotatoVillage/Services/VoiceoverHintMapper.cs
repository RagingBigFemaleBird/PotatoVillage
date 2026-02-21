using System.Collections.Generic;

namespace PotatoVillage.Services
{
    /// <summary>
    /// Maps game hint IDs to voiceover text for the announcer.
    /// </summary>
    public static class VoiceoverHintMapper
    {
        // Mapping of hint IDs to Chinese voiceover text
        private static readonly Dictionary<int, string> HintToVoiceover = new()
        {
            // Night phase announcements (50-51 pattern for open/close eyes)
            { 50, "{role}请睁眼" },  // Role opens eyes
            { 51, "{role}请闭眼" },  // Role closes eyes
            { 52, "幸运儿请睁眼" },
            { 53, "幸运儿请闭眼" },
            
            // Game start/end
            { 1000, "请确认身份" },
            { 1001, "天黑请闭眼" },
            { 1002, "天亮了" },
            { 1003, "{info}胜利" },  // Winner announcement
            
            // Sheriff election
            { 100, "请竞选警长" },
            { 102, "请发言" },
            { 103, "请投票" },
            { 104, "警长竞选发言" },
            { 105, "警长决选发言" },
            
            // Day phase
            { 110, "警长请归票" },
            { 111, "请投票" },
            { 150, "请移交警徽" },
            { 152, "公布死亡信息" },
            { 153, "请选择发言方向" },
            { 154, "公布投票结果" },
            
            // Role-specific actions
            { 1, "狼人请杀人" },
            { 2, "女巫请睁眼" },
            { 3, "女巫请选择" },
            { 4, "预言家请查验" },
            { 5, "狼人请睁眼" },
            { 6, "假面请查验" },
            { 7, "预言家查验结果" },
            
            // LaoShu (Mouse) actions
            { 72, "老鼠请标记" },
            { 75, "确认标记状态" },
            { 76, "幸运儿请行动" },
            
            // Hunter
            { 151, "猎人请开枪" },
        };

        // Role name mapping for {role} placeholder
        private static readonly Dictionary<string, string> RoleNameMapping = new()
        {
            { "LangRen", "狼人" },
            { "NvWu", "女巫" },
            { "YuYanJia", "预言家" },
            { "LieRen", "猎人" },
            { "ShouWei", "守卫" },
            { "BaiChi", "白痴" },
            { "PingMin", "平民" },
            { "JiaMian", "假面" },
            { "WuZhe", "舞者" },
            { "LaoShu", "老鼠" },
            { "DaMao", "大猫" },
            { "MiceCheck", "确认老鼠" },
            { "LuckyCheck", "幸运儿" },
        };

        /// <summary>
        /// Gets the voiceover text for a given hint ID.
        /// </summary>
        /// <param name="hintId">The hint ID from the game</param>
        /// <param name="roleInfo">Optional role information for placeholder replacement</param>
        /// <param name="additionalInfo">Optional additional info for placeholder replacement</param>
        /// <returns>The voiceover text, or null if no mapping exists</returns>
        public static string? GetVoiceoverText(int hintId, string? roleInfo = null, string? additionalInfo = null)
        {
            if (!HintToVoiceover.TryGetValue(hintId, out var template))
                return null;

            var text = template;

            // Replace {role} placeholder with localized role name
            if (roleInfo != null && text.Contains("{role}"))
            {
                var localizedRole = RoleNameMapping.TryGetValue(roleInfo, out var name) ? name : roleInfo;
                text = text.Replace("{role}", localizedRole);
            }

            // Replace {info} placeholder with additional info
            if (additionalInfo != null && text.Contains("{info}"))
            {
                // Try to localize the info if it's a known value
                var localizedInfo = additionalInfo switch
                {
                    "good" or "Good" => "好人",
                    "evil" or "Evil" => "狼人",
                    _ => additionalInfo
                };
                text = text.Replace("{info}", localizedInfo);
            }

            return text;
        }

        /// <summary>
        /// Registers a custom hint-to-voiceover mapping.
        /// </summary>
        public static void RegisterHint(int hintId, string voiceoverText)
        {
            HintToVoiceover[hintId] = voiceoverText;
        }

        /// <summary>
        /// Gets the localized role name for a given role key.
        /// </summary>
        public static string GetLocalizedRoleName(string roleKey)
        {
            return RoleNameMapping.TryGetValue(roleKey, out var name) ? name : roleKey;
        }
    }
}
