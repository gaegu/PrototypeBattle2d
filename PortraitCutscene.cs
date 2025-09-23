//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using TMPro;                        // TextMeshPro 관련 클래스 모음 ( UnityEngine.UI.Text 대신 이걸 사용 )
using uLipSync.Timeline;

//using System.Threading;             // 쓰레드
using UnityEngine;                  // UnityEngine 기본
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

[DisallowMultipleComponent]
[ExecuteInEditMode]
public class PortraitCutscene : BaseCutscene<PortraitCutsceneModel>
{
    //=================================================================
    // 불필요한 부분은 지우고 사용하시면 됩니다.
    //=================================================================

    public PortraitCutsceneModel Model { get { return GetModel<PortraitCutsceneModel>(); } }

    //============================================================
    //=========    Coding rule에 맞춰서 작업 바랍니다.   =========
    //========= Coding rule region은 절대 지우지 마세요. =========
    //=========    문제 시 '김철옥'에게 문의 바랍니다.   =========
    //============================================================

    [Serializable]
    public struct UITargetTextModel
    {
        public GameObject uiTarget;
        public TextMeshProUGUI[] texts;
    }

    public struct TextSoundPair
    {
        public TextMeshProUGUI textUI;
        public SoundType soundType;
        public string path;
        public bool isSync;

        public TextSoundPair(TextMeshProUGUI textUI, SoundType soundType, string path, bool isSync)
        {
            this.textUI = textUI;
            this.soundType = soundType;
            this.path = path;
            this.isSync = isSync;
        }
    }

    #region Coding rule : Property
    public override bool IsUseViewCanvas => true;

    // 포트레이트 씬은 사양에 따라 화면 방향 강제
    public override bool IsRotate => true;
#if UNITY_ANDROID || UNITY_IOS
    public override bool IsHorizontal => GameSettingManager.Instance.GraphicSettingModel.IsHorizontalViewMode();
#else
    public override bool IsHorizontal => true;
#endif

#if UNITY_EDITOR
    /// <summary>에디터용</summary>
    public PortraitCameraSupport PortraitCamera => portraitCamera;

    /// <summary>에디터용</summary>
    [HideInInspector]
    public PortraitTargetSupport testTarget = null;

    /// <summary>에디터용</summary>
    public StoryPortraitUnitModel PortraitUnitModel => Model.PortraitUnitModel;
#endif
    #endregion Coding rule : Property

    #region Coding rule : Value
    [Header("말풍선 타이밍 무시 (테스트 모드)")]
    [SerializeField]
    private bool isTestMode = false;

    [Header("레퍼런스 카메라 Sensor Size")]
    [SerializeField]
    private Vector2 baseCameraSensorSize = new Vector2(36f, 24f);

    [Header("레퍼런스 해상도")]
    [SerializeField]
    private Vector2 baseCameraResolution = new Vector2(1920, 1440);

    [Header("배경")]
    [SerializeField]
    private MeshRenderer background = null;

    [Header("UI")]
    [SerializeField]
    private PortraitUISupport portraitUI = null;

    [Header("카메라")]
    [SerializeField]
    private PortraitCameraSupport portraitCamera = null;

    [Header("말풍선")]
    [SerializeField]
    private SpeechBubbleLoaderSupport[] speechBubbles = null;

    [Header("캐릭터 타겟")]
    [SerializeField]
    private PortraitTargetSupport[] targets = null;

    [Header("로컬변환이 필요한 텍스트가 있는 UI들")]
    [SerializeField]
    private UITargetTextModel[] uiTextTargets = null;

    private int speechBubbleIndex = 0;
    private bool isPlaySpeech = false;
    private bool isOrthographicCamera = false;
    private bool isRotating = false; // 에디터 전용
    private string speechCharacter = null;
    private Action onEventShowScript = null;
    private HashSet<string> playingAnimationTargets = new HashSet<string>();
    private Queue<TextSoundPair> uiTextSoundQueue = new Queue<TextSoundPair>();
    private SpeechBubbleLoaderSupport currentSpeechBubble = null;

    // 타임라인 세팅 값들
    private Dictionary<string, List<PortraitAnimationMarker>> animationMarkers = new Dictionary<string, List<PortraitAnimationMarker>>();
    private Dictionary<string, List<(double start, double end)>> lipSyncTimes = new Dictionary<string, List<(double, double)>>();

    private readonly double twoFrame = 2.0 / 60;
    private readonly double fiveFrame = 5.0 / 60;
    #endregion Coding rule : Value

    #region Coding rule : Function
    private new void Awake()
    {
        base.Awake();

        // BGM 정지되게 수정
        isStopBasicBGM = true;

        SetBindTargetAnimationEvent();

        SetBindPortraitObject();

        InitializeCanvas();
    }

    private void Update()
    {
        if (Model.OnEventShowTouchScreen == null)
            Model.SetEventShowTouchScreen(portraitUI.ShowTouchScreen);

        UpdateCharacterLipSyncLayer();

        CheckTextSound();
    }

    public override async UniTask OnLoadCutscene()
    {
        await base.OnLoadCutscene();

        Camera viewCamera = CameraManager.Instance.GetCamera(GameCameraType.View);
        isOrthographicCamera = viewCamera.orthographic;
        CameraManager.Instance.SetOrthographic(GameCameraType.View, true);

        if (portraitUI != null)
            portraitUI.SetActiveCanvas(false);

        LoadResources();

        InitializeCameraSensorSize();
    }

