using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using IronJade.LowLevel.Server.Web;

// BattleProcessManagerNew의 부분 클래스로 이펙트 통합 부분 추가
public partial class BattleProcessManagerNew : MonoBehaviour
{
    // BP 관련 필드
    private int pendingBPUse = 0;  // UI에서 선택한 BP 사용량
    public int PendingBPUse
    {
        get { return pendingBPUse; }
        set { pendingBPUse = value; }
    }


    /// <summary>
    /// 모든 캐릭터의 BP 초기화
    /// </summary>
    private void InitializeAllCharacterBP()
    {
        foreach (var info in AllyInfos)
        {
            if (info != null)
            {
                info.InitializeBP();
            }
        }

        foreach (var info in EnemyInfos)
        {
            if (info != null)
            {
                info.InitializeBP();
            }
        }

        var allActors = GetAllBattleActors();
        foreach (var actor in allActors)
        {
            if (actor.BPManager != null)
            {
                actor.BPManager.ResetBP();
            }
        }

        Debug.Log("[BP] All characters initialized with 1 BP");
    }

    /// <summary>
    /// 턴 시작 시 BP 증가 처리
    /// </summary>
    private void ProcessTurnBP(BattleActor actor)
    {
        if (actor?.BattleActorInfo == null) return;

        var info = actor.BattleActorInfo;

        // skipBPGainNextTurn 체크는 BattleCharInfoNew에 추가 필요
        // 간단하게 UsedBPThisTurn으로 대체
        if (info.UsedBPThisTurn == 0 && info.Bp < info.MaxBp)
        {
            info.IncreaseBP();
            Debug.Log($"[BP] {info.Name} BP increased to {info.Bp}/{info.MaxBp}");
        }

        // 턴 시작 시 사용한 BP 리셋
        info.ResetBPThisTurn();
    }



    // StateMachine getter 추가 (BattleActor에서 접근용)
    public UnifiedBattleStateMachine GetStateMachine()
    {
        return stateMachine;
    }

    /// <summary>
    /// ExecuteAction 메서드 수정 - 이펙트 시스템 통합
    /// </summary>
    private async UniTask ExecuteAction(TurnInfo turnInfo, CancellationToken token)
    {
        var currentTurn = stateMachine.CurrentTurn;
        if (currentTurn.Target == null)
        {
            Debug.LogError($"[Battle] No target set for {turnInfo.Actor.name}");
            return;
        }

        if (currentTurn.Target.IsDead)
        {
            Debug.Log($"[Battle] Target is dead, finding new target");
            if (turnInfo.IsAllyTurn)
            {
                currentTurn.Target = BattleTargetSystem.Instance != null ?
                    BattleTargetSystem.Instance.GetDefaultTargetForAlly(enemyActors, turnInfo.CharacterIndex) :
                    GetFirstAliveEnemy();
            }
            else
            {
                //currentTurn.Target = BattleTargetSystem.Instance != null ?
                //  BattleTargetSystem.Instance.GetTargetForEnemy(allyActors) :
                //                    GetRandomAliveAlly();

                //여기 임시임. .
                var targetList = turnInfo.IsAllyTurn ? enemyActors : allyActors;


                currentTurn.Target = BattleTargetSystem.Instance != null ?
                            BattleTargetSystem.Instance.FindTargetByAggro(targetList) :
                            targetList.FirstOrDefault(t => !t.IsDead);
            }



            stateMachine.SetTarget(currentTurn.Target);

            if (currentTurn.Target == null)
            {
                Debug.LogWarning($"[Battle] No valid targets available");
                return;
            }


        }


        // BP 사용 체크 추가
        if (pendingBPUse > 0 && currentTurn.Command == "attack")
        {
            // BP 연속 공격
            await ExecuteBPAttack(turnInfo, currentTurn.Target, pendingBPUse, token);
            pendingBPUse = 0;  // 리셋
            return;
        }
        else if (pendingBPUse > 0 && currentTurn.Command == "skill")
        {
            // BP 스킬 강화 (TODO)
            Debug.Log($"[BP] Skill enhanced with {pendingBPUse} BP");
            pendingBPUse = 0;
        }



        // 커맨드에 따른 이펙트 처리
        switch (currentTurn.Command.ToLower())
        {
            case "attack":
                await ExecuteAttack(turnInfo, currentTurn.Target, token);
                break;

            case "skill":
                await ExecuteSkill(turnInfo, currentTurn.Target, token);
                break;

            default:

                Debug.LogError("#### 존재하지 않는 커맨드 ");
                break;
        }
    }



