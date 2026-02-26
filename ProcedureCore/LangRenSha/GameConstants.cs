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
        GameBeginAnnouncement = 5,
        RoleCheck = 6,
        PutDownDevice = 7,
        NightTimeAnnouncement = 8,

        ShenLangGongWu1_OpenEyes = 10,
        ShenLangGongWu1_Check = 11,
        ShenLangGongWu1_CloseEyes = 12,

        // === DaMao (20-22) ===
        DaMao_OpenEyes = 20,
        DaMao_Act = 21,
        DaMao_CloseEyes = 22,

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

        // === LieRen (170-172) ===
        LieRen_OpenEyes = 170,
        LieRen_Act = 171,
        LieRen_CloseEyes = 172,

        // === LangQiang (175-177) ===
        LangQiang_OpenEyes = 175,
        LangQiang_Act = 176,
        LangQiang_CloseEyes = 177,

        // === Xiong (180-182) ===
        Xiong_OpenEyes = 180,
        Xiong_Act = 181,
        Xiong_CloseEyes = 182,

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

        // === Death Handling (97-100) ===
        DeathHandlingInterrupt = 97,
        DeadPlayerSkillsProcessing = 98,
        DeadPlayerSheriffHandover = 99,
        DeadPlayerSpeak = 100,
    }
}