    public override async UniTask OnStartCutscene()
    {
        IronJade.Debug.LogError($"[Portrait] Try to load : {gameObject.scene.name}");

        try
        {
            await base.OnStartCutscene();

            CutsceneManager.Instance.ShowSkipButton(false);

            Model.SetEventShowSpeechBubble(OnEventShowSpeechBubble);
            Model.SetEventCheckTestMode(() => { return isTestMode; });
            bool isAutoMode = PlayerPrefsWrapper.GetBool(StringDefine.KEY_PLAYER_PORTRAIT_AUTO_PLAY);
            Model.SetIsAutoModeOn(isAutoMode);

            ResetValues();

            HidePortraitTargets();

            InitializeTimeline();

            SetActiveBattleUI(false);

            if (portraitUI != null)
            {
                portraitUI.SetActiveCanvas(true);
                portraitUI.SetActiveAutoButton(true);
            }

            await ShowPortraitUI();

            DeviceRenderQuality.SetPortraitMode(true);
        }
        catch (System.Exception e)
        {
            IronJade.Debug.LogError($"[Portraite] Exception : {e}");
        }
    }

    public override void OnAfterActiveCutscene()
    {
        if (Model.PortraitUnitModel != null)
        {
            if (Model.PortraitUnitModel.OnEventStartProcess == null)
                IronJade.Debug.LogError("Portrait not bind event: OnEventStartProcess");
            else
                Model.PortraitUnitModel.OnEventStartProcess();
        }

        if (UICanvas != null)
            UICanvas.enabled = true;
    }

    public override async UniTask OnUnloadCutscene()
    {
        uiTextSoundQueue.Clear();
        TokenPool.Cancel(GetHashCode());

        UnloadResources();

        // 팝업은 포트레이트 UI 보다 위에 있어야 합니다.
        await HidePopup();

        await ExitPortraitUI();

        SetActiveBattleUI(true);

        CameraManager.Instance.SetOrthographic(GameCameraType.View, isOrthographicCamera);

        DeviceRenderQuality.SetPortraitMode(false);
    }

    public override void OnNotifyTimeLine(CutsceneTimeLineEvent timeLineState, string key)
    {

    }

    public override void OnEndCutscene()
    {
        base.OnEndCutscene();

        HidePortraitUI();

        HideSpeechBubbles();
    }

    public override void OnSkipCutscene()
    {
        if (Model.PortraitUnitModel != null)
            Model.PortraitUnitModel.OnEventBeforeExit?.Invoke();

        base.OnSkipCutscene();
    }

    private void OnEventShowHistoryPopup()
    {
        portraitUI.SetAutoButton(false);

        var dialogHistoryPopup = UIManager.Instance.GetController(UIType.DialogHistoryPopup);
        var dialogHistoryModel = dialogHistoryPopup.GetModel<DialogHistoryPopupModel>();

        dialogHistoryModel.SetHistoryModels(Model.GetHistoryUnitModels());
        dialogHistoryModel.SetEventSectionJump(OnEventSectionJump);
        dialogHistoryModel.SetEventPlayVoice(OnEventPlayVoice);

        UIManager.Instance.EnterAsync(dialogHistoryPopup).Forget();
    }

    private void OnEventAutoPlay(bool isOn)
    {
        PlayerPrefsWrapper.SetBool(StringDefine.KEY_PLAYER_PORTRAIT_AUTO_PLAY, isOn);

        Model.SetIsAutoModeOn(isOn);

        if (isOn && !Cutscene.CheckPlaying())
            PlayCutscene();
    }

    private void InitializeCameraSensorSize()
    {
        if (portraitCamera == null || portraitCamera.MainCamera == null)
            return;

        if (baseCameraResolution.x == 0 || baseCameraResolution.y == 0)
            return;

        float widthRatio = Screen.width / baseCameraResolution.x;
        float heightRatio = Screen.height / baseCameraResolution.y;
        // 위아래 시야를 기준으로 센서 크기 조정
        float newSensorWidth = baseCameraSensorSize.x * widthRatio;
        float newSensorHeight = baseCameraSensorSize.y * heightRatio;
        // 카메라 센서 크기 적용
        portraitCamera.MainCamera.sensorSize = new Vector2(newSensorWidth, newSensorHeight);
        // Gate Fit을 명시적으로 Overscan으로 설정 - Kojun 추가
        portraitCamera.MainCamera.gateFit = Camera.GateFitMode.Overscan;
    }
    /// <summary>
    /// 타임라인을 탐색하고 특정 트랙에 대해 세팅
    /// </summary>
    private void InitializeTimeline()
    {
        lipSyncTimes.Clear();
        animationMarkers.Clear();

        TimelineAsset timelineAsset = Cutscene.playableAsset as TimelineAsset;

        foreach (var track in timelineAsset.GetOutputTracks())
        {
            switch (track)
            {
                case uLipSyncTrack lipSyncTrack:
                    OnEventLipSyncTrackSet(lipSyncTrack);
                    break;
            }

            foreach (var marker in track.GetMarkers())
            {
                OnEventAnimationMarkerSet(marker as PortraitAnimationMarker);
            }
        }
    }

    // 립싱크 시간 정보를 가져옴
    private void OnEventLipSyncTrackSet(uLipSyncTrack lipSyncTrack)
    {
        if (lipSyncTrack == null)
            return;

        var bindingObject = Cutscene.GetGenericBinding(lipSyncTrack);

        if (bindingObject == null)
            return;

        string characterName = bindingObject.name.Split('_')[0];

        foreach (var clip in lipSyncTrack.GetClips())
        {
            var time = (clip.start, clip.end);

            if (!lipSyncTimes.ContainsKey(characterName))
                lipSyncTimes[characterName] = new List<(double, double)> { time };
            else
                lipSyncTimes[characterName].Add(time);
        }
    }

    // 애니메이션 교체 정보를 가져옴
    private void OnEventAnimationMarkerSet(PortraitAnimationMarker portraitAnimationMarker)
    {
        if (portraitAnimationMarker == null)
            return;

        string key = portraitAnimationMarker.data.characterKey;

        if (string.IsNullOrEmpty(key))
            return;

        if (!animationMarkers.ContainsKey(key))
            animationMarkers[key] = new List<PortraitAnimationMarker> { portraitAnimationMarker };
        else
            animationMarkers[key].Add(portraitAnimationMarker);
    }

