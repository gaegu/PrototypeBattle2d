#pragma warning disable CS1998
using UnityEngine;
using Cysharp.Threading.Tasks;
using IronJade.Flow.Core;
using UnityEngine.SceneManagement;
using System;
using UnityEngine.EventSystems;
using IronJade.Table.Data;

public class ExtraStoryFlow : BaseFlow
{
    //============================================================
    //=========    Coding rule에 맞춰서 작업 바랍니다.   =========
    //========= Coding rule region은 절대 지우지 마세요. =========
    //=========    문제 시 '김철옥'에게 문의 바랍니다.   =========
    //============================================================
    
    // 아래 코드는 필수 코드 입니다.
    public ExtraStoryFlowModel Model { get { return GetModel<ExtraStoryFlowModel>(); } }
    //=========================================================================================================

    #region Coding rule : Property
    #endregion Coding rule : Property

    #region Coding rule : Value
    #endregion Coding rule : Value

    #region Coding rule : Function

    public override void Enter()
    {
#if UNITY_EDITOR
        IronJade.Debug.Log("ExtraStory : Enter");
#endif
    }

    public override async UniTask<bool> Back()
    {
        bool isBack = await UIManager.Instance.BackAsync();

        return isBack;
    }

    public override async UniTask Exit()
    {
#if UNITY_EDITOR
        IronJade.Debug.Log("ExtraStory : Exit");
#endif
        AC.KickStarter.ResetData();
        AC.KickStarter.DeletePersistentEngine();

        CameraManager.Instance.SetActiveTownCameras(true);
        CameraManager.Instance.SetActiveCamera(GameCameraType.View, true);
        CameraManager.Instance.SetActiveCamera(GameCameraType.Blur, true);
        CameraManager.Instance.SetActiveCamera(GameCameraType.Popup, true);
        CameraManager.Instance.SetActiveCamera(GameCameraType.Front, true);

        UIManager.Instance.SafeSetActive(true);
        SoundManager.SetAudioListener(true);
        GameManager.Instance.GetComponentInChildren<EventSystem>().SafeSetEnable(true);

        await UtilModel.Resources.UnLoadSceneAsync(StringDefine.SCENE_EXTRASTORY);

        ExtraStoryTable extraStoryTable = TableManager.Instance.GetTable<ExtraStoryTable>();
        var extraStoryData = extraStoryTable.GetDataByID(Model.ExtraStoryDataId);
        await UtilModel.Resources.UnLoadSceneAsync(extraStoryData.GetSTORY_SCENE());
    }

    public override async UniTask LoadingProcess(System.Func<UniTask> onEventExitPrevFlow)
    {
        await TransitionManager.In(TransitionType.Rotation);

        UIManager.Instance.Home();

        // ExtraStoryScene 전용 
        await UtilModel.Resources.LoadSceneAsync(StringDefine.SCENE_EXTRASTORY, LoadSceneMode.Additive);
        await onEventExitPrevFlow();

        //1. 씬 로딩
        await SceneLoadingProcess();

        //2. 서버에서 데이터 로드
        await RequestDataFromNetwork();

        //3. 로드 데이터 씬 적용
        await LoadDataProcess();

        //4. 플레이 활성화
        ActiveExtraStory();

        await TransitionManager.Out(TransitionType.Rotation);
    }

    private async UniTask SceneLoadingProcess()
    {
        await SoundManager.PrepareExtraStoryBanks();

        ExtraStoryTable extraStoryTable = TableManager.Instance.GetTable<ExtraStoryTable>();
        var extraStoryData = extraStoryTable.GetDataByID(Model.ExtraStoryDataId);

        Scene scene = await UtilModel.Resources.LoadSceneAsync(extraStoryData.GetSTORY_SCENE(), LoadSceneMode.Additive);

        CameraManager.Instance.SetActiveTownCameras(false);
        CameraManager.Instance.SetActiveCamera(GameCameraType.View, false);
        CameraManager.Instance.SetActiveCamera(GameCameraType.Blur, false);
        CameraManager.Instance.SetActiveCamera(GameCameraType.Popup, false);
        CameraManager.Instance.SetActiveCamera(GameCameraType.Front, false);

        UIManager.Instance.SafeSetActive(false);
        SoundManager.SetAudioListener(false);
        GameManager.Instance.GetComponentInChildren<EventSystem>().SafeSetEnable(false);

        SceneManager.SetActiveScene(scene);

        SoundManager.SetAudioListenerFollowTarget(AC.KickStarter.eventManager.gameObject.transform);
    }

    private async UniTask RequestDataFromNetwork()
    {

    }

    private async UniTask LoadDataProcess()
    {

    }

    private void ActiveExtraStory()
    {
        //SoundManager.PlayExtraStoryBGM();
    }

    public override async UniTask ChangeStateProcess(Enum state)
    {
        
    }
    #endregion Coding rule : Function
}