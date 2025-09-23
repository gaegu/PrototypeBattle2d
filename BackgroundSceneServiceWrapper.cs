using Cysharp.Threading.Tasks;
using IronJade.UI.Core;
using UnityEngine;

public class BackgroundSceneServiceWrapper : IBackgroundSceneService
{
    private BackgroundSceneManager manager => BackgroundSceneManager.Instance;

    #region Town Group

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

    #endregion

    #region View Change

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

    #endregion

    #region Decorator

    public void OperateTownDecoratorFactory()
    {
        manager?.OperateTownDecoratorFactory();
    }

    #endregion

    #region Cinemachine

    public void SetCinemachineFollowTarget()
    {
        manager?.SetCinemachineFollowTarget();
    }

    #endregion

    #region Cutscene

    public void PlayCutsceneState(bool isShow, bool isTownCutscene)
    {
        manager?.PlayCutsceneState(isShow, isTownCutscene);
    }

    #endregion

    #region Properties

    public Transform TownObjectParent => manager?.TownObjectParent?.transform;

    #endregion
}