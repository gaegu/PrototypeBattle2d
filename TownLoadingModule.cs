// TownLoadingModule.cs (새 파일)
using Cysharp.Threading.Tasks;
using IronJade.UI.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TownLoadingModule : ITownLoadingModule
{
    private TownFlowModel model;

    public void Initialize(TownFlowModel model)
    {
        this.model = model;
    }

    public async UniTask CleanUp()
    {
        // 정리 작업
    }

    public async UniTask LoadTownScene(string sceneName)
    {
        Debug.Log($"[TownLoadingModule] Loading scene: {sceneName}");

        // 1. 씬 로딩
        if (!UtilModel.Resources.CheckLoadedScene(StringDefine.SCENE_TOWN))
        {
            await UtilModel.Resources.LoadSceneAsync(StringDefine.SCENE_TOWN, LoadSceneMode.Additive);
        }

        // 2. 배경 씬 로딩
        if (!string.IsNullOrEmpty(sceneName))
        {
            Scene scene = await UtilModel.Resources.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
        }
    }

    public async UniTask CreatePlayer()
    {
        Debug.Log("[TownLoadingModule] Creating player");

        // 플레이어 생성 로직 (기존 TownFlow에서 추출)
        await PlayerManager.Instance.CreateMyPlayerUnit();
        await PlayerManager.Instance.LoadMyPlayerCharacterObject();
        await PlayerManager.Instance.MyPlayer.TownPlayer.VisibleTownObject(true);

        // 플레이어 위치 설정
        PlayerManager.Instance.SetMyPlayerSpawn(
            TownObjectType.WarpPoint,
            StringDefine.TOWN_OBJECT_TARGET_PORTAL_DEFAULT,
            true
        );
    }

    public async UniTask LoadNPCs()
    {
        Debug.Log("[TownLoadingModule] Loading NPCs");

        // NPC 로딩 로직 (기존 TownFlow에서 추출)
        TownObjectManager.Instance.LoadAllTownNpcInfoGroup();
        BackgroundSceneManager.Instance.AddTownObjects(model.CurrentFiledMapDefine);
        await TownObjectManager.Instance.StartProcessAsync();
        TownObjectManager.Instance.SetConditionRoad();
    }

    public async UniTask SetupUI()
    {
        Debug.Log("[TownLoadingModule] Setting up UI");

        // UI 설정 로직 (기존 TownFlow에서 추출)
        await TownMiniMapManager.Instance.ShowTownMinimap();
        await UIManager.Instance.EnterAsync(UIType.LobbyView);
    }
}