    private async UniTask ShowPortraitUI()
    {
        Model.SetPortraitUnitModel(new StoryPortraitUnitModel());

        Model.PortraitUnitModel.SetEventCharacterPositionSet(OnEventCharacterPositionSet);
        Model.PortraitUnitModel.SetEventSetBackground(OnEventSetBackground);
        Model.PortraitUnitModel.SetEventPlayEffect(OnEventPlayEffect);
        Model.PortraitUnitModel.SetEventClickNext(OnEventClickNext);
        Model.PortraitUnitModel.SetEventBeforeStartTimeline(OnEventBeforeStartTimeline);
        Model.PortraitUnitModel.SetEventStartTimeline(PlayCutscene);
        Model.PortraitUnitModel.SetEventPauseTimeline(PauseCutscene);
        Model.PortraitUnitModel.SetEventPositionTypeGet(GetCharacterPositionTypes);
        Model.PortraitUnitModel.SetEventTalkTargetScreenPointGet(GetCharacterTalkTargetScreenPoint);
        Model.PortraitUnitModel.SetEventAfterAnimationSet(OnEventShowScriptSet);
        Model.PortraitUnitModel.SetEventOtherSpeechBubbleFullSpeechSet(OnEventSetFullSpeech);
        Model.PortraitUnitModel.SetEventOtherSpeechBubbleFullSpeechCheck(OnEventCheckFullSpeech);
        Model.PortraitUnitModel.SetStoryLocalization(Model.LocalizationScriptDataId);
        Model.PortraitUnitModel.SetIsTestMode(isTestMode);

        IPortraitScript[] scripts = Model.GetScripts();

        Model.PortraitUnitModel.SetScripts(scripts);

        SetLocalizedTexts(scripts);

        // UI 구조로 인해 빈 UI 오픈
        await UIManager.Instance.EnterAsync(UIType.PortraitView);

        // 포트레이트 UI 보여줌
        await portraitUI.ShowPortraitUnit(Model.PortraitUnitModel);

        // 자동 진행 버튼 초기화
        bool isAutoMode = Model.IsAutoModeOn;
        portraitUI.SetAutoButton(isAutoMode);

        UIManager.Instance.EnableCanvas(CanvasType.View, false);
        UIManager.Instance.EnableCanvas(CanvasType.ApplicationView, false);
        UIManager.Instance.EnableLetterBox(true);

        CameraManager.Instance.SetActiveCamera(GameCameraType.TownCharacter, false);
    }

    private void SetActiveBattleUI(bool isActive)
    {
        if (FlowManager.Instance.CheckFlow(FlowType.BattleFlow))
        {
            BattleBridgeManager.Instance.ActiveBattleBackground(isActive);
            BattleBridgeManager.Instance.ActiveBattleUI(isActive);
        }
    }

    private void SetLocalizedTexts(IPortraitScript[] scripts)
    {
        if (scripts == null || uiTextTargets == null)
            return;

        int index = 0;
        int textIndex = 0;

        foreach (var uiTextTarget in uiTextTargets)
        {
            foreach (var textTarget in uiTextTarget.texts)
            {
                if (!TryGetUITextScript(scripts, ref index, textIndex, out PortraitUITextScript textScript))
                    return;

                textTarget.SafeSetText(textScript.texts[textIndex]);

                if (textScript.TextSoundCount > textIndex && textScript.textSounds[textIndex].soundType != SoundType.None)
                    uiTextSoundQueue.Enqueue(new TextSoundPair(textTarget, textScript.textSounds[textIndex].soundType, textScript.textSounds[textIndex].path, true));

                textIndex++;

                if (textScript.TextCount <= textIndex)
                {
                    index++;
                    textIndex = 0;
                }
            }
        }
    }

    private bool TryGetUITextScript(IPortraitScript[] scripts, ref int startIndex, int textIndex, out PortraitUITextScript textScript)
    {
        if (scripts == null)
        {
            textScript = default;
            return false;
        }

        if (scripts.Length <= startIndex || startIndex < 0)
        {
            textScript = default;
            return false;
        }

        while (true)
        {
            if (scripts[startIndex] is PortraitUITextScript uiTextScript)
            {
                if (uiTextScript.TextCount > textIndex)
                {
                    break;
                }
            }

            startIndex++;

            if (scripts.Length <= startIndex)
            {
                textScript = default;
                return false;
            }
        }

        textScript = (PortraitUITextScript)scripts[startIndex];

        return true;
    }

    private void HidePortraitUI()
    {
        if (Model.PortraitUnitModel == null)
            return;

        Model.PortraitUnitModel.OnEventHideView();
    }

    private void HideSpeechBubbles()
    {
        foreach (var speechBubble in speechBubbles)
            speechBubble.SafeSetActive(false);
    }

    private async UniTask HidePopup()
    {
        await UIManager.Instance.Exit(UIType.DialogHistoryPopup);
    }

    private async UniTask ExitPortraitUI()
    {
        if (FG9SceneSettingsApplier.Instance != null)
            FG9SceneSettingsApplier.Instance.SetEnableFog(true);

        Model.SetPortraitUnitModel(null);

        await UIManager.Instance.Exit(UIType.PortraitView);

        UIManager.Instance.EnableCanvas(CanvasType.View, true);
        UIManager.Instance.EnableCanvas(CanvasType.ApplicationView, true);
        UIManager.Instance.EnableLetterBox(false);
        UIManager.Instance.EnableApplicationFrame(false);

        CameraManager.Instance.SetActiveCamera(GameCameraType.TownCharacter, true);
    }

