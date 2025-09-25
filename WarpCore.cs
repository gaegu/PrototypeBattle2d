using UnityEngine;

using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

public static class WarpCore
{
    public static async UniTask Execute(TownFlowModel.WarpInfo warpInfo, TownFlowModel model)
    {
        string targetScene = warpInfo.targetDataFieldMapEnumId;
        bool isChangeBackground = !model.CurrentFiledMapDefine.ToString().Equals(targetScene);

        if (isChangeBackground)
        {
            await ChangeScene(model, targetScene);
        }

        await UpdatePlayerPosition(warpInfo);
        await SavePlayerPosition(targetScene);

        if (isChangeBackground)
        {
            await RefreshTownEnvironment(model);
        }
    }

    private static async UniTask ChangeScene(TownFlowModel model, string targetScene)
    {
        Debug.Log($"[WarpCore] Changing scene to: {targetScene}");

        // 기존 오브젝트 정리
        PlayerManager.Instance.ResetTempLeaderCharacter();
        TownObjectManager.Instance.OnEventClearTownTag();
        TownObjectManager.Instance.ClearTownObjects();

        // 씬 전환
        SceneManager.SetActiveScene(UtilModel.Resources.GetScene(StringDefine.SCENE_TOWN));
        await UtilModel.Resources.UnLoadSceneAsync(model.CurrentScene);
        await UtilModel.Resources.UnloadUnusedAssets(true);

        model.SetCurrentScene(targetScene);
        Scene scene = await UtilModel.Resources.LoadSceneAsync(model.CurrentScene, LoadSceneMode.Additive);
        SceneManager.SetActiveScene(scene);

        // 배경 활성화
        BackgroundSceneManagerNew.Instance.ShowTownGroup(true);
        BackgroundSceneManagerNew.Instance.AddTownObjects(model.CurrentFiledMapDefine);
    }

    private static async UniTask UpdatePlayerPosition(TownFlowModel.WarpInfo warpInfo)
    {
        Debug.Log($"[WarpCore] Updating player position");

        await PlayerManager.Instance.LoadMyPlayerCharacterObject();
        await PlayerManager.Instance.MyPlayer.TownPlayer.OnEventCollisionExit(null);
        await PlayerManager.Instance.MyPlayer.TownPlayer.VisibleTownObject(true);

        PlayerManager.Instance.SetMyPlayerSpawn(
            warpInfo.targetTownObjectType,
            warpInfo.targetDataEnumId,
            false,
            true
        );

        await PlayerManager.Instance.MyPlayer.TownPlayer.RefreshProcess();
    }

    private static async UniTask SavePlayerPosition(string targetScene)
    {
        var leaderCharacterPosition = PlayerManager.Instance.UserSetting
            .GetUserSettingData<LeaderCharacterPositionUserSetting>();

        leaderCharacterPosition.SetScenePath(targetScene);
        leaderCharacterPosition.SetPosition(PlayerManager.Instance.MyPlayer.TownPlayer);

        await PlayerManager.Instance.UserSetting.SetUserSettingData(
            UserSettingModel.Save.Server,
            leaderCharacterPosition
        );
    }

    private static async UniTask RefreshTownEnvironment(TownFlowModel model)
    {
        Debug.Log($"[WarpCore] Refreshing town environment");

        // 배경 오브젝트 갱신
        BackgroundSceneManagerNew.Instance.OperateTownDecoratorFactory();
        TownObjectManager.Instance.OperateDecoratorFactory();

        await TownObjectManager.Instance.StartProcessAsync();
        TownObjectManager.Instance.SetConditionRoad();

        // UI 갱신
        await TownMiniMapManager.Instance.ShowTownMinimap();
        await TownSceneManager.Instance.RefreshAsync(model.CurrentUI);

        // 미션 추적
        MissionManager.Instance.NotifyStoryQuestTracking();

        // BGM 재생
        TownSceneManager.Instance.PlayBGM();
    }
}