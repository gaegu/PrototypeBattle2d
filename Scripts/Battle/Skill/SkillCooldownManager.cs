// =====================================================
// SkillCooldownManager.cs
// 스킬 쿨다운 관리 시스템
// =====================================================

using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using SkillSystem;


// 스킬 티어별 기본 쿨다운 설정
public static class SkillCooldownDefaults
{
    public static int GetDefaultCooldown(int tier, SkillSystem.SkillCategory category)
    {
        if (category == SkillSystem.SkillCategory.Passive )
            return 0;

        return tier switch
        {
            1 => 0,    // 기본 스킬: 쿨다운 없음
            2 => 2,    // 2티어: 2턴
            3 => 3,    // 3티어: 3턴
            4 => 4,    // 4티어: 4턴
            5 => 5,    // 5티어: 5턴
            _ => 3     // 기본값: 3턴
        };
    }
}

/// <summary>
/// 쿨다운 정보 구조체
/// </summary>
public struct CooldownInfo
{
    public int SkillId;
    public int RemainingTurns;
    public int MaxCooldown;
    public float Progress; // 0 ~ 1

    public bool IsReady => RemainingTurns <= 0;
}


/// <summary>
/// 스킬 쿨다운 관리자
/// BattleActor별로 스킬 쿨다운을 중앙 관리
/// </summary>
public class SkillCooldownManager
{
    private Dictionary<int, int> skillCooldowns = new Dictionary<int, int>(); // skillId -> remainingCooldown
    private Dictionary<int, int> maxCooldowns = new Dictionary<int, int>(); // skillId -> maxCooldown
    private BattleActor owner;

    /// <summary>
    /// 쿨다운 변경 이벤트
    /// </summary>
    public System.Action<int, int, int> OnCooldownChanged; // skillId, current, max

    public SkillCooldownManager(BattleActor owner)
    {
        this.owner = owner;
    }

    /// <summary>
    /// 스킬 사용 - 쿨다운 시작
    /// </summary>
    public void UseSkill(AdvancedSkillData skillData)
    {
        if (skillData == null) return;

        // 쿨다운이 0이면 쿨다운 없는 스킬
        if (skillData.cooldown <= 0) return;

        skillCooldowns[skillData.skillId] = skillData.cooldown;
        maxCooldowns[skillData.skillId] = skillData.cooldown;

        Debug.Log($"[Cooldown] {owner.name} used {skillData.skillName}. Cooldown: {skillData.cooldown} turns");
        OnCooldownChanged?.Invoke(skillData.skillId, skillData.cooldown, skillData.cooldown);
    }

    /// <summary>
    /// 스킬 사용 가능 여부 체크
    /// </summary>
    public bool CanUseSkill(int skillId)
    {
        if (!skillCooldowns.ContainsKey(skillId))
            return true;

        return skillCooldowns[skillId] <= 0;
    }

    /// <summary>
    /// 남은 쿨다운 턴 수 반환
    /// </summary>
    public int GetRemainingCooldown(int skillId)
    {
        if (!skillCooldowns.ContainsKey(skillId))
            return 0;

        return skillCooldowns[skillId];
    }

    /// <summary>
    /// 턴 종료 시 쿨다운 감소
    /// </summary>
    public void ProcessTurnEnd()
    {
        var skillIds = skillCooldowns.Keys.ToList();

        foreach (var skillId in skillIds)
        {
            if (skillCooldowns[skillId] > 0)
            {
                skillCooldowns[skillId]--;

                Debug.Log($"[Cooldown] {owner.name} - Skill {skillId} cooldown: {skillCooldowns[skillId]} turns remaining");

                OnCooldownChanged?.Invoke(skillId, skillCooldowns[skillId], maxCooldowns[skillId]);

                // 쿨다운 완료
                if (skillCooldowns[skillId] <= 0)
                {
                    Debug.Log($"[Cooldown] {owner.name} - Skill {skillId} is ready!");
                    skillCooldowns.Remove(skillId);
                    maxCooldowns.Remove(skillId);
                }
            }
        }
    }

    /// <summary>
    /// 특정 스킬 쿨다운 리셋
    /// </summary>
    public void ResetSkillCooldown(int skillId)
    {
        if (skillCooldowns.ContainsKey(skillId))
        {
            skillCooldowns.Remove(skillId);
            maxCooldowns.Remove(skillId);
            Debug.Log($"[Cooldown] {owner.name} - Skill {skillId} cooldown reset!");
            OnCooldownChanged?.Invoke(skillId, 0, 0);
        }
    }

    /// <summary>
    /// 모든 스킬 쿨다운 리셋
    /// </summary>
    public void ResetAllCooldowns()
    {
        foreach (var skillId in skillCooldowns.Keys.ToList())
        {
            OnCooldownChanged?.Invoke(skillId, 0, 0);
        }

        skillCooldowns.Clear();
        maxCooldowns.Clear();
        Debug.Log($"[Cooldown] {owner.name} - All cooldowns reset!");
    }

    /// <summary>
    /// 쿨다운 감소 (특수 효과용)
    /// </summary>
    public void ReduceCooldowns(int amount)
    {
        var skillIds = skillCooldowns.Keys.ToList();

        foreach (var skillId in skillIds)
        {
            if (skillCooldowns[skillId] > 0)
            {
                skillCooldowns[skillId] = Mathf.Max(0, skillCooldowns[skillId] - amount);

                OnCooldownChanged?.Invoke(skillId, skillCooldowns[skillId], maxCooldowns[skillId]);

                if (skillCooldowns[skillId] <= 0)
                {
                    skillCooldowns.Remove(skillId);
                    maxCooldowns.Remove(skillId);
                }
            }
        }

        Debug.Log($"[Cooldown] {owner.name} - All cooldowns reduced by {amount}");
    }

    /// <summary>
    /// 쿨다운 증가 (디버프 효과용)
    /// </summary>
    public void IncreaseCooldowns(int amount)
    {
        foreach (var skillId in skillCooldowns.Keys.ToList())
        {
            skillCooldowns[skillId] += amount;
            OnCooldownChanged?.Invoke(skillId, skillCooldowns[skillId], maxCooldowns[skillId]);
        }

        Debug.Log($"[Cooldown] {owner.name} - All cooldowns increased by {amount}");
    }

    /// <summary>
    /// 사용 가능한 스킬 목록 반환
    /// </summary>
    public List<int> GetAvailableSkills(List<AdvancedSkillData> allSkills)
    {
        var availableSkills = new List<int>();

        foreach (var skill in allSkills)
        {
            if (CanUseSkill(skill.skillId))
            {
                availableSkills.Add(skill.skillId);
            }
        }

        return availableSkills;
    }

    /// <summary>
    /// 쿨다운 상태 정보 반환 (UI용)
    /// </summary>
    public Dictionary<int, CooldownInfo> GetCooldownStatus()
    {
        var status = new Dictionary<int, CooldownInfo>();

        foreach (var kvp in skillCooldowns)
        {
            status[kvp.Key] = new CooldownInfo
            {
                SkillId = kvp.Key,
                RemainingTurns = kvp.Value,
                MaxCooldown = maxCooldowns[kvp.Key],
                Progress = 1f - (float)kvp.Value / maxCooldowns[kvp.Key]
            };
        }

        return status;
    }

    /// <summary>
    /// 전투 종료 시 정리
    /// </summary>
    public void Clear()
    {
        skillCooldowns.Clear();
        maxCooldowns.Clear();
        OnCooldownChanged = null;
    }
}
