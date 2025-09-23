using Cysharp.Threading.Tasks;
using IronJade.UI.Core;
using System;
using UnityEngine;

public class TownSceneService : ITownSceneService
{
    // 싱글톤 래핑
    private TownSceneManager manager => TownSceneManager.Instance;

    public TownInputSupport TownInputSupport => manager?.TownInputSupport;

    public bool IsLoaded => throw new NotImplementedException();

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

    public UniTask UpdateLobbyMenu()
    {
        throw new NotImplementedException();
    }

    public UniTask ChangeViewAsync(UIType uiType, UIType prevUIType)
    {
        throw new NotImplementedException();
    }

    public UniTask ChangePopupAsync(UIType uiType, UIType prevUIType)
    {
        throw new NotImplementedException();
    }

    public UniTask TalkInteracting(bool isTalk)
    {
        throw new NotImplementedException();
    }

    public UniTask TutorialStepAsync(TutorialExplain tutorialExplain)
    {
        throw new NotImplementedException();
    }

    public void SetEventInteraction(Func<TownTalkInteraction, UniTask> eventFunc)
    {
        throw new NotImplementedException();
    }

    public void ShowTownCanvas()
    {
        throw new NotImplementedException();
    }

    public void StopBGM()
    {
        throw new NotImplementedException();
    }

    public void SetActiveTownInput(bool isActive)
    {
        throw new NotImplementedException();
    }

    public void PlayCutsceneState(bool isPlay)
    {
        throw new NotImplementedException();
    }
}
