namespace ProcedureCore.LangRenSha
{
    /// <summary>
    /// Night action constants for all roles.
    /// Actions are executed in ascending order during the night phase.
    /// </summary>
    public enum ActionConstant
    {
        // === Game Setup (1-10) ===
        ShenLangGongWu1_Convert = 1,
        Thief_Setup = 2,
        GameBeginAnnouncement = 5,
        RoleCheck = 6,
        PutDownDevice = 7,
        NightTimeAnnouncement = 8,

        ShenLangGongWu1_OpenEyes = 10,
        ShenLangGongWu1_Check = 11,
        ShenLangGongWu1_CloseEyes = 12,

        // === XueYue (13-15) - Only triggers after XueYue reveals during day ===
        XueYue_OpenEyes = 13,
        XueYue_SelectTarget = 14,
        XueYue_CloseEyes = 15,

        // === AwkShiXiangGui Converted Check (16-18) - Day 1+ only, after ShenLangGongWu1 ===
        AwkShiXiangGui_ConvertedOpenEyes = 16,
        AwkShiXiangGui_ConvertedCheckAttack = 17,
        AwkShiXiangGui_ConvertedCloseEyes = 18,

        // === Thief (19-22) ===
        Thief_OpenEyes = 19,
        Thief_PickRole = 20,
        Thief_ShowAttackStatus = 21,
        Thief_CloseEyes = 22,

        // === HunZi (23-25) ===
        HunZi_OpenEyes = 23,
        HunZi_Act = 24,
        HunZi_CloseEyes = 25,

        // === GhostBride (29-43) ===
        GhostBride_OpenEyes = 29,
        GhostBride_ChooseGroom = 30,
        GhostBride_CloseEyes = 31,
        GhostBride_GroomOpenEyes = 32,
        GhostBride_GroomCheckLinked = 33,
        GhostBride_GroomCloseEyes = 34,
        GhostBride_CoupleOpenEyes = 35,
        GhostBride_CoupleChooseWitness = 36,
        GhostBride_CoupleCloseEyes = 37,
        GhostBride_WitnessLuckyOpenEyes = 38,
        GhostBride_WitnessCheckLinked = 39,
        GhostBride_WitnessLuckyCloseEyes = 40,
        GhostBride_WitnessOpenEyes = 41,
        GhostBride_WitnessInfo = 42,
        GhostBride_WitnessCloseEyes = 43,

        // === JiXieLang (44-47, 133-136) ===
        JiXieLang_OpenEyes = 44,
        JiXieLang_Act = 45,
        JiXieLang_Info = 46,
        JiXieLang_CloseEyes = 47,
        JiXieLang_ActAgain_OpenEyes = 133,
        JiXieLang_ActAgain = 134,
        JiXieLang_ActAgain_Info = 135,
        JiXieLang_ActAgain_CloseEyes = 136,

        // === ZhuangJiaLang (48-51, 137-140) - Acts after JiXieLang ===
        ZhuangJiaLang_OpenEyes = 48,
        ZhuangJiaLang_Act = 49,
        ZhuangJiaLang_Info = 50,
        ZhuangJiaLang_CloseEyes = 51,
        ZhuangJiaLang_ActAgain_OpenEyes = 137,
        ZhuangJiaLang_ActAgain = 138,
        ZhuangJiaLang_ActAgain_Info = 139,
        ZhuangJiaLang_ActAgain_CloseEyes = 140,

        // === YingZi (60-63) ===
        YingZi_OpenEyes = 60,
        YingZi_Act = 61,
        YingZi_Info = 62,
        YingZi_CloseEyes = 63,

        // === MengYan (56-58) - Acts after YingZi ===
        MengYan_OpenEyes = 56,
        MengYan_Act = 57,
        MengYan_CloseEyes = 58,

        // === DaMao (65-67) ===
        DaMao_OpenEyes = 65,
        DaMao_Act = 66,
        DaMao_CloseEyes = 67,

        // === LaoShu (70-78) ===
        LaoShu_OpenEyes = 70,
        LaoShu_Tag = 71,
        LaoShu_CloseEyes = 72,
        LaoShu_LuckyOneOpenEyes = 73,
        LaoShu_CheckMice = 74,
        LaoShu_LuckyOneCloseEyes = 75,
        LaoShu_GiftOpenEyes = 76,
        LaoShu_GiftAct = 77,
        LaoShu_GiftCloseEyes = 78,

