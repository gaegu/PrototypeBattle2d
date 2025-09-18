using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using IronJade.Table.Data;
using SkillSystem;
using System;

/// <summary>
/// BattleActor의 부분 클래스 - 스킬 시스템 통합
/// </summary>
public partial class BattleActor : MonoBehaviour
{
    
    #region Fields
    private BattleSkillManager skillManager;
    public BattleSkillManager SkillManager => skillManager;


    [Header("BP System")]
    private BPSkillManager bpManager;
    public BPSkillManager BPManager => bpManager;


    public SkillCooldownManager CooldownManager { get; private set; }
    #endregion


    #region Forced Target Fields

    // 강제 타겟 관련 필드
    private BattleActor forcedTarget = null;

    private int forcedTargetRemainingTurns;
    public int ForcedTargetRemainingTurns => forcedTargetRemainingTurns;
    private bool hasForcedTarget = false;

    // 도발자 추적 (여러 도발이 있을 경우)
    private BattleActor taunter = null;

    // 이벤트
    public event Action<BattleActor> OnForcedTargetSet;
    public event Action OnForcedTargetCleared;

    #endregion

    #region Forced Target Properties

    /// <summary>
    /// 현재 강제 타겟이 설정되어 있는지
    /// </summary>
    public bool HasForcedTarget => hasForcedTarget && forcedTarget != null && !forcedTarget.IsDead;

    /// <summary>
    /// 현재 강제 타겟
    /// </summary>
    public BattleActor ForcedTarget => forcedTarget;

    /// <summary>
    /// 도발 상태인지 (도발한 캐릭터가 있는지)
    /// </summary>
    public bool IsTaunted => taunter != null && !taunter.IsDead;

    #endregion




    #region Initialization
    /// <summary>
    /// 스킬 시스템 초기화
    /// </summary>
    private void InitializeSkillSystem()
    {
        if (skillManager == null)
        {
            skillManager = new BattleSkillManager();
            skillManager.SetOwner(this);
        }

        // 쿨다운 매니저 추가
        if (CooldownManager == null)
        {
            CooldownManager = new SkillCooldownManager(this);
        }

    }

    /// <summary>
    /// BP 시스템 초기화 (InitializeSkillSystem에 추가)
    /// </summary>
    private void InitializeBPSystem()
    {
        if (bpManager == null)
        {
            bpManager = GetComponent<BPSkillManager>();
            if (bpManager == null)
            {
                bpManager = gameObject.AddComponent<BPSkillManager>();
            }
        }
    }

    /// <summary>
    /// BP 테스트 모드 초기화
    /// </summary>
    private void InitializeBPTestMode()
    {
        Debug.Log($"[BP Test] Initializing BP test mode for {name}");

        // 1. BP Manager 설정
        if (bpManager == null)
        {
            bpManager = gameObject.AddComponent<BPSkillManager>();
        }

        // BP 데이터베이스 연결
        if (BattleActorInfo.TestBPDatabase != null)
        {
            SetBPDatabase(BattleActorInfo.TestBPDatabase);
        }

        // BP 레벨 설정
        bpManager.ResetBP();
        bpManager.SetBP(BattleActorInfo.TestBPLevel);

        // 2. 테스트 스킬 로드
        LoadTestSkills();

        // 3. BP 업그레이드 경로 자동 로드
        if (bpManager != null)
        {
            bpManager.AutoLoadUpgradePaths();
        }

        Debug.Log($"[BP Test] Setup complete: {BattleActorInfo.TestBaseSkillIds.Count} skills, BP Level: {BattleActorInfo.TestBPLevel}");
    }
    /// <summary>
    /// 테스트 스킬 로드
    /// </summary>
    private void LoadTestSkills()
    {
        if (skillManager == null)
        {
            skillManager = new BattleSkillManager();
            skillManager.SetOwner(this);
        }

        // 스킬 데이터베이스 로드
        var skillDatabase = AdvancedSkillDatabase.Load();

        foreach (int skillId in BattleActorInfo.TestBaseSkillIds)
        {
            var skillData = skillDatabase.GetSkillById(skillId);
            if (skillData != null)
            {
#if UNITY_EDITOR
                // 테스트용 메서드 사용
                skillManager.AddSkillForTest(skillData);
#else
            // 프로덕션에서는 정상 경로 사용
            skillManager.ApplySkill(skillData, this, this);
#endif

                Debug.Log($"[BP Test] Loaded skill {skillId}: {skillData.skillName}");
            }
        }
    }