    private void HidePortraitTargets()
    {
        foreach (var target in targets)
        {
            HidePortraitTarget(target);
        }
    }

    private void HidePortraitTarget(PortraitTargetSupport target)
    {
        if (!target)
            return;

        target.SetPositionType(PortraitPositionType.None);
        target.SetPosition(portraitCamera.CharacterRootPosition);
        target.SafeSetActive(true);
    }

    private void ResetValues()
    {
        speechBubbleIndex = 0;
        speechCharacter = null;
        onEventShowScript = null;

        playingAnimationTargets.Clear();
    }

    private void LoadResources()
    {
        portraitCamera?.LoadAudioClips().Forget();
        portraitUI?.LoadAudioClips().Forget();
    }

    private void UnloadResources()
    {
        portraitCamera?.UnLoadAudioClips();
        portraitUI?.UnLoadAudioClips();

        // ADH 여기서 이걸 호출하면 전투를 먼저 로드 하고 컷신이 나오는경우 전투 리소스들이 날라가는 경우가 생김.
        //UtilModel.Resources.UnloadUnusedAssets().Forget();
    }

    private void OnEventShowSpeechBubble(string text, string textColor, Action onEventAfterShow)
    {
        if (speechBubbleIndex < speechBubbles.Length)
        {
            currentSpeechBubble = speechBubbles[speechBubbleIndex++];

            if (currentSpeechBubble)
            {
                IronJade.Debug.Log($"Current SpeechBubble : {currentSpeechBubble.name} / {speechBubbleIndex}");

                currentSpeechBubble.SetCamera(portraitUI.UICanvas.worldCamera);
                currentSpeechBubble.SetCanvas(portraitUI.UICanvas);

                currentSpeechBubble.SetText(text, textColor);
                currentSpeechBubble.SetEventAfterShow(onEventAfterShow);

                if (currentSpeechBubble.gameObject.activeInHierarchy)
                    currentSpeechBubble.ShowAsync().Forget();
                else
                    currentSpeechBubble.SetIsStartLoad(true);
            }
        }
    }

    private async UniTask OnEventCharacterPositionSet(PortraitPositionScript positionScript)
    {
        if (portraitCamera == null)
            return;

        List<Func<UniTask>> inMovingTasks = new List<Func<UniTask>>();
        List<Func<UniTask>> outMovingTasks = new List<Func<UniTask>>();
        Dictionary<PortraitTargetSupport, PortraitPosition> targetMovings = new Dictionary<PortraitTargetSupport, PortraitPosition>();
        bool isNotMotion = positionScript.IsNotMotion;

        // 배치가 될 예정인 캐릭터
        foreach (var pair in positionScript.GetEnumerable())
        {
            if (pair.value.position == PortraitPositionType.None)
                continue;

            if (TryGetTarget(pair.key, out var target))
            {
                targetMovings[target] = pair.value;
            }
            else if (string.IsNullOrEmpty(pair.key))
            {
                // 캐릭터 키가 없는 값인 경우 해당 위치에 배치된 캐릭터 퇴장
                var positionedTarget = FindTarget(pair.value.position);

                if (positionedTarget != null)
                {
                    var position = pair.value;
                    position.position = PortraitPositionType.None;

                    targetMovings[positionedTarget] = position;
                }
            }
        }

        // 배치된 캐릭터들은 모두 퇴장 대기
        foreach (var target in GetTargets())
        {
            if (target.CurrentPosition == PortraitPositionType.None)
                continue;

            // 퇴장 방향 체크 및 추가
            if (!targetMovings.ContainsKey(target))
            {
                bool foundExitDirection = false;
                PortraitPosition position = default;

                foreach (var targetMoving in targetMovings)
                {
                    if (target.CurrentPosition == targetMoving.Value.position)
                    {
                        foundExitDirection = true;
                        position = targetMoving.Value;
                        break;
                    }
                }

                // 찾은 방향으로 퇴장
                if (foundExitDirection)
                {
                    position.position = PortraitPositionType.None;
                    targetMovings[target] = position;
                }
                // 방향이 따로 없으면 기존 방향으로 퇴장
                else
                {
                    var movePosition = new PortraitPosition(PortraitPositionType.None);

                    if (target.CurrentPosition == PortraitPositionType.Right)
                        movePosition.pushDirection = PortraitPositionType.Right;

                    targetMovings[target] = movePosition;
                }
            }
        }

        // 캐릭터 이동 준비
        foreach (var targetMoving in targetMovings)
        {
            var target = targetMoving.Key;
            var from = target.CurrentPosition;
            var to = targetMoving.Value.position;

            if (from == to)
                continue;

            switch (from)
            {
                // 카메라 밖에서 내부로 들어갈 때
                case PortraitPositionType.None:
                    {
                        inMovingTasks.Add(() => portraitCamera.StartMovingInPosition(target, to, targetMoving.Value.enterDirection, isNotMotion));
                    }
                    break;

                // 카메라 내부에서 이동할 때
                case PortraitPositionType.Left:
                case PortraitPositionType.Right:
                case PortraitPositionType.Center:
                    {
                        // 내부 -> 밖
                        if (to == PortraitPositionType.None)
                        {
                            outMovingTasks.Add(() => portraitCamera.StartMovingOutPosition(target, targetMoving.Value.pushDirection, () => HidePortraitTarget(target), isNotMotion));
                        }
                        // 내부 -> 내부
                        else
                        {
                            inMovingTasks.Add(() => portraitCamera.StartMovingRotateAnimation(target, to, isNotMotion));
                        }
                    }
                    break;
            }
        }

        // 캐릭터 이동 시작
        if (outMovingTasks.Count > 0)
        {
            portraitCamera.PlayCharacterOutSound();
            await UniTask.WhenAll(outMovingTasks.Select(task => task()));
        }

        portraitCamera.SetDepth(positionScript.GetPositionTypes());

        if (inMovingTasks.Count > 0)
        {
            portraitCamera.PlayCharacterInSound();
            await UniTask.WhenAll(inMovingTasks.Select(task => task()));
        }
    }

