using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using IronJade.Observer.Core;
using UnityEngine;
using UnityEngine.Playables;

public class CharacterGetCutscene : BaseCutscene<CharacterGetCutsceneModel>, IObserver
{
    public CharacterGetCutsceneModel Model => GetModel<CharacterGetCutsceneModel>();

    [SerializeField]
    private GameObject touchButton = null;

    [Header("스킵시 건너뛰기할 시간")]
    [SerializeField]
    private float[] skipTime;


    [Header("버튼 클릭 전 반복할 시간")]
    [SerializeField]
    private float loopTime = 0;

    [Header("버튼 클릭하면 시작되는 시간")]
    [SerializeField]
    private float continueTime = 0;

    [Header("10연차시 앞부분 스킵하고 재생될 시간")]
    [SerializeField]
    private float repeatTime = 0;

    [Header("캐릭터별 연출 세팅 타임라인")]
    [SerializeField]
    private PlayableDirector doorTimeline;

    private const string GACHA_KEY_LICENSE_PRISM = "AirGround";
    private const string GACHA_KEY_LICENSE_BLUE = "MidGround";
    private const string GACHA_KEY_LICENSE_BLACK = "UnderGround";

    private const string GACHA_KEY_TIER_HIGH = "HighTier";
    private const string GACHA_KEY_TIER_LOW = "LowTier";

    public override UniTask OnLoadCutscene()
    {
        ObserverManager.AddObserver(TimelineObserverID.Notify, this);
        CameraManager.Instance.EanbleDof(false);
        CameraManager.Instance.SetActiveTownCameras(false);
        return base.OnLoadCutscene();
    }

    public override void OnEndCutscene()
    {
        ObserverManager.RemoveObserver(TimelineObserverID.Notify, this);
        CameraManager.Instance.EanbleDof(true);
        CameraManager.Instance.SetActiveTownCameras(true);

        base.OnEndCutscene();
    }

    public override async UniTask OnStartCutscene()
    {
        await base.OnStartCutscene();

        Cutscene.initialTime = Model.IsRepeat ? repeatTime : 0;

        doorTimeline.SetTrackUnMuteAll();

        IronJade.Debug.Log($"Chainlink Cutscene - {Model.LicenseType}, {Model.ResultTier}");

        List<string> muteKey = new List<string>();

        //라이센스 그룹 뮤트
        switch (Model.LicenseType)
        {
            case LicenseType.Black:
                {
                    //muteKey.Add(GACHA_KEY_LICENSE_BLACK);
                    muteKey.Add(GACHA_KEY_LICENSE_BLUE);
                    muteKey.Add(GACHA_KEY_LICENSE_PRISM);
                    break;
                }
            case LicenseType.Blue:
                {
                    muteKey.Add(GACHA_KEY_LICENSE_BLACK);
                    //muteKey.Add(GACHA_KEY_LICENSE_BLUE);
                    muteKey.Add(GACHA_KEY_LICENSE_PRISM);
                    break;
                }
            case LicenseType.Prism:
                {
                    muteKey.Add(GACHA_KEY_LICENSE_BLACK);
                    muteKey.Add(GACHA_KEY_LICENSE_BLUE);
                    //muteKey.Add(GACHA_KEY_LICENSE_PRISM);
                    break;
                }
        }

        //티어 그룹 뮤트
        switch (Model.ResultTier)
        {
            case CharacterGetCutsceneModel.GachaTier.XA:
                {
                    //muteKey.Add(GACHA_KEY_TIER_HIGH);
                    muteKey.Add(GACHA_KEY_TIER_LOW);
                    break;
                }
            case CharacterGetCutsceneModel.GachaTier.X:
            case CharacterGetCutsceneModel.GachaTier.None:
                {
                    muteKey.Add(GACHA_KEY_TIER_HIGH);
                    //muteKey.Add(GACHA_KEY_TIER_LOW);
                    break;
                }
        }

        doorTimeline.SetTrackMuteOnlyKeys(muteKey.ToArray());
    }

    public override void OnSkipCutscene()
    {
        Model.OnEventBgmState?.Invoke(2);
        if (skipTime == null)
        {
            base.OnSkipCutscene();
        }
        else
        {
            //현재 컷씬 재생시간이 구간 내에 해당되면 건너뛰기
            for (int i = 0; i < skipTime.Length; i++)
            {
                if (Cutscene.time < skipTime[i])
                {
                    touchButton.SafeSetActive(false);
                    Cutscene.time = skipTime[i];
                    Cutscene.Play();
                    return;
                }
            }

            base.OnSkipCutscene();
        }
    }

    public void OnClickTouch()
    {
        Model.OnEventBgmState?.Invoke(2);
        touchButton.SafeSetActive(false);
        Cutscene.time = continueTime;
        PlayCutscene();
    }

    public void HandleMessage(Enum observerMessage, IObserverParam observerParam)
    {
        if (observerParam is TimelineObserverParam param)
        {
            switch (observerMessage)
            {
                case TimelineObserverID.Notify:
                    {
                        OnNotifyTimeLine(param.EventType, param.EventKey);
                        switch (param.EventType)
                        {
                            case CutsceneTimeLineEvent.Pause:
                                Cutscene.time = loopTime;
                                break;

                            case CutsceneTimeLineEvent.Event:
                                touchButton.SafeSetActive(true);
                                break;
                        }
                        break;
                    }
            }
        }
    }
}
