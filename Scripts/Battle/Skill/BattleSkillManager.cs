using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using SkillSystem;

/// <summary>
/// 스킬 시스템 매니저 - 새로운 AdvancedSkillRuntime 전용
/// 기존 BattleSkillBase 시스템 완전 제거
/// </summary>
public class BattleSkillManager
{
    #region Core Fields
    private BattleActor owner;

    // 대기 큐 (다음 프레임에 적용될 스킬들)
    private Queue<AdvancedSkillRuntime> pendingSkills = new Queue<AdvancedSkillRuntime>();

    // 활성 스킬 관리
    private Dictionary<int, List<AdvancedSkillRuntime>> skillsById = new Dictionary<int, List<AdvancedSkillRuntime>>();
    private List<AdvancedSkillRuntime> activeSkills = new List<AdvancedSkillRuntime>();

    // StatusType별 빠른 검색용
    private Dictionary<SkillSystem.StatusType, List<AdvancedSkillRuntime>> skillsByStatusType = new Dictionary<SkillSystem.StatusType, List<AdvancedSkillRuntime>>();

    // 그룹별 스킬 관리
    private Dictionary<EffectGroupType, List<AdvancedSkillRuntime>> skillsByGroup = new Dictionary<EffectGroupType, List<AdvancedSkillRuntime>>();

    // 파티클 이펙트 관리
    private Dictionary<AdvancedSkillRuntime, List<GameObject>> particleEffects = new Dictionary<AdvancedSkillRuntime, List<GameObject>>();

    // 임시 리스트 (GC 최적화)
    private List<AdvancedSkillRuntime> tempUpdateList = new List<AdvancedSkillRuntime>();
    private List<AdvancedSkillRuntime> tempRemoveList = new List<AdvancedSkillRuntime>();
    #endregion

    #region Properties
    public BattleActor Owner => owner;
    public int ActiveSkillCount => activeSkills.Count;
    public bool HasActiveSkills => activeSkills.Count > 0;
    #endregion

    #region Initialization
    /// <summary>
    /// 오너 설정
    /// </summary>
    public void SetOwner(BattleActor actor)
    {
        owner = actor;
        Clear();
    }
    #endregion

    #region Main API - Skill Application

    /// <summary>
    /// 스킬 적용 - 메인 메서드
    /// </summary>
    public void ApplySkill(AdvancedSkillData skillData, BattleActor sender, BattleActor receiver)
    {
        if (!ValidateParameters(skillData, sender, receiver))
            return;

        // 데이터 검증
        if (!AdvancedSkillFactory.ValidateSkillData(skillData, out string error))
        {
            Debug.LogError($"[SkillManager] Invalid skill data: {error}");
            return;
        }

        // 차단 체크 (면역, 저주 등)
        if (IsBlockedSkill(skillData))
        {
            Debug.Log($"[SkillManager] Skill {skillData.skillName} blocked");
            return;
        }

        // 적절한 런타임 생성
        var skillRuntime = AdvancedSkillFactory.CreateSkill(skillData);
        if (skillRuntime == null)
        {
            Debug.LogError($"[SkillManager] Failed to create skill runtime for {skillData.skillName}");
            return;
        }

        // 스킬 초기화
        skillRuntime.Initialize(sender, receiver, skillData);

        // 시작 조건 체크
        if (!skillRuntime.CheckStartCondition(sender, receiver))
        {
            Debug.Log($"[SkillManager] Skill {skillData.skillName} start condition not met");
            AdvancedSkillFactory.ReturnToPool(skillRuntime);
            return;
        }

        // 중첩 처리
        if (!HandleOverlap(skillRuntime, skillData))
        {
            AdvancedSkillFactory.ReturnToPool(skillRuntime);
            return;
        }

        // 즉발 vs 지속 처리
        if (skillRuntime.IsOneShot)
        {
            // 즉시 실행 후 종료
            skillRuntime.OnStart();
            skillRuntime.OnEnd();
            AdvancedSkillFactory.ReturnToPool(skillRuntime);

            // 통계 기록
            AdvancedSkillFactory.Statistics.RecordCreation("OneShot");
        }
        else
        {
            // StatusEffect는 즉시 등록 필요
            bool isStatusEffect = skillData.effects.Any(e =>
                e.type == SkillSystem.EffectType.StatusEffect);

            if (isStatusEffect)
            {
                // StatusEffect는 즉시 등록
                skillRuntime.OnStart();
                RegisterSkill(skillRuntime);
                Debug.Log($"[SkillManager] StatusEffect {skillData.skillName} immediately registered");
            }
            else
            {
                // 다른 지속 효과는 기존대로 대기열에
                PendingSkill(skillRuntime);
            }

            AdvancedSkillFactory.Statistics.RecordCreation("Persistent");
        }

        // 이펙트 재생
        PlaySkillEffect(skillData, sender, receiver);
    }