    private async UniTask OnEventSetBackground(string backgroundName)
    {
        if (background == null)
            return;

        string path = string.Format(StringDefine.PATH_BACKGROUND_PORTRAIT, backgroundName);

        Sprite loadedBackground = await UtilModel.Resources.LoadAsync<Sprite>(path, this);

        if (loadedBackground == null)
        {
            IronJade.Debug.LogError($"해당 배경을 불러올 수 없습니다. {path}");
        }

        // 배경 삽입
        background.material.mainTexture = loadedBackground != null ? loadedBackground.texture : null;
    }

    private async UniTask OnEventPlayEffect(StorySceneEffect effect)
    {
        switch (effect)
        {
            case StorySceneEffect.Quake:
                await StartShakeEffect(0.5f);
                break;

            case StorySceneEffect.FadeIn:
                await TransitionManager.Out(TransitionType.FadeInOut);
                TransitionManager.LoadingUI(false, false);
                break;

            case StorySceneEffect.FadeOut:
                await TransitionManager.In(TransitionType.FadeInOut);
                TransitionManager.LoadingUI(false, false);
                break;

            default:
                return;
        }
    }

    private void OnEventClickNext()
    {
        portraitUI.SetAutoButton(false);
    }

    private void OnEventBeforeStartTimeline()
    {
        IronJade.Debug.LogError($"포트레이트 애니메이션 테스트: 진행 중 타겟 초기화");
        playingAnimationTargets.Clear();
    }

    /// <summary>
    /// 대사 시작 지점에서 애니메이션이 있는지 확인
    /// </summary>
    private bool CheckHaveAnimationMarker(string key)
    {
        if (animationMarkers == null || string.IsNullOrEmpty(key))
            return false;

        if (animationMarkers.TryGetValue(key, out var markers))
        {
            if (markers != null)
            {
                int index = markers.FindIndex((marker) =>
                {
                    return Model.CaptionStartTime <= marker.time && Model.CaptionStartTime + fiveFrame >= marker.time;
                });

                return index >= 0;
            }
        }

        IronJade.Debug.LogError($"포트레이트 애니메이션 테스트: 마커 못찾음 {key}, {Model.CaptionStartTime}-{Model.CaptionEndTime}");

        return false;
    }

    private PortraitPositionType[] GetCharacterPositionTypes(string[] keys)
    {
        if (keys == null)
            return null;

        PortraitPositionType[] types = new PortraitPositionType[keys.Length];

        for (int i = 0; i < keys.Length; i++)
        {
            if (TryGetTarget(keys[i], out var target))
                types[i] = target.CurrentPosition;
        }

        return types;
    }

    private VectorWrapper GetCharacterTalkTargetScreenPoint(string key)
    {
        if (targets.Length == 0)
            return default;

        if (TryGetTarget(key, out var target))
            return VectorWrapper.Get(portraitCamera.GetTalkTargetScreenPoint(target));
        else
            return default;
    }

    private PortraitTargetSupport FindTarget(PortraitPositionType positionType)
    {
        return targets.Find(t => t.CurrentPosition == positionType);
    }

    private bool TryGetTarget(string key, out PortraitTargetSupport target)
    {
        target = targets.Find(t => t.CheckKey(key));

        if (target == null)
            IronJade.Debug.LogError($"Not found portrait target: {key}");

        return target != null;
    }

    public IEnumerable<PortraitTargetSupport> GetTargets()
    {
        if (targets == null || targets.Length == 0)
            yield break;

        foreach (var target in targets)
            yield return target;
    }

    public PortraitTargetSupport GetTarget(int index)
    {
        if (targets.Length <= index || index < 0)
            return null;

        return targets[index];
    }

    private void SetBindTargetAnimationEvent()
    {
        if (targets == null)
            return;

        foreach (var target in targets)
        {
            target.SetEventAnimationReceiver(OnEventSetCharacterAnimation);
        }
    }

    private void SetBindPortraitObject()
    {
        if (portraitUI == null)
        {
            PortraitUISupport[] findObjects = transform.SafeGetChildByComponent<PortraitUISupport>();

            if (findObjects.Length > 0)
                portraitUI = findObjects[0];
        }

        if (portraitUI != null)
        {
            portraitUI.SetEventSkip(OnSkipCutscene);
            portraitUI.SetEventShowHistoryPopup(OnEventShowHistoryPopup);
            portraitUI.SetEventAutoPlay(OnEventAutoPlay);
        }

        if (portraitCamera == null)
        {
            PortraitCameraSupport[] findObjects = transform.SafeGetChildByComponent<PortraitCameraSupport>();

            if (findObjects.Length > 0)
                portraitCamera = findObjects[0];
        }
    }

    private void InitializeCanvas()
    {
        if (portraitUI != null)
            portraitUI.InitializeCanvas();
    }

    private void UpdateCharacterLipSyncLayer()
    {
        foreach (var target in GetTargets())
        {
            if (!target)
                continue;

            if (!lipSyncTimes.TryGetValue(target.Key, out var times))
                continue;

            bool isLipSyncOn = false;

            foreach (var time in times)
            {
                // 립싱크 구간이면 립싱크 레이어로 변경함
                if (time.start <= Cutscene.time && Cutscene.time <= time.end - twoFrame)
                {
                    target.SetLipSync(true);
                    isLipSyncOn = true;
                    break;
                }
            }

            // 아니면, 원래대로
            if (!isLipSyncOn)
                target.SetLipSync(false);
        }
    }

