using Cysharp.Threading.Tasks;
using IronJade.UI.Core;
using UnityEngine;

public class TownSceneService : ITownSceneService
{
    // 싱글톤 래핑
    private TownSceneManager manager => TownSceneManager.Instance;

    public TownInputSupport TownInputSupport => manager?.TownInputSupport;

    public async UniTask ShowTown(bool isShow)
    {
        if (manager != null)
            manager.ShowTown(isShow);
        await UniTask.Yield();
    }

    public async UniTask PlayLobbyMenu(bool isShow)
    {
        if (manager != null)
            await manager.PlayLobbyMenu(isShow);
    }

    public async UniTask RefreshAsync(UIType uiType)
    {
        if (manager != null)
            await manager.RefreshAsync(uiType);
    }

    public void ShowFieldMapName(FieldMapDefine fieldMap, bool isDelay = false)
    {
        manager?.ShowFieldMapName(fieldMap, isDelay);
    }

    public void PlayBGM()
    {
        manager?.PlayBGM();
    }

    public void SetTownInput(Camera camera)
    {
      //  manager?.SetTownInput(camera);
    }
}