    /// <summary>
    /// 타겟 그룹에 스킬 적용
    /// </summary>
    public void ApplySkillToGroup(AdvancedSkillData skillData, BattleActor sender, List<BattleActor> receivers)
    {
        foreach (var receiver in receivers)
        {
            if (receiver != null && !receiver.IsDead)
            {
                ApplySkill(skillData, sender, receiver);
            }
        }
    }

    /// <summary>
    /// 스킬 ID로 적용 (편의 메서드)
    /// </summary>
    public void ApplySkillById(int skillId, BattleActor sender, BattleActor receiver)
    {
        // SkillDatabase에서 로드
        var skillData = LoadSkillData(skillId);
        if (skillData != null)
        {
            ApplySkill(skillData, sender, receiver);
        }
    }

    #endregion

    #region Overlap Handling

    /// <summary>
    /// 중첩 처리
    /// </summary>
    private bool HandleOverlap(AdvancedSkillRuntime newSkill, AdvancedSkillData skillData)
    {
        var existingSkills = GetSkillsById(newSkill.SkillID);

        if (existingSkills.Count == 0)
            return true; // 첫 적용

        if (newSkill.IsStackable)
        {
            // 최대 스택 체크
            var maxStacks = skillData.effects.Max(e => e.maxStacks);
            if (maxStacks <= 0) maxStacks = 5; // 기본값

            if (existingSkills.Count >= maxStacks)
            {
                // 가장 오래된 것 제거
                var oldest = existingSkills.OrderBy(s => s.RemainTurn).First();
                RemoveSkill(oldest);
            }
            return true;
        }
        else
        {
            // 중첩 불가 - 기존 스킬 갱신
            var existing = existingSkills.First();
            existing.RefreshDuration();
            Debug.Log($"[SkillManager] Refreshed duration for skill {existing.SkillName}");
            return false;
        }
    }

    #endregion

    #region Update Loop

    /// <summary>
    /// 매 프레임 업데이트 (BattleActor에서 호출)
    /// </summary>
    public void Update()
    {
        // 대기 중인 스킬 처리
        ProcessPendingSkills();

        // 턴제 게임이므로 매 프레임 업데이트는 최소화
        // 필요시 이펙트나 애니메이션만 업데이트
    }

    /// <summary>
    /// 턴 업데이트 (턴 종료 시 호출)
    /// </summary>
    public void OnTurnUpdate()
    {
        tempUpdateList.Clear();
        tempUpdateList.AddRange(activeSkills);

        foreach (var skill in tempUpdateList)
        {
            if (skill != null)
            {
                skill.OnTurnUpdate();

                if (skill.CheckEndCondition())
                {
                    tempRemoveList.Add(skill);
                }
            }
        }

        RemoveSkills(tempRemoveList);
    }

    /// <summary>
    /// 대기 스킬 처리
    /// </summary>
    private void ProcessPendingSkills()
    {
        while (pendingSkills.Count > 0)
        {
            var skill = pendingSkills.Dequeue();
            if (skill == null) continue;

            RegisterSkill(skill);
        }
    }

    #endregion

