using Cysharp.Threading.Tasks;

public interface ITownState
{
    string StateName { get; }
    UniTask Enter(TownStateContext context);
    UniTask Execute(TownStateContext context);
    UniTask Exit();
    bool CanTransitionTo(FlowState nextState);

    // 새로 추가
    bool IsInterruptible { get; }  // 인터럽트 가능 여부
    int Priority { get; }  // 우선순위 (높을수록 우선)
}