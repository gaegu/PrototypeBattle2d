using Cinemachine;
using Cysharp.Threading.Tasks;
using IronJade.LowLevel.Server.Web;
using IronJade.Server.Web.Core;
using IronJade.Table.Data;
using IronJade.UI.Core;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;




public interface ITownSceneService
{
    // 기본 기능
    UniTask ShowTown(bool isShow);
    UniTask PlayLobbyMenu(bool isShow);
    UniTask RefreshAsync(UIType uiType);
    UniTask UpdateLobbyMenu();

    // UI 전환
    UniTask ChangeViewAsync(UIType uiType, UIType prevUIType);
    UniTask ChangePopupAsync(UIType uiType, UIType prevUIType);

    // 상호작용
    UniTask TalkInteracting(bool isTalk);
    UniTask TutorialStepAsync(TutorialExplain tutorialExplain);
    void SetEventInteraction(Func<TownTalkInteraction, UniTask> eventFunc);

    // 필드맵
    void ShowFieldMapName(FieldMapDefine fieldMap, bool isDelay = false);
    void ShowTownCanvas();

    // 사운드
    void PlayBGM();
    void StopBGM();

    // 입력
    void SetTownInput(Camera camera);
    void SetActiveTownInput(bool isActive);
    TownInputSupport TownInputSupport { get; }

    // 컷씬
    void PlayCutsceneState(bool isPlay);

    // 프로퍼티
    bool IsLoaded { get; }
}

   
public interface IPlayerService
{
    // 플레이어 관리
    Player MyPlayer { get; }
   // UserSetting UserSetting { get; }

    // 플레이어 생성
    UniTask CreateMyPlayerUnit();
    UniTask LoadMyPlayerCharacterObject();
    UniTask DestroyTownMyPlayerCharacter();
    UniTask DestroyTownOtherPlayer();

    // 스폰
    void SetMyPlayerSpawn(TownObjectType objectType, string targetId,
                          bool isLeaderPosition, bool isUpdateTargetPosition = false);
    void ShowMyPlayerSpawnEffect(bool isShow);

    // 캐릭터 변경
    void SetTempLeaderCharacter(int characterId);
    void ResetTempLeaderCharacter();
    bool CheckFixedAllyTeam(DungeonTableData dungeonData);
    bool CheckFixedAllyHaveTeam(DungeonTableData dungeonData);

    // 위치
    void UpdateLeaderCharacterPosition();
    void CancelLeaderCharacterPosition();
    bool CheckMleofficeIndoorFiled();

    // 로드/저장
    UniTask LoadDummyUserSetting();
    UniTask<bool> SaveUserSetting(UserSettingModel.Save saveType, object data);
}

public interface ITownObjectService
{
    // 오브젝트 검색
    ITownSupport GetTownObjectByEnumId(string enumId);
    ITownSupport GetTownObjectByDataId(int dataId);
    ITownSupport GetTownObjectByType(TownObjectType type, string targetId);

    // 프로세스
    UniTask StartProcessAsync();
    UniTask RefreshProcess(bool isMissionUpdate = false);

    // 오브젝트 관리
    void ClearTownObjects();
    void DestroyTownObject();
    void SetConditionRoad();

    // NPC 정보
    void LoadAllTownNpcInfoGroup();
    void UnloadAllTownNpcInfoGroup();
    bool IsMissionRelatedNpc(FieldMapDefine fieldMap, int npcId);

    // 데코레이터
    void OperateDecoratorFactory();

    // 태그
    void OnEventClearTownTag();
}

public interface IMissionService
{
    #region Properties

    bool IsWait { get; }
    bool IsAutoMove { get; }
    bool IsExistMissionBehavior { get; }
    bool IsShowWarpNextStory { get; }
    bool IsQuestRewardProcess { get; }
    MissionBehaviorBuffer MissionBehaviorBuffer { get; }
    MissionWaitState WaitState { get; }