    #region Skill Lifecycle

    /// <summary>
    /// 스킬 대기 등록
    /// </summary>
    private void PendingSkill(AdvancedSkillRuntime skill)
    {
        skill.OnStart();
        skill.AddObserverIds();
        pendingSkills.Enqueue(skill);

        Debug.Log($"[SkillManager] Pending skill: {skill.SkillName}");
    }

    /// <summary>
    /// 스킬 등록
    /// </summary>
    private void RegisterSkill(AdvancedSkillRuntime skill)
    {
        // ID별 목록
        if (!skillsById.ContainsKey(skill.SkillID))
        {
            skillsById[skill.SkillID] = new List<AdvancedSkillRuntime>();
        }
        skillsById[skill.SkillID].Add(skill);

        // 활성 목록
        activeSkills.Add(skill);

        // StatusType별 목록
        var statusType = skill.GetStatusType();
        if (statusType != SkillSystem.StatusType.None)
        {
            if (!skillsByStatusType.ContainsKey(statusType))
            {
                skillsByStatusType[statusType] = new List<AdvancedSkillRuntime>();
            }
            skillsByStatusType[statusType].Add(skill);
        }

        // 그룹별 목록
        var groupType = skill.GetEffectGroupType();
        if (groupType != EffectGroupType.None)
        {
            if (!skillsByGroup.ContainsKey(groupType))
            {
                skillsByGroup[groupType] = new List<AdvancedSkillRuntime>();
            }
            skillsByGroup[groupType].Add(skill);
        }

        Debug.Log($"[SkillManager] Registered skill: {skill.SkillName}");
    }

    /// <summary>
    /// 스킬 종료
    /// </summary>
    private void EndSkill(AdvancedSkillRuntime skill)
    {
        skill.OnEnd();
        skill.RemoveObserverIds();
        CleanupParticleEffects(skill);

        Debug.Log($"[SkillManager] Ended skill: {skill.SkillName}");
    }

    /// <summary>
    /// 스킬 제거
    /// </summary>
    public void RemoveSkill(AdvancedSkillRuntime skill)
    {
        if (skill == null) return;

        EndSkill(skill);
        UnregisterSkill(skill);

        // 풀에 반환
        AdvancedSkillFactory.ReturnToPool(skill);
    }

    /// <summary>
    /// 여러 스킬 제거
    /// </summary>
    private void RemoveSkills(List<AdvancedSkillRuntime> skillsToRemove)
    {
        foreach (var skill in skillsToRemove)
        {
            RemoveSkill(skill);
        }
        skillsToRemove.Clear();
    }

    /// <summary>
    /// 스킬 등록 해제
    /// </summary>
    private void UnregisterSkill(AdvancedSkillRuntime skill)
    {
        // ID별 목록에서 제거
        if (skillsById.TryGetValue(skill.SkillID, out var skillList))
        {
            skillList.Remove(skill);
            if (skillList.Count == 0)
            {
                skillsById.Remove(skill.SkillID);
            }
        }

        // 활성 목록에서 제거
        activeSkills.Remove(skill);

        // StatusType별 목록에서 제거
        var statusType = skill.GetStatusType();
        if (statusType != SkillSystem.StatusType.None && skillsByStatusType.ContainsKey(statusType))
        {
            skillsByStatusType[statusType].Remove(skill);
            if (skillsByStatusType[statusType].Count == 0)
            {
                skillsByStatusType.Remove(statusType);
            }
        }

        // 그룹별 목록에서 제거
        var groupType = skill.GetEffectGroupType();
        if (groupType != EffectGroupType.None && skillsByGroup.ContainsKey(groupType))
        {
            skillsByGroup[groupType].Remove(skill);
            if (skillsByGroup[groupType].Count == 0)
            {
                skillsByGroup.Remove(groupType);
            }
        }
    }

    #endregion

    #region Query Methods