    private void CheckTextSound()
    {
        if (Cutscene == null || Cutscene.state != UnityEngine.Playables.PlayState.Playing)
            return;

        if (uiTextSoundQueue.Count == 0)
            return;

        if (uiTextSoundQueue.Peek().textUI == null)
        {
            uiTextSoundQueue.Dequeue();
            return;
        }

        if (uiTextSoundQueue.Peek().textUI.gameObject.activeInHierarchy)
        {
            var pair = uiTextSoundQueue.Dequeue();

            OnEventPlaySound(pair.soundType, pair.path, pair.isSync).Forget();
            return;
        }
    }

    private bool SetCharacterAnimation(PortraitTargetSupport target, string animationName)
    {
        if (target == null || string.IsNullOrEmpty(animationName))
            return false;

        if (!target.CheckAnimator())
            return false;

        // 애니메이션 재생 중인 타겟 등록
        if (!playingAnimationTargets.Contains(target.Key))
            playingAnimationTargets.Add(target.Key);

        if (target.CheckPlayingAnimation(animationName))
            return false;

        return target.SetAnimation(animationName);
    }

    private async UniTask StartShakeEffect(float second)
    {
        portraitCamera.SetActiveShake(true);

        await UniTask.Delay(Mathf.RoundToInt(second * IntDefine.TIME_MILLISECONDS_ONE));

        portraitCamera.SetActiveShake(false);
    }

    public void OnEventSetCharacterAnimation(PortraitAnimationMarkerData data)
    {
        if (string.IsNullOrEmpty(data.characterKey))
            return;

        if (!TryGetTarget(data.characterKey, out var target))
            return;

        string animationName = string.IsNullOrEmpty(data.animationKey) ? StringDefine.ANIMATOR_PARAMETER_NAME_IDLE : data.animationKey;
        bool isSpeechTargetCharacter = speechCharacter != null && target.CheckKey(speechCharacter);

        if (data.isFaceAnimation)
        {
            target.SetFaceAnimation(data.animationKey);

            return;
        }

        // 대사 타임라인이 먼저 진행된 후 여기를 타는지 확인
        if (isSpeechTargetCharacter && !isPlaySpeech)
        {
            isPlaySpeech = true;
            WaitStartAnimationState(target, animationName, onEventShowScript).Forget();
        }
        else
        {
            IronJade.Debug.LogError($"포트레이트 애니메이션 테스트 {target.Key}: {animationName} Only Animation");

            SetCharacterAnimation(target, animationName);
        }
    }

    private async UniTask OnEventPlaySound(SoundType soundType, string path, bool isSync)
    {
        Model.PortraitUnitModel.AddSound(soundType, path, isSync);

        await portraitUI.PortraitUnit.PlaySound();
    }

    private async UniTask WaitStartAnimationState(PortraitTargetSupport target, string animationName, Action onEventFinish)
    {
        if (!SetCharacterAnimation(target, animationName))
        {
            onEventFinish();
            return;
        }

        var stateInfo = target.GetAnimationState();

        PauseCutscene();

        bool isIdle = animationName == StringDefine.ANIMATOR_PARAMETER_NAME_IDLE;

        // End 까지 기다림
        while (target)
        {
            stateInfo = target.GetAnimationState();

            bool checkEnd = stateInfo.IsName(isIdle ? StringDefine.ANIMATOR_PARAMETER_NAME_IDLE : StringDefine.ANIMATOR_STATE_NAME_START);

            if (checkEnd)
                break;

            await UniTask.NextFrame();

            IronJade.Debug.LogError($"포트레이트 애니메이션 재생: {target.Key} wait animation state: {animationName}");
        }

        PlayCutscene();

        onEventFinish();
    }

    private bool OnEventShowScriptSet(string key, Action onEventAfterAnimation)
    {
        // 이미 애니메이션이 진행된 경우
        if (playingAnimationTargets.Contains(key))
        {
            speechCharacter = null;
            onEventShowScript = null;
            isPlaySpeech = true;

            IronJade.Debug.LogError($"포트레이트 애니메이션 테스트: 애니메이션 진행중 -> {key}");

            return false;
        }

        // 애니메이션 변동이 있으면 추후에 변경 후 호출
        if (CheckHaveAnimationMarker(key))
        {
            speechCharacter = key;
            onEventShowScript = onEventAfterAnimation;
            isPlaySpeech = false;

            return true;
        }
        else
        {
            speechCharacter = null;
            onEventShowScript = null;
            isPlaySpeech = true;

            return false;
        }
    }

    private void OnEventSetFullSpeech()
    {
        if (currentSpeechBubble == null)
            return;

        currentSpeechBubble.SetFullSpeech();
    }

    private bool OnEventCheckFullSpeech()
    {
        if (currentSpeechBubble == null)
            return true;

        return currentSpeechBubble.CheckFinishedMessage();
    }

    private async void OnEventSectionJump(int scriptIndex)
    {
        var script = Model.PortraitUnitModel.GetTextHistory(scriptIndex);

        if (script.IsEmptyScript)
        {
            IronJade.Debug.LogError($"해당 인덱스(index: {scriptIndex})에 해당하는 대사를 찾지 못했습니다.");
            return;
        }

        // 타임라인을 멈춤
        PauseCutscene();

        // 진행중인 UI를 정지시킴
        portraitUI.PortraitUnit.SetActivePortraitGroup(false);
        portraitUI.PortraitUnit.StopScriptProcess();
        portraitUI.PortraitUnit.StopSound();
        portraitUI.ShowTouchScreen(false);

        for (int index = scriptIndex; index < speechBubbles.Length; index++)
        {
            speechBubbles[index].SetEmptyText();
        }

        // 이동하려는 인덱스로 수정
        Model.PortraitUnitModel.SetSectionJumpIndex(script.Index);

        // 타임라인 위치를 수정, 로드되는 말풍선 인덱스 수정
        speechBubbleIndex = Model.OnEventJumpToTimeline(scriptIndex);

        // 다시 진행
        Model.PortraitUnitModel.OnEventStartProcess();
    }

