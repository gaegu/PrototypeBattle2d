using Cysharp.Threading.Tasks;
using System.Drawing.Drawing2D;
using UnityEngine;
public class MissionWarpTemplate : WarpTemplateBase
{
    public MissionWarpTemplate(TownFlowModel.WarpInfo warpInfo, TownFlowModel model)
        : base(warpInfo, model) { }

    protected override async UniTask BeforeTransition()
    {
        // 미션 워프 전처리
        MissionManager.Instance.ResetDelayedNotifyUpdateQuest();
        MissionManager.Instance.SetWaitWarpProcessState(true);

        await base.BeforeTransition();
    }

    protected override async UniTask ExecuteWarp()
    {
        // 미션 워프 실행
        Debug.Log("[MissionWarpTemplate] Executing mission warp");
        await WarpCore.Execute(warpInfo, model);
    }

    protected override async UniTask AfterTransition()
    {
        await base.AfterTransition();

        // 미션 워프 후처리
        MissionManager.Instance.SetWaitWarpProcessState(false);

        // 미션 스토리 처리
        if (MissionManager.Instance.CheckHasDelayedStory(true))
        {
            await StorySceneManager.Instance.PlayMissionStory(true, async () => { });
        }
    }
}