    /// <summary>
    /// 특정 ID의 스킬 목록 가져오기
    /// </summary>
    public List<AdvancedSkillRuntime> GetSkillsById(int skillId)
    {
        if (skillsById.TryGetValue(skillId, out var skills))
        {
            return new List<AdvancedSkillRuntime>(skills);
        }
        return new List<AdvancedSkillRuntime>();
    }

    public AdvancedSkillRuntime GetSkillById(int skillId)
    {
        List<AdvancedSkillRuntime> listSkillRuntime = GetSkillsById(skillId);
        if (listSkillRuntime.Count > 0)
            return listSkillRuntime[0];

        return null;
    }

    /// <summary>
    /// 스킬 런타임 직접 추가 (테스트용)
    /// </summary>
    public void AddSkillRuntime(AdvancedSkillRuntime runtime)
    {
        if (runtime == null) return;
        RegisterSkill(runtime);
    }

    /// <summary>
    /// 스킬 데이터 업데이트
    /// </summary>
    public void UpdateSkillData(int skillId, AdvancedSkillData newData)
    {
        if (skillsById.TryGetValue(skillId, out var skills))
        {
            foreach (var skill in skills)
            {
                skill.UpdateSkillData(newData);
            }
        }
    }


    /// <summary>
    /// 특정 StatusType 스킬 존재 여부
    /// </summary>
    public bool HasSkill(SkillSystem.StatusType statusType)
    {
        return skillsByStatusType.ContainsKey(statusType) &&
               skillsByStatusType[statusType].Count > 0;
    }

    /// <summary>
    /// 특정 StatusType 스킬 개수
    /// </summary>
    public int GetSkillCount(SkillSystem.StatusType statusType)
    {
        if (skillsByStatusType.TryGetValue(statusType, out var skills))
        {
            return skills.Count;
        }
        return 0;
    }

    /// <summary>
    /// 특정 StatusType의 첫 번째 스킬 가져오기
    /// </summary>
    public AdvancedSkillRuntime GetSkillByType(SkillSystem.StatusType statusType)
    {
        if (skillsByStatusType.TryGetValue(statusType, out var skills) && skills.Count > 0)
        {
            return skills[0];
        }
        return null;
    }

    /// <summary>
    /// 특정 그룹의 스킬 목록
    /// </summary>
    public List<AdvancedSkillRuntime> GetSkillsByGroup(EffectGroupType groupType)
    {
        if (skillsByGroup.TryGetValue(groupType, out var skills))
        {
            return new List<AdvancedSkillRuntime>(skills);
        }
        return new List<AdvancedSkillRuntime>();
    }

    /// <summary>
    /// 모든 활성 스킬 목록
    /// </summary>
    public List<AdvancedSkillRuntime> GetAllActiveSkills()
    {
        return new List<AdvancedSkillRuntime>(activeSkills);
    }



    /// <summary>
    /// Reflectバフを取得
    /// </summary>
    public List<AdvancedSkillRuntime> GetReflectBuffs()
    {
        var reflectBuffs = new List<AdvancedSkillRuntime>();

        foreach (var skill in activeSkills)
        {
            if (skill.SkillData != null && skill.SkillData.effects.Any(e => e.type == SkillSystem.EffectType.Reflect))
            {
                reflectBuffs.Add(skill);
            }
        }

        return reflectBuffs;
    }



    /// <summary>
    /// Reflect効果があるか確認
    /// </summary>
    public bool HasReflect()
    {
        return GetReflectBuffs().Count > 0;
    }

    /// <summary>
    /// 最大Reflect率を取得
    /// </summary>
    public float GetMaxReflectPercent()
    {
        var reflectBuffs = GetReflectBuffs();
        if (reflectBuffs.Count == 0) return 0;

        float maxReflect = 0;
        foreach (var buff in reflectBuffs)
        {
            foreach (var effect in buff.SkillData.effects)
            {
                if (effect.type == SkillSystem.EffectType.Reflect)
                {
                    maxReflect = Mathf.Max(maxReflect, effect.value);
                }
            }
        }

        return maxReflect;
    }
    /// <summary>
    /// 데미지 무시 체크
    /// </summary>
    public bool IsIgnoreDamage()
    {
        //return HasSkill(SkillSystem.StatusType.) || HasSkill(SkillSystem.StatusType.Invincible);
        return false;
    }

