// ITownFlowModule.cs (새 파일)
using Cysharp.Threading.Tasks;

public interface ITownFlowModule
{
    void Initialize(TownFlowModel model);
    UniTask CleanUp();
}

// 각 모듈별 인터페이스
public interface ITownWarpModule : ITownFlowModule
{
    UniTask ProcessWarp(TownFlowModel.WarpInfo warpInfo);
    UniTask ProcessMissionWarp(TownFlowModel.WarpInfo warpInfo);
    UniTask ProcessDefaultWarp(TownFlowModel.WarpInfo warpInfo);
}

public interface ITownInteractionModule : ITownFlowModule
{
    UniTask HandleInteraction(TownTalkInteraction interaction);
    UniTask HandlePrologueInteraction(TownTalkInteraction interaction);
}

public interface ITownLoadingModule : ITownFlowModule
{
    UniTask LoadTownScene(string sceneName);
    UniTask CreatePlayer();
    UniTask LoadNPCs();
    UniTask SetupUI();
}

public interface ITownMissionModule : ITownFlowModule
{
    UniTask UpdateMission();
    UniTask ProcessMissionWarp();
    bool CheckHasDelayedStory();
}

public interface ITownSceneModule : ITownFlowModule
{
    UniTask LoadScene(string sceneName);
    UniTask UnloadScene(string sceneName);
    UniTask ShowFieldMapName(FieldMapDefine fieldMap);
}