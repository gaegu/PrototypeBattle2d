using Cysharp.Threading.Tasks;
using IronJade.UI.Core;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// UIManager를 서비스로 래핑
/// </summary>
public class UIServiceWrapper : IUIService
{
    private UIManager manager => UIManager.Instance;

    #region UI Management

    public async UniTask EnterAsync(UIType uiType)
    {
        if (manager != null)
            await manager.EnterAsync(uiType);
    }

    public async UniTask EnterAsync(BaseController controller)
    {
        if (manager != null && controller != null)
            await manager.EnterAsync(controller);
    }

    public async UniTask Exit(UIType uiType)
    {
        if (manager != null)
            await manager.Exit(uiType);
    }

    public async UniTask Exit(BaseController controller)
    {
        if (manager != null && controller != null)
            await manager.Exit(controller);
    }

    public async UniTask<bool> BackAsync()
    {
        if (manager != null)
            return await manager.BackAsync();
        return false;
    }

    public void Home()
    {
        manager?.Home();
    }

    #endregion

    #region UI Check

    public bool CheckOpenUI(UIType uiType)
    {
        return manager?.CheckOpenUI(uiType) ?? false;
    }

    public bool CheckOpenCurrentUI(UIType uiType)
    {
        return manager?.CheckOpenCurrentUI(uiType) ?? false;
    }

    public bool CheckOpenCurrentView(UIType uiType)
    {
        return manager?.CheckOpenCurrentView(uiType) ?? false;
    }

    public bool CheckBlockEscapeUI(UIType uiType)
    {
        return manager?.CheckBlockEscapeUI(uiType) ?? false;
    }

    public bool CheckExitByEscapeUITypes(UIType uiType)
    {
        return manager?.CheckExitByEscapeUITypes(uiType) ?? false;
    }

    public bool CheckApplication(UIType uiType)
    {
        return manager?.CheckApplication(uiType) ?? false;
    }

    public bool CheckContinuousEnterQueue()
    {
        return manager?.CheckContinuousEnterQueue() ?? false;
    }

    public bool CheckOpenedPopup()
    {
        return manager?.CheckOpenedPopup() ?? false;
    }

    #endregion

    #region Navigator & Controller

    public Navigator GetCurrentNavigator()
    {
        return manager?.GetCurrentNavigator();
    }

    public BaseController GetController(UIType uiType)
    {
        return manager?.GetController(uiType);
    }

    public BaseController GetNoneStackController(UIType uiType)
    {
        return manager?.GetNoneStackController(uiType);
    }

    #endregion

    #region Stack Management

    public bool CheckPrevStackNavigator()
    {
        return manager?.CheckPrevStackNavigator() ?? false;
    }

    public void SavePrevStackNavigator()
    {
        manager?.SavePrevStackNavigator();
    }

    public async UniTask LoadPrevStackNavigator()
    {
        if (manager != null)
            await manager.LoadPrevStackNavigator();
    }

    public void RemovePrevStackNavigator()
    {
        manager?.RemovePrevStackNavigator();
    }

    public void DeleteAllUI()
    {
        manager?.DeleteAllUI();
    }

    #endregion

    #region Events

    public void SetEventUIProcess(Func<UniTask> onEventHome,
                                  Func<UIState, UISubState, UIType, UIType, UniTask> onEventChangeState)
    {
        manager?.SetEventUIProcess(onEventHome, onEventChangeState);
    }

    public void SetSpecificLastUIExitEvent(Func<UniTask> onEvent)
    {
        manager?.SetSpecificLastUIExitEvent(onEvent);
    }

    #endregion

    #region Application & Resolution

    public void EnableApplicationFrame(UIType uiType)
    {
        manager?.EnableApplicationFrame(uiType);
    }

    public void ChangeResolution(UIType uiType)
    {
        manager?.ChangeResolution(uiType);
    }

    #endregion

    #region Login & Touch

    public void HideLoginUI()
    {
        manager?.HideLoginUI();
    }

    public void ShowTouchScreen(Action onTouch)
    {
        manager?.ShowTouchScreen(onTouch);
    }

    public void HideTouchScreen()
    {
        manager?.HideTouchScreen();
    }

    #endregion

    #region Back Target

    public async UniTask BackToTarget(UIType targetUIType, bool isAnimation)
    {
        if (manager != null)
            await manager.BackToTarget(targetUIType, isAnimation);
    }

    #endregion

    #region Queue Management

    public async UniTask EnterAsyncFromQueue()
    {
        if (manager != null)
            await manager.EnterAsyncFromQueue();
    }

    public void AddContinuousEnterQueue(BaseController controller)
    {
        manager?.AddContinuousEnterQueue(controller);
    }

    #endregion
}