    /// <summary>
    /// 행동 불가 상태 체크
    /// </summary>
    public bool IsIncapacitated()
    {
        return HasSkill(SkillSystem.StatusType.Stun) ||
               HasSkill(SkillSystem.StatusType.Sleep) ||
               HasSkill(SkillSystem.StatusType.Freeze);
    }

    /// <summary>
    /// 타겟 불가 상태 체크
    /// </summary>
    public bool IsUntargetable()
    {
        return HasSkill(SkillSystem.StatusType.Sleep);
    }

    /// <summary>
    /// 공격 불가 상태 체크
    /// </summary>
    public bool CannotAttack()
    {
        return IsIncapacitated();// || HasSkill(SkillSystem.StatusType.Provoke);
    }

    /// <summary>
    /// 스킬 사용 불가 상태 체크
    /// </summary>
    public bool CannotUseSkill()
    {
        return IsIncapacitated() || HasSkill(SkillSystem.StatusType.Silence);
    }

    /// <summary>
    /// 특정 상태이상 체크 (외부 호출용 - 기존 호환)
    /// </summary>
    public bool IsBattleSkillBase(SkillSystem.StatusType statusType)
    {
        return HasSkill(statusType);
    }

    #endregion

    #region Buff/Debuff Management

    /// <summary>
    /// 모든 디버프 제거
    /// </summary>
    /// <summary>
    /// 지정된 개수만큼 디버프 제거 (우선순위 적용)
    /// </summary>
    public int RemoveDebuffs(int count = 99, DispelPriority priority = DispelPriority.Random)
    {
        var debuffs = GetSkillsByGroup(EffectGroupType.Debuff);

        if (debuffs.Count == 0)
            return 0;

        // 우선순위에 따라 정렬
        var sortedDebuffs = SortByDispelPriority(debuffs, priority, false);

        // 지정된 개수만큼 제거
        int removeCount = Mathf.Min(count, sortedDebuffs.Count);
        var toRemove = sortedDebuffs.GetRange(0, removeCount);

        RemoveSkills(toRemove);

        Debug.Log($"[SkillManager] Removed {removeCount} debuffs");
        return removeCount;
    }

    /// <summary>
    /// 지정된 개수만큼 버프 제거 (우선순위 적용)
    /// </summary>
    public int RemoveBuffs(int count = 99, DispelPriority priority = DispelPriority.Random)
    {
        var buffs = GetSkillsByGroup(EffectGroupType.Buff);

        if (buffs.Count == 0)
            return 0;

        // 우선순위에 따라 정렬
        var sortedBuffs = SortByDispelPriority(buffs, priority, true);

        // 지정된 개수만큼 제거
        int removeCount = Mathf.Min(count, sortedBuffs.Count);
        var toRemove = sortedBuffs.GetRange(0, removeCount);

        RemoveSkills(toRemove);

        Debug.Log($"[SkillManager] Removed {removeCount} buffs");
        return removeCount;
    }


    /// <summary>
    /// 지정된 개수만큼 상태이상 제거
    /// </summary>
    public int RemoveStatusEffects(int count = 99, DispelPriority priority = DispelPriority.Random)
    {
        // StatusEffect 타입의 스킬들 찾기
        var statusEffects = new List<AdvancedSkillRuntime>();

        foreach (var kvp in skillsByStatusType)
        {
            // 실제 상태이상만 (버프/디버프 제외)
            if (IsStatusEffect(kvp.Key))
            {
                statusEffects.AddRange(kvp.Value);
            }
        }

        if (statusEffects.Count == 0)
            return 0;

        // 우선순위에 따라 정렬
        var sortedEffects = SortByDispelPriority(statusEffects, priority, false);

        // 지정된 개수만큼 제거
        int removeCount = Mathf.Min(count, sortedEffects.Count);
        var toRemove = sortedEffects.GetRange(0, removeCount);

        RemoveSkills(toRemove);

        Debug.Log($"[SkillManager] Removed {removeCount} status effects");
        return removeCount;
    }

