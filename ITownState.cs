// ITownState.cs (새 파일)
using System;
using Cysharp.Threading.Tasks;

public interface ITownState
{
    string StateName { get; }
    UniTask Enter(TownStateContext context);
    UniTask Execute(TownStateContext context);
    UniTask Exit();
    bool CanTransitionTo(FlowState nextState);
}