    #endregion

    #region Mission Update & State Management

    void UpdateMission();
    UniTask StartRequestUpdateStoryQuestProcess(bool isDialog);
    void SetWaitUpdateTownState(bool isWait);
    void SetWaitWarpProcessState(bool isWait);
    void NotifyStoryQuestTracking();
    void AddWaitState(MissionWaitState state);
    void RemoveWaitState(MissionWaitState state);
    bool CheckWaitState(MissionWaitState state);

    #endregion

    #region Delayed Processing

    bool CheckHasDelayedStory(bool isStart);
    bool CheckHasDelayedPopup();
    bool CheckHasDelayedInstantTalk();
    bool CheckHasDelayed();
    bool CheckHasDelayedNotifyQuest();
    bool CheckDailyDelayed();
    bool CheckHasWarpProcessByBehaviorBuffer();
    void ResetDelayedNotifyUpdateQuest();
    void ResetDelayedPopupByQuest();

    #endregion

    #region Mission Process

    UniTask StartAutoProcess(TransitionType? transitionType = null, bool isNotify = true, bool isNextQuest = false);
    UniTask StartDelayedPopupProcess();
    UniTask StartBehaviorProcess();
    UniTask SendDelayedInstantMessage();
    void ResetMissionBehavior();
    void ShowWarpNextStory(bool show);
    void SetMissionBehavior(MissionBehaviorBuffer buffer);
    void LoadBehaviorByUserSetting();

    #endregion

    #region Auto Move

    void SetAutoMove(bool isAutoMove);
    void OnEventAutoMove(bool isHome = false);

    #endregion

    #region Quest Management

    UniTask SetTrackingDataId(int dataId);
    UniTask RequestAccept(BaseMission mission, bool isNotify = true);
    UniTask RequestReward(BaseMission mission, bool isNotify = true);
    UniTask RequestCancel(BaseMission mission);
    UniTask AddCountOnCondition(IMissionCondition condition);
    UniTask<bool> CheckAcceptFirstStoryQuest(bool isNotify);
    UniTask<bool> CheckAcceptFirstSubStoryQuest(bool isNotify);
    UniTask<bool> CheckAcceptFirstCharacterQuest(bool isNotify);

    #endregion

    #region Mission Data Access

    BaseMission GetMission(MissionContentType contentType, int dataId);
    BaseMission GetMission(int dataId);
    IEnumerable<BaseMission> GetMissions(params MissionContentType[] types);
    IEnumerable<BaseMission> GetUnAcceptedMissions(params MissionContentType[] types);
    IEnumerable<BaseMission> GetRewardReadyMissions(params MissionContentType[] types);
    IEnumerable<BaseStoryQuest> GetActiveMarkQuests();
    BaseMission GetTrackingMission(MissionContentType type);
    StoryQuest GetCurrentTimeline();
    int GetMissionCount(MissionContentType type, MissionProgressState progressState);
    int GetNextMissionDataId(MissionContentType type, int dataId);

    #endregion

    #region Mission Progress & Type

    MissionProgressState GetMissionProgress(MissionContentType type, int dataId);
    MissionProgressState GetMissionProgress(int dataId);
    MissionContentType GetContentType(int dataId);
    Enum GetMissionType(MissionContentType type, int dataId);
    string GetPriorityMissionTypeIcon(List<BaseTownInteraction> interactions);
    string GetMissionTypeIcon(BaseTownInteraction interaction);

    #endregion

    #region Mission Check Status

    bool CheckMissionClear(int conditionDataId);
    bool CheckClearStoryQuest(int questDataId);
    bool CheckFirstEpisodeClear();
    bool CheckMissionProgressState(TownNpcMissionCondition missionCondition);
    bool CheckMissionProgressState(MissionContentType type, int dataId, MissionProgressState progressState);
    bool CheckStoryQuestOpenCondition(int dataStoryQuestId);
    bool CheckSubStoryQuestOpenCondition(int dataSubStoryQuestId);
    bool CheckCharacterQuestOpenCondition(int dataCharacterQuestId);
    bool CheckCharacterLikeabilityLevelCondition(int dataCharacterId, int level);

