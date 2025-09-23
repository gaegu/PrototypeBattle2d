//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
using UnityEngine;
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class StoryToonController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.StoryToonView; } }
    public override void SetModel() { SetModel(new StoryToonViewModel()); }
    private StoryToonView View { get { return base.BaseView as StoryToonView; } }
    private StoryToonViewModel Model { get { return GetModel<StoryToonViewModel>(); } }
    //=========================================================================================================
    //============================================================
    //=========    Coding rule에 맞춰서 작업 바랍니다.   =========
    //========= Coding rule region은 절대 지우지 마세요. =========
    //=========    문제 시 '김철옥'에게 문의 바랍니다.   =========
    //============================================================

    #region Coding rule : Property
    #endregion Coding rule : Property

    #region Coding rule : Value
    private float verticalTilt;
    private float horizontalTilt;
    private Vector2 gyroCenter = new Vector2(0, 30);
    private Vector2 gyroRange = new Vector2(335, 10);
    private Vector2 tilt;
    private float smoothing = .5f;
    #endregion Coding rule : Value

    #region Coding rule : Function
    public override void Enter()
    {
        Model.SetEventUnLoadStoryToon(UnLoadStoryToon);
        Model.SetEventUpdate(OnEventUpdate);
        Model.SetEventCheckLoadedToon(OnEventCheckLoadedToon);
        Model.SetIsScrollEnd(false);
    }

    public override async UniTask LoadingProcess()
    {
    }

    public override async UniTask Process()
    {
        ChangeToPerspectiveCamera();

        await View.ShowAsync();

        await ShowScrollGuide();
    }

    public override void Refresh()
    {
    }

    public override async UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        ChangeToOrthographicCamera();

        return await base.Exit(onEventExtra);
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Story/StoryToonView";
    }

    public override void InitializeTestModel(params object[] parameters)
    {
#if UNITY_EDITOR
        Model.IsTestPlay = true;
#endif

        Camera camera = CameraManager.Instance.GetCamera(GameCameraType.View);

        camera.orthographic = false;
        camera.nearClipPlane = 0.01f;
    }

    public override async UniTask<bool> Back(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        return true;
    }

    private void UnLoadStoryToon()
    {
        if (View)
            View.UnLoadStoryToon();
    }

    private void ChangeToPerspectiveCamera()
    {
        Camera camera = CameraManager.Instance.GetCamera(GameCameraType.View);

        camera.orthographic = false;
        camera.nearClipPlane = 0.01f;
    }

    private void ChangeToOrthographicCamera()
    {
        Camera camera = CameraManager.Instance.GetCamera(GameCameraType.View);

        camera.orthographic = true;
        camera.nearClipPlane = 0f;
    }

    private void OnEventUpdate()
    {
        if (!View)
            return;

        if (Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer)
        {
            Quaternion attitude = GyroToUnity(Input.gyro.attitude);
            float angleDiff = ((attitude.eulerAngles.y - gyroCenter.x + 180) % 360) - 180;
            tilt.x = (Mathf.InverseLerp(-gyroRange.x, gyroRange.x, angleDiff) * 2) - 1;
            angleDiff = ((attitude.eulerAngles.x - gyroCenter.y + 180) % 360) - 180;
            tilt.y = -((Mathf.InverseLerp(-gyroRange.y, gyroRange.y, angleDiff) * 2) - 1);

            verticalTilt = Mathf.Lerp(verticalTilt, tilt.y, Time.deltaTime * (1.0f / smoothing));
            horizontalTilt = Mathf.Lerp(horizontalTilt, tilt.x, Time.deltaTime * (1.0f / smoothing));
        }
        else
        {
            horizontalTilt = (Mathf.Clamp(Input.mousePosition.x / Screen.width, 0.0f, 1.0f) - 0.5f) * 2.0f;
            verticalTilt = (Mathf.Clamp(Input.mousePosition.y / Screen.height, 0.0f, 1.0f) - 0.5f) * 2.0f;
        }

        View.SetScrollGraphicPosition(horizontalTilt, verticalTilt);
    }

    private bool OnEventCheckLoadedToon()
    {
        if (!View)
            return false;

        return View.CheckLoadStoryToon();
    }

    private Quaternion GyroToUnity(Quaternion q)
    {
        return new Quaternion(q.x, q.y, -q.z, -q.w);
    }

    private async UniTask ShowScrollGuide()
    {
        int userId = PlayerManager.Instance.MyPlayer.User.UserId;
        string key = string.Format(StringDefine.KEY_PLAYER_PREFS_STORYTOON_SCROLL_GUIDE_CLEAR, userId);
        if (PlayerPrefsWrapper.HasKey(key))
            return;

        PlayerPrefsWrapper.SetBool(key, true);

        BaseController contentsGuidePopup = UIManager.Instance.GetController(UIType.ContentsGuidePopup);
        ContentsGuidePopupModel model = contentsGuidePopup.GetModel<ContentsGuidePopupModel>();

        model.SetGuideType(ContentsGuidePopupModel.GuideType.ScrollGuide);
        model.SetActiveBlur(true);

        await UIManager.Instance.EnterAsync(contentsGuidePopup);
    }
    #endregion Coding rule : Function
}