        // === MoShuShi (79-81) - Acts after LaoShu, before SheMengRen ===
        MoShuShi_OpenEyes = 79,
        MoShuShi_Swap = 80,
        MoShuShi_CloseEyes = 81,

        // === SheMengRen (82-84) ===
        SheMengRen_OpenEyes = 82,
        SheMengRen_Act = 83,
        SheMengRen_CloseEyes = 84,

        // === ShouWei (85-87) ===
        ShouWei_OpenEyes = 85,
        ShouWei_Act = 86,
        ShouWei_CloseEyes = 87,

        // === AwkSheMengRen Guard Phase (88-90) - After ShouWei ===
        AwkSheMengRen_OpenEyes = 88,
        AwkSheMengRen_Act = 89,
        AwkSheMengRen_CloseEyes = 90,

        // === ShiXiangGui (91-94) - Evil TongLingShi ===
        ShiXiangGui_OpenEyes = 91,
        ShiXiangGui_ChaYan = 92,
        ShiXiangGui_Result = 93,
        ShiXiangGui_CloseEyes = 94,

        // === WuZhe (99-101, 150) ===
        WuZhe_OpenEyes = 99,
        WuZhe_Act = 100,
        WuZhe_CloseEyes = 101,
        WuZhe_Dance = 150,

        // === JiaMian (109-112) ===
        JiaMian_OpenEyes = 109,
        JiaMian_ChaYan = 110,
        JiaMian_Reverse = 111,
        JiaMian_CloseEyes = 112,

        // === GuiShuShi (113-115) - Acts after JiaMian ===
        GuiShuShi_OpenEyes = 113,
        GuiShuShi_Act = 114,
        GuiShuShi_CloseEyes = 115,

        // === HongTaiLang (117-119, 222) - Shifted by +3 to preserve order after GuiShuShi ===
        HongTaiLang_OpenEyes = 117,
        HongTaiLang_ChooseTarget = 118,
        HongTaiLang_CloseEyes = 119,
        HongTaiLang_KillTarget = 222,

        // === TuFu (120-122) - Day 1+ only, shifted by +3 ===
        TuFu_OpenEyes = 120,
        TuFu_Act = 121,
        TuFu_CloseEyes = 122,

        // === LangRen (124-131, 220) - shifted by +3 ===
        LangRen_OpenEyes = 124,
        LangRen_SelectTarget = 125,
        LangRen_ConfirmKill = 126,
        LangMeiRen_Act = 127,
        LangRen_CloseEyes = 131,
        LangRen_Kill = 220,

        // === NvWu (139-141) ===
        NvWu_OpenEyes = 139,
        NvWu_Act = 140,
        NvWu_CloseEyes = 141,

        // === LieMoRen (145-147) - Day 1+ only ===
        LieMoRen_OpenEyes = 145,
        LieMoRen_Act = 146,
        LieMoRen_CloseEyes = 147,

        // === YuYanJia (169-172) ===
        YuYanJia_OpenEyes = 169,
        YuYanJia_ChaYan = 170,
        YuYanJia_Result = 171,
        YuYanJia_CloseEyes = 172,

        // === TongLingShi (173-176) ===
        TongLingShi_OpenEyes = 173,
        TongLingShi_ChaYan = 174,
        TongLingShi_Result = 175,
        TongLingShi_CloseEyes = 176,

        // === Xiong (180-182, 310) ===
        Xiong_OpenEyes = 180,
        Xiong_Act = 181,
        Xiong_CloseEyes = 182,
        Xiong_BarkCheck = 310,

        // === ShouMuRen (185-188) - Day 1+ only, receives info about voted out player ===
        ShouMuRen_OpenEyes = 185,
        ShouMuRen_Info = 186,
        ShouMuRen_CloseEyes = 187,

        // === MeiYangYang (240-249) ===
        MeiYangYang_OpenEyes = 240,
        MeiYangYang_ChooseSacrifice = 241,
        MeiYangYang_Info = 242,
        MeiYangYang_CloseEyes = 243,
        MeiYangYang_CivilianOpenEyes = 244,
        MeiYangYang_CivilianInfo = 245,
        MeiYangYang_CivilianCloseEyes = 246,
        MeiYangYang_SacrificeOpenEyes = 247,
        MeiYangYang_SacrificeAction = 248,
        MeiYangYang_SacrificeCloseEyes = 249,