    #endregion

    #region Red Dot & New Quest

    bool CheckHasMissionRedDot();
    bool CheckHasStoryQuestRedDot();
    bool CheckHasQuestRedDot(MissionContentType missionContentType);
    bool CheckNewQuest();
    bool CheckNewQuest(QuestContentType questContentType);

    #endregion

    #region Story Quest Result

    StoryQuestResult GetExpectedResultByStoryQuest();
    IEnumerable<(int dataStoryQuestId, bool isStart)> DequeueDelayedStories(bool isStart);
    IEnumerable<int> GetStoryQuestDataIdByDelayedStory(bool isStart);
    void SetStorySceneDataIds(ref List<int> dataIds, StoryQuestTableData storyQuestData, bool isStart);
    void SetSubStorySceneDataIds(ref List<int> dataIds, SubStoryQuestTableData storyQuestData, bool isStart);

    #endregion

    #region Network Requests

    UniTask RequestAllTownQuestGet();
    UniTask RefreshTownQuest(bool isAutoMove);
    UniTask<bool> RequestGet(MissionContentType type);
    UniTask RequestDailyQuestByResetTimePassing();

    #endregion

    #region Event Settings

    void SetEventPlayLobbyMenu(Func<bool, UniTask> eventFunc);
    void SetEventInput(Action<bool> eventFunc);
    void SetEventQuestTracking(Action<BaseStoryQuest, QuestTrackingUIState> eventFunc);
    void SetEventShowInstantTalk(Func<InstantTalkTriggerType, List<int>, bool, UniTask> eventFunc);

    #endregion

    #region NPC Interaction

    bool SetNpcInteraction(ref BaseTownInteraction interaction);

    #endregion

    #region Character Management

    int GetCurrentMissionCharacter();

    #endregion

    #region Save & Reset

    bool CheckQuestDailyReset(QuestContentType questContentType, bool isSave);
    bool CheckQuestAutoMoveWarningWeeklyReset(bool isSave);

    #endregion

    #region Progress Model Access

    IMissionProgressSet GetProgressModel(MissionContentType type);
    T GetProgressModel<T>() where T : class, IMissionProgressSet;
    void InitializeModel<T>(MissionContentType type, IEnumerable<T> dtos) where T : IMissionDto;
    void UpdateMission<T>(MissionContentType type, IEnumerable<T> dtos) where T : IMissionDto;
    void UpdateMission<T>(MissionContentType type, T dto) where T : IMissionDto;

    #endregion

    #region Game Event

    GameEventDto GetGameEventDto(int id);

    #endregion

    #region Contents Open

    ContentsOpenDefine GetContentsOpenDefine(Mission mission);

    #endregion
}


public interface IResourceService
{ // 씬 관리
    UniTask<Scene> LoadSceneAsync(string sceneName, LoadSceneMode mode);
    UniTask UnLoadSceneAsync(string sceneName);
    bool CheckLoadedScene(string sceneName);
    Scene GetScene(string sceneName);

    // 메모리 관리
    UniTask UnloadUnusedAssets(bool isGC = false);

    // 배치 작업
    UniTask LoadScenesAsync(string[] sceneNames, LoadSceneMode mode);
    UniTask UnloadScenesAsync(string[] sceneNames);

    // 씬 전환
    UniTask<Scene> TransitionSceneAsync(string fromScene, string toScene);
}

public interface IBackgroundSceneService
{
    // 타운 그룹
    void ShowTownGroup(bool isShow);
    void AddTownObjects(FieldMapDefine fieldMap);
    bool CheckActiveTown();

