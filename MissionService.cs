using Cysharp.Threading.Tasks;
using IronJade.LowLevel.Server.Web;
using IronJade.Server.Web.Core;
using IronJade.Table.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// MissionManager의 완전한 서비스 래핑
/// 기존 MissionManager의 모든 기능을 서비스 인터페이스로 제공
/// </summary>
public class MissionService : IMissionService
{
    private MissionManager manager => MissionManager.Instance;

    #region Properties

    public bool IsWait => manager?.IsWait ?? false;
    public bool IsAutoMove => manager?.IsAutoMove ?? false;
    public bool IsExistMissionBehavior => manager?.IsExistMissionBehavior ?? false;
    public bool IsShowWarpNextStory => manager?.IsShowWarpNextStory ?? false;
    public bool IsQuestRewardProcess => manager?.IsQuestRewardProcess ?? false;
    public MissionBehaviorBuffer MissionBehaviorBuffer => manager?.MissionBehaviorBuffer ?? default;
    public MissionWaitState WaitState => manager?.WaitState ?? MissionWaitState.None;

    #endregion

    #region Mission Update & State Management

    public void UpdateMission()
    {
        manager?.UpdateMission();
    }

    public async UniTask StartRequestUpdateStoryQuestProcess(bool isDialog)
    {
        if (manager != null)
            await manager.StartRequestUpdateStoryQuestProcess(isDialog);
    }

    public void SetWaitUpdateTownState(bool isWait)
    {
        manager?.SetWaitUpdateTownState(isWait);
    }

    public void SetWaitWarpProcessState(bool isWait)
    {
        manager?.SetWaitWarpProcessState(isWait);
    }

    public void NotifyStoryQuestTracking()
    {
        manager?.NotifyStoryQuestTracking();
    }

    public void AddWaitState(MissionWaitState state)
    {
        manager?.AddWaitState(state);
    }

    public void RemoveWaitState(MissionWaitState state)
    {
        manager?.RemoveWaitState(state);
    }

    public bool CheckWaitState(MissionWaitState state)
    {
        return manager?.CheckWaitState(state) ?? false;
    }

    #endregion

    #region Delayed Processing

    public bool CheckHasDelayedStory(bool isStart)
    {
        return manager?.CheckHasDelayedStory(isStart) ?? false;
    }

    public bool CheckHasDelayedPopup()
    {
        return manager?.CheckHasDelayedPopup() ?? false;
    }

    public bool CheckHasDelayedInstantTalk()
    {
        return manager?.CheckHasDelayedInstantTalk() ?? false;
    }

    public bool CheckHasDelayed()
    {
        return manager?.CheckHasDelayed() ?? false;
    }

    public bool CheckHasDelayedNotifyQuest()
    {
        return manager?.CheckHasDelayedNotifyQuest() ?? false;
    }

    public bool CheckDailyDelayed()
    {
        return manager?.CheckDailyDelayed() ?? false;
    }

    public bool CheckHasWarpProcessByBehaviorBuffer()
    {
        return manager?.CheckHasWarpProcessByBehaviorBuffer() ?? false;
    }

    public void ResetDelayedNotifyUpdateQuest()
    {
        manager?.ResetDelayedNotifyUpdateQuest();
    }

    public void ResetDelayedPopupByQuest()
    {
        manager?.ResetDelayedPopupByQuest();
    }

    #endregion

    #region Mission Process

    public async UniTask StartAutoProcess(TransitionType? transitionType = null, bool isNotify = true, bool isNextQuest = false)
    {
        if (manager != null)
        {
            if (transitionType.HasValue)
                await manager.StartAutoProcess(transitionType.Value, isNotify, isNextQuest);
            else
                await manager.StartAutoProcess(TransitionType.None, isNotify, isNextQuest);
        }
    }

    public async UniTask StartDelayedPopupProcess()
    {
        if (manager != null)
            await manager.StartDelayedPopupProcess();
    }

    public async UniTask StartBehaviorProcess()
    {
        if (manager != null)
            await manager.StartBehaviorProcess();
    }

