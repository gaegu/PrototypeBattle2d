using System;
using System.Collections.Generic;

public struct PrologueBridge
{
    #region Coding rule : Property
    #endregion Coding rule : Property

    #region Coding rule : Value
    private Dictionary<PrologueSequenceType, Type> prologueTypeDic;
    #endregion Coding rule : Value

    #region Coding rule : Function
    public Dictionary<PrologueSequenceType, Type> GetBridgeDic()
    {
        prologueTypeDic = new Dictionary<PrologueSequenceType, Type>()
        {
            {PrologueSequenceType.PlayVideo, typeof(VideoPrologueSequence)},
            {PrologueSequenceType.PlayCutscene, typeof(CutscenePrologueSequence)},
            {PrologueSequenceType.TownFlow, typeof(TownFlowPrologueSequence)},
            {PrologueSequenceType.Battle, typeof(BattlePrologueSequence)},
            {PrologueSequenceType.InstantTalk, typeof(InstantTalkPrologueSequence)},
            {PrologueSequenceType.AdditivePrefab, typeof(AdditivePrefabPrologueSequence)},
            {PrologueSequenceType.CinemachineTimeline, typeof(CinemachineTimelinePrologueSequence)},
            {PrologueSequenceType.UIEnter, typeof(UIEnterPrologueSequence)},
            {PrologueSequenceType.PVBattleLoading, typeof(PVBattleLoadingPrologueSequence)},
            {PrologueSequenceType.StoryScene, typeof(StoryScenePrologueSequence)},
            {PrologueSequenceType.CreateNickname , typeof(CreateNicknamePrologueSequence)},
            {PrologueSequenceType.Transition, typeof(TransitionPrologueSequence)},
        };

        return prologueTypeDic;
    }
    #endregion Coding rule : Function
}
