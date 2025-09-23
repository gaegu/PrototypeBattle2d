// TownWarpModule.cs (새 파일)
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TownWarpModule : ITownWarpModule
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

    public async UniTask ProcessWarp(TownFlowModel.WarpInfo warpInfo)
    {
        Debug.Log($"[TownWarpModule] Processing warp");

        // Template 패턴 사용으로 중복 제거
        WarpTemplateBase warpTemplate = warpInfo.isForMission
            ? new MissionWarpTemplate(warpInfo, model)
            : new DefaultWarpTemplate(warpInfo, model);

        await warpTemplate.Execute();
    }

    public async UniTask ProcessMissionWarp(TownFlowModel.WarpInfo warpInfo)
    {
        var template = new MissionWarpTemplate(warpInfo, model);
        await template.Execute();
    }

    public async UniTask ProcessDefaultWarp(TownFlowModel.WarpInfo warpInfo)
    {
        var template = new DefaultWarpTemplate(warpInfo, model);
        await template.Execute();
    }

}