    public async UniTask SendDelayedInstantMessage()
    {
        if (manager != null)
            await manager.SendDelayedInstantMessage();
    }

    public void ResetMissionBehavior()
    {
        manager?.ResetMissionBehavior();
    }

    public void ShowWarpNextStory(bool show)
    {
        manager?.ShowWarpNextStory(show);
    }

    public void SetMissionBehavior(MissionBehaviorBuffer buffer)
    {
        manager?.SetMissionBehavior(buffer);
    }

    public void LoadBehaviorByUserSetting()
    {
        manager?.LoadBehaviorByUserSetting();
    }

    #endregion

    #region Auto Move

    public void SetAutoMove(bool isAutoMove)
    {
        manager?.SetAutoMove(isAutoMove);
    }

    public void OnEventAutoMove(bool isHome = false)
    {
        manager?.OnEventAutoMove(isHome);
    }

    #endregion

    #region Quest Management

    public async UniTask SetTrackingDataId(int dataId)
    {
        if (manager != null)
            await manager.SetTrackingDataId(dataId);
    }

    public async UniTask RequestAccept(BaseMission mission, bool isNotify = true)
    {
        if (manager != null && mission != null)
            await manager.RequestAccept(mission, isNotify);
    }

    public async UniTask RequestReward(BaseMission mission, bool isNotify = true)
    {
        if (manager != null && mission != null)
            await manager.RequestReward(mission, isNotify);
    }

    public async UniTask RequestCancel(BaseMission mission)
    {
        if (manager != null && mission != null)
            await manager.RequestCancel(mission);
    }

    public async UniTask AddCountOnCondition(IMissionCondition condition)
    {
        if (manager != null && condition != null)
            await manager.AddCountOnCondition(condition);
    }

    public async UniTask<bool> CheckAcceptFirstStoryQuest(bool isNotify)
    {
        if (manager != null)
            return await manager.CheckAcceptFirstStoryQuest(isNotify);
        return false;
    }

    public async UniTask<bool> CheckAcceptFirstSubStoryQuest(bool isNotify)
    {
        if (manager != null)
            return await manager.CheckAcceptFirstSubStoryQuest(isNotify);
        return false;
    }

    public async UniTask<bool> CheckAcceptFirstCharacterQuest(bool isNotify)
    {
        if (manager != null)
            return await manager.CheckAcceptFirstCharacterQuest(isNotify);
        return false;
    }

    #endregion

    #region Mission Data Access

    public BaseMission GetMission(MissionContentType contentType, int dataId)
    {
        return manager?.GetMission(contentType, dataId);
    }

    public BaseMission GetMission(int dataId)
    {
        return manager?.GetMission(dataId);
    }

    public IEnumerable<BaseMission> GetMissions(params MissionContentType[] types)
    {
        return manager?.GetMissions(types) ?? Enumerable.Empty<BaseMission>();
    }

    public IEnumerable<BaseMission> GetUnAcceptedMissions(params MissionContentType[] types)
    {
        return manager?.GetUnAcceptedMissions(types) ?? Enumerable.Empty<BaseMission>();
    }

    public IEnumerable<BaseMission> GetRewardReadyMissions(params MissionContentType[] types)
    {
        return manager?.GetRewardReadyMissions(types) ?? Enumerable.Empty<BaseMission>();
    }

    public IEnumerable<BaseStoryQuest> GetActiveMarkQuests()
    {
        return manager?.GetActiveMarkQuests() ?? Enumerable.Empty<BaseStoryQuest>();
    }

    public BaseMission GetTrackingMission(MissionContentType type)
    {
        return manager?.GetTrackingMission(type);
    }

    public StoryQuest GetCurrentTimeline()
    {
        return manager?.GetCurrentTimeline();
    }

    public int GetMissionCount(MissionContentType type, MissionProgressState progressState)
    {
        return manager?.GetMissionCount(type, progressState) ?? 0;
    }

