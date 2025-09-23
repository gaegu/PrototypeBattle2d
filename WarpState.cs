using Cysharp.Threading.Tasks;
using IronJade.Observer.Core;
using UnityEngine;

public class WarpState : ServicedTownStateBase
{
    public override string StateName => "Warp";
    private readonly ITownWarpModule warpModule;

    // 모듈을 생성자로 주입받도록 수정
    public WarpState(IServiceContainer container = null, ITownWarpModule module = null)
        : base(container)
    {
        warpModule = module ?? new TownWarpModule();
    }

    protected override async UniTask OnEnter(TownStateContext context)
    {
        var warpInfo = context.GetParameter<TownFlowModel.WarpInfo>("WarpInfo");

        // 서비스 사용 (null-safe)
        TownSceneService?.TownInputSupport?.SetInput(false);
        PlayerService?.MyPlayer?.SetInteracting(true);

        // 워프 실행
        await warpModule.ProcessWarp(warpInfo);

        // 복원
        TownSceneService?.TownInputSupport?.SetInput(true);
        PlayerService?.MyPlayer?.SetInteracting(false);
    }


    public override async UniTask Exit()
    {
        Debug.Log($"[{StateName}] Exit");

        // 워프 종료
        var model = warpModule as ITownFlowModule;
        await model.CleanUp();

        // 입력 복원
        TownSceneManager.Instance.TownInputSupport.SetInput(true);
        PlayerManager.Instance.MyPlayer.SetInteracting(false);
    }

    public override bool CanTransitionTo(FlowState nextState)
    {
        // 워프 중에는 다른 워프로 전환 불가
        return nextState != FlowState.Warp;
    }
}