    public async UniTask ProcessBPAction(BattleActor attacker, BattleActor target, int bpAmount)
    {
        if (attacker.BPManager == null)
        {
            Debug.LogError("[BattleProcess] BPManager not found");
            return;
        }

        Debug.Log($"[BattleProcess] Processing BP action: {bpAmount} BP");

        // BP 연속 공격 실행
        attacker.ExecuteBPCombo(target, bpAmount);

        // 애니메이션 및 이펙트 처리
        await ShowBPEffects(attacker, bpAmount);
    }

    private async UniTask ShowBPEffects(BattleActor actor, int bpAmount)
    {
        // BP 사용 이펙트 표시
        // TODO: 실제 이펙트 구현
        await UniTask.Delay(500);
    }


    // BP 연속 공격 메서드 추가
    private async UniTask ExecuteBPAttack(TurnInfo turnInfo, BattleActor target, int bpCount, CancellationToken token)
    {
        var attacker = turnInfo.Actor;
        Debug.Log($"[BP Attack] {attacker.name} using {bpCount} BP for {bpCount} attacks");

       
        // BP 사용 정보를 BattleActor에 전달
        attacker.BattleActorInfo.SetBPThisTurn(bpCount);

        // BP 이펙트 표시
        await attacker.ShowBPChargeEffect(bpCount);


        // BP 개수만큼 연속 공격
        for (int i = 0; i < bpCount; i++)
        {
            // 타겟 생존 확인
            if (target.IsDead)
            {
                // 새 타겟 찾기
                target = turnInfo.IsAllyTurn ?
                    GetFirstAliveEnemy() :
                    GetRandomAliveAlly();

                if (target == null) break;
            }

            // 기존 ExecuteAttack 호출
            await ExecuteAttack(turnInfo, target, token);

            // 다음 공격까지 딜레이
            if (i < bpCount - 1)
            {
                await UniTask.Delay(300, cancellationToken: token);
            }
        }
        
        // BP 사용 완료 후 리셋
        attacker.BattleActorInfo.ResetBPThisTurn();

        //이펙트 끄기. 
        attacker.ClearBPEffect();
    }

    /// <summary>
    /// 공격 실행 (이펙트 포함)
    /// </summary>
    private async UniTask ExecuteAttack(TurnInfo turnInfo, BattleActor target, CancellationToken token)
    {
        BattleActor attacker = turnInfo.Actor;

        // 공격 애니메이션과 이펙트
        await attacker.PerformAttack();


        // 이벤트 발생
        var actionData = new ActionEventData(BattleEventType.AfterAction)
        {
            Actor = attacker,
            Target = target,
            ActionName = "Attack"
        };
        BattleEventManager.Instance.TriggerEvent(BattleEventType.AfterAction, actionData);
    }


    /// <summary>
    /// 스킬 실행 (이펙트 포함)
    /// </summary>
    private async UniTask ExecuteSkill(TurnInfo turnInfo, BattleActor target, CancellationToken token)
    {

        var attacker = turnInfo.Actor;
        Debug.Log($"[Skill] {attacker.name} uses skill on {target.name}");

        attacker.SetState(BattleActorState.Skill);

        // selectedSkillId가 설정되어 있으면 사용
        int skillId = selectedSkillId > 0 ? selectedSkillId : GetDefaultSkillId(attacker);

        Debug.LogError($"[Skill] Using skill ID: {skillId} SelectedSkill : {selectedSkillId}");

        if (attacker.SkillManager != null && skillId > 0)
        {
            var skillData = attacker.SkillManager.GetOwnedSkillById(skillId);
            if (skillData != null)
            {
                Debug.LogError($"[Skill] Using skill Name: {skillData.skillName} ");

                await attacker.UseSkill(skillData, target);
            }
        }
        else
        {

            ApplyBasicSkillDamage(attacker, target);
        }


        attacker.SetState(BattleActorState.Idle);
        // 스킬 ID 리셋
        selectedSkillId = 0;

    }


    // 헬퍼 메서드들
    private int GetDefaultSkillId(BattleActor actor)
    {
        if( actor.SkillManager.HasActiveSkills == true )
        {
            return actor.SkillManager.GetActiveSkillByIndex(0).SkillID;
        }

        return 0; // 임시
    }


    private void ApplyBasicSkillDamage(BattleActor attacker, BattleActor target)
    {
        // 기본 스킬 데미지
        var damage = attacker.BattleActorInfo.Attack * 1.5f;
        target.BattleActorInfo.TakeDamage((int)damage);
    }
}