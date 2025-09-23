//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine.UI;               // UnityEngine의 UI 기본
//using TMPro;                        // TextMeshPro 관련 클래스 모음 ( UnityEngine.UI.Text 대신 이걸 사용 )
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
using UnityEngine;                  // UnityEngine 기본
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class StoryToonCutscene : BaseCutscene<StoryToonCutsceneModel>
{
    //=================================================================
    // 불필요한 부분은 지우고 사용하시면 됩니다.
    //=================================================================

    public StoryToonCutsceneModel Model { get { return GetModel<StoryToonCutsceneModel>(); } }

    //============================================================
    //=========    Coding rule에 맞춰서 작업 바랍니다.   =========
    //========= Coding rule region은 절대 지우지 마세요. =========
    //=========    문제 시 '김철옥'에게 문의 바랍니다.   =========
    //============================================================

    #region Coding rule : Property
    protected override bool IsEndOnTimeline { get { return false; } }
    #endregion Coding rule : Property

    #region Coding rule : Value
    [Header("스토리 UI")]
    [SerializeField]
    private StoryToonMainUnit storyToonUnit = null;

    private bool isPlayed = false;
    #endregion Coding rule : Value

    #region Coding rule : Function
    public override async UniTask OnLoadCutscene()
    {
        await base.OnLoadCutscene();
    }

    public override async UniTask OnStartCutscene()
    {
        await base.OnStartCutscene();

        Cutscene.Stop();

        await ShowStoryToon();
    }

    public override async UniTask OnUnloadCutscene()
    {

    }

    public override void OnNotifyTimeLine(CutsceneTimeLineEvent timeLineState, string key)
    {

    }

    private async UniTask ShowStoryToon()
    {
        SetActiveUI();

        isPlayed = true;

        StoryToonMainUnitModel model = storyToonUnit.Model;

        model.SetScreenMode(GameSettingManager.Instance.GraphicSettingModel.GetViewModeType());
        model.SetSceneCount(GetCutCount());
        model.SetCallbackEvent(GetEvents());

        storyToonUnit.SetModel(model);
        await storyToonUnit.ShowAsync();
    }

    private void SetActiveUI()
    {
        Transform parent = storyToonUnit.transform.parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            child.SafeSetActive(child == storyToonUnit.transform);
        }
    }

    private int GetCutCount()
    {
        int cutCount = 0;

        if (Cutscene == null)
            return 0;

        foreach (var trackName in Cutscene.GetTrackNames())
        {
            cutCount++;
        }

        return cutCount / 2;
    }

    private StoryToonCallbackEvent GetEvents()
    {
        return new StoryToonCallbackEvent(
            onEventGetTimelineKey: GetTimeLineKey,
            onEventSetRenderTexture: SetRenderTexture,
            onEventPlayTimeline: PlayTimeline,
            onEventStopTimeline: StopTimeline,
            onEventPlayTimelineOnce: PlayTimelineOnce
            );
    }

    private string GetTimeLineKey(int sceneNumber)
    {
        SettingViewModeType viewModeType = GameSettingManager.Instance.GraphicSettingModel.GetViewModeType();
        string screenDirection = viewModeType == SettingViewModeType.Horizontal ? "H" : "V";

        return $"{screenDirection}_C{sceneNumber}";
    }

    private void SetRenderTexture(RenderTexture renderTexture)
    {
        if (WorldCamera)
            WorldCamera.targetTexture = renderTexture;
    }

    private void PlayTimeline(string key)
    {
        if (!Cutscene)
            return;

        Cutscene.Stop();

        Cutscene.SetTrackUnMuteOnlyKey(key);
        Cutscene.Play();
    }

    private void StopTimeline()
    {
        if (!Cutscene)
            return;

        Cutscene.Stop();
    }

    private async UniTask PlayTimelineOnce(string key)
    {
        if (!Cutscene)
            return;

        Cutscene.SetTrackUnMuteOnlyKey(key);
        Cutscene.Play();

        await UniTask.NextFrame();

        if (WorldCamera)
            WorldCamera.Render();

        Cutscene.Stop();
    }

#if UNITY_EDITOR
    private void OnGUI()
    {
        if (!isPlayed)
            return;

        StoryToonMainUnitModel model = storyToonUnit.Model;
        bool IsHorizontal = model.ViewModeType == SettingViewModeType.Horizontal;
        string buttonText = IsHorizontal ? "세로 모드" : "가로 모드";

        if (GUI.Button(new Rect(0, 0, 200, 80), buttonText))
        {
            model.SetScreenMode(IsHorizontal ? SettingViewModeType.Vertical : SettingViewModeType.Horizontal);

            storyToonUnit.SetModel(model);
            storyToonUnit.ShowAsync().Forget();
        }
    }
#endif
    #endregion Coding rule : Function
}
