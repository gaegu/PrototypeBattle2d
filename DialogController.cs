//=========================================================================================================
#pragma warning disable CS1998
using System;
//using System.Collections;
using System.Collections.Generic;
using System.Threading;

//using UnityEngine.UI;               // UnityEngine의 UI 기본
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.Observer.Core;
using IronJade.Table.Data;          // 데이터 테이블
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using System.Threading;             // 쓰레드
using UnityEngine;                  // UnityEngine 기본
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class DialogController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.DialogPopup; } }
    private DialogPopup View { get { return base.BaseView as DialogPopup; } }
    protected DialogPopupModel Model { get; private set; }
    public DialogController() { Model = GetModel<DialogPopupModel>(); }
    public DialogController(BaseModel baseModel) : base(baseModel) { }
    //=========================================================================================================
    //============================================================
    //=========    Coding rule에 맞춰서 작업 바랍니다.   =========
    //========= Coding rule region은 절대 지우지 마세요. =========
    //=========    문제 시 '김철옥'에게 문의 바랍니다.   =========
    //============================================================

    #region Coding rule : Property
    #endregion Coding rule : Property

    #region Coding rule : Value
    private CancellationTokenSource cancellationTokenSource;
    private List<Animator> dialogAnimators = null;
    #endregion Coding rule : Value

    #region Coding rule : Function
    public override void Enter()
    {
        if (Model.State == DialogPopupModel.DialogState.ReadyWarp)
            return;

        // 대화 대상 오브젝트를 가져온다.
        Model.SetTownSupport(TownObjectManager.Instance.GetTownObjectByEnumId(Model.TownTalkInteraction.TalkTarget.targetEnumId));
        Model.SetEventStartInteraction(StartInteraction);
        Model.SetEventExit(OnEventExit);
        Model.SetEventShowTurningPoint(OnEventShowTurningPoint);
        Model.SetEventNextTurningPoint(OnEventNextTurningPoint);
        Model.SetEventSectionJump(OnEventSectionJump);
        Model.SetEventAuto(OnEventAuto);
        Model.SetEventShowDialogHistory(OnEventShowDialogHistory);
        Model.SetEventShowDialogSummary(OnEventShowDialogSummary);

        SetPlayerName();

        CameraManager.Instance.SetDofTargetByTalkTarget();

        cancellationTokenSource = new CancellationTokenSource();
    }

    public override void BackEnter()
    {
        OnEventShowTurningPoint();
    }

    public override async UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        // 상호작용 중이면 준비 상태로 되돌아간다.
        switch (Model.State)
        {
            case DialogPopupModel.DialogState.StartInteraction:
            case DialogPopupModel.DialogState.StopInteraction:
                {
                    await ReadyForInteraction(isStartTalkCamera: false);

                    return true;
                }
        }

        ResetValues(true);
        Model.SetIsEnterBattle(false);

        ResetAnimators();

        CameraManager.Instance.RestoreDofTarget();

        await Model.OnEventInteraction(new TownTalkInteraction(false, Model.TownTalkInteraction.TalkTarget));

        if (cancellationTokenSource != null)
        {
            cancellationTokenSource.Cancel(true);
            cancellationTokenSource = null;
        }

        return await base.Exit(async (state) =>
        {
            if (state == UISubState.AfterLoading)
                await MissionManager.Instance.StartRequestUpdateStoryQuestProcess(true);

            await OnEventExtra(onEventExtra, state);
        });
    }

    public override async UniTask<bool> Back(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        // 백버튼 안눌리도록 함
        return true;
    }

    public override async UniTask LoadingProcess()
    {
    }

    public override async UniTask Process()
    {
        // 상호작용 준비 상태
        await ReadyForInteraction(isStartTalkCamera: true);
    }

    public override void Refresh()
    {
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Dialog/DialogPopup";
    }

    public override void InitializeTestModel(params object[] parameters)
    {
        Model.IsTest = true;
    }

    /// <summary>
    /// 상태 초기화
    /// </summary>
    private void ResetValues(bool isClearTalkTargets = true)
    {
        // 상호작용 준비 상태
        Model.SetState(DialogPopupModel.DialogState.ReadyForInteraction);

        // 선택된 상호작용 정보를 없앤다.
        Model.SetInteraction(null);

        // 진행중인 타임라인 종료
        StopTimeline();

        // 터닝포인트 목록을 없앤다.
        Model.SetTurningPoint(null);

        // (이전 카메라 시점 타겟의 상태를 대화 종료로 전환한다)
        if (isClearTalkTargets)
        {
            Model.ClearTalkTargets();

            // 플레이어 및 Dof 초기화
            PlayerManager.Instance.MyPlayer.SetTalking(false);
            CameraManager.Instance.RestoreDofTarget();
            CameraManager.Instance.SetTalkCamera(false);
        }

        // 자동진행 딜레이를 초기화 한다.
        Model.SetAutoDelayMilliSeconds(0);

        // 대화 히스토리를 초기화 한다.
        Model.ClearHistory();

        // 현재 대화 텍스트를 초기화 한다.
        Model.ClearBuffer();

        // 이전 카메라 키 초기화
        Model.SetCameraTarget(string.Empty);
        Model.ClearPrevCameraTargetKey();

        // 선택된 상호작용을 꺼준다.
        View.HideStartInteraction();

        // NPC 말풍선을 꺼준다.
        View.HideDialogBox();

        // NPC 애니메이션을 기본으로 변경한다.
        SetDefaultEmotion(isClearTalkTargets);
    }

    private void SetInteraction()
    {
        if (Model.TownSupport == null)
            return;

        Model.TownSupport.SetInteraction();
    }

    /// <summary>
    /// 상호작용 목록을 보여준다. (대화 준비 상태)
    /// </summary>
    private async UniTask ReadyForInteraction(bool isStartTalkCamera)
    {
        IronJade.Debug.Log("ReadyForInteraction 111111111");

        //try
        {
            cancellationTokenSource.Cancel(true);
            cancellationTokenSource = new CancellationTokenSource();

            // 상호작용 정지 상태
            Model.SetState(DialogPopupModel.DialogState.StopInteraction);

            ITownSupport townSupport = TownObjectManager.Instance.GetTownObjectByEnumId(Model.TownTalkInteraction.TalkTarget.targetEnumId);

            // 현재 대화 대상의 카메라 시점으로 전환한다.
            // 현재 대화 대상을 대화 시작으로 전환한다.
            townSupport.StartTalk();

            // NPC 애니메이션을 기본으로 변경한다.
            SetDefaultEmotion(false);

            // 시네머신 카메라 블렌딩을 기다린다.
            if (await UniTask.NextFrame(cancellationToken: cancellationTokenSource.Token).SuppressCancellationThrow())
            {
                ResetValues(false);
                return;
            }

            if (await UniTask.WaitWhile(CameraManager.Instance.CheckCinemachineClearShotBlending, cancellationToken: cancellationTokenSource.Token).SuppressCancellationThrow())
            {
                ResetValues(false);
                return;
            }

            // 대화 대상 목록을 모두 초기화 한다.
            ResetValues(false);

            // 상호작용 준비 상태
            Model.SetState(DialogPopupModel.DialogState.ReadyForInteraction);

            //CameraManager.Instance.ChangeDofTarget(townSupport.TownObject.DofTarget);
            PlayerManager.Instance.MyPlayer.SetTalking(true);

            // 현재 대화 상대를 담는다.
            Model.AddTalkTarget(townSupport.DataId, townSupport);
        }
        //catch
        //{
        //    // 셋팅이 안돼서 에러나는 경우가 종종 있다.
        //    // 우선 진행은 되게끔.
        //}

        IronJade.Debug.Log("ReadyForInteraction 333333");
#if UNITY_EDITOR
        if (Model.IsTest)
        {
            await PlayTestProcess();
        }
        else
#endif
        {
            // 상호작용 목록을 보여준다.
            await View.ShowReadyInteraction();
        }
    }

    /// <summary>
    /// 선택한 상호작용을 시작한다.
    /// </summary>
    private async UniTask StartInteraction(BaseTownInteraction townInteraction)
    {
        // 상호작용 시작 상태
        Model.SetState(DialogPopupModel.DialogState.StartInteraction);

        // 선택한 상호작용을 담는다.
        Model.SetInteraction(townInteraction);

        // 상호작용 목록을 꺼준다.
        View.HideReadyInteraction();

        switch (townInteraction.InteractionType)
        {
            case TownInteractionType.ContentsOpen:
                {
                    ContentsOpenManager.Instance.ShowContents(townInteraction.ContentsOpenDataId).Forget();
                    break;
                }

            case TownInteractionType.Warp:
                {
                    Model.SetState(DialogPopupModel.DialogState.ReadyForInteraction);

                    if (string.IsNullOrEmpty(townInteraction.WarpFieldMapEnumId))
                    {
                        IronJade.Debug.LogError($"이동할 곳을 찾을 수 없습니다. (Interaction Data({townInteraction.DataId}) WarpFieldMapID is none.)");
                        break;
                    }

                    Exit(async (state) =>
                    {
                        if (state == UISubState.Finished)
                        {
                            MissionManager.Instance.SetAutoMove(false);

                            string targetDataFieldMapEnumId = townInteraction.WarpFieldMapEnumId;
                            string targetDataEnumId = townInteraction.WarpTargetKey;
                            TownObjectType targetTownObjectType = townInteraction.WarpTargetObjectType;
                            Transition transition = townInteraction.WarpTransition;
                            TownFlowModel townFlowModel = FlowManager.Instance.GetCurrentFlow().GetModel<TownFlowModel>();
                            townFlowModel.SetWarp(new TownFlowModel.WarpInfo(targetDataFieldMapEnumId, targetDataEnumId, targetTownObjectType, transition));
                            await FlowManager.Instance.ChangeStateProcess(FlowState.Warp);
                        }
                    }).Forget();
                    break;
                }

            case TownInteractionType.Battle:
                {
                    ShowBattle(townInteraction.DungeonId).Forget();
                    break;
                }

            case TownInteractionType.Prologue:
                // 현재 아무 동작 없음
                break;

            default:
                {
                    // 상호작용을 초기화 후 시작한다.
                    View.HideStartInteraction();
                    View.ShowStartInteraction();

                    // 현재 상호작용에 해당하는 타임라인을 실행한다.
                    PlayDialog(townInteraction.InteractionLocalization);

                    // 대화를 시작한다.
                    PlaySpeech().Forget();
                    PlaySpeechSound().Forget();
                    break;
                }
        }
    }

    /// <summary>
    /// 대화를 한다.
    /// </summary>
    private async UniTask PlaySpeech()
    {
        try
        {
            while (Model.State == DialogPopupModel.DialogState.StartInteraction)
            {
                if (Model.IsUpdateDialogBox)
                {
                    View.SetDialogBoxPosition(GetSpeechBubblePosition(out bool leftSide));
                    Model.SetDialogTail(leftSide);
                    await View.ShowDialogBox();
                    Model.SetCompleteUpdateDialogBox();
                    PlayCharacterAnimation();
                }

                // 대사를 갱신한다.
                //View.ShowSpeaker();
                if (Model.IsSpeech)
                {
                    Model.SetNextCharacter();
                    View.ShowSpeaking();
                }

                // 카메라 시점을 변경한다.
                if (await ChangeCameraPerspective().SuppressCancellationThrow())
                    break;

                if (Model.IsAuto)
                {
                    if (await UniTask.Delay(Model.AutoDelayMilliSeconds, cancellationToken: cancellationTokenSource.Token).SuppressCancellationThrow())
                        break;
                }

                if (await UniTask.NextFrame(cancellationToken: cancellationTokenSource.Token).SuppressCancellationThrow())
                    break;
            }

        }
        catch
        {
        }
    }

    /// <summary>
    /// 대사 사운드를 플레이한다.
    /// </summary>
    private async UniTask PlaySpeechSound()
    {
        try
        {
            while (Model.State == DialogPopupModel.DialogState.StartInteraction)
            {
                if (View == null)
                    return;

                if (!string.IsNullOrEmpty(Model.Voice))
                {
                    string path = LocalizationModel.GetVoicePath(GameSettingManager.Instance.GameSetting, Model.Voice);

                    if (Model.IsInitializeSpeech)
                    {
                        await SoundManager.Voice.Play(path, trackerObj: View);
                        Model.SetInitializeSpeech(false);
                    }

                    Model.SetPlayingSoundFlag(SoundManager.Voice.CheckPlayingSound(path));
                }
                else
                {
                    Model.SetPlayingSoundFlag(false);
                }

                if (await UniTask.NextFrame(cancellationToken: cancellationTokenSource.Token).SuppressCancellationThrow())
                    return;
            }
        }
        catch
        {
        }
    }

    /// <summary>
    /// 대사의 캐릭터 애니메이션을 재생한다.
    /// </summary>
    private void PlayCharacterAnimation()
    {
        var animationStates = Model.GetAnimationState();

        SetEmotion(Model.TownSupport.TownObject.Animator, true, animationStates.face, animationStates.animation);
    }

    /// <summary>
    /// 기본 애니메이션을 재생한다.
    /// </summary>
    private void SetDefaultEmotion(bool isReset)
    {
        SetEmotion(Model.TownSupport.TownObject.Animator, !isReset);
    }

    /// <summary>
    /// 현재 대화 중인 캐릭터의 감정 표현 애니메이션을 재생한다.
    /// </summary>
    private void SetEmotion(Animator animator, bool isOn, string faceMotion = null, string bodyMotion = null)
    {
        if (animator == null)
            return;

        if (dialogAnimators == null)
            dialogAnimators = new List<Animator>();

        if (string.IsNullOrEmpty(bodyMotion) || bodyMotion == "None")
            bodyMotion = StringDefine.ANIMATOR_PARAMETER_NAME_IDLE;

        if (string.IsNullOrEmpty(faceMotion) || faceMotion == "None")
            faceMotion = StringDefine.ANIMATOR_PARAMETER_NAME_FACE_NONE;
        else
            faceMotion = string.Format(StringDefine.ANIMATOR_PARAMETER_NAME_FACE_FORMAT, faceMotion);

        SetBodyMotion(animator, bodyMotion);
        SetFaceMotion(animator, faceMotion);

        int index = dialogAnimators.FindIndex((findAnimator) => findAnimator.Equals(animator));
        bool newAnimator = index == -1;

        if (newAnimator && isOn)
        {
            SetCharacterEmotionAnimation(animator, true);

            dialogAnimators.Add(animator);
        }
    }

    private void SetBodyMotion(Animator animator, string animationName)
    {
        if (!animator)
            return;

        animator.SetTrigger(string.Format(StringDefine.ANIMATOR_PARAMETER_NAME_TRIGGER_FORMAT, animationName));

        foreach (var parameter in animator.parameters)
        {
            if (parameter.type != AnimatorControllerParameterType.Bool)
                continue;

            bool isOn = parameter.name == animationName;
            animator.SetBool(parameter.name, isOn);
        }
    }

    private void SetFaceMotion(Animator animator, string faceName)
    {
        if (!animator)
            return;

        // 표정 변화 전에 false 로 해야함
        animator.SetBool(StringDefine.ANIMATOR_PARAMETER_NAME_FACE_ON, false);
        int index = System.Array.FindIndex(animator.parameters, (p) => p.type == AnimatorControllerParameterType.Trigger && p.name == faceName);

        if (index == -1)
            faceName = StringDefine.ANIMATOR_PARAMETER_NAME_FACE_NONE;

        // 표정 적용
        animator.SetTrigger(faceName);
    }

    /// <summary>
    /// 감정 표현을 변경한 Animator 를 기존으로 변경 후 리스트는 초기화한다.
    /// </summary>
    private void ResetAnimators()
    {
        if (dialogAnimators == null)
            return;

        foreach (var animator in dialogAnimators)
        {
            SetCharacterEmotionAnimation(animator, false);
        }

        dialogAnimators = null;
    }

    /// <summary>
    /// 캐릭터 애니메이터 레이어를 즉시 변경함
    /// </summary>
    private void SetCharacterEmotionAnimation(Animator animator, bool isOn)
    {
        if (animator == null)
            return;

        animator.SetLayerWeight((int)PortraitCharacterAnimationLayer.FACE_WITHOUT_LIPSYNC, isOn ? 1f : 0f);

        // 립싱크 비활성화
        animator.SetLayerWeight((int)PortraitCharacterAnimationLayer.FACE_WITH_LIPSYNC, 0f);
    }

    private void SetPlayerName()
    {
        CharacterTable characterTable = TableManager.Instance.GetTable<CharacterTable>();
        int characterDataId = PlayerManager.Instance.MyPlayer.User.CharacterModel.MissionDataCharacterId;
        characterDataId = characterDataId != 0 ? characterDataId : PlayerManager.Instance.MyPlayer.User.CharacterModel.LeaderDataCharacterId;
        CharacterTableData characterData = characterTable.GetDataByID(characterDataId);

        Model.SetPlayerName(TableManager.Instance.GetLocalization(characterData.GetNAME()));
    }

    private async UniTask PlaySkipAction()
    {
        bool checkEnd = false;
        Queue<string> interactionIds = new Queue<string>();
        HashSet<string> checkInteractionIds = new HashSet<string>();

        string localizedText = TableManager.Instance.GetLocalization(Model.GetInteractionId());
        string interactionEnumId = null;

        // 모든 선택지 탐색
        do
        {
            if (interactionIds.Count > 0)
            {
                interactionEnumId = interactionIds.Dequeue();
                localizedText = TableManager.Instance.GetLocalization(interactionEnumId);
            }

            if (!CheckConvertToScript(localizedText, out DialogScript[] scripts, out DialogTurningPoint[] turningPoints))
                continue;

            foreach (var turningPoint in turningPoints)
            {
                switch (turningPoint.type)
                {
                    case TownInteractionTurningPointType.Talk:
                        {
                            if (checkInteractionIds.Contains(turningPoint.extendedValue))
                                continue;

                            checkInteractionIds.Add(turningPoint.extendedValue);
                            interactionIds.Enqueue(turningPoint.extendedValue);
                        }
                        break;
                    case TownInteractionTurningPointType.Questtalk:
                    case TownInteractionTurningPointType.Battle:
                    case TownInteractionTurningPointType.GachaCharacter:
                        {
                            checkEnd = true;
                            await OnEventNextTurningPoint(turningPoint);
                        }
                        return;
                }
            }
        } while (interactionIds.Count > 0);

        // 만약, 마지막 선택지를 찾을 수 없다면 종료
        if (!checkEnd)
            Exit().Forget();
    }

    private bool CheckConvertToScript(string localizedText, out DialogScript[] scripts, out DialogTurningPoint[] turningPoints)
    {
        scripts = null;
        turningPoints = null;

        if (string.IsNullOrEmpty(localizedText))
            return false;

        ScriptConvertModel convertModel = new ScriptConvertModel();

        scripts = convertModel.GetScripts<DialogScript>(localizedText, out List<string> remainTexts);
        turningPoints = convertModel.GetScripts<DialogTurningPoint>(ref remainTexts);

        bool existScripts = scripts != null && scripts.Length > 0;
        bool existTurningPoints = turningPoints != null && turningPoints.Length > 0;

        return existScripts || existTurningPoints;
    }

    /// <summary>
    /// 대사 실행
    /// </summary>
    private void PlayDialog(int localizationId)
    {
        if (!Model.SetScripts(localizationId))
        {
            IronJade.Debug.LogError($"Failed convert to DialogScript. (localization: {localizationId}");
            return;
        }

        SetCurrentSection(Model.ScriptIndex);
    }

    /// <summary>
    /// 대사 실행
    /// </summary>
    private void PlayDialog(string localizationEnumId)
    {
        if (!Model.SetScripts(localizationEnumId))
        {
            IronJade.Debug.LogError($"Failed convert to DialogScript. (localization: {localizationEnumId}");
            return;
        }

        Model.SetSectionJumpFlag(false);

        SetCurrentSection(Model.ScriptIndex);
    }

    public void SetCurrentSection(int index)
    {
        if (Model.CheckEndScript(index))
        {
            if (Model.SetTurningPoints())
                OnEventShowTurningPoint();
            else
                IronJade.Debug.LogError("The conversation has ended. But no turning point was found.");

            return;
        }

        Model.SetCurrentSection(index);
    }

    /// <summary>
    /// 타임라인 종료
    /// </summary>
    private void StopTimeline()
    {
        if (Model.TownSupport == null)
            return;

        Model.TownSupport.StopTimeline();
    }

    /// <summary>
    /// 카메라 시점을 변경한다.
    /// </summary>
    private async UniTask ChangeCameraPerspective()
    {
        try
        {
            bool isPrevTargetEndTalk = !string.IsNullOrEmpty(Model.PrevTalkCameraTargetEnumId);

            // 이전 카메라 시점 타겟의 상태를 대화 종료로 전환한다.
            // (화면에 안보이기 위함)
            if (isPrevTargetEndTalk)
            {
                if (Model.PrevTalkCameraTargetEnumId != Model.TalkCameraTargetEnumId)
                {
                    ITownSupport prevTownSupport = TownObjectManager.Instance.GetTownObjectByEnumId(Model.PrevTalkCameraTargetEnumId);

                    if (prevTownSupport != null)
                    {
                        prevTownSupport.EndTalk();

                        PlayerManager.Instance.MyPlayer.SetTalking(false);
                        CameraManager.Instance.RestoreDofTarget();
                    }

                    Model.ClearPrevCameraTargetKey();
                }
            }

            ITownSupport townSupport = TownObjectManager.Instance.GetTownObjectByEnumId(Model.TalkCameraTargetEnumId);

            if (townSupport == null)
                return;

            if (townSupport.TownObjectType == TownObjectType.MyPlayer)
            {
                // 이전 카메라 시점 타겟의 상태를 대화 종료로 전환한다.
                // (화면에 안보이기 위함)
                ITownSupport prevTownSupport = Model.TownSupport;
                prevTownSupport.EndTalk();
            }

            // 현재 카메라 시점 타겟의 상태를 대화 시작으로 전환한다.
            townSupport.StartTalk();
            //CameraManager.Instance.ChangeDofTarget(townSupport.TownObject.DofTarget);
            PlayerManager.Instance.MyPlayer.SetTalking(true);

            // 현재 대화 상대를 담는다.
            Model.AddTalkTarget(townSupport.DataId, townSupport);
        }
        catch
        {
        }
    }

    /// <summary>
    /// 다음 대화 진행
    /// </summary>
    private void TurningTalkProcess(DialogTurningPoint dialogTurningPoint)
    {
        Model.SetTurningPoint(null);
        PlayDialog(dialogTurningPoint.extendedValue);
    }

    /// <summary>
    /// 배틀
    /// </summary>
    private async UniTask TurningBattleProcess(DialogTurningPoint dialogTurningPoint)
    {
        if (int.TryParse(dialogTurningPoint.extendedValue, out int dataDungeonId))
            await ShowBattle(dataDungeonId);
        else
            IronJade.Debug.LogError($"던전 데이터로 변환할 수 없습니다: {dialogTurningPoint.extendedValue}");
    }

    /// <summary>
    /// 미션 달성
    /// </summary>
    private async UniTask TurningQuestProcess(DialogTurningPoint dialogTurningPoint)
    {
        Model.SetTurningPoint(null);

        // 도입부 진행중엔 미션 변경 과정을 생략함.
        if (PrologueManager.Instance.IsProgressing)
        {
            ResetValues(false);

            Exit().Forget();

            return;
        }

        if (Model.Interaction == null)
            return;

        if (Model.Interaction.InteractionType == TownInteractionType.Quest)
        {
            MissionContentType contentType = Model.Interaction.ProgressingMissionType;
            int dataId = Model.Interaction.ProgressingDataId;
            MissionProgressState progressState = Model.Interaction.ProgressingMissionCheckState;

            if (dataId != 0)
            {
                BaseMission mission = MissionManager.Instance.GetMission(contentType, dataId);

                if (progressState != mission.GetMissionProgressState())
                    throw new Exception($"This is an invalid state. DataID: {mission.DataId} => expect: {progressState}, real: {mission.GetMissionProgressState()}");

                switch (progressState)
                {
                    // 미션을 받는다.
                    case MissionProgressState.UnAccepted:
                        {
                            await MissionManager.Instance.RequestAccept(mission);
                        }
                        break;

                    // 진행 중이면 진행도를 추가한다.
                    case MissionProgressState.Progressing:
                        {
                            await MissionManager.Instance.AddCountOnCondition(new QuestCondition.TalkNpc(dataId, Model.TownSupport.DataId, 1));
                        }
                        break;

                    // 클리어를 했다면 보상을 받는다.
                    case MissionProgressState.RewardReady:
                        {
                            await MissionManager.Instance.RequestReward(mission);
                        }
                        break;

                    // 여기로 들어오면 에러
                    case MissionProgressState.Completed:
                        throw new Exception("This is an incorrect entry. The mission status you are trying to check cannot be completed.");
                }

                SetInteraction();
            }
            else
            {
                IronJade.Debug.LogError($"This is an invalid value. => MissionType: {contentType}, DataId: {dataId}");
            }
        }

        var result = MissionManager.Instance.GetExpectedResultByStoryQuest();

        // 기대 결과에 워프의 유무에 따라 워프 로직 or UI 상태 변경 로직에서 처리
        if (result.HasWarp)
        {
            await MissionManager.Instance.StartRequestUpdateStoryQuestProcess(true);

            ResetValues(true);
            Model.SetState(DialogPopupModel.DialogState.ReadyWarp);

            await MissionManager.Instance.StartAutoProcess();
        }
        else
        {
            ResetValues(false);

            await Exit();
        }
    }

    /// <summary>
    /// 대화 종료
    /// </summary>
    private async UniTask ExitProcess()
    {
        if (Model.Interaction.InteractionType == TownInteractionType.ContentsOpen)
        {
            // 서버에 저장
            var contentsOpenActivate = PlayerManager.Instance.UserSetting.GetUserSettingData<ContentsOpenActivateUserSetting>();
            contentsOpenActivate.SetActivate(Model.Interaction.ContentsOpenDataId, true);
            await PlayerManager.Instance.UserSetting.SetUserSettingData(UserSettingModel.Save.Server, contentsOpenActivate);
        }

        ResetValues(false);

        await Exit();
    }

    private BaseMission GetInteractMission()
    {
        if (Model.Interaction.InteractionType != TownInteractionType.Quest)
            return null;

        return MissionManager.Instance.GetMission(Model.Interaction.ProgressingMissionType, Model.Interaction.ProgressingDataId);
    }

    private Vector2 GetSpeechBubblePosition(out bool leftSide)
    {
        leftSide = false;

        float screenHalfWidth = Screen.width * 0.5f;
        float screenHalfHeight = Screen.height * 0.5f;

        if (Model.TownSupport == null || Model.TownSupport.TownObject == null)
            return new Vector2(0, screenHalfHeight);

        // 말풍선 위치의 기준이 될 타겟을 가져옴
        Transform target = Model.TownSupport.TownObject.HeadTarget;

        target ??= Model.TownSupport.TownObject.HeightTarget;
        target ??= Model.TownSupport.TownObject.TalkTarget;
        target ??= Model.TownSupport.TownObject.UITarget;

        // 기준되는 타겟이 없으면 기본 위치로
        if (target == null)
            return new Vector2(0, screenHalfHeight);

        Camera camera = CameraManager.Instance.GetCamera(GameCameraType.Town3DUI);
        Camera viewCamera = CameraManager.Instance.GetCamera(GameCameraType.View);

        // 카메라가 없으면 기본 위치로
        if (camera == null || viewCamera == null)
            return new Vector2(0, screenHalfHeight);

        Vector3 screenPoint = camera.WorldToScreenPoint(target.position);
        Vector3 viewportPoint = camera.WorldToViewportPoint(target.position);
        RectTransform viewCanvas = UIManager.Instance.GetCanvas(CanvasType.View).transform as RectTransform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(viewCanvas, screenPoint, viewCamera, out Vector2 localPoint);

        // 말풍선 꼬리 방향용
        leftSide = viewportPoint.x < 0.1f;

        float xSign = Mathf.Sign(localPoint.x);
        float ySign = Mathf.Sign(localPoint.y);

        localPoint.x = Mathf.Min(Mathf.Abs(localPoint.x), screenHalfWidth - (View.DialogBoxWidth * 0.5f)) * xSign;
        localPoint.y = Mathf.Min(Mathf.Abs(localPoint.y), screenHalfHeight - (View.DialogBoxHeight * 0.5f)) * ySign;

        return localPoint;
    }

    private async UniTask ShowBattle(int dataDungeonId)
    {
        if (dataDungeonId == 0)
        {
            IronJade.Debug.LogError("Cannot proceed because the dungeon data id is 0.");
            return;
        }

        System.Func<UniTask> onEventEnterPopup = async () =>
        {
            BaseController stageInfoController = UIManager.Instance.GetController(UIType.StageInfoWindow);
            StageInfoPopupModel stageInfoPopupModel = stageInfoController.GetModel<StageInfoPopupModel>();
            stageInfoPopupModel.SetCurrentDeckType(DeckType.Story);
            stageInfoPopupModel.SetUser(PlayerManager.Instance.MyPlayer.User);
            stageInfoPopupModel.SetPopupByDungeonDataId(dataDungeonId);
            stageInfoPopupModel.SetHideAfterStart(true);
            stageInfoPopupModel.SetListOpen(true);
            stageInfoPopupModel.SetExStage(false);
            stageInfoPopupModel.SetEventBeforeExit(async (isEnterBattle) =>
            {
                if (isEnterBattle)
                {
                    if (Model.OnEventBeforeEnterBattle != null)
                        await Model.OnEventBeforeEnterBattle();
                }
                else
                {
                    // 대화 대상 목록을 모두 초기화 한다.
                    ResetValues(false);

                    await Exit();
                }
            });

            View.Hide();

            await UIManager.Instance.EnterAsync(stageInfoController);
        };

        DungeonTable dungeonTable = TableManager.Instance.GetTable<DungeonTable>();
        DungeonTableData dungeonData = dungeonTable.GetDataByID(dataDungeonId);

        // 던전에 팀원이 완전 고정인 경우 StageInfoPopup을 스킵하고 즉시 전투 입장.
        // 고정캐릭터 사용 시 팀편성 스킵 - 25.01.17 전현구/염하정
        if (PlayerManager.Instance.CheckFixedAllyTeam(dungeonData))
        {
            if (PlayerManager.Instance.CheckFixedAllyHaveTeam(dungeonData))
            {
                await onEventEnterPopup();
            }
            else
            {
                BattleTeamGeneratorModel battleTeamGenerator = new BattleTeamGeneratorModel();
                Team fixedTeam = battleTeamGenerator.CreateFixedAllyTeamByDungeonData(dungeonData, PlayerManager.Instance.MyPlayer.User);
                BattleInfo battleInfo = BattleInfo.CreateBattleInfo(dungeonData.GetID(), fixedTeam, BattleResultInfoModel.CreateBattleResultInfoModel(), 0, 0);

                // UI들 가림
                View.HideDialogBox();

                if (Model.OnEventBeforeEnterBattle != null)
                    await Model.OnEventBeforeEnterBattle();

                // 대화 대상 목록을 모두 초기화 한다.
                ResetValues(false);

                await MissionManager.Instance.StartRequestUpdateStoryQuestProcess(true);

                TownFlowModel townFlowModel = FlowManager.Instance.GetCurrentFlow().GetModel<TownFlowModel>();
                townFlowModel.SetBattleInfo(battleInfo);
                await FlowManager.Instance.ChangeStateProcess(FlowState.Battle);
            }
        }
        else
        {
            await onEventEnterPopup();
        }
    }

    /// <summary>
    /// 터닝포인트를 보여준다.
    /// </summary>
    private async void OnEventShowTurningPoint()
    {
        if (Model.State == DialogPopupModel.DialogState.ReadyWarp)
            return;

        View.ShowTurningPoint().Forget();
    }

    /// <summary>
    /// 다이알로그를 완전히 끄기 위한 이벤트
    /// </summary>
    public void OnEventExit()
    {
        if (Model.State == DialogPopupModel.DialogState.ReadyWarp)
            return;

        // 대화 대상 목록을 모두 초기화 한다.
        ResetValues();

        Exit().Forget();
    }

    /// <summary>
    /// 다음 터닝포인트를 보여준다.
    /// (타입에 따라 대화, 미션, 전투 등등)
    /// </summary>
    private async UniTask OnEventNextTurningPoint(DialogTurningPoint dialogTurningPoint)
    {
        if (Model.State == DialogPopupModel.DialogState.ReadyWarp)
            return;

        View.SetOffTurningPointInteractable();

        // 계속 진행되는 대화가 아니면 상단 버튼은 끈다.
        if (dialogTurningPoint.type != TownInteractionTurningPointType.Talk)
            View.SetOffTopButtons();

        bool isHide = Model.CheckDifferentNextDialog(dialogTurningPoint.extendedValue);

        if (isHide)
        {
            // 잔여 대사 삭제
            Model.ResetDialog();
            Model.ClearBuffer();

            // NPC 말풍선을 가림
            View.HideDialogBox();
        }

        // 터닝 포인트 애니메이션을 기다린다.
        int delay = (int)((View.TurningPointAnimationDuration + 1f) * IntDefine.TIME_MILLISECONDS_ONE);
        if (await UniTask.Delay(delay, cancellationToken: cancellationTokenSource.Token).SuppressCancellationThrow())
            return;


        View.HideStartInteraction(isHide);

        Model.AddHistory(Color.red, DialogSenderType.Player, Model.PlayerName, dialogTurningPoint.text);

        switch (dialogTurningPoint.type)
        {
            case TownInteractionTurningPointType.Talk:
                {
                    TurningTalkProcess(dialogTurningPoint);
                    break;
                }

            // 사용 안함
            case TownInteractionTurningPointType.Task:
                {
                    //await TurningTaskProcess(dialogTurningPoint);
                    await ExitProcess();
                    break;
                }

            case TownInteractionTurningPointType.Battle:
                {
                    await TurningBattleProcess(dialogTurningPoint);
                    break;
                }

            case TownInteractionTurningPointType.Questtalk:
                {
                    await TurningQuestProcess(dialogTurningPoint);
                    break;
                }

            case TownInteractionTurningPointType.Exit:
                {
                    await ExitProcess();
                    break;
                }

            case TownInteractionTurningPointType.GachaCharacter:
                {
                    await TurningQuestProcess(dialogTurningPoint);
                    break;
                }
        }
    }

    /// <summary>
    /// 구간 점프
    /// </summary>
    private void OnEventSectionJump()
    {
        if (Model.State == DialogPopupModel.DialogState.ReadyWarp)
            return;

        if (Model.State == DialogPopupModel.DialogState.StopInteraction)
            return;

        if (Model.State == DialogPopupModel.DialogState.ReadyForInteraction)
        {
            //상호작용 목록이 하나라면 해당 상호작용 실행
            var interactions = Model.TownSupport.GetCurrentInteractions();
            if (interactions.Count == 1)
                StartInteraction(interactions[0]).Forget();
            return;
        }

        if (!Model.CheckJump())
        {
            //선택지가 하나라면 해당 선택지 실행
            if (Model.TurningPointCount == 1)
                View.OnEventClickDialogTurningPoint();  //버튼 클릭시 트윈도 재생해야 해서 OnEventNextTurningPoint가 아니라 버튼 클릭이벤트와 연결

            return;
        }

        if (Model.IsSpeech)
        {
            Model.SetSectionJumpFlag(true);
            View.ShowSpeaking();
            return;
        }

        string path = LocalizationModel.GetVoicePath(GameSettingManager.Instance.GameSetting, Model.Voice);

        SoundManager.Voice.Stop(path);

        int index = Model.ScriptIndex + 1;

        SetCurrentSection(index);

        if (Model.CheckEndScript(index) && !Model.CheckHaveTurningPoint())
        {
            // End
            Exit().Forget();
        }
    }

    /// <summary>
    /// 자동진행
    /// </summary>
    private void OnEventAuto()
    {
        Model.SetAutoFlag(!Model.IsAuto);
    }

    /// <summary>
    /// 히스토리를 보여준다.
    /// </summary>
    private void OnEventShowDialogHistory()
    {
        if (Model.State == DialogPopupModel.DialogState.ReadyWarp)
            return;

        BaseController historyPopup = UIManager.Instance.GetController(UIType.DialogHistoryPopup);
        DialogHistoryPopupModel model = historyPopup.GetModel<DialogHistoryPopupModel>();
        model.SetHistoryModels(Model.HistoryUnitModels);

        UIManager.Instance.EnterAsync(historyPopup).Forget();
    }

    /// <summary>
    /// 스킵
    /// </summary>
    private void OnEventShowDialogSummary()
    {
        if (Model.State == DialogPopupModel.DialogState.ReadyWarp)
            return;

        BaseController summaryPopup = UIManager.Instance.GetController(UIType.DialogSummaryPopup);
        DialogSummaryPopupModel model = summaryPopup.GetModel<DialogSummaryPopupModel>();
        BaseMission mission = GetInteractMission();

        string title = mission == null ? string.Empty : TableManager.Instance.GetLocalization(mission.TitleLocalization);
        string summary = TableManager.Instance.GetLocalization(Model.Interaction.SummaryLocalization);

        model.SetTitle(title);
        model.SetContents(summary);
        model.SetEventSkip(OnEventSkip);

        UIManager.Instance.EnterAsync(summaryPopup).Forget();
    }

    /// <summary>
    /// 스킵
    /// </summary>
    private async UniTask OnEventSkip()
    {
        if (Model.State == DialogPopupModel.DialogState.ReadyWarp)
            return;

        await PlaySkipAction();
    }

#if UNITY_EDITOR
    private async UniTask PlayTestProcess()
    {
        // 상호작용을 초기화 후 시작한다.
        Model.SetState(DialogPopupModel.DialogState.StartInteraction);
        Model.SetInteraction(new TownNpcInteractionInfo());
        View.HideReadyInteraction();
        View.ShowStartInteraction();

        // 대화를 시작한다.
        PlaySpeech().Forget();
        PlaySpeechSound().Forget();
    }
#endif
    #endregion Coding rule : Function
}