    public int GetNextMissionDataId(MissionContentType type, int dataId)
    {
        return manager?.GetNextMissionDataId(type, dataId) ?? 0;
    }

    #endregion

    #region Mission Progress & Type

    public MissionProgressState GetMissionProgress(MissionContentType type, int dataId)
    {
        return manager?.GetMissionProgress(type, dataId) ?? MissionProgressState.UnAccepted;
    }

    public MissionProgressState GetMissionProgress(int dataId)
    {
        return manager?.GetMissionProgress(dataId) ?? MissionProgressState.UnAccepted;
    }

    public MissionContentType GetContentType(int dataId)
    {
        return manager?.GetContentType(dataId) ?? MissionContentType.None;
    }

    public Enum GetMissionType(MissionContentType type, int dataId)
    {
        return manager?.GetMissionType(type, dataId);
    }

    public string GetPriorityMissionTypeIcon(List<BaseTownInteraction> interactions)
    {
        return manager?.GetPriorityMissionTypeIcon(interactions);
    }

    public string GetMissionTypeIcon(BaseTownInteraction interaction)
    {
        return manager?.GetMissionTypeIcon(interaction);
    }

    #endregion

    #region Mission Check Status

    public bool CheckMissionClear(int conditionDataId)
    {
        return manager?.CheckMissionClear(conditionDataId) ?? false;
    }

    public bool CheckClearStoryQuest(int questDataId)
    {
        return manager?.CheckClearStoryQuest(questDataId) ?? false;
    }

    public bool CheckFirstEpisodeClear()
    {
        return manager?.CheckFirstEpisodeClear() ?? false;
    }

    public bool CheckMissionProgressState(TownNpcMissionCondition missionCondition)
    {
        return manager?.CheckMissionProgressState(missionCondition) ?? false;
    }

    public bool CheckMissionProgressState(MissionContentType type, int dataId, MissionProgressState progressState)
    {
        return manager?.CheckMissionProgressState(type, dataId, progressState) ?? false;
    }

    public bool CheckStoryQuestOpenCondition(int dataStoryQuestId)
    {
        return manager?.CheckStoryQuestOpenCondition(dataStoryQuestId) ?? false;
    }

    public bool CheckSubStoryQuestOpenCondition(int dataSubStoryQuestId)
    {
        return manager?.CheckSubStoryQuestOpenCondition(dataSubStoryQuestId) ?? false;
    }

    public bool CheckCharacterQuestOpenCondition(int dataCharacterQuestId)
    {
        return manager?.CheckCharacterQuestOpenCondition(dataCharacterQuestId) ?? false;
    }

    public bool CheckCharacterLikeabilityLevelCondition(int dataCharacterId, int level)
    {
        return manager?.CheckCharacterLikeabilityLevelCondition(dataCharacterId, level) ?? false;
    }

    #endregion

    #region Red Dot & New Quest

    public bool CheckHasMissionRedDot()
    {
        return manager?.CheckHasMissionRedDot() ?? false;
    }

    public bool CheckHasStoryQuestRedDot()
    {
        return manager?.CheckHasStoryQuestRedDot() ?? false;
    }

    public bool CheckHasQuestRedDot(MissionContentType missionContentType)
    {
        return manager?.CheckHasQuestRedDot(missionContentType) ?? false;
    }

    public bool CheckNewQuest()
    {
        return manager?.CheckNewQuest() ?? false;
    }

    public bool CheckNewQuest(QuestContentType questContentType)
    {
        return manager?.CheckNewQuest(questContentType) ?? false;
    }

    #endregion

    #region Story Quest Result

    public StoryQuestResult GetExpectedResultByStoryQuest()
    {
        return manager?.GetExpectedResultByStoryQuest() ?? new StoryQuestResult();
    }

    public IEnumerable<(int dataStoryQuestId, bool isStart)> DequeueDelayedStories(bool isStart)
    {
        return manager?.DequeueDelayedStories(isStart) ?? Enumerable.Empty<(int, bool)>();
    }