    // 뷰 전환
    UniTask ChangeViewAsync(UIType uiType, UIType prevUIType);
    UniTask ChangePopupAsync(UIType uiType, UIType prevUIType);

    // 데코레이터
    void OperateTownDecoratorFactory();

    // 시네머신
    void SetCinemachineFollowTarget();

    // 컷씬
    void PlayCutsceneState(bool isShow, bool isTownCutscene);

    // 프로퍼티
    Transform TownObjectParent { get; }
}

public interface ICameraService
{
    // 카메라 관리
    Transform TownCamera { get; }
    Camera GetCamera(GameCameraType type);
    void SetActiveTownCameras(bool isActive);

    // 브레인
    void SetEnableBrain(bool isEnable);
    CinemachineBrain GetBrainCamera();

    // DOF
    void RestoreDofTarget();

    // 시네머신
    void SetLiveVirtualCamera();
    UniTask WaitCinemachineClearShotBlend();

    // 대화 카메라
    void SetTalkCamera(bool isTalk);

    // 볼륨
    void ChangeVolumeType(VolumeType volumeType);
    void ChangeVolumeType(string scenePath, VolumeType defaultType);
    void ChangeFreeLockVolume();

    // Additive Prefab
    void SetAdditivePrefabCameraState(UIType uiType);

    // 프로퍼티
    float RecomposerPan { get; }

    // 렌더
    UniTask ShowBackgroundRenderTexture(bool isShow);
    void RestoreCharacterCameraCullingMask();
}


public interface IUIService
{
    // UI 관리
    UniTask EnterAsync(UIType uiType);
    UniTask EnterAsync(BaseController controller);
    UniTask Exit(UIType uiType);
    UniTask Exit(BaseController controller);
    UniTask<bool> BackAsync();
    void Home();

    // UI 체크
    bool CheckOpenUI(UIType uiType);
    bool CheckOpenCurrentUI(UIType uiType);
    bool CheckOpenCurrentView(UIType uiType);
    bool CheckBlockEscapeUI(UIType uiType);
    bool CheckExitByEscapeUITypes(UIType uiType);
    bool CheckApplication(UIType uiType);

    // 네비게이터
    Navigator GetCurrentNavigator();

    BaseController GetController(UIType uiType);
    BaseController GetNoneStackController(UIType uiType);

    // 스택
    bool CheckPrevStackNavigator();
    void SavePrevStackNavigator();
    UniTask LoadPrevStackNavigator();
    void RemovePrevStackNavigator();
    void DeleteAllUI();

    // 이벤트
    void SetEventUIProcess(Func<UniTask> onEventHome,
                          Func<UIState, UISubState, UIType, UIType, UniTask> onEventChangeState);

    // 애플리케이션
    void EnableApplicationFrame(UIType uiType);
    void ChangeResolution(UIType uiType);

    // 로그인
    void HideLoginUI();

    // 터치
    void ShowTouchScreen(Action onTouch);
    void HideTouchScreen();

    // 백 타겟
    UniTask BackToTarget(UIType targetUIType, bool isAnimation);
}

public interface IUIServiceExtended : IUIService
{
    bool CheckContinuousEnterQueue();
    bool CheckOpenedPopup();
    UniTask EnterAsyncFromQueue();
    void AddContinuousEnterQueue(BaseController controller);
    void SetSpecificLastUIExitEvent(Func<UniTask> onEvent);
}



public interface INetworkService
{
    // 웹 프로세스
    BaseProcess GetWebProcess(WebProcess processType);
    UniTask<bool> RequestAsync(BaseProcess process);

    // 넷마이닝
    UniTask RequestNetMining();

    // 레드닷
    UniTask RequestReddot();
    UniTask MailRedDot();
    UniTask GuildRedDot();
    UniTask DispatchRedDot();

    // 시즌
    UniTask RequestUpdateSeason(SeasonType seasonType);

    // 이벤트
    UniTask CheckAndUpdateEvents();
}