    /// <summary>
    /// Dispel 우선순위에 따라 정렬
    /// </summary>
    private List<AdvancedSkillRuntime> SortByDispelPriority(
        List<AdvancedSkillRuntime> skills,
        DispelPriority priority,
        bool isBuff)
    {
        var sorted = new List<AdvancedSkillRuntime>(skills);

        switch (priority)
        {
            case DispelPriority.Random:
                // 랜덤 순서
                for (int i = 0; i < sorted.Count; i++)
                {
                    int randomIndex = UnityEngine.Random.Range(i, sorted.Count);
                    var temp = sorted[i];
                    sorted[i] = sorted[randomIndex];
                    sorted[randomIndex] = temp;
                }
                break;

            case DispelPriority.Newest:
                // 최신 것부터 (RemainTurn이 많은 순)
                sorted.Sort((a, b) => b.RemainTurn.CompareTo(a.RemainTurn));
                break;

            case DispelPriority.Oldest:
                // 오래된 것부터 (RemainTurn이 적은 순)
                sorted.Sort((a, b) => a.RemainTurn.CompareTo(b.RemainTurn));
                break;

            case DispelPriority.Strongest:
                // 효과가 강한 것부터 (value 기준)
                sorted.Sort((a, b) =>
                {
                    float aValue = a.CurrentEffect?.value ?? 0;
                    float bValue = b.CurrentEffect?.value ?? 0;
                    return bValue.CompareTo(aValue);
                });
                break;

            case DispelPriority.Weakest:
                // 효과가 약한 것부터
                sorted.Sort((a, b) =>
                {
                    float aValue = a.CurrentEffect?.value ?? 0;
                    float bValue = b.CurrentEffect?.value ?? 0;
                    return aValue.CompareTo(bValue);
                });
                break;
        }

        return sorted;
    }


    /// <summary>
    /// 상태이상 타입 체크 (버프/디버프와 구분)
    /// </summary>
    private bool IsStatusEffect(SkillSystem.StatusType type)
    {
        // 실제 상태이상 타입들
        return type == SkillSystem.StatusType.Stun ||
               type == SkillSystem.StatusType.Freeze ||
               type == SkillSystem.StatusType.Petrify ||
               type == SkillSystem.StatusType.Sleep ||
               type == SkillSystem.StatusType.Silence ||
               type == SkillSystem.StatusType.Blind ||
               type == SkillSystem.StatusType.Slow ||
               type == SkillSystem.StatusType.Poison ||
               type == SkillSystem.StatusType.Burn ||
               type == SkillSystem.StatusType.Bleeding ||
               type == SkillSystem.StatusType.Confuse ||
               type == SkillSystem.StatusType.Fear ||
               type == SkillSystem.StatusType.Curse ||
               type == SkillSystem.StatusType.Weaken;
    }



    /// <summary>
    /// 특정 StatusType의 모든 스킬 제거
    /// </summary>
    public void RemoveSkillsByType(SkillSystem.StatusType statusType)
    {
        if (skillsByStatusType.TryGetValue(statusType, out var skills))
        {
            var skillsToRemove = new List<AdvancedSkillRuntime>(skills);
            RemoveSkills(skillsToRemove);
        }
    }

    /// <summary>
    /// 특정 ID의 모든 스킬 제거
    /// </summary>
    public void RemoveSkillsById(int skillId)
    {
        if (skillsById.TryGetValue(skillId, out var skills))
        {
            var skillsToRemove = new List<AdvancedSkillRuntime>(skills);
            RemoveSkills(skillsToRemove);
        }
    }

