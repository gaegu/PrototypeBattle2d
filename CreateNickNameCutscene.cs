#pragma warning disable CS1998

using System;
using Cinemachine;
using Cysharp.Threading.Tasks;
using IronJade.Observer.Core;
using IronJade.UI.Core;
using UnityEngine;

public class CreateNickNameCutscene : BaseCutscene<CreateNickNameCutsceneModel>, IObserver
{
    #region Coding rule : Property
    public CreateNickNameCutsceneModel Model => GetModel<CreateNickNameCutsceneModel>();

    protected override bool IsEndOnTimeline => false;
    #endregion Coding rule : Property

    #region Coding rule : Value

    #region Cutscene
    [Header("CreateNickNameCutscene")]
    [SerializeField]
    private RectTransform canvasRect;

    [SerializeField]
    private DroneBoxControlUnit droneBoxControlUnit;

    [SerializeField]
    private ChipControlUnit chipControlUnit;

    [SerializeField]
    private CinemachineBrain brain;

    [SerializeField]
    private CinemachineVirtualCamera virtualCamera;

    [SerializeField]
    private GameObject dofTarget;

    [SerializeField]
    private float blendingTime = 1f;

    #endregion
    #endregion Coding rule : Value

    #region Coding rule : Function

    protected override void Awake()
    {
        brain.SafeSetActive(false);
        virtualCamera.SafeSetActive(false);
        ViewCamera.SafeSetActive(false);

        CutsceneManager.Instance.ShowSkipButton(false);
    }

    public override async UniTask OnLoadCutscene()
    {
        await base.OnLoadCutscene();

        InitializeUnitModels();

        CameraManager.Instance.EanbleDof(false);
        ObserverManager.AddObserver(TimelineObserverID.Notify, this);
    }

    public override async UniTask OnStartCutscene()
    {
        await base.OnStartCutscene();

        //버튼 입력이 겹쳐서 실제 컷신 재생시 스킵버튼 활성화
        CutsceneManager.Instance.ShowSkipButton(true);

        // fx 겹쳐보이는 문제
        CameraManager.Instance.SetActiveCamera(GameCameraType.View, false);
        ViewCamera.SafeSetActive(true);

        Model.ChangeSequence(CreateNickNameCutsceneModel.Sequence.OpenBox);

        // 임시처리.. 나중에는 닉네임 컷씬은 프롤로그에서만 씀
        if (PrologueManager.Instance.IsProgressing)
            PlayCinemachineCamera();

        await PlayerManager.Instance.MyPlayer.TownPlayer.VisibleTownObject(false);
    }

    public override void OnEndCutscene()
    {
        base.OnEndCutscene();

        CameraManager.Instance.SetActiveCamera(GameCameraType.View, true);
        CameraManager.Instance.EanbleDof(true);

        if (dofTarget != null)
            CameraManager.Instance.RestoreDofTarget();

        if (blendingTime > 0)
            CameraManager.Instance.RestoreCinemachineBlendTime();

        ObserverManager.RemoveObserver(TimelineObserverID.Notify, this);
    }

    public override void OnSkipCutscene()
    {
        CheckOnSkipCutscene().Forget();
    }

    private async UniTask CheckOnSkipCutscene()
    {
        if (!string.IsNullOrEmpty(PlayerManager.Instance.MyPlayer.User.NickName))
        {
            OnCompleteCreateNickName().Forget();
            return;
        }
        else
        {
            OnEventOpenNickNamePopup().Forget();
        }
    }

    public override void OnNotifyTimeLine(CutsceneTimeLineEvent timeLineState, string key)
    {
        OnEnterSequence();
    }

    private void OnEnterSequence()
    {
        IronJade.Debug.Log($"OnEnter Sequence : {Model.CurrentSequence}");

        switch (Model.CurrentSequence)
        {
            case CreateNickNameCutsceneModel.Sequence.None:
                break;

            case CreateNickNameCutsceneModel.Sequence.OpenBox:
                OnEnterOpenBox();
                break;

            case CreateNickNameCutsceneModel.Sequence.InsertChip:
                OnEnterInsertChip();
                break;

            case CreateNickNameCutsceneModel.Sequence.CreateNickName:
                OnEnterCreateNickName();
                break;
        }
    }

    #region Open Box
    private void OnEnterOpenBox()
    {
        chipControlUnit.SafeSetActive(false);

        droneBoxControlUnit.ShowAsync().Forget();
        droneBoxControlUnit.SetCurrentCutsceneTime((float)Cutscene.time);
    }

    private void OnEventOpenBox()
    {
        droneBoxControlUnit.Hide();

        Model.NextSequence();
        PlayCutscene();
    }
    #endregion