        // === AwkSheMengRen Judge Phase (285-287) - Just before LieRen ===
        AwkSheMengRen_JudgeOpenEyes = 285,
        AwkSheMengRen_JudgeAct = 286,
        AwkSheMengRen_JudgeCloseEyes = 287,

        // === LieRen (290-292) ===
        LieRen_OpenEyes = 290,
        LieRen_Act = 291,
        LieRen_CloseEyes = 292,

        // === LangQiang (295-297) ===
        LangQiang_OpenEyes = 295,
        LangQiang_Act = 296,
        LangQiang_CloseEyes = 297,

        // === FuChouZhe (300-303) ===
        FuChouZhe_OpenEyes = 300,
        FuChouZhe_ThirdPartyKill = 301,
        FuChouZhe_DeadShoot = 302,
        FuChouZhe_CloseEyes = 303,

        // === AwkShiXiangGui Conversion Check (305-307) - Day 0 only, very last before MengMianRen ===
        AwkShiXiangGui_LuckyOneOpenEyes = 305,
        AwkShiXiangGui_CheckConversion = 306,
        AwkShiXiangGui_LuckyOneCloseEyes = 307,

        // === MengMianRen (350) - Must be last night action ===
        MengMianRen_Act = 350,

        // === Day Time (1000) ===
        DayTimeAnnouncement = 1000,

    }

    /// <summary>
    /// Day phase speak/action constants.
    /// These control the flow of the day phase.
    /// </summary>
    public enum SpeakConstant
    {
        // === Sheriff Election (0-8) ===
        SheriffVolunteer = 0,
        SheriffSpeech = 1,
        WithdrawOrReveal = 2,  // 退水自爆 - Players can withdraw from sheriff or LangRen can reveal
        SheriffVoteTally = 3,
        SheriffVoteResult = 4,
        SheriffPKSpeech = 5,
        SheriffPKVote = 6,
        SheriffPKResult = 7,
        OwnerSheriffSelect = 8,

        // === Death Announcement (9-10) ===
        DeathAnnouncement = 9,
        DeathProcessingEntry = 10,


        // === Win Condition (20) ===
        WinConditionCheck = 20,

        // === Day Speech (30-38) ===
        SheriffChooseDirection = 30,
        DaySpeech = 31,
        SheriffRecommendVote = 32,
        Vote1 = 33,
        Vote1Result = 34,
        VoteoutSpeech = 35,
        Vote2 = 36,
        Vote2Result = 37,
        VotedOut = 38,
        OwnerVoteSelect = 39,

        // === End of Day (40) ===
        EndOfDay = 40,
        VotedOutAnnouncement = 41,
        VotedOutHandlerProcessing = 42,
        VotedOutReveal = 43, // After first voteout result, players may reveal (e.g. DingXuWangZi)

        // === Death Handling (97-102) ===
        DeathHandlingInterrupt = 97,
        DeadPlayerSkillsProcessing = 98,
        DeadPlayerSheriffHandover = 99,
        DeadPlayerSpeak = 100,
        MengMianRenDeath = 101,
        SkillUseAnnouncement = 102,
    }

    /// <summary>
    /// User action target hint constants.
    /// These are used to display appropriate UI hints for player actions.
    /// </summary>
    public enum HintConstant
    {
        // === LangRen (1, 11-12) ===
        LangRen_Kill = 1,
        LangRen_KillTarget = 11,
        LangRen_ConvertedSuccession = 12,

        // === YuYanJia (2, 7) ===
        YuYanJia_ChaYan = 2,
        YuYanJia_Result = 7,

        // === TongLingShi (13-14) ===
        TongLingShi_ChaYan = 13,
        TongLingShi_Result = 14,

        // === ShiXiangGui (29-30) - Evil TongLingShi ===
        ShiXiangGui_ChaYan = 29,
        ShiXiangGui_Result = 30,

        // === NvWu (3) ===
        NvWu_Act = 3,

        // === WuZhe (4) ===
        WuZhe_Act = 4,

        // === JiaMian (5-6) ===
        JiaMian_OpenEyes = 5,
        JiaMian_ChaYan = 6,

        // === ShouWei (70) ===
        ShouWei_Act = 70,

        // === MengYan (71) ===
        MengYan_Act = 71,
        SkillDisabled = 99, // Indicates skill is disabled for this action

