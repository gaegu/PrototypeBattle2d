// ITownFlow.cs (새 파일)
using System;
using Cysharp.Threading.Tasks;
using IronJade.Flow.Core;

public interface ITownFlow
{
    // BaseFlow의 핵심 메서드들
    UniTask LoadingProcess(Func<UniTask> onEventExitPrevFlow);
    UniTask ChangeStateProcess(Enum state);
    UniTask<bool> Back();
    UniTask Exit();
    UniTask TutorialStepAsync(Enum type);

    // TownFlow 특화 메서드들
    void Enter();
    TownFlowModel Model { get; }
}