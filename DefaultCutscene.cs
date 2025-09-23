using Cysharp.Threading.Tasks;

public class DefaultCutscene : BaseCutscene
{
    private void OnEnable()
    {
        //if(ViewCamera == null && CameraManager.Instance != null)
        //{
        //    ChangeViewCamera(CameraManager.Instance.GetCamera(GameCameraType.View));
        //}

        //if(UICanvas != null && UICanvas.worldCamera != null && ViewCamera != null)
        //{
        //    UICanvas.worldCamera = ViewCamera;
        //}
    }

    public override async UniTask OnLoadCutscene()
    {
        if (ViewCamera == null && CameraManager.Instance != null)
        {
            ChangeViewCamera(CameraManager.Instance.GetCamera(GameCameraType.View));
        }

        if (UICanvas != null && UICanvas.worldCamera != null && ViewCamera != null)
        {
            UICanvas.worldCamera = ViewCamera;
        }

        await base.OnLoadCutscene();
    }

    public override void OnNotifyTimeLine(CutsceneTimeLineEvent timeLineState, string key)
    {
        return;
    }

    public override UniTask OnStartCutscene()
    {
        base.OnStartCutscene();

        return UniTask.CompletedTask;
    }
}
