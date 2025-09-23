using UnityEngine;

using Cysharp.Threading.Tasks;
using IronJade.UI.Core;
public class UITransitionTemplate
{
    private readonly UIType fromUI;
    private readonly UIType toUI;
    private readonly TransitionType transitionType;

    public UITransitionTemplate(UIType from, UIType to)
    {
        fromUI = from;
        toUI = to;
        transitionType = DetermineTransitionType(from, to);
    }

    public async UniTask Execute()
    {
        await BeforeTransition();
        await TransitionManager.In(transitionType);

        try
        {
            await ChangeUI();
        }
        finally
        {
            await TransitionManager.Out(transitionType);
            await AfterTransition();
        }
    }

    private TransitionType DetermineTransitionType(UIType from, UIType to)
    {
        // UI 전환별 트랜지션 결정 (기존 GetEnterTransitionType/GetExitTransitionType 통합)
        if (to == UIType.StageDungeonView) return TransitionType.Tram;
        if (to == UIType.NetMiningView) return TransitionType.Rotation;
        if (to == UIType.CharacterIntroduceView) return TransitionType.Rotation;
        if (from == UIType.CharacterIntroduceView && to == UIType.CharacterDetailView)
            return TransitionType.Rotation;

        return TransitionType.None;
    }

    private async UniTask BeforeTransition()
    {
        // 공통 전처리
        if (fromUI < UIType.MaxView)
        {
            await TownSceneManager.Instance.PlayLobbyMenu(false);
        }
    }

    private async UniTask ChangeUI()
    {
        // UI 전환
        if (fromUI != UIType.None)
        {
            await UIManager.Instance.Exit(fromUI);
        }

        if (toUI != UIType.None)
        {
            await UIManager.Instance.EnterAsync(toUI);
        }
    }

    private async UniTask AfterTransition()
    {
        // 공통 후처리
        if (toUI == UIType.LobbyView)
        {
            await TownSceneManager.Instance.PlayLobbyMenu(true);
        }
    }
}