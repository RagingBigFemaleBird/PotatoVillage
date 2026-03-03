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

        // === DaMao (25-27) ===
        DaMao_OpenEyes = 25,
        DaMao_Act = 26,
        DaMao_CloseEyes = 27,

        // === LaoShu (30-38) ===
        LaoShu_OpenEyes = 30,
        LaoShu_Tag = 31,
        LaoShu_CloseEyes = 32,
        LaoShu_LuckyOneOpenEyes = 33,
        LaoShu_CheckMice = 34,
        LaoShu_LuckyOneCloseEyes = 35,
        LaoShu_GiftOpenEyes = 36,
        LaoShu_GiftAct = 37,
        LaoShu_GiftCloseEyes = 38,

        // === SheMengRen (40-42) ===
        SheMengRen_OpenEyes = 40,
        SheMengRen_Act = 41,
        SheMengRen_CloseEyes = 42,

        // === WuZhe (59-61, 130) ===
        WuZhe_OpenEyes = 59,
        WuZhe_Act = 60,
        WuZhe_CloseEyes = 61,
        WuZhe_Dance = 130,

        // === JiaMian (69-72) ===
        JiaMian_OpenEyes = 69,
        JiaMian_ChaYan = 70,
        JiaMian_Reverse = 71,
        JiaMian_CloseEyes = 72,

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

        // === Xiong (160-162) ===
        Xiong_OpenEyes = 160,
        Xiong_Act = 161,
        Xiong_CloseEyes = 162,

        // === LieRen (270-272) ===
        LieRen_OpenEyes = 270,
        LieRen_Act = 271,
        LieRen_CloseEyes = 272,

        // === LangQiang (275-277) ===
        LangQiang_OpenEyes = 275,
        LangQiang_Act = 276,
        LangQiang_CloseEyes = 277,

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
        // === Sheriff Election (0-6) ===
        SheriffVolunteer = 0,
        SheriffSpeech = 1,
        SheriffVoteTally = 2,
        SheriffVoteResult = 3,
        SheriffPKSpeech = 4,
        SheriffPKVote = 5,
        SheriffPKResult = 6,

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

        // === SheMengRen (8) ===
        SheMengRen_Act = 8,

        // === Xiong (9) ===
        Xiong_Act = 9,

        // === Thief (10) ===
        Thief_PickRole = 10,

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

        // === Sheriff Election (100-105) ===
        SheriffVolunteer = 100,
        SheriffVote = 101,
        RoundTable = 102,
        SheriffVoteVote = 103,
        SheriffSpeech = 104,
        SheriffPK = 105,

        // === Day Voting (110-111) ===
        SheriffRecommendVote = 110,
        VoteOut = 111,

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