    /// <summary>
    /// BP 데이터베이스 설정 (Private 필드 접근용)
    /// </summary>
    private void SetBPDatabase(BPUpgradeDatabase database)
    {
        if (bpManager == null) return;

        // 리플렉션으로 private 필드 설정
        var field = typeof(BPSkillManager).GetField("upgradeDatabase",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(bpManager, database);
        }

        // 자동 로드 활성화
        var autoLoadField = typeof(BPSkillManager).GetField("autoLoadFromDatabase",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (autoLoadField != null)
        {
            autoLoadField.SetValue(bpManager, true);
        }
    }


    #endregion

    #region Public API
    /// <summary>
    /// 스킬 사용 (테이블 ID 기반)
    /// </summary>
    public void UseSkill(AdvancedSkillData skillData, BattleActor target)
    {
        if (skillManager == null)
        {
            Debug.LogError($"[BattleActor] SkillManager not initialized for {name}");
            return;
        }

        if (skillData == null)
        {
            Debug.LogError($"[BattleActor] SkillData is null");
            return;
        }

        // MP 체크
        /*if (BattleActorInfo.Mp < skillData.manaCost)
        {
            Debug.Log($"[BattleActor] Not enough MP! Need {skillData.manaCost}, have {BattleActorInfo.Mp}");
            return;
        }

        // MP 소모
        BattleActorInfo.UseMP(skillData.manaCost);*/


        // MP 체크 제거, 쿨다운 체크로 교체
        if (!CooldownManager.CanUseSkill(skillData.skillId))
        {
            int remaining = CooldownManager.GetRemainingCooldown(skillData.skillId);
            Debug.Log($"[BattleActor] Skill on cooldown! {remaining} turns remaining");
            return;
        }

        // 쿨다운 시작
        CooldownManager.UseSkill(skillData);


        // 스킬 적용
        skillManager.ApplySkill(skillData, this, target);

        // 스킬 이펙트 시각화
        PlaySkillVisualEffect(skillData);
    }

    // BattleActor.cs에 추가
    public bool HasBuff()
    {
        // 버프 상태 체크
        if (SkillManager != null)
        {
            // 버프 스킬이 있는지 확인
            return false; // 실제 구현 필요
        }
        return false;
    }

    public bool HasDebuff()
    {
        // 디버프 상태 체크
        if (SkillManager != null)
        {
            // 디버프 상태 확인
            return false; // 실제 구현 필요
        }
        return false;
    }

    public bool HasStatusEffect(SkillSystem.StatusType statusType)
    {
        if (SkillManager != null)
        {
            return SkillManager.HasSkill(statusType);
        }
        return false;
    }



    public AdvancedSkillRuntime GetAvailableActiveSkill()
    {
        List<AdvancedSkillRuntime> listAllActiveSkill = SkillManager.GetAllActiveSkills();

        foreach (var skill in listAllActiveSkill)
        {
            if (this.CooldownManager.CanUseSkill(skill.SkillID) == true)
                return skill;
        }
        return null;
    }


    /// <summary>
    /// BP를 사용한 스킬 실행
    /// </summary>
    public void UseSkillWithBP(AdvancedSkillData skillData, BattleActor target, bool autoUpgrade = true)
    {
        if (skillData == null || target == null) return;

        // 자동 업그레이드 모드면 BP 사용하여 업그레이드
        if (autoUpgrade && bpManager != null && bpManager.CanUseBP)
        {
            // 스킬 업그레이드 시도
            bpManager.UpgradeSkillAuto(skillData.skillId);

            // 업그레이드된 데이터 가져오기
            skillData = bpManager.GetUpgradedSkillData(skillData.skillId);
        }

        // 기존 스킬 사용 로직
        UseSkill(skillData, target);
    }


    /// <summary>
    /// 그룹 대상 스킬 사용
    /// </summary>
    public void UseSkillOnGroup(AdvancedSkillData skillData, List<BattleActor> targets)
    {
        if (skillManager == null)
        {
            Debug.LogError($"[BattleActor] SkillManager not initialized for {name}");
            return;
        }

        if (skillData == null)
        {
            Debug.LogError($"[BattleActor] SkillData is null");
            return;
        }

        
        // MP 체크
        if( this.CooldownManager.CanUseSkill(skillData.skillId) == false )
        {
            Debug.Log($"[BattleActor] Not enough Cool! Need");
            return;
        }

        // 스킬 적용
        skillManager.ApplySkillToGroup(skillData, this, targets);

        // 스킬 이펙트 시각화
        PlaySkillVisualEffect(skillData);
    }

    /// <summary>
    /// 스킬 효과 적용 (외부에서 호출)
    /// 기존 호환성을 위해 유지
    /// </summary>
    public void ApplySkillEffect(BattleActor sender, int effectId, int level = 1)
    {
        // 임시 AdvancedSkillData 생성 (추후 제거 예정)
        var tempSkillData = CreateTempSkillData(effectId, level);
        if (tempSkillData != null)
        {
            skillManager?.ApplySkill(tempSkillData, sender, this);
        }
    }


    private AdvancedSkillData CreateTempSkillData(int effectId, int level)
    {
        // effectId를 기반으로 임시 스킬 데이터 생성
        var tempData = new AdvancedSkillData
        {
            skillId = effectId,
            skillName = $"Effect_{effectId}",
            description = "Temporary skill for compatibility",
            category = SkillSystem.SkillCategory.Active,
            tier = level,
            effects = new List<AdvancedSkillEffect>()
        };

        // effectId에 따라 효과 추가 (예시)
        switch (effectId)
        {
            case 2001: // Burn
                tempData.effects.Add(new AdvancedSkillEffect
                {
                    type = SkillSystem.EffectType.StatusEffect,
                    statusType = SkillSystem.StatusType.Burn,
                    targetType = SkillSystem.TargetType.EnemySingle,
                    duration = 3,
                    value = 10
                });
                break;

            case 2002: // Poison
                tempData.effects.Add(new AdvancedSkillEffect
                {
                    type = SkillSystem.EffectType.StatusEffect,
                    statusType = SkillSystem.StatusType.Poison,
                    targetType = SkillSystem.TargetType.EnemySingle,
                    duration = 5,
                    value = 15
                });
                break;

            default:
                // 기본 데미지 효과
                tempData.effects.Add(new AdvancedSkillEffect
                {
                    type = SkillSystem.EffectType.Damage,
                    targetType = SkillSystem.TargetType.EnemySingle,
                    value = 100
                });
                break;
        }

        return tempData;
    }

    #endregion

    #region Status Checks
    /// <summary>
    /// 스턴 상태 체크
    /// </summary>
    public bool IsStunned => skillManager?.HasSkill(SkillSystem.StatusType.Stun) ?? false;

    /// <summary>
    /// 침묵 상태 체크
    /// </summary>
    public bool IsSilenced => skillManager?.HasSkill(SkillSystem.StatusType.Silence) ?? false;

    /// <summary>
    /// 은신 상태 체크
    /// </summary>
   // public bool IsHidden => skillManager?.HasSkill(SkillSystem.StatusType.Hide) ?? false;

    /// <summary>
    /// 무적 상태 체크
    /// </summary>
    //public bool IsInvincible => skillManager?.HasSkill(SkillSystem.StatusType.) ?? false;

    /// <summary>
    /// 스턴 상태 설정 (개별 스킬에서 호출)
    /// </summary>
    public void SetStunned(bool stunned)
    {
        // UI 업데이트 등 추가 처리
        if (stunned)
        {
            // 스턴 시각 효과
            ShowStatusIcon(StatusType.Stun);
        }
        else
        {
            // 스턴 해제 시각 효과
            HideStatusIcon(StatusType.Stun);
        }
    }
    #endregion

    #region Visual Effects
    /// <summary>
    /// 스킬 시각 효과 재생
    /// </summary>
    private void PlaySkillVisualEffect(AdvancedSkillData skillData)
    {
        if (skillData == null)
            return;

        // 이펙트 프리팹이 있으면 재생
        if (skillData.effectPrefab != null)
        {
            var effect = Instantiate(skillData.effectPrefab, transform.position, Quaternion.identity);
            Destroy(effect, 3f); // 3초 후 제거
        }

        // 사운드 이펙트가 있으면 재생
        if (skillData.soundEffect != null)
        {
            var audioSource = GetComponent<AudioSource>();
            if (audioSource != null)
            {
                audioSource.PlayOneShot(skillData.soundEffect);
            }
        }

        // 아이콘 표시 (UI)
        if (skillData.icon != null)
        {
            // UI 매니저를 통한 스킬 아이콘 표시
            Debug.Log($"[BattleActor] Showing skill icon: {skillData.skillName}");
        }
    }

    /// <summary>
    /// 이펙트 재생 (기존 시스템 활용)
    /// </summary>
    private void PlayEffect(string effectPath, float duration)
    {
        if (BattleProcessManagerNew.Instance?.battleObjectPoolSupport == null)
            return;

        var effect = BattleProcessManagerNew.Instance.battleObjectPoolSupport.ShowEffect(
            transform,
            effectPath,
            duration > 0 ? duration : 2f // 기본 2초
        );

        Debug.Log($"[BattleActor] Playing effect: {effectPath} for {duration}s");
    }

    /// <summary>
    /// 상태이상 아이콘 표시
    /// </summary>
    private void ShowStatusIcon(StatusType statusType)
    {
        // UI 매니저를 통한 아이콘 표시
        string iconPath = GetStatusIconPath(statusType);
        if (!string.IsNullOrEmpty(iconPath))
        {
            // UI 표시 로직
            Debug.Log($"[BattleActor] Showing status icon: {statusType}");
        }
    }

    /// <summary>
    /// 상태이상 아이콘 숨기기
    /// </summary>
    private void HideStatusIcon(StatusType statusType)
    {
        // UI 매니저를 통한 아이콘 숨기기
        Debug.Log($"[BattleActor] Hiding status icon: {statusType}");
    }

    /// <summary>
    /// 상태이상별 아이콘 경로
    /// </summary>
    private string GetStatusIconPath(StatusType type)
    {
        return type switch
        {
            StatusType.Stun => "Icons/Stun",
            StatusType.Poison => "Icons/Poison",
            StatusType.Bleeding => "Icons/Bleeding",
            StatusType.Shield => "Icons/Shield",
            StatusType.Buff => "Icons/Buff",
            StatusType.DeBuff => "Icons/Debuff",
            _ => ""
        };
    }
    #endregion

    /// <summary>
    /// 매 프레임 스킬 시스템 업데이트
    /// </summary>
    private void UpdateSkillSystem()
    {
        skillManager?.Update();
    }



    /// <summary>
    /// 스킬 시스템 정리
    /// </summary>
    private void CleanupSkillSystem()
    {
        skillManager?.Clear();
    }



    #region Forced Target Methods

    /// <summary>
    /// 강제 타겟 설정
    /// </summary>
    /// <param name="target">강제로 공격할 타겟</param>
    /// <param name="duration">지속 시간 (0 = 무한)</param>
    /// <param name="isTaunt">도발로 인한 강제 타겟인지</param>
    public void SetForcedTarget(BattleActor target, int remainTurn = 0, bool isTaunt = false)
    {
        if (target == null || target == this)
        {
            Debug.LogWarning($"[ForcedTarget] Invalid target for {name}");
            return;
        }

        // 이전 강제 타겟 해제
        if (hasForcedTarget && forcedTarget != target)
        {
            ClearForcedTarget();
        }

        forcedTarget = target;
        forcedTargetRemainingTurns = remainTurn;
        hasForcedTarget = true;

        // 도발인 경우 도발자 기록
        if (isTaunt)
        {
            taunter = target;
            // 도발 상태 플래그 추가
            BattleActorInfo?.AddStatusFlag(StatusFlag.Taunted);
        }

       /*Debug.Log($"[ForcedTarget] {name} forced to target {target.name}" +
                 (duration > 0 ? $" for {duration} seconds" : " indefinitely") +
                 (isTaunt ? " (Taunted)" : ""));*/

        // 이벤트 발생
        OnForcedTargetSet?.Invoke(target);

        // UI 업데이트 (도발 아이콘 등)
        UpdateForcedTargetUI(true);
    }

    /// <summary>
    /// 강제 타겟 해제
    /// </summary>
    public void ClearForcedTarget()
    {
        if (!hasForcedTarget)
            return;

        var previousTarget = forcedTarget;

        forcedTarget = null;
        forcedTargetRemainingTurns = 0;
        
        hasForcedTarget = false;

        // 도발 해제
        if (taunter != null)
        {
            taunter = null;
            BattleActorInfo?.RemoveStatusFlag(StatusFlag.Taunted);
        }

        Debug.Log($"[ForcedTarget] {name} cleared forced target" +
                 (previousTarget != null ? $" ({previousTarget.name})" : ""));

        // 이벤트 발생
        OnForcedTargetCleared?.Invoke();

        // UI 업데이트
        UpdateForcedTargetUI(false);
    }

    /// <summary>
    /// 타겟 가져오기 (강제 타겟 우선)
    /// </summary>
    public BattleActor GetCurrentTarget(BattleActor defaultTarget = null)
    {
        // 강제 타겟이 있으면 우선
        if (HasForcedTarget)
        {
            return forcedTarget;
        }

        // 도발 상태 체크 (다른 방식의 도발)
        if (IsTaunted)
        {
            return taunter;
        }

        // 기본 타겟 반환
        return defaultTarget;
    }

    /// <summary>
    /// 공격 가능한 타겟 선택 (강제 타겟 고려)
    /// </summary>
    public BattleActor SelectTarget(System.Collections.Generic.List<BattleActor> potentialTargets)
    {
        // 강제 타겟이 있고 유효하면 반환
        if (HasForcedTarget)
        {
            // 강제 타겟이 선택 가능한 목록에 있는지 확인
            if (potentialTargets.Contains(forcedTarget))
            {
                Debug.Log($"[ForcedTarget] {name} must attack forced target: {forcedTarget.name}");
                return forcedTarget;
            }
            else
            {
                Debug.LogWarning($"[ForcedTarget] Forced target {forcedTarget.name} not in valid targets list");
                // 강제 타겟이 유효하지 않으면 해제
                ClearForcedTarget();
            }
        }

        // 일반 타겟 선택 로직
        return SelectNormalTarget(potentialTargets);
    }

    /// <summary>
    /// 일반 타겟 선택 (강제 타겟이 없을 때)
    /// </summary>
    private BattleActor SelectNormalTarget(System.Collections.Generic.List<BattleActor> potentialTargets)
    {
        if (potentialTargets == null || potentialTargets.Count == 0)
            return null;

        // BattleTargetSystem을 사용한 타겟 선택
        if (BattleTargetSystem.Instance != null)
        {
            return BattleTargetSystem.Instance.FindTargetByAggro(potentialTargets);
        }

        // Fallback: 첫 번째 유효한 타겟
        return potentialTargets.Find(t => t != null && !t.IsDead);
    }



    /// <summary>
    /// 턴 종료 시 강제 타겟 체크
    /// </summary>
    public void OnTurnEndForcedTargetCheck()
    {
        // 도발자가 죽었으면 해제
        if (taunter != null && taunter.IsDead)
        {
            Debug.Log($"[ForcedTarget] Taunter died, clearing forced target for {name}");
            ClearForcedTarget();
            return;
        }

        // 일반 강제 타겟이 죽었으면 해제
        if (forcedTarget != null && forcedTarget.IsDead)
        {
            Debug.Log($"[ForcedTarget] Forced target died, clearing for {name}");
            ClearForcedTarget();
        }

        if (hasForcedTarget && forcedTargetRemainingTurns > 0)
        {
            UpdateForcedTargetTurn();
        }
    }

    #endregion

    #region UI Updates

    /// <summary>
    /// 강제 타겟 UI 업데이트
    /// </summary>
    private void UpdateForcedTargetUI(bool isForced)
    {
        if (actorTag != null)
        {
            // 도발 아이콘 표시/숨김
            if (isForced && IsTaunted)
            {
                // actorTag.ShowStatusIcon(StatusIconType.Taunt);

                // 타겟 라인 표시 (선택적)
                ShowTargetLine(forcedTarget);
            }
            else
            {
                // actorTag.HideStatusIcon(StatusIconType.Taunt);
                HideTargetLine();
            }
        }
    }


    public void UpdateForcedTargetTurn()
    {
        if (!hasForcedTarget || forcedTargetRemainingTurns <= 0)
            return;

        forcedTargetRemainingTurns--;

        Debug.Log($"[ForcedTarget] {name} forced target turns remaining: {forcedTargetRemainingTurns}");

        if (forcedTargetRemainingTurns <= 0)
        {
            Debug.Log($"[ForcedTarget] Turns expired for {name}");
            ClearForcedTarget();
        }
    }

    /// <summary>
    /// 타겟 라인 표시
    /// </summary>
    private void ShowTargetLine(BattleActor target)
    {
        if (target == null) return;

        // 라인 렌더러나 UI 라인으로 연결 표시
        // 구현 예시:
        /*
        if (targetLineRenderer == null)
        {
            GameObject lineObj = new GameObject("TargetLine");
            lineObj.transform.SetParent(transform);
            targetLineRenderer = lineObj.AddComponent<LineRenderer>();
            // 라인 렌더러 설정
        }
        
        targetLineRenderer.enabled = true;
        targetLineRenderer.SetPosition(0, transform.position);
        targetLineRenderer.SetPosition(1, target.transform.position);
        */
    }

    /// <summary>
    /// 타겟 라인 숨김
    /// </summary>
    private void HideTargetLine()
    {
        /*
        if (targetLineRenderer != null)
        {
            targetLineRenderer.enabled = false;
        }
        */
    }

    // =====================================================
    // 7. 남은 턴 확인 메서드 (신규 추가)
    // =====================================================

    /// <summary>
    /// 강제 타겟 남은 턴 수 가져오기
    /// </summary>
    public int GetForcedTargetRemainingTurns()
    {
        return forcedTargetRemainingTurns;
    }

    // =====================================================
    // 8. 턴 연장 메서드 (신규 추가)
    // =====================================================

    /// <summary>
    /// 강제 타겟 턴 수 연장
    /// </summary>
    public void ExtendForcedTargetTurns(int additionalTurns)
    {
        if (hasForcedTarget && forcedTargetRemainingTurns > 0)
        {
            forcedTargetRemainingTurns += additionalTurns;
            Debug.Log($"[ForcedTarget] Extended {name}'s forced target by {additionalTurns} turns. " +
                     $"Total remaining: {forcedTargetRemainingTurns}");
        }
    }


    #endregion
}