    public IEnumerable<int> GetStoryQuestDataIdByDelayedStory(bool isStart)
    {
        return manager?.GetStoryQuestDataIdByDelayedStory(isStart) ?? Enumerable.Empty<int>();
    }

    public void SetStorySceneDataIds(ref List<int> dataIds, StoryQuestTableData storyQuestData, bool isStart)
    {
        manager?.SetStorySceneDataIds(ref dataIds, storyQuestData, isStart);
    }

    public void SetSubStorySceneDataIds(ref List<int> dataIds, SubStoryQuestTableData storyQuestData, bool isStart)
    {
        manager?.SetSubStorySceneDataIds(ref dataIds, storyQuestData, isStart);
    }

    #endregion

    #region Network Requests

    public async UniTask RequestAllTownQuestGet()
    {
        if (manager != null)
            await manager.RequestAllTownQuestGet();
    }

    public async UniTask RefreshTownQuest(bool isAutoMove)
    {
        if (manager != null)
            await manager.RefreshTownQuest(isAutoMove);
    }

    public async UniTask<bool> RequestGet(MissionContentType type)
    {
        if (manager != null)
            return await manager.RequestGet(type);
        return false;
    }

    public async UniTask RequestDailyQuestByResetTimePassing()
    {
        if (manager != null)
            await manager.RequestDailyQuestByResetTimePassing();
    }

    #endregion

    #region Event Settings

    public void SetEventPlayLobbyMenu(Func<bool, UniTask> eventFunc)
    {
        manager?.SetEventPlayLobbyMenu(eventFunc);
    }

    public void SetEventInput(Action<bool> eventFunc)
    {
        manager?.SetEventInput(eventFunc);
    }

    public void SetEventQuestTracking(Action<BaseStoryQuest, QuestTrackingUIState> eventFunc)
    {
        manager?.SetEventQuestTracking(eventFunc);
    }

    public void SetEventShowInstantTalk(Func<InstantTalkTriggerType, List<int>, bool, UniTask> eventFunc)
    {
        manager?.SetEventShowInstantTalk(eventFunc);
    }

    #endregion

    #region NPC Interaction

    public bool SetNpcInteraction(ref BaseTownInteraction interaction)
    {
        return manager?.SetNpcInteraction(ref interaction) ?? false;
    }

    #endregion

    #region Character Management

    public int GetCurrentMissionCharacter()
    {
        return manager?.GetCurrentMissionCharacter() ?? 0;
    }

    #endregion

    #region Save & Reset

    public bool CheckQuestDailyReset(QuestContentType questContentType, bool isSave)
    {
        return manager?.CheckQuestDailyReset(questContentType, isSave) ?? false;
    }

    public bool CheckQuestAutoMoveWarningWeeklyReset(bool isSave)
    {
        return manager?.CheckQuestAutoMoveWarningWeeklyReset(isSave) ?? false;
    }

    #endregion

    #region Progress Model Access

    public IMissionProgressSet GetProgressModel(MissionContentType type)
    {
        return manager?.GetProgressModel(type);
    }

    public T GetProgressModel<T>() where T : class, IMissionProgressSet
    {
        return manager?.GetProgressModel<T>();
    }

    public void InitializeModel<T>(MissionContentType type, IEnumerable<T> dtos) where T : IMissionDto
    {
        manager?.InitializeModel(type, dtos);
    }

    public void UpdateMission<T>(MissionContentType type, IEnumerable<T> dtos) where T : IMissionDto
    {
        manager?.UpdateMission(type, dtos);
    }

    public void UpdateMission<T>(MissionContentType type, T dto) where T : IMissionDto
    {
        manager?.UpdateMission(type, dto);
    }

    #endregion

    #region Game Event

    public GameEventDto GetGameEventDto(int id)
    {
        return manager?.GetGameEventDto(id) ?? default;
    }

    #endregion

    #region Contents Open

    public ContentsOpenDefine GetContentsOpenDefine(Mission mission)
    {
        return manager?.GetContentsOpenDefine(mission) ?? ContentsOpenDefine.None;
    }

    #endregion
}