        // === YingZi (78-79) ===
        YingZi_Act = 78,
        YingZi_Info = 79,

        // === FuChouZhe (80-82) ===
        FuChouZhe_DeadShoot = 80,
        FuChouZhe_ThirdPartyKill = 81,
        FuChouZhe_AllianceInfo = 82,

        // === SheMengRen (8) ===
        SheMengRen_Act = 8,

        // === MoShuShi (31) ===
        MoShuShi_Swap = 31,

        // === GuiShuShi (32) ===
        GuiShuShi_Swap = 32,

        // === Xiong (9) ===
        Xiong_Act = 9,

        // === Thief (10) ===
        Thief_PickRole = 10,

        // === HunZi (15) ===
        HunZi_Act = 15,

        // === JiXieLang (16-19) ===
        JiXieLang_Act = 16,
        JiXieLang_Info = 17,
        JiXieLang_ActAgain = 18,
        JiXieLang_ActAgain_Info = 19,

        // === MeiYangYang (20-26) ===
        MeiYangYang_ChooseSacrifice = 20,
        MeiYangYang_Info = 21,
        MeiYangYang_CivilianInfo = 22,
        MeiYangYang_SacrificeAction = 23,
        MeiYangYang_SacrificeSelf = 24,
        MeiYangYang_CivilianMayAct = 25,
        MeiYangYang_CivilianSacrificeForMeiYangYang = 26,

        // === HongTaiLang (27-28) ===
        HongTaiLang_ChooseTarget = 27,
        HongTaiLang_Info = 28,

        // === Open/Close Eyes Announcements (50-55) ===
        OpenEyes = 50,
        CloseEyes = 51,
        LuckyOneOpenEyes = 52,
        LuckyOneCloseEyes = 53,
        ConvertedOpenEyes = 54,
        ConvertedCloseEyes = 55,

        // === DaMao (62) ===
        DaMao_Act = 62,

        // === LangMeiRen (63) ===
        LangMeiRen_Act = 63,

        // === AwkShiXiangGui (64-65) ===
        AwkShiXiangGui_Act = 64,
        AwkShiXiangGui_CheckConversion = 65,

        // === XueYue (66) ===
        XueYue_SelectTarget = 66,

        // === LaoShu (72, 75-76) ===
        LaoShu_Tag = 72,
        LaoShu_CheckMice = 75,
        LaoShu_GiftedPoison = 76,

        // === MengMianRen (77) ===
        MengMianRen_Death = 77,

        // === GhostBride (83-92) ===
        GhostBride_ChooseGroom = 83,
        GhostBride_GroomCheckLinked = 84,
        GhostBride_CoupleChooseWitness = 85,
        GhostBride_WitnessCheckLinked = 86,
        GhostBride_WitnessInfo = 87,
        GhostBride_AttackStatus = 92,

        // === LieMoRen (93) ===
        LieMoRen_Act = 93,

        // === TuFu (94-95) ===
        TuFu_Act = 94,

        // === ShouMuRen (96) ===
        ShouMuRen_Info = 96,

        // === AwkSheMengRen (97-98) ===
        AwkSheMengRen_Act = 97,
        AwkSheMengRen_JudgeAct = 98,

        // === Sheriff Election (100-107) ===
        SheriffVolunteer = 100,
        SheriffVote = 101,
        RoundTable = 102,
        SheriffVoteVote = 103,
        SheriffSpeech = 104,
        SheriffPK = 105,
        OwnerSheriffSelect = 106,
        WithdrawOrReveal = 107,  // 退水自爆

        // === Day Voting (110-113) ===
        SheriffRecommendVote = 110,
        VoteOut = 111,
        OwnerVoteSelect = 112,
        VotedOutReveal = 113, // Reveal phase after first voteout result
        // === Death Handling (150-154) ===
        SheriffHandover = 150,
        HunterKill = 151,
        DeathAnnouncement = 152,
        SheriffChooseDirection = 153,
        VoteResult = 154,
        VotedOutAnnouncement = 155,

        // === Announcements (1000-1020) ===
        CheckPrivate = 1000,
        NightTime = 1001,
        DayTime = 1002,
        GameOver = 1003,
        PutDownDevice = 1020,

        // === Skill Use Announcement (1100) ===
        SkillUseAnnouncement = 1100,
    }
}
