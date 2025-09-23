using Cysharp.Threading.Tasks;
using UnityEngine;
public class DefaultWarpTemplate : WarpTemplateBase
{
    public DefaultWarpTemplate(TownFlowModel.WarpInfo warpInfo, TownFlowModel model)
        : base(warpInfo, model) { }

    protected override async UniTask ExecuteWarp()
    {
        // 일반 워프 실행
        Debug.Log("[DefaultWarpTemplate] Executing default warp");
        await WarpCore.Execute(warpInfo, model);
    }
}