    private void OnEventPlayVoice(int findIndex)
    {
        var script = Model.PortraitUnitModel.GetTextHistory(findIndex);

        if (script.IsEmptyScript)
            return;

        portraitUI.PortraitUnit.StopSound();

        var soundPath = script.GetSoundPath(0);
        OnEventPlaySound(soundPath.type, soundPath.path, false).Forget();
    }

#if UNITY_EDITOR

    protected override void OnEventSceneSaving(Scene scene, string path)
    {
        DestoryTestModeGameManager();

        SetBindObjects();

        InitializeAllTimeline();

        RemoveAudioListeners();

        InitializeCanvas();

        InitializeUITargets();

        if (testTarget)
        {
            DestroyImmediate(testTarget.gameObject);
            testTarget = null;
        }
    }

    private void DestoryTestModeGameManager()
    {
        GameObject testModeGameManager = GameObject.Find("TestModeGameManager");

        if (testModeGameManager != null)
            DestroyImmediate(testModeGameManager);
    }

    /// <summary>에디터용</summary>
    private void SetBindObjects()
    {
        try
        {
            targets = transform.SafeGetChildByComponent<PortraitTargetSupport>();

            PortraitCameraSupport[] findCameraObjects = transform.SafeGetChildByComponent<PortraitCameraSupport>();
            if (findCameraObjects.Length > 0)
                portraitCamera = findCameraObjects[0];

            PortraitUISupport[] findUIObjects = transform.SafeGetChildByComponent<PortraitUISupport>();

            if (findUIObjects.Length > 0)
                portraitUI = findUIObjects[0];

            foreach (var target in targets)
            {
                target.SetBindObjects();

                UnityEditor.EditorUtility.SetDirty(target);
            }

        }
        catch (Exception)
        {
        }
    }

    /// <summary>에디터용</summary>
    private void InitializeAllTimeline()
    {
        if (portraitUI == null)
        {
            IronJade.Debug.LogError("PortraitUISupport 가 없습니다.");
            return;
        }

        if (Cutscene == null || Cutscene.playableAsset == null)
        {
            IronJade.Debug.LogError("PlayableDirector 또는 PlayableAsset이 설정되지 않았습니다.");
            return;
        }

        Cutscene.playOnAwake = false;

        // TimelineAsset 가져오기
        TimelineAsset timelineAsset = Cutscene.playableAsset as TimelineAsset;

        if (timelineAsset == null)
        {
            IronJade.Debug.LogError("PlayableAsset이 TimelineAsset이 아닙니다.");
            return;
        }

        // 특정 이름의 그룹 찾기
        foreach (var track in timelineAsset.GetRootTracks())
        {
            OnEventControlTrack(null, track as ControlTrack);
            OnEventActivationTrack(null, track as ActivationTrack);

            if (track is not GroupTrack)
                continue;

            foreach (var subTrack in track.GetChildTracks())
            {
                OnEventControlTrack(track.name, subTrack as ControlTrack);
                OnEventActivationTrack(track.name, subTrack as ActivationTrack);
            }
        }

        Cutscene.RebuildGraph();
    }

    /// <summary>에디터용</summary>
    private void OnEventControlTrack(string parentTrackName, ControlTrack controlTrack)
    {
        if (controlTrack == null || Cutscene == null)
            return;

        // 저장 시 ControlTrack 에 등록된 타임라인은 비활성화로 전환
        foreach (var clip in controlTrack.GetClips())
        {
            if (clip.asset is not ControlPlayableAsset playableAsset)
                continue;

            if (!playableAsset.updateDirector)
                continue;

            var sourceObject = playableAsset.sourceGameObject.Resolve(Cutscene);

            if (sourceObject.CheckSafeNull())
                continue;

            if (sourceObject.TryGetComponent(out PlayableDirector playableDirector))
            {
                playableDirector.playOnAwake = false;

                foreach (var track in playableDirector.GetTargetTracksOfType<PortraitScriptTrack>())
                {
                    track.muted = true;
                }
            }
        }
    }

    /// <summary>에디터용</summary>
    private void OnEventActivationTrack(string parentTrackName, ActivationTrack activationTrack)
    {
        if (activationTrack == null || Cutscene == null)
            return;

        // UI 그룹만 적용
        if (string.IsNullOrEmpty(parentTrackName) || parentTrackName != "UI")
            return;

        var trackTargetObject = Cutscene.GetGenericBinding(activationTrack) as GameObject;
        var signalAsset = UnityEditor.AssetDatabase.LoadAssetAtPath(StringDefine.PATH_DOTWEEN_SIGNAL_ASSET_1, typeof(SignalAsset)) as SignalAsset;

        if (signalAsset == null)
        {
            IronJade.Debug.LogError($"시그널 이벤트 연결 중 시그널 에셋을 찾을 수 없습니다: {StringDefine.PATH_DOTWEEN_SIGNAL_ASSET_1}");
            return;
        }

        InitializeUISignalReceiver(trackTargetObject, signalAsset);
        InitializeUISignalMarker(activationTrack, signalAsset);
    }

