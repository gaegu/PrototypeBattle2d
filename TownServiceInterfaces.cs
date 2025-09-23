using Cysharp.Threading.Tasks;
using IronJade.UI.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

public interface ITownSceneService
{
    UniTask ShowTown(bool isShow);
    UniTask PlayLobbyMenu(bool isShow);
    UniTask RefreshAsync(UIType uiType);
    void ShowFieldMapName(FieldMapDefine fieldMap, bool isDelay = false);
    void PlayBGM();
    void SetTownInput(Camera camera);
    TownInputSupport TownInputSupport { get; }
}

public interface IPlayerService
{
    Player MyPlayer { get; }
    UniTask CreateMyPlayerUnit();
    UniTask LoadMyPlayerCharacterObject();
    void SetMyPlayerSpawn(TownObjectType objectType, string targetId, bool isLeaderPosition);
    void ResetTempLeaderCharacter();
    void SetTempLeaderCharacter(int characterId);
}

public interface ITownObjectService
{
    ITownSupport GetTownObjectByEnumId(string enumId);
    ITownSupport GetTownObjectByDataId(int dataId);
    UniTask StartProcessAsync();
    UniTask RefreshProcess(bool isMissionUpdate = false);
    void ClearTownObjects();
    void SetConditionRoad();
    void LoadAllTownNpcInfoGroup();
}

public interface IMissionService
{
    bool IsWait { get; }
    bool IsAutoMove { get; }
    UniTask StartRequestUpdateStoryQuestProcess(bool isDialog);
    UniTask StartAutoProcess(TransitionType? transitionType = null);
    void SetWaitUpdateTownState(bool isWait);
    void SetWaitWarpProcessState(bool isWait);
    void ResetDelayedNotifyUpdateQuest();
    void NotifyStoryQuestTracking();
    bool CheckHasDelayedStory(bool isStart);
}

public interface IResourceService
{
    UniTask LoadSceneAsync(string sceneName, LoadSceneMode mode);
    UniTask UnLoadSceneAsync(string sceneName);
    UniTask UnloadUnusedAssets(bool isGC = false);
    bool CheckLoadedScene(string sceneName);
    Scene GetScene(string sceneName);
}