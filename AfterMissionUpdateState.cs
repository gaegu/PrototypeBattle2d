// States/AfterMissionUpdateState.cs
using Cysharp.Threading.Tasks;
using IronJade.Observer.Core;
using IronJade.Table.Data;
using IronJade.UI.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

public class AfterMissionUpdateState : ServicedTownStateBase
{
    public override string StateName => "AfterMissionUpdate";

    private readonly IMissionService missionService;

    private TownStateContext context;

    public AfterMissionUpdateState(IServiceContainer container = null) : base(container)
    {
        missionService = container?.GetService<IMissionService>();
    }

    protected override async UniTask OnEnter(TownStateContext context)
    {
        Debug.Log($"[{StateName}] Processing mission completion and rewards");

        this.context = context;

        // AfterMissionInfo 파라미터 가져오기
        var goods = context.GetParameter<IReadOnlyList<Goods>>("Goods");
        var isChangeEpisode = context.GetParameter<bool>("IsChangeEpisode");
        var isRefreshUI = context.GetParameter<bool>("IsRefreshUI");

        try
        {
            // 1. 보상 처리
            if (goods != null && goods.Count > 0)
            {
                await ProcessRewards(goods);
            }

            // 2. 에피소드 변경 처리
            if (isChangeEpisode)
            {
                await HandleEpisodeChange(context);
            }

            // 3. UI 갱신
            if (isRefreshUI)
            {
                await RefreshUI(context);
            }

            // 4. 완료 알림
            await ShowCompletionNotification(context);

            // 5. 다음 미션 체크
            await CheckNextMission(context);
        }
        catch (Exception e)
        {
            Debug.LogError($"[{StateName}] Failed to process mission completion: {e}");
        }
    }

    private async UniTask ProcessRewards(IReadOnlyList<Goods> goods)
    {
        Debug.Log($"[{StateName}] Processing {goods.Count} rewards");

        // 보상 아이템 지급
        foreach (var reward in goods)
        {
            if (reward == null) continue;

            Debug.Log($"[{StateName}] Reward: {reward.GoodsType} x{reward.Count}");

            // 실제 보상 처리 로직
            switch (reward.GoodsType)
            {
                case GoodsType.Currency:
                    // 재화 추가
                    break;
                case GoodsType.Item:
                    // 아이템 추가
                    break;
                case GoodsType.Character:
                    // 캐릭터 추가
                    break;
            }
        }

        // 보상 UI 표시
        await ShowRewardUI(goods);
    }

    private async UniTask ShowRewardUI(IReadOnlyList<Goods> goods)
    {
        Debug.Log($"[{StateName}] Showing reward UI");

        // 보상 팝업 표시
        var rewardController = UIManager.Instance.GetController(UIType.RewardPopup);
        if (rewardController != null)
        {
            var rewardModel = rewardController.GetModel<RewardPopupModel>();
            //rewardModel.p(goods);


            await UIManager.Instance.EnterAsync(rewardController);

            // 보상 팝업이 닫힐 때까지 대기
            await UniTask.WaitUntil(() => !UIManager.Instance.CheckOpenUI(UIType.RewardPopup));
        }
    }

    private async UniTask HandleEpisodeChange(TownStateContext context)
    {
        Debug.Log($"[{StateName}] Episode changed!");

        // 에피소드 변경 연출
        await TransitionManager.In(TransitionType.Rotation);

        // 에피소드 변경 처리
        await TownSceneManager.Instance.UpdateMission(
            context.Model.AfterMission,
            OnEpisodeChangeWarp
        );

        await TransitionManager.Out(TransitionType.Rotation);
    }

    private async UniTask OnEpisodeChangeWarp(StoryQuest storyQuest)
    {
        Debug.Log($"[{StateName}] Episode warp for quest: {storyQuest?.DataId}");

        if (storyQuest == null) return;

        // 다음 에피소드의 시작 위치로 워프
        StoryQuestTable storyQuestTable = TableManager.Instance.GetTable<StoryQuestTable>();
        StoryQuestTableData nextQuestData = storyQuestTable.GetDataByID(storyQuest.NextDataQuestId);

        if (nextQuestData.IsNull()) return;

        // 워프 포인트가 있으면 워프 처리
        if (nextQuestData.GetQUEST_START_WARP_POINT() != 0)
        {
            // 워프 정보 설정
            NpcInteractionTable interactionTable = TableManager.Instance.GetTable<NpcInteractionTable>();
            NpcInteractionTableData interactionData = interactionTable.GetDataByID(nextQuestData.GetQUEST_START_WARP_POINT());

            if (!interactionData.IsNull())
            {
                var warpInfo = new TownFlowModel.WarpInfo(
                    interactionData.GetWARP_TARGET_FIELD_MAP_ID().ToString(),
                    interactionData.GetWARP_POINT(),
                    TownObjectType.WarpPoint,
                    (Transition)interactionData.GetEND_TRANSITION(),
                    true // isForMission
                );

                // 워프 상태로 전환 요청
                var stateMachine = context.GetParameter<TownStateMachine>("StateMachine");
                if (stateMachine != null)
                {
                    var warpParams = new Dictionary<string, object>
                    {
                        ["WarpInfo"] = warpInfo,
                        ["IsForMission"] = true
                    };
                    await stateMachine.TransitionTo(FlowState.Warp, warpParams);
                }
            }
        }
    }

    private async UniTask RefreshUI(TownStateContext context)
    {
        Debug.Log($"[{StateName}] Refreshing UI");

        if (TownSceneService != null)
        {
            await TownSceneService.RefreshAsync(context.Model.CurrentUI);
        }
        else
        {
            await TownSceneManager.Instance.RefreshAsync(context.Model.CurrentUI);
        }
    }

    private async UniTask ShowCompletionNotification(TownStateContext context)
    {
        Debug.Log($"[{StateName}] Showing completion notification");

        // 미션 완료 토스트 메시지
        MessageBoxManager.ShowToastMessage("Mission Complete!");

        // 완료 이펙트
        await UniTask.Delay(500);
    }

    private async UniTask CheckNextMission(TownStateContext context)
    {
        Debug.Log($"[{StateName}] Checking for next mission");

        // 다음 미션이 자동으로 시작되는지 체크
        if (missionService != null)
        {
            bool hasAutoStart = missionService.CheckHasDelayedStory(true);
            if (hasAutoStart)
            {
                await missionService.StartAutoProcess();
            }
        }
        else
        {
            if (MissionManager.Instance.CheckHasDelayedStory(true))
            {
                await MissionManager.Instance.StartAutoProcess();
            }
        }
    }

    public override async UniTask Exit()
    {
        Debug.Log($"[{StateName}] Exit");

        // 미션 관련 임시 데이터 정리
        if (missionService != null)
        {
            missionService.ResetDelayedNotifyUpdateQuest();
        }
        else
        {
            MissionManager.Instance?.ResetDelayedNotifyUpdateQuest();
        }

        await base.Exit();
    }

    public override bool CanTransitionTo(FlowState nextState)
    {
        // 미션 완료 후에는 대부분의 상태로 전환 가능
        return true;
    }
}