    /// <summary>
    /// 모든 버프 지속시간 연장
    /// </summary>
    public void ExtendBuffDurations(int additionalTurns)
    {
        var buffs = GetSkillsByGroup(EffectGroupType.Buff);
        foreach (var buff in buffs)
        {
            buff.ExtendDuration(additionalTurns);
        }
    }

    /// <summary>
    /// 특정 스킬의 지속시간 갱신
    /// </summary>
    public void RefreshSkillDuration(SkillSystem.StatusType statusType, int newDuration)
    {
        var skill = GetSkillByType(statusType);
        if (skill != null)
        {
            skill.SetRemainTurn(newDuration);
        }
    }

    #endregion

    #region Blocking Checks

    /// <summary>
    /// 스킬 차단 체크
    /// </summary>
    private bool IsBlockedSkill(AdvancedSkillData skillData)
    {
        if (skillData.effects.Count > 0)
        {
            var firstEffect = skillData.effects[0];

            // StatusEffect인 경우 면역 체크
            if (firstEffect.type == SkillSystem.EffectType.StatusEffect)
            {
               // var statusType = ConvertToStatusType(firstEffect.statusType);
                if (firstEffect.type == SkillSystem.EffectType.Immunity )
                {
                    Debug.Log($"[SkillManager] Skill blocked by immunity: {firstEffect.statusType}");
                    return true;
                }
            }

            // 저주 체크
            if (CheckCursed(skillData.skillId))
            {
                Debug.Log($"[SkillManager] Skill blocked by curse: {skillData.skillId}");
                return true;
            }
        }

        return false;
    }


    /// <summary>
    /// 저주 체크
    /// </summary>
    private bool CheckCursed(int skillId)
    {
        var curseSkill = GetSkillByType(SkillSystem.StatusType.Curse);
        if (curseSkill != null)
        {
            // 저주가 있으면 특정 스킬 차단
            // 구체적인 저주 로직은 스킬 데이터에 따라 결정
            return false; // 기본적으로는 차단하지 않음
        }
        return false;
    }

    #endregion

    #region Effects Management

    /// <summary>
    /// 스킬 이펙트 재생
    /// </summary>
    private void PlaySkillEffect(AdvancedSkillData skillData, BattleActor sender, BattleActor target)
    {
        // 이펙트 프리팹이 있으면 재생
        if (skillData.effectPrefab != null)
        {
            var effect = GameObject.Instantiate(
                skillData.effectPrefab,
                target.transform.position,
                Quaternion.identity
            );

            // 자동 제거
            GameObject.Destroy(effect, 2f);
        }

        // 사운드 이펙트 재생
        if (skillData.soundEffect != null && owner != null)
        {
            var audioSource = owner.GetComponent<AudioSource>();
            if (audioSource != null)
            {
                audioSource.PlayOneShot(skillData.soundEffect);
            }
        }
    }

    /// <summary>
    /// 파티클 이펙트 정리
    /// </summary>
    private void CleanupParticleEffects(AdvancedSkillRuntime skill)
    {
        if (particleEffects.TryGetValue(skill, out var particles))
        {
            foreach (var particle in particles)
            {
                if (particle != null)
                {
                    GameObject.Destroy(particle);
                }
            }
            particleEffects.Remove(skill);
        }
    }

    #endregion

    #region Cleanup

    /// <summary>
    /// 모든 스킬 정리
    /// </summary>
    public void Clear()
    {
        // 모든 스킬 종료
        StopAllSkills();

        // 컬렉션 초기화
        pendingSkills.Clear();
        skillsById.Clear();
        activeSkills.Clear();
        skillsByStatusType.Clear();
        skillsByGroup.Clear();
        particleEffects.Clear();
    }

    /// <summary>
    /// 모든 활성 스킬 중지
    /// </summary>
    private void StopAllSkills()
    {
        foreach (var skill in activeSkills.ToList())
        {
            if (skill != null)
            {
                EndSkill(skill);
            }
        }
        activeSkills.Clear();
    }

