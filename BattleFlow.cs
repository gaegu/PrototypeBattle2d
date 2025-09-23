#pragma warning disable CS1998
using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using IronJade.Flow.Core;
using IronJade.Observer.Core;
using IronJade.UI.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BattleFlow : BaseFlow, IObserver
{
    //============================================================
    //=========    Coding rule에 맞춰서 작업 바랍니다.   =========
    //========= Coding rule region은 절대 지우지 마세요. =========
    //=========    문제 시 '김철옥'에게 문의 바랍니다.   =========
    //============================================================

    // 아래 코드는 필수 코드 입니다.
    public BattleFlowModel Model { get { return GetModel<BattleFlowModel>(); } }
    //=========================================================================================================

    #region Coding rule : Property
    #endregion Coding rule : Property

    #region Coding rule : Value
    private bool isCleanedUp = false;
    #endregion Coding rule : Value

    #region Coding rule : Function

    public override void Enter()
    {
#if UNITY_EDITOR
        IronJade.Debug.Log("Battle : Enter");
#endif

        //GameSettingManager.Instance.GraphicInit();

        UIManager.Instance.SetEventUIProcess(OnEventHome, OnEventChangeState);
        GameManager.Instance.ActiveRendererFeatures(true);
        CameraManager.Instance.SetFreeLookZoomInOut(true);
    }

    public override async UniTask<bool> Back()
    {
        if (UIManager.Instance.CheckOpenCurrentView(UIType.CharacterSkillPreviewView))
            return await UIManager.Instance.BackAsync();

        if (UIManager.Instance.CheckOpenUI(IronJade.UI.Core.UIType.BattleResultChainPopup, false))
            return await UIManager.Instance.BackAsync();

        if (!UIManager.Instance.CheckOpenUI(UIType.BattlePausePopup))
        {
            if (BattleProcessManager.Instance.GetUsePauseBattle())
            {
                BattleSceneManager.Instance.OnClickGamePause();
            }

            return true;
        }

        return await UIManager.Instance.BackAsync();
    }

    public override async UniTask Exit()
    {
#if UNITY_EDITOR
        IronJade.Debug.Log("Battle : Exit");
#endif

        ObserverManager.RemoveObserver(ViewObserverID.Refresh, this);

        // 결과창 닫을때 꺼줌.
        SoundManager.SfxFmod.Stop();

        UIManager.Instance.DeleteAllUI();

        await CleanUpBattle();

        //// 4. 미션 정보 갱신
        //await MissionManager.Instance.StartRequestUpdateStoryQuestProcess(false);

        if (Model.BattleInfo.BattleDungeonType == DungeonType.Training)
            await UtilModel.Resources.UnLoadSceneAsync(StringDefine.SCENE_BATTLECHARACTERREVIEWER);
        else
            await UtilModel.Resources.UnLoadSceneAsync(StringDefine.SCENE_BATTLE);
        
        // 메모리 해제
        await UtilModel.Resources.UnloadUnusedAssets(true);
    }

    private async UniTask CleanUpBattle()
    {
        if (isCleanedUp)
            return;

        if (BattleCameraManager.Instance != null)
            BattleCameraManager.Instance.DestroyBattleResultScreenEffect();

        // 2. 배틀 정보 초기화

        if (BattleProcessManager.Instance != null)
        {
            BattleProcessManager.Instance.ResetUFE(false, false);

            BattleProcessManager.Instance.StopBGM();
        }

        // 3. 배틀 Prefab Fog정보 Clear
        if (BattleProcessManager.Instance != null)
            BattleProcessManager.Instance.ClearPrefabFogSetting();

        if (BattleSceneManager.Instance != null)
        {
            BattleSceneManager.Instance.InitFollowTarget();

            await BattleSceneManager.Instance.UnloadResourse();
        }

        isCleanedUp = true;

        IronJade.Debug.Log("Battle Clean Up");
    }

    public override async UniTask LoadingProcess(System.Func<UniTask> onEventExitPrevFlow)
    {
        // Registration : 로딩 프로세스를 등록 (UniTask or Action)
        // Start        : 로딩 시작 (SceneType, LoadingScene Active)
        // End          : 로딩 종료

        // 1. 던전 타입에 따른 구분
        // 2. 스토리가 있는 경우
        //    -> 스토리 트랜지션 -> 로딩 -> 스토리 진행 -> 인트로 연출
        // 3. 스토리가 없는 경우
        //    -> 던전 트랜지션 -> 로딩 -> 인트로 연출
        // 4. 시퀀스가 있는 경우는 프롤로그인 경우라서 별개
        // 5. 아레나, 스킬미리보기는 별개

        // 씬 로드 및 배틀 공통사항 로드
        System.Func<string, UniTask> waitCommonProcess = async (scenePath) =>
        {
            int loadingCount = 5; // 프롤로그에서 로딩 옵저버를 써야해서 임시처리 (기존에는 로딩매니저에서 했으나 이제는 안써서..)
            ObserverManager.NotifyObserver(CommonObserverID.Loading, new IntParam(loadingCount));

            UIManager.Instance.EnableLetterBox(false);

            // 전투에서 사용하는 FMOD 뱅크를 준비
            await SoundManager.PrepareBattleBanks();

            // =============================================
            // ================== 씬 로딩 ==================
            if (!UtilModel.Resources.CheckLoadedScene(scenePath))
                await UtilModel.Resources.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            SceneManager.SetActiveScene(UtilModel.Resources.GetScene(scenePath));

            ObserverManager.NotifyObserver(CommonObserverID.Loading, null); // 로딩

            // 이전 Flow 닫기
            await onEventExitPrevFlow();
            // =============================================
            // =============================================

            CameraManager.Instance.SetActiveTownCameras(false);
            GameSettingManager.Instance.SetTargetFrame(false);

#if CHEAT
            CheatManager.Instance.CheckNowFrameRate();
#endif
            ObserverManager.NotifyObserver(CommonObserverID.Loading, null); // 로딩

            if (Model.BattleInfo.BattleDungeonType == DungeonType.Training)
                await UIManager.Instance.LoadAsync(UIManager.Instance.GetController(UIType.CharacterSkillPreviewView));
            else
                await UIManager.Instance.LoadAsync(UIManager.Instance.GetController(UIType.BattleView));

            BattleProcessManager.Instance.ClearBattleObjectContaier();

            // 사용할 캐릭터의 뱅크 로딩

            if (Model.BattleInfo.BattleType == BattleType.Raid)
            {
                BattleSceneManager.Instance.SetBattleInfo(Model.BattleInfo);
                BattleRaidProcessManager.Instance.SetBattleInfo(Model.BattleInfo);
                BattleRaidProcessManager.Instance.SetEvent(OnEventHome, OnEventBack, OnEventTeamUpdate, OnEventRetry);

                BattleSceneManager.Instance.ReSetBattleInfo(BattleProcessManager.Instance.BattleInfo);

                // 3. 배틀 배경 프리팹을 로드한다.
                await BattleProcessManager.Instance.LoadBackground(BattleSceneManager.Instance.GetBattleBackGround());

                // 3.1 Raid일경우 wave표시 타임라인을 로드한다.
                await BattleProcessManager.Instance.LoadRaidWaveTimeline();

                // 3.2 Wave MonsterGroup에 Boss가 있을경우 BossEntranceUI를 로드한다.
                await BattleProcessManager.Instance.LoadBossEntranceTimeline();
            }
            else
            {
                BattleSceneManager.Instance.SetBattleInfo(Model.BattleInfo);
                BattleProcessManager.Instance.SetBattleInfo(Model.BattleInfo);
                BattleProcessManager.Instance.SetEvent(OnEventHome, OnEventBack, OnEventTeamUpdate, OnEventRetry);

                BattleSceneManager.Instance.ReSetBattleInfo(BattleProcessManager.Instance.BattleInfo);

                // 3. 배틀 배경 프리팹을 로드한다.
                await BattleProcessManager.Instance.LoadBackground(BattleSceneManager.Instance.GetBattleBackGround());

                // 3.2 Wave MonsterGroup에 Boss가 있을경우 BossEntranceUI를 로드한다.
                await BattleProcessManager.Instance.LoadBossEntranceTimeline();
            }

            ObserverManager.NotifyObserver(CommonObserverID.Loading, null); // 로딩

            // 4. 비활성화 된 채로 배틀 정보를 불러온다.
            //추가..
            CameraManager.Instance.ChangeVolumeType(Model.BattleInfo.DungeonData, VolumeType.Volume_Battle_Type1_Films);
            CameraManager.Instance.OffVolumeBlur();

            BattleSceneManager.Instance.SetFollowTarget();
            BattleSceneManager.Instance.CamareSupportSetCamera();
            BattleCameraManager.Instance.SetMainCamara();

            CameraManager.Instance.SetActiveTownCameras(false);
            UIManager.Instance.EnableApplicationFrame(false);

            // 5. 배틀 배경을 활성화한다.
            BattleSceneManager.Instance.HideSkipButton();
            BattleSceneManager.Instance.CreateBattleSceneUI(false);

            await UniTask.WaitUntil(() => BattleSceneManager.Instance.IsCreateBattleSceneUI());

            ObserverManager.NotifyObserver(CommonObserverID.Loading, null); // 로딩

            SetBattleBridge();

            // 부드럽게 전투와 연결되는 프리팹 연출이 있다면 로드
            await CinemachineTimelineManager.Instance.PrepareBattleTransition(Model.BattleInfo.DungeonData,
                BattleSceneManager.Instance.BattleObjectContainer, BattleSceneManager.Instance.GetBattleStartPosition(true, 0));

            ObserverManager.NotifyObserver(CommonObserverID.Loading, null); // 로딩
            BattleCameraManager.Instance.Init(Model.BattleInfo.BattleType, false);

            // 포트레이트 카메라 우선도보다 높아서 우선도를 임시로 내린다.
            if (BattleManager.Instance.CheckHaveStartStory(Model.BattleInfo.DungeonData))
            {
                BattleHelper.PrepareStorySceneWhileBattle(true);

                if (CinemachineTimelineManager.Instance.IsReady)
                    CinemachineTimelineManager.Instance.ActiveCinemachineUnit(false);
            }

            System.GC.Collect();
        };

        // 환경설정
        System.Func<UniTask> waitSettingProcess = async () =>
        {
            //배틀 배경 프리팹 Fog 적용
            BattleProcessManager.Instance.ApplyBattlePrefabFogSetting();

            if (GameSettingManager.Instance.GraphicSettingModel.OptionData != null)
            {
                BattleEffect(GameSettingManager.Instance.GraphicSettingModel.OptionData.battleEffect);
                UFE.Instance.SetIsBattleEffectQualityDown(GameSettingManager.Instance.GraphicSettingModel.OptionData
                    .battleEffect == 1);
            }
        };

        // 전투진입
        System.Action waitBattleStartProcess = async () =>
        {
            BattleProcessManager.Instance.ShowBattleSceneUI();
            Model.LoadingCallBakcInvoke(true);
            ObserverManager.AddObserver(ViewObserverID.Refresh, this);
            SoundManager.PlayBattleBGM(Model.BattleInfo.DungeonData);

            IronJade.Debug.Log($"Start Dungeon : {Model.BattleInfo.DungeonData.GetID()}");
        };

        // 시퀀스가 없다는 것은 일반 전투라는 것
        // 시퀀스가 있다는 것은 프롤로그 전투라는 것
        if (Model.LoadingSequenceInfo == null)
        {
            switch (Model.BattleInfo.BattleDungeonType)
            {
                case DungeonType.StoryQuest:
                case DungeonType.StageDungeon:
                case DungeonType.ExStage:
                case DungeonType.InfinityCircuit:
                case DungeonType.Code:
                case DungeonType.SkillMaterialDungeon:
                case DungeonType.None:
                    {
                        // 1. 스토리씬
                        // 2. 트랜지션
                        // 3. 기본적인 씬 로드
                        System.Func<UniTask> onEventLoading = async () =>
                        {
                            await waitCommonProcess(StringDefine.SCENE_BATTLE);
                            await waitSettingProcess();
                        };
                        System.Func<UniTask> onEventBattleTransition = async () =>
                        {
                            await CinemachineTimelineManager.Instance.Prewarm();
                        };
                        System.Func<UniTask> onEventPlayTimelineIntro = async () =>
                        {
                            if (!CinemachineTimelineManager.Instance.IsReady)
                                await BattleProcessManager.Instance.OnPlayTimeLineIntro(true, false);
                        };

                        await StorySceneManager.Instance.PlayBattleStartStory(Model.BattleInfo.DungeonData, onEventLoading,
                                               Model.BattleInfo.IsBattleTransition ? onEventBattleTransition : null, Model.BattleInfo.IsBattleTransition ? null : onEventPlayTimelineIntro);

                        // 전투 진입 연출은 라이팅이 중간에 바뀌지 않게 재생 전에 켜줌
                        if (CinemachineTimelineManager.Instance.IsReadyBattleEnterIntro)
                            BattleCameraManager.Instance.SetActiveBattleLighting(true);

                        await CinemachineTimelineManager.Instance.Play();

                        BattleHelper.PrepareStorySceneWhileBattle(false);

                        waitBattleStartProcess();
                        break;
                    }

                case DungeonType.Arena:
                    {
                        // 확인 필요
                        await TransitionManager.In(Model.BattleInfo.DungeonStartTransition);
                        await waitCommonProcess(StringDefine.SCENE_BATTLE);
                        await waitSettingProcess();
                        CameraManager.Instance.SetActiveTownCameras(true);
                        await TransitionManager.Out(Model.BattleInfo.DungeonStartTransition);
                        waitBattleStartProcess();

                        //// 이거 현시점 기획 확인 받아야 합니다. (기존에 트랜지션을 대체하던 UI)
                        //BaseController loadingController = UIManager.Instance.GetController(UIType.BattleArenaLoadingPopup);
                        //ArenaBattleInfo info = Model.BattleInfo as ArenaBattleInfo;
                        //BattleArenaLoadingPopupModel model = loadingController.GetModel<BattleArenaLoadingPopupModel>();
                        //model.SetLoadingPopupEnemyInfo(info.EnemyNickName, info.EnemyIllust, info.EnemyArenaModel, info.EnemyPower);
                        //await UIManager.Instance.EnterAsync(loadingController);
                        break;
                    }

                case DungeonType.Training:
                    {
                        await TransitionManager.In(Model.BattleInfo.DungeonStartTransition);
                        await waitCommonProcess(StringDefine.SCENE_BATTLECHARACTERREVIEWER);
                        await waitSettingProcess();
                        CameraManager.Instance.SetActiveTownCameras(true);
                        await TransitionManager.Out(Model.BattleInfo.DungeonStartTransition);
                        waitBattleStartProcess();
                        break;
                    }
            }
        }
        else
        {
            var loadingSequenceInfo = Model.LoadingSequenceInfo;

            if (loadingSequenceInfo.BeforeLoadingTask != null)
                await loadingSequenceInfo.BeforeLoadingTask();

            if (loadingSequenceInfo.WhenAllTask != null)
                await loadingSequenceInfo.WhenAllTask();

            await waitCommonProcess(StringDefine.SCENE_BATTLE);

            CameraManager.Instance.SetActiveTownCameras(true);

            if (loadingSequenceInfo.AfterLoadingTask != null)
                await loadingSequenceInfo.AfterLoadingTask();

            await CinemachineTimelineManager.Instance.Play();
            await waitSettingProcess();

            //if (!Model.SkipIntroTimeline && !Model.BattleInfo.IsBattleTransition)
            //    await BattleProcessManager.Instance.OnPlayTimeLineIntro(false, false);

            waitBattleStartProcess();
        }
    }

    public override async UniTask ChangeStateProcess(System.Enum state)
    {
        FlowState flowState = (FlowState)state;

        switch (flowState)
        {
            case FlowState.None:
                {
                    if (Model.BattleInfo.BattleDungeonType == DungeonType.Training)
                    {
                        var characterSkillPreviewView = UIManager.Instance.GetController(UIType.CharacterSkillPreviewView);
                        var viewModel = characterSkillPreviewView.GetModel<CharacterSkillPreviewViewModel>();
                        viewModel.SetCharacter(Model.BattleInfo.TeamPlayer.GetCharacterByIndex(0));
                        await UIManager.Instance.EnterAsync(UIType.CharacterSkillPreviewView);
                    }
                    else
                    {
                        await UIManager.Instance.EnterAsync(UIType.BattleView);
                    }

                    Model.ProcessCallBackInvoke(true);

                    BattleProcessManager.Instance.StartBattle(true);
                    //// 3. 대체 로딩화면을 재생중인 경우 로딩화면을 끈다.
                    //await PlayLoadingTransitionOut(Model.BattleInfo);
                    break;
                }
        }
    }

    private void RefreshScene()
    {
        BattleSceneManager.Instance.RefreshAsync(Model.CurrentView).Forget();
    }

    private async UniTask OnEventChangeState(UIState state, UISubState subState, UIType prevUIType, UIType uiType)
    {
        if (subState == UISubState.Finished)
        {
            UnloadAdditiveScenes().Forget();

            if (uiType > UIType.MaxView)
            {
                BattleSceneManager.Instance?.ChangePopupAsync(uiType).Forget();
                return;
            }

            BattleSceneManager.Instance?.ChangeViewAsync(uiType).Forget();

            Model.SetCurrentView(uiType);
        }
    }

    private async UniTask UnloadAdditiveScenes()
    {
        await AdditivePrefabManager.Instance.UnLoadCloseUIAsync();
    }

    void IObserver.HandleMessage(Enum observerMessage, IObserverParam observerParam)
    {
        switch (observerMessage)
        {
            case ViewObserverID.Refresh:
                {
                    RefreshScene();
                    break;
                }
        }
    }

    private void SetBattleBridge()
    {
        BattleBridgeManager.Instance.SetOnActiveBattleGround(BattleSceneManager.Instance.SetActvieBattleBackground);
        BattleBridgeManager.Instance.SetOnActiveBattleDollyCamera(BattleCameraManager.Instance.SetActiveDollyVirtualCamera);
        BattleBridgeManager.Instance.SetOnActiveBattleUI(SetActiveBattleUI);
        BattleBridgeManager.Instance.SetApplyBattleFog(BattleProcessManager.Instance.ApplyBattlePrefabFogSetting);
        BattleBridgeManager.Instance.SetOnGetEnemySideGameObject(GetEnemyTeamSideGameObjects);
        BattleBridgeManager.Instance.SetOnGetAllySideGameObject(GetAllyTeamSideGameObjects);
        BattleBridgeManager.Instance.SetOnPlayBattleBgm(PlayBattleBGM);
        BattleBridgeManager.Instance.SetOnCleanUpBattle(CleanUpBattle);
        BattleBridgeManager.Instance.SetOnGetBattleEnterIntroBindingInfo(BattleProcessManager.Instance.GetBattleEnterIntroBindingInfo);
        BattleBridgeManager.Instance.SetOnRestoreAllySlotInfo(BattleProcessManager.Instance.RestoreAllySlotInfoByTimeLine);
        BattleBridgeManager.Instance.SetOnSetTimelinePlayers(BattleProcessManager.Instance.SetTimelinePlayers);
        BattleBridgeManager.Instance.SetOnSetControlsScriptWorldPosRot(BattleProcessManager.Instance.SetControlScriptWorldPosRot);
        BattleBridgeManager.Instance.SetShowUIParentCanvasGroup(BattleSceneManager.Instance.ShowUIParentCanvasGroup);
    }

    private List<GameObject> GetEnemyTeamSideGameObjects()
    {
        return UFE.Instance.GetControlsScriptTeam(UFE_TeamSide.Enemy).Select(x => x.CharacterObject).ToList();
    }

    private List<GameObject> GetAllyTeamSideGameObjects()
    {
        return UFE.Instance.GetControlsScriptTeam(UFE_TeamSide.Ally).Select(x => x.CharacterObject).ToList();
    }

    private void SetActiveBattleUI(bool value)
    {
        if (value)
        {
            BattleProcessManager.Instance.ShowBattleSceneUI();
        }
        else
        {
            BattleProcessManager.Instance.HideBattleSceneUI();
        }
    }

    public void BattleEffect(int battleEffect)
    {
        IronJade.Debug.Log("BattleEffect : " + battleEffect);
        switch (battleEffect)
        {
            case 0:
                UFE.Instance.SetEffectLoadOn(false);
                UFE.Instance.SetEffectActiveOn(false);
                UFE.Instance.SetEffectNoHitOn(false);
                UFE.Instance.SetEffectAttackOn(false);
                break;
            case 1:
            case 2:
                UFE.Instance.SetEffectLoadOn(true);
                UFE.Instance.SetEffectActiveOn(true);
                UFE.Instance.SetEffectNoHitOn(true);
                UFE.Instance.SetEffectAttackOn(true);
                break;
        }
    }

    private void PlayBattleBGM()
    {
        // 던전 BGM으로 교체 
        if (BattleProcessManager.Instance == null)
            return;

        SoundManager.PlayBattleBGM(BattleProcessManager.Instance.BattleInfo.DungeonData);
    }

    private async UniTask OnEventHome()
    {
        CameraManager.Instance.OffVolumeBlur();

        UIManager.Instance.RemovePrevStackNavigator();

        await AdditivePrefabManager.Instance.UnLoadCloseUIAsync();

        BattleCameraManager.Instance.DestroyBattleResultScreenEffect();

        TownFlowModel model = new TownFlowModel();
        model.SetBattleInfo(Model.BattleInfo);
        await FlowManager.Instance.ChangeFlow(FlowType.TownFlow, model, isStack: false);
    }

    private async UniTask OnEventBack()
    {
        CameraManager.Instance.OffVolumeBlur();

        await AdditivePrefabManager.Instance.UnLoadCloseUIAsync();

        BattleCameraManager.Instance.DestroyBattleResultScreenEffect();

        TownFlowModel model = new TownFlowModel();
        model.SetBattleInfo(Model.BattleInfo);
        await FlowManager.Instance.ChangeFlow(FlowType.TownFlow, model, isStack: false);
    }

    private async UniTask OnEventTeamUpdate()
    {
        // 7. 팀편성 UI를 보여준다.
        DeckType deckType = Model.BattleInfo.BattleDungeonType switch
        {
            DungeonType.InfinityCircuit => DeckType.InfinityDungeonGeneral,
            DungeonType.SkillMaterialDungeon => DeckType.SkillMaterialDungeon,
            _ => DeckType.Story
        };
        BaseController teamUpdateController = UIManager.Instance.GetController(UIType.TeamUpdateView);
        TeamUpdateViewModel viewModel = teamUpdateController.GetModel<TeamUpdateViewModel>();
        viewModel.SetCurrentDeckType(deckType);
        viewModel.SetOpenPresetNumber(0);

        Navigator navigator = new Navigator(teamUpdateController);

        UIManager.Instance.AddPrevStackNavigator(navigator);

        await Back();
    }

    private async UniTask OnEventRetry()
    {
        // 1. 연출을 보여준다.
        // 테일즈오브어라이브 참고
        await TransitionManager.In(TransitionType.Rotation);

        await AdditivePrefabManager.Instance.UnLoadCloseUIAsync();

        // 2. 모든 UI를 날린다.
        //UIManager.Instance.DeleteAllUI();

        // 3. 배틀 UI를 보여준다.
        BattleSceneManager.Instance.ShowBattleSceneUI();

        await BattleProcessManager.Instance.HideResultPopup();

        // 4. 배틀을 다시 시작한다.
        BattleProcessManager.Instance.ReStartBattle();

        // 5. 연출을 끝낸다.
        TransitionManager.Out(TransitionType.Rotation).Forget();
    }
    #endregion Coding rule : Function
}