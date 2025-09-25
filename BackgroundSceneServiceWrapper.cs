using Cysharp.Threading.Tasks;
using Cinemachine;
using IronJade.UI.Core;
using UnityEngine;

public class BackgroundSceneServiceWrapper : IBackgroundSceneService
{
    private BackgroundSceneManagerNew manager => BackgroundSceneManagerNew.Instance;

    public Transform TownObjectParent => manager?.TownObjectParent?.transform;

    public void ShowTownGroup(bool isShow)
    {
        manager?.ShowTownGroup(isShow);
    }

    public void AddTownObjects(FieldMapDefine fieldMap)
    {
        manager?.AddTownObjects(fieldMap);
    }

    public bool CheckActiveTown()
    {
        return manager?.CheckActiveTown() ?? false;
    }

    public async UniTask ChangeViewAsync(UIType uiType, UIType prevUIType)
    {
        if (manager != null)
            await manager.ChangeViewAsync(uiType, prevUIType);
    }

    public async UniTask ChangePopupAsync(UIType uiType, UIType prevUIType)
    {
        if (manager != null)
            await manager.ChangePopupAsync(uiType, prevUIType);
    }

    public void OperateTownDecoratorFactory()
    {
        manager?.OperateTownDecoratorFactory();
    }

    public void PlayCutsceneState(bool isShow, bool isTownCutscene)
    {
        manager?.PlayCutsceneState(isShow, isTownCutscene);
    }
}