    private void InitializeUISignalReceiver(GameObject target, SignalAsset signalAsset)
    {
        if (target == null)
            return;

        if (target.TryGetComponent(out SignalReceiver signalReceiver) && signalReceiver.enabled)
            return;

        if (signalReceiver == null)
            signalReceiver = target.AddComponent<SignalReceiver>();

        signalReceiver.enabled = true;

        // 기존 시그널 이벤트 삭제
        var signals = signalReceiver.GetRegisteredSignals().ToArray();

        foreach (var signal in signals)
            signalReceiver.Remove(signal);

        // 새 이벤트 및 시그널 추가
        signalReceiver.AddReaction(signalAsset, new UnityEvent());

        var animations = target.transform.SafeGetChildByComponent<DG.Tweening.DOTweenAnimation>();

        if (animations.Length == 0 || (animations.Length == 1 && animations[0].gameObject == target))
            return;

        UnityEditor.SerializedObject serializedObject = new UnityEditor.SerializedObject(signalReceiver);
        UnityEditor.SerializedProperty eventsProperty = serializedObject.FindProperty("m_Events");
        UnityEditor.SerializedProperty signalEventProperty = serializedObject.FindProperty("m_Events").FindPropertyRelative("m_Events").GetArrayElementAtIndex(0);

        // 모든 애니메이션에 대해 시그널 이벤트 추가
        for (int i = 0; i < animations.Length; i++)
        {
            if (animations[i].gameObject == signalReceiver.gameObject)
                continue;

            signalEventProperty.FindPropertyRelative("m_PersistentCalls.m_Calls").InsertArrayElementAtIndex(i);

            UnityEditor.SerializedProperty newCall = signalEventProperty.FindPropertyRelative("m_PersistentCalls.m_Calls").GetArrayElementAtIndex(0);
            newCall.FindPropertyRelative("m_Target").objectReferenceValue = animations[i];
            newCall.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = animations[i].GetType().AssemblyQualifiedName;
            newCall.FindPropertyRelative("m_MethodName").stringValue = "DOPlayForward";
            newCall.FindPropertyRelative("m_CallState").enumValueIndex = (int)UnityEventCallState.RuntimeOnly;
            newCall.FindPropertyRelative("m_Mode").enumValueIndex = (int)PersistentListenerMode.EventDefined;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void InitializeUISignalMarker(TrackAsset track, SignalAsset signalAsset)
    {
        if (track == null)
            return;

        int markerCount = track.GetMarkerCount();

        if (markerCount > 0)
            return;

        foreach (var clip in track.GetClips())
        {
            SignalEmitter signalEmitter = track.CreateMarker<SignalEmitter>(clip.start);

            signalEmitter.asset = signalAsset;
        }
    }

    /// <summary>에디터용</summary>
    private void RemoveAudioListeners()
    {
        try
        {
            // AudioListener 삭제
            var audioListeners = transform.SafeGetChildByComponent<AudioListener>();

            for (int i = 0; i < audioListeners.Length; i++)
                DestroyImmediate(audioListeners[i]);
        }
        catch (Exception)
        {
        }
    }

    private void InitializeUITargets()
    {
        if (uiTextTargets == null)
            return;

        for (int index = 0; index < uiTextTargets.Length; index++)
        {
            if (uiTextTargets[index].uiTarget == null)
                continue;

            var texts = uiTextTargets[index].uiTarget.transform.SafeGetChildByComponent<TextMeshProUGUI>();

            if (texts.Length >= 0)
            {
                foreach (var text in texts)
                {
                    if (text.gameObject.TryGetComponent(out LocalizationSupport localizationSupport))
                        DestroyImmediate(localizationSupport);
                }

                uiTextTargets[index].texts = uiTextTargets[index].uiTarget.transform.SafeGetChildByComponent<TextMeshProUGUI>();
            }
        }
    }

    /// <summary>에디터용</summary>
    public UnityEngine.Playables.PlayableDirector GetPlayableDirector()
    {
        return Cutscene;
    }

    [ContextMenu("카메라 SensorSize 적용")]
    public void ApplySensorSizeByResolution()
    {
        InitializeCameraSensorSize();
    }

    private void OnGUI()
    {
        if (!Application.isPlaying)
            return;

        bool isHorizontal = Screen.orientation != ScreenOrientation.Portrait && Screen.orientation != ScreenOrientation.PortraitUpsideDown;
        string buttonText = isHorizontal ? "세로 모드로 변경" : "가로 모드로 변경";
        float width = Screen.width * 0.2f;
        float height = width / 3;

        if (GUI.Button(new Rect(Screen.width - width, Screen.height - (height * 2), width, height), "현재 말풍선"))
        {
            if (currentSpeechBubble)
                UnityEditor.EditorGUIUtility.PingObject(currentSpeechBubble.gameObject);
        }

        if (GUI.Button(new Rect(Screen.width - width, Screen.height - height, width, height), buttonText))
        {
            if (isRotating)
                return;

            // 화면 전환 세팅
            isRotating = true;
            SettingViewModeType rotateViewMode = isHorizontal ? SettingViewModeType.Vertical : SettingViewModeType.Horizontal;

            GameSettingManager.Instance.GraphicSettingModel.ViewMode((int)rotateViewMode);

            // 캔버스 업데이트
            Canvas.ForceUpdateCanvases();

            WaitRotate(!isHorizontal, () =>
            {
                InitializeCameraSensorSize();
                isRotating = false;
            }).Forget();
        }
    }

    private async UniTask WaitRotate(bool isHorizontal, System.Action onEventFinish)
    {
        if (isHorizontal)
        {
            await UniTask.WaitUntil(() =>
            {
                if (Screen.orientation == ScreenOrientation.LandscapeLeft ||
                Screen.orientation == ScreenOrientation.LandscapeRight)
                    return true;

                return false;
            });
        }
        else
        {
            await UniTask.WaitUntil(() =>
            {
                if (Screen.orientation == ScreenOrientation.Portrait ||
                Screen.orientation == ScreenOrientation.PortraitUpsideDown)
                    return true;

                return false;
            });
        }

        // 바로 적용이 안되서 한 프레임 쉬어줌
        await UniTask.NextFrame();

        onEventFinish?.Invoke();
    }
#endif
    #endregion Coding rule : Function
}