    #region Insert Chip
    private void OnEnterInsertChip()
    {
        chipControlUnit.ShowAsync().Forget();
    }

    private void OnEventEndInsertChip()
    {
        chipControlUnit.Hide();

        Model.NextSequence();
        PlayCutscene();
    }
    #endregion

    #region Create NickName
    private void OnEnterCreateNickName()
    {
        OnEventOpenNickNamePopup().Forget();
    }

    private async UniTask OnEventOpenNickNamePopup()
    {
        if (UIManager.Instance.CheckOpenCurrentUI(UIType.NickNamePopup))
            return;

        if (PlayerManager.Instance?.MyPlayer?.User != null && PlayerManager.Instance.MyPlayer.User.IsCreatedNickName)
            return;

#if UNITY_EDITOR
        bool isUserGet = await RequestUserGet();

        if (isUserGet)
        {
            OnCompleteCreateNickName().Forget();
            return;
        }
#endif

        BaseController nickNameController = UIManager.Instance.GetController(UIType.NickNamePopup);
        NickNamePopupModel model = nickNameController.GetModel<NickNamePopupModel>();
        model.SetEventLoginProcess(OnCompleteCreateNickName);

        await UIManager.Instance.EnterAsync(nickNameController);

        await UniTask.WaitWhile(() => UIManager.Instance.CheckOpenUI(UIType.NickNamePopup));
    }

    private async UniTask OnCompleteCreateNickName()
    {
        droneBoxControlUnit.Hide();
        chipControlUnit.Hide();

        // TownFlow 에서 Car 트랜지션 사용하므로
        await TransitionManager.In(TransitionType.Rotation);

        OnEndCutscene();
    }
    #endregion

    private void InitializeUnitModels()
    {
        DroneBoxControlUnitModel sajangBoxControlUnitModel = new DroneBoxControlUnitModel();
        ChipControlUnitModel chipControlUnitModel = new ChipControlUnitModel();

        sajangBoxControlUnitModel.SetGetAnchoredPostion(GetAnchoredPositionFromTouchPos);
        sajangBoxControlUnitModel.SetOnEventOpenBox(OnEventOpenBox);
        sajangBoxControlUnitModel.SetEvaluateCutscene(EvaluateCutsceneTime);

        if (droneBoxControlUnit)
            droneBoxControlUnit.SetModel(sajangBoxControlUnitModel);

        chipControlUnitModel.SetOnEventEndInsertChip(OnEventEndInsertChip);
        chipControlUnitModel.SetGetAnchoredPostion(GetAnchoredPosition);

        if (chipControlUnit)
            chipControlUnit.SetModel(chipControlUnitModel);
    }

    private void PlayCinemachineCamera()
    {
        if (virtualCamera != null && brain != null)
        {
            CameraManager.Instance.SetCinemachineBlendTime(blendingTime);
            //CameraManager.Instance.ChangeDofTarget(dofTarget.transform);
            virtualCamera.Priority = 99;
            brain.SafeSetActive(false);
            virtualCamera.SafeSetActive(true);
            ChangeWorldCmaera(CameraManager.Instance.GetCamera(GameCameraType.TownCharacter));
        }
    }

    private VectorWrapper GetAnchoredPosition(VectorWrapper targetWorldPosition)
    {
        Vector3 screenPos = WorldCamera.WorldToScreenPoint(targetWorldPosition.Vector3);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, ViewCamera, out Vector2 localPosition);

        return new VectorWrapper(localPosition);
    }

    private VectorWrapper GetAnchoredPositionFromTouchPos(VectorWrapper touchPos)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, touchPos.Vector3, ViewCamera, out Vector2 localPosition);

        return new VectorWrapper(localPosition);
    }

    private async UniTask<bool> RequestUserGet()
    {
        UserGetProcess userGetProcess = NetworkManager.Web.GetProcess<UserGetProcess>();

        if (await userGetProcess.OnNetworkAsyncRequest())
        {
            userGetProcess.OnNetworkResponse();

            return true;
        }

        return false;
    }

    public void HandleMessage(Enum observerMessage, IObserverParam observerParam)
    {
        if (observerParam is TimelineObserverParam param)
        {
            switch (param.EventType)
            {
                case CutsceneTimeLineEvent.Pause:
                    PauseCutscene();
                    break;

                case CutsceneTimeLineEvent.Resume:
                    PlayCutscene();
                    break;
            }

            switch (observerMessage)
            {
                case TimelineObserverID.Notify:
                    OnNotifyTimeLine(param.EventType, param.EventKey);
                    break;
            }
        }
    }
    #endregion Coding rule : Function
}
