// States/BeforeMissionUpdateState.cs
using Cysharp.Threading.Tasks;
using IronJade.Observer.Core;
using System;
using UnityEngine;

public class BeforeMissionUpdateState : ServicedTownStateBase
{
    public override string StateName => "BeforeMissionUpdate";

    private readonly IMissionService missionService;
    private readonly ITownObjectService townObjectService;

    public BeforeMissionUpdateState(IServiceContainer container = null) : base(container)
    {
        // 서비스가 있으면 사용, 없으면 싱글톤 폴백
        missionService = container?.GetService<IMissionService>();
        townObjectService = container?.GetService<ITownObjectService>();
    }

    protected override async UniTask OnEnter(TownStateContext context)
    {
        Debug.Log($"[{StateName}] Processing mission updates before transition");

        // BeforeMissionInfo 파라미터 가져오기
        var missionType = context.GetParameter<MissionContentType>("MissionType");
        var missions = context.GetParameter<BaseMission[]>("Missions");
        var webProcess = context.GetParameter<WebProcess>("WebProcess");

        // 미션 업데이트 대기 상태 설정
        SetMissionWaitState(true);

        try
        {
            // 1. 미션 처리 (있는 경우)
            if (missions != null && missions.Length > 0)
            {
                await ProcessMissions(missions, missionType);
            }

            // 2. 타운 오브젝트 갱신 (미션 조건 변경 반영)
            await RefreshTownObjects();

            // 3. 플레이어 상태 갱신
            await RefreshPlayerState();

            // 4. UI 갱신
            await UpdateMissionUI(context);

              
            await ProcessNetworkRequest(webProcess);
   

            // 6. Observer 알림
            NotifyMissionUpdate();
        }
        catch (Exception e)
        {
            Debug.LogError($"[{StateName}] Failed to update mission: {e}");
        }
        finally
        {
            SetMissionWaitState(false);
        }
    }

    private async UniTask ProcessMissions(BaseMission[] missions, MissionContentType missionType)
    {
        Debug.Log($"[{StateName}] Processing {missions.Length} missions of type {missionType}");

        foreach (var mission in missions)
        {
            if (mission == null) continue;

            Debug.Log($"[{StateName}] Processing mission: {mission.DataId}, State: {mission.GetMissionProgressState()}");

            // 미션 상태별 처리
            switch (mission.GetMissionProgressState())
            {
                case MissionProgressState.UnAccepted:
                    // 미션 수락 가능 상태로 변경
                    Debug.Log($"[{StateName}] Mission {mission.DataId} is now available");
                    break;

                case MissionProgressState.Progressing:
                    // 진행 중인 미션 업데이트
                    Debug.Log($"[{StateName}] Mission {mission.DataId} progress updated");
                    break;

                case MissionProgressState.RewardReady:
                    // 보상 대기 상태
                    Debug.Log($"[{StateName}] Mission {mission.DataId} ready for reward");
                    break;
            }
        }
    }

    private async UniTask RefreshTownObjects()
    {
        Debug.Log($"[{StateName}] Refreshing town objects");

        if (townObjectService != null)
        {
            // 서비스 사용
            await townObjectService.RefreshProcess(true);
        }
        else
        {
            // 싱글톤 폴백
            if (BackgroundSceneManager.Instance != null && BackgroundSceneManager.Instance.CheckActiveTown())
            {
                await TownObjectManager.Instance.RefreshProcess(true);
            }
        }
    }

    private async UniTask RefreshPlayerState()
    {
        Debug.Log($"[{StateName}] Refreshing player state");

        if (PlayerService != null)
        {
            // 서비스 사용
            if (PlayerService.MyPlayer?.TownPlayer != null)
            {
                await PlayerService.MyPlayer.TownPlayer.RefreshProcess();
            }
        }
        else
        {
            // 싱글톤 폴백
            if (PlayerManager.Instance?.MyPlayer?.TownPlayer != null)
            {
                await PlayerManager.Instance.MyPlayer.TownPlayer.RefreshProcess();
            }
        }

        // 행동트리 갱신을 위한 프레임 대기
        await UniTask.NextFrame();
    }

    private async UniTask UpdateMissionUI(TownStateContext context)
    {
        Debug.Log($"[{StateName}] Updating mission UI");

        // 미니맵 갱신
        if (TownMiniMapManager.Instance != null)
        {
            await TownMiniMapManager.Instance.UpdateMission(context.Model.BeforeMission);
        }

        // 추적 퀘스트 갱신
        if (missionService != null)
        {
            missionService.NotifyStoryQuestTracking();
        }
        else
        {
            MissionManager.Instance?.NotifyStoryQuestTracking();
        }
    }

    private async UniTask ProcessNetworkRequest(WebProcess webProcess)
    {
        Debug.Log($"[{StateName}] Processing network request: {webProcess}");

        // 네트워크 요청 처리 (필요한 경우)
        await UniTask.Delay(100); // 임시
    }

    private void SetMissionWaitState(bool isWait)
    {
        if (missionService != null)
        {
            missionService.SetWaitUpdateTownState(isWait);
        }
        else
        {
            MissionManager.Instance?.SetWaitUpdateTownState(isWait);
        }
    }

    private void NotifyMissionUpdate()
    {
        ObserverManager.NotifyObserver(MissionObserverID.Update, null);
    }

    public override bool CanTransitionTo(FlowState nextState)
    {
        // 미션 업데이트 중에는 특정 상태로만 전환 가능
        return nextState == FlowState.None ||
               nextState == FlowState.AfterMissionUpdate ||
               nextState == FlowState.Warp ||
               nextState == FlowState.Home;
    }
}