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

        // === Thief (15-18) ===
        Thief_OpenEyes = 15,
        Thief_PickRole = 16,
        Thief_ShowAttackStatus = 17,
        Thief_CloseEyes = 18,

        // === HunZi (20-22) ===
        HunZi_OpenEyes = 20,
        HunZi_Act = 21,
        HunZi_CloseEyes = 22,

        // === JiXieLang (24-27, 105-108) ===
        JiXieLang_OpenEyes = 24,
        JiXieLang_Act = 25,
        JiXieLang_Info = 26,
        JiXieLang_CloseEyes = 27,
        JiXieLang_ActAgain_OpenEyes = 105,
        JiXieLang_ActAgain = 106,
        JiXieLang_ActAgain_Info = 107,
        JiXieLang_ActAgain_CloseEyes = 108,

        // === YingZi (30-33) ===
        YingZi_OpenEyes = 30,
        YingZi_Act = 31,
        YingZi_Info = 32,
        YingZi_CloseEyes = 33,

        // === DaMao (35-37) ===
        DaMao_OpenEyes = 35,
        DaMao_Act = 36,
        DaMao_CloseEyes = 37,

        // === LaoShu (40-48) ===
        LaoShu_OpenEyes = 40,
        LaoShu_Tag = 41,
        LaoShu_CloseEyes = 42,
        LaoShu_LuckyOneOpenEyes = 43,
        LaoShu_CheckMice = 44,
        LaoShu_LuckyOneCloseEyes = 45,
        LaoShu_GiftOpenEyes = 46,
        LaoShu_GiftAct = 47,
        LaoShu_GiftCloseEyes = 48,

        // === SheMengRen (50-52) ===
        SheMengRen_OpenEyes = 50,
        SheMengRen_Act = 51,
        SheMengRen_CloseEyes = 52,

        // === ShouWei (55-57) ===
        ShouWei_OpenEyes = 55,
        ShouWei_Act = 56,
        ShouWei_CloseEyes = 57,

        // === WuZhe (69-71, 130) ===
        WuZhe_OpenEyes = 69,
        WuZhe_Act = 70,
        WuZhe_CloseEyes = 71,
        WuZhe_Dance = 130,

        // === JiaMian (79-82) ===
        JiaMian_OpenEyes = 79,
        JiaMian_ChaYan = 80,
        JiaMian_Reverse = 81,
        JiaMian_CloseEyes = 82,

        // === LangRen (99-102, 200) ===
        LangRen_OpenEyes = 99,
        LangRen_SelectTarget = 100,
        LangRen_ConfirmKill = 101,
        LangRen_CloseEyes = 102,
        LangRen_Kill = 200,

        // === NvWu (119-121) ===
        NvWu_OpenEyes = 119,
        NvWu_Act = 120,
        NvWu_CloseEyes = 121,

        // === YuYanJia (149-152) ===
        YuYanJia_OpenEyes = 149,
        YuYanJia_ChaYan = 150,
        YuYanJia_Result = 151,
        YuYanJia_CloseEyes = 152,

        // === TongLingShi (153-156) ===
        TongLingShi_OpenEyes = 153,
        TongLingShi_ChaYan = 154,
        TongLingShi_Result = 155,
        TongLingShi_CloseEyes = 156,

        // === Xiong (160-162, 230) ===
        Xiong_OpenEyes = 160,
        Xiong_Act = 161,
        Xiong_CloseEyes = 162,
        Xiong_BarkCheck = 290,

        // === LieRen (270-272) ===
        LieRen_OpenEyes = 270,
        LieRen_Act = 271,
        LieRen_CloseEyes = 272,

        // === LangQiang (275-277) ===
        LangQiang_OpenEyes = 275,
        LangQiang_Act = 276,
        LangQiang_CloseEyes = 277,

        // === FuChouZhe (280-283) ===
        FuChouZhe_OpenEyes = 280,
        FuChouZhe_ThirdPartyKill = 281,
        FuChouZhe_DeadShoot = 282,
        FuChouZhe_CloseEyes = 283,

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

        // === Death Handling (97-101) ===
        DeathHandlingInterrupt = 97,
        DeadPlayerSkillsProcessing = 98,
        DeadPlayerSheriffHandover = 99,
        DeadPlayerSpeak = 100,
        MengMianRenDeath = 101,
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

        // === NvWu (3) ===
        NvWu_Act = 3,

        // === WuZhe (4) ===
        WuZhe_Act = 4,

        // === JiaMian (5-6) ===
        JiaMian_OpenEyes = 5,
        JiaMian_ChaYan = 6,

        // === ShouWei (70) ===
        ShouWei_Act = 70,

        // === YingZi (78-79) ===
        YingZi_Act = 78,
        YingZi_Info = 79,

        // === FuChouZhe (80-82) ===
        FuChouZhe_DeadShoot = 80,
        FuChouZhe_ThirdPartyKill = 81,
        FuChouZhe_AllianceInfo = 82,

        // === SheMengRen (8) ===
        SheMengRen_Act = 8,

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

        // === Open/Close Eyes Announcements (50-55) ===
        OpenEyes = 50,
        CloseEyes = 51,
        LuckyOneOpenEyes = 52,
        LuckyOneCloseEyes = 53,
        ConvertedOpenEyes = 54,
        ConvertedCloseEyes = 55,

        // === DaMao (62) ===
        DaMao_Act = 62,

        // === LaoShu (72, 75-76) ===
        LaoShu_Tag = 72,
        LaoShu_CheckMice = 75,
        LaoShu_GiftedPoison = 76,

        // === MengMianRen (77) ===
        MengMianRen_Death = 77,

        // === Sheriff Election (100-107) ===
        SheriffVolunteer = 100,
        SheriffVote = 101,
        RoundTable = 102,
        SheriffVoteVote = 103,
        SheriffSpeech = 104,
        SheriffPK = 105,
        OwnerSheriffSelect = 106,
        WithdrawOrReveal = 107,  // 退水自爆

        // === Day Voting (110-112) ===
        SheriffRecommendVote = 110,
        VoteOut = 111,
        OwnerVoteSelect = 112,
        // === Death Handling (150-154) ===
        SheriffHandover = 150,
        HunterKill = 151,
        DeathAnnouncement = 152,
        SheriffChooseDirection = 153,
        VoteResult = 154,

        // === Announcements (1000-1020) ===
        CheckPrivate = 1000,
        NightTime = 1001,
        DayTime = 1002,
        GameOver = 1003,
        PutDownDevice = 1020,
    }
}