    /// <summary>
    /// 강제로 특정 스킬 종료
    /// </summary>
    public void ForceEndSkill(SkillSystem.StatusType statusType)
    {
        var skill = GetSkillByType(statusType);
        if (skill != null)
        {
            skill.ForceEnd();
            RemoveSkill(skill);
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// 파라미터 검증
    /// </summary>
    private bool ValidateParameters(AdvancedSkillData skillData, BattleActor sender, BattleActor receiver)
    {
        if (skillData == null)
        {
            Debug.LogError("[SkillManager] Skill data is null");
            return false;
        }

        if (sender == null)
        {
            Debug.LogError("[SkillManager] Sender is null");
            return false;
        }

        if (receiver == null)
        {
            Debug.LogError("[SkillManager] Receiver is null");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 스킬 데이터 로드 (ID 기반)
    /// </summary>
    private AdvancedSkillData LoadSkillData(int skillId)
    {
        // SkillDatabase에서 로드하거나
        // ScriptableObject에서 로드하거나
        // 임시로 null 반환
        Debug.LogWarning($"[SkillManager] Skill data loading not implemented for ID: {skillId}");
        return null;
    }

    #endregion

    #region Debug

    /// <summary>
    /// 스킬 상태 정보 출력
    /// </summary>
    public void PrintSkillStatus()
    {
        Debug.Log($"[SkillManager] === Skill Status for {owner?.name ?? "Unknown"} ===");
        Debug.Log($"Pending Skills: {pendingSkills.Count}");
        Debug.Log($"Active Skills: {activeSkills.Count}");
        Debug.Log($"Skills by ID: {skillsById.Count} unique IDs");
        Debug.Log($"Skills by StatusType: {skillsByStatusType.Count} types");
        Debug.Log($"Skills by Group: {skillsByGroup.Count} groups");

        foreach (var skill in activeSkills)
        {
            Debug.Log($"  - {skill.SkillName} (ID:{skill.SkillID}): {skill.RemainTurn} turns remaining");
        }
    }

    /// <summary>
    /// 현재 활성 스킬 수 가져오기
    /// </summary>
    public int GetActiveSkillCount()
    {
        return activeSkills.Count;
    }

    public AdvancedSkillRuntime GetActiveSkillByIndex( int index )
    {
        return activeSkills[index];
    }



    /// <summary>
    /// 특정 그룹의 스킬 수 가져오기
    /// </summary>
    public int GetSkillCountByGroup(EffectGroupType groupType)
    {
        if (skillsByGroup.TryGetValue(groupType, out var skills))
        {
            return skills.Count;
        }
        return 0;
    }

    #endregion

    #region Editor Support
#if UNITY_EDITOR
    public Queue<AdvancedSkillRuntime> EditorPendingSkills => pendingSkills;
    public List<AdvancedSkillRuntime> EditorActiveSkills => activeSkills;
    public Dictionary<int, List<AdvancedSkillRuntime>> EditorSkillsById => skillsById;
    public Dictionary<SkillSystem.StatusType, List<AdvancedSkillRuntime>> EditorSkillsByType => skillsByStatusType;
    public Dictionary<EffectGroupType, List<AdvancedSkillRuntime>> EditorSkillsByGroup => skillsByGroup;


    /// <summary>
    /// 테스트용 스킬 직접 추가 메서드
    /// </summary>
    public void AddSkillForTest(AdvancedSkillData skillData)
    {
        if (skillData == null || owner == null) return;

        // 스킬 런타임 생성
        var skillRuntime = AdvancedSkillFactory.CreateSkill(skillData);
        if (skillRuntime == null) return;

        // 초기화
        skillRuntime.Initialize(owner, owner, skillData);

        // 액티브 스킬로 등록
        RegisterSkill(skillRuntime);

        Debug.Log($"[Test] Added skill: {skillData.skillName}");
    }

    /// <summary>
    /// RegisterSkill을 public으로 노출 (테스트용)
    /// </summary>
    public void RegisterSkillForTest(AdvancedSkillRuntime skill)
    {
        RegisterSkill(skill);
    }



#endif
    #endregion
}