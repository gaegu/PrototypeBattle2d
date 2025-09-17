// =====================================================
// BPSkillManager.cs
// BP 사용 관리 및 스킬 업그레이드 시스템
// =====================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SkillSystem;

/// <summary>
/// BP(Break Point) 스킬 업그레이드 관리자
/// 각 BattleActor의 BP 사용과 스킬 업그레이드를 관리
/// </summary>
public class BPSkillManager : MonoBehaviour
{
    #region Fields
    [Header("BP 상태")]
    [SerializeField] private int currentBP = 0;
    [SerializeField] private int maxBP = 5;
    [SerializeField] private int totalBPUsedThisBattle = 0;

    [Header("업그레이드 정보")]
    [SerializeField] private Dictionary<int, UpgradedSkillData> upgradedSkills = new Dictionary<int, UpgradedSkillData>();
    [SerializeField] private Dictionary<int, SkillUpgradePath> upgradePaths = new Dictionary<int, SkillUpgradePath>();


    [Header("Auto Load Settings")]
    [SerializeField] private BPUpgradeDatabase upgradeDatabase;
    [SerializeField] private bool autoLoadFromDatabase = true;

    // BP 사용 히스토리
    private List<BPUsageRecord> bpHistory = new List<BPUsageRecord>();

    // 컴포넌트 참조
    private BattleActor owner;
    private BattleSkillManager skillManager;

    // 이벤트
    public event Action<int, BPUpgradeLevel> OnSkillUpgraded;
    public event Action<int> OnBPUsed;
    public event Action<int> OnBPRestored;
    #endregion

    #region Properties
    public int CurrentBP => currentBP;
    public int MaxBP => maxBP;
    public int TotalBPUsed => totalBPUsedThisBattle;
    public bool CanUseBP => currentBP > 0;
    #endregion

    #region Initialization
    private void Awake()
    {
        owner = GetComponent<BattleActor>();
        skillManager = owner?.SkillManager;

        if (autoLoadFromDatabase && upgradeDatabase != null)
        {
            AutoLoadUpgradePaths();
        }
    }
    /// <summary>
    /// 데이터베이스에서 업그레이드 경로 자동 로드
    /// </summary>
    public void AutoLoadUpgradePaths()
    {
        // owner가 없으면 가져오기
        if (owner == null) owner = GetComponent<BattleActor>();
        if (owner?.SkillManager == null)
        {
            Debug.LogWarning("[BPManager] No SkillManager found");
            return;
        }

        // 기존 경로 클리어
        upgradePaths.Clear();

        // 데이터베이스 확인 (리플렉션으로 가져온 것 사용)
        var dbField = typeof(BPSkillManager).GetField("upgradeDatabase",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var database = dbField?.GetValue(this) as BPUpgradeDatabase;

        if (database == null)
        {
            Debug.LogWarning("[BPManager] No upgrade database found");
            return;
        }

        // 모든 스킬에 대해 업그레이드 경로 로드
        var allSkills = owner.SkillManager.GetAllActiveSkills();
        foreach (var skill in allSkills)
        {
            var skillData = skill.SkillData;
            // 부모 스킬만 처리
            if (skillData.bpLevel == 0 || skillData.parentSkillId == 0)
            {
                var path = database.GetUpgradePath(skill.SkillID);
                if (path != null)
                {
                    upgradePaths[skill.SkillID] = path;
                    Debug.Log($"[BPManager] Loaded upgrade path for skill {skill.SkillID}: {path.pathName}");
                }
            }
        }

        Debug.Log($"[BPManager] Loaded {upgradePaths.Count} upgrade paths");
    }


    #endregion

    #region BP Management

    /// <summary>
    /// 테스트용 BP 설정
    /// </summary>
    public void SetBP(int amount)
    {
        currentBP = Mathf.Clamp(amount, 0, maxBP);
        Debug.Log($"[BPManager] BP set to {currentBP}");
    }


    /// <summary>
    /// BP 사용
    /// </summary>
    public bool UseBP(int amount = 1)
    {
        if (currentBP < amount)
        {
            Debug.LogWarning($"[BPManager] Not enough BP. Need {amount}, have {currentBP}");
            return false;
        }

        currentBP -= amount;
        totalBPUsedThisBattle += amount;

        // 히스토리 기록
        RecordBPUsage(amount, "Manual Use");

        // 이벤트 발생
        OnBPUsed?.Invoke(amount);

        Debug.Log($"[BPManager] Used {amount} BP. Remaining: {currentBP}");
        return true;
    }

    /// <summary>
    /// BP 회복
    /// </summary>
    public void RestoreBP(int amount)
    {
        int previous = currentBP;
        currentBP = Mathf.Clamp(currentBP + amount, 0, maxBP);
        int restored = currentBP - previous;

        if (restored > 0)
        {
            OnBPRestored?.Invoke(restored);
            Debug.Log($"[BPManager] Restored {restored} BP. Current: {currentBP}");
        }
    }


    /// <summary>
    /// BP 리셋 (전투 시작 시)
    /// </summary>
    public void ResetBP()
    {
        currentBP = maxBP;
        totalBPUsedThisBattle = 0;
        bpHistory.Clear();

        // 모든 스킬 업그레이드 초기화
        foreach (var upgrade in upgradedSkills.Values)
        {
            upgrade.currentLevel = BPUpgradeLevel.Base;
            upgrade.totalBPUsed = 0;
            upgrade.appliedModifiers.Clear();
        }

        upgradedSkills.Clear();

        Debug.Log($"[BPManager] BP Reset. Starting with {maxBP} BP");
    }
    #endregion

    #region Skill Upgrade System
    /// <summary>
    /// 스킬 업그레이드 (자동 - BP 사용 시 다음 레벨로)
    /// </summary>
    public bool UpgradeSkillAuto(int skillId)
    {
        // 업그레이드 경로 확인
        if (!upgradePaths.TryGetValue(skillId, out var path))
        {
            Debug.LogError($"[BPManager] No upgrade path for skill {skillId}");
            return false;
        }

        // 현재 업그레이드 상태 가져오기 또는 생성
        if (!upgradedSkills.TryGetValue(skillId, out var upgradeData))
        {
            upgradeData = CreateUpgradedSkillData(skillId);
            if (upgradeData == null) return false;
            upgradedSkills[skillId] = upgradeData;
        }

        // 다음 업그레이드 필요 BP 계산
        int requiredBP = path.GetNextUpgradeBPCost(upgradeData.totalBPUsed);
        if (requiredBP == 0)
        {
            Debug.Log($"[BPManager] Skill {skillId} is already at MAX level");
            return false;
        }

        // BP 사용 가능 체크
        int bpToUse = requiredBP - upgradeData.totalBPUsed;
        if (currentBP < bpToUse)
        {
            Debug.LogWarning($"[BPManager] Not enough BP for upgrade. Need {bpToUse}, have {currentBP}");
            return false;
        }

        // BP 사용 및 업그레이드 적용
        if (UseBP(bpToUse))
        {
            upgradeData.totalBPUsed = requiredBP;
            var modifier = path.GetUpgradeForBP(requiredBP);

            if (modifier != null)
            {
                ApplyUpgradeToSkill(upgradeData, modifier);
                upgradeData.currentLevel = path.GetUpgradeLevel(requiredBP);

                // 이벤트 발생
                OnSkillUpgraded?.Invoke(skillId, upgradeData.currentLevel);

                // 히스토리 기록
                RecordBPUsage(bpToUse, $"Upgrade Skill {skillId} to {upgradeData.currentLevel}");

                Debug.Log($"[BPManager] Upgraded skill {skillId} to {upgradeData.currentLevel}");
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 특정 레벨로 직접 업그레이드 (치트/테스트용)
    /// </summary>
    public bool UpgradeSkillToLevel(int skillId, BPUpgradeLevel targetLevel)
    {
        if (!upgradePaths.TryGetValue(skillId, out var path))
            return false;

        if (!upgradedSkills.TryGetValue(skillId, out var upgradeData))
        {
            upgradeData = CreateUpgradedSkillData(skillId);
            if (upgradeData == null) return false;
            upgradedSkills[skillId] = upgradeData;
        }

        // 목표 레벨까지의 모든 업그레이드 적용
        upgradeData.currentLevel = BPUpgradeLevel.Base;
        upgradeData.appliedModifiers.Clear();
        upgradeData.currentSkill = upgradeData.baseSkill.Clone();

        if (targetLevel >= BPUpgradeLevel.Upgrade1 && path.upgrade1 != null)
        {
            ApplyUpgradeToSkill(upgradeData, path.upgrade1);
            upgradeData.currentLevel = BPUpgradeLevel.Upgrade1;
        }

        if (targetLevel >= BPUpgradeLevel.Upgrade2 && path.upgrade2 != null)
        {
            ApplyUpgradeToSkill(upgradeData, path.upgrade2);
            upgradeData.currentLevel = BPUpgradeLevel.Upgrade2;
        }

        if (targetLevel >= BPUpgradeLevel.MAX && path.upgradeMax != null)
        {
            ApplyUpgradeToSkill(upgradeData, path.upgradeMax);
            upgradeData.currentLevel = BPUpgradeLevel.MAX;
        }

        OnSkillUpgraded?.Invoke(skillId, upgradeData.currentLevel);
        return true;
    }

    /// <summary>
    /// 업그레이드 적용
    /// </summary>
    private void ApplyUpgradeToSkill(UpgradedSkillData upgradeData, BPUpgradeModifier modifier)
    {
        // UpgradedSkillData의 ApplyUpgrade 메서드 호출
        upgradeData.ApplyUpgrade(modifier);

        // 실제 스킬 매니저에 업데이트된 스킬 적용
        if (skillManager != null && upgradeData.currentSkill != null)
        {
            // 기존 스킬 제거하고 업그레이드된 버전으로 교체
            UpdateSkillInManager(upgradeData);
        }

        // 시각 효과 업데이트
        ApplyVisualUpgrade(upgradeData, modifier);
    }

    /// <summary>
    /// 스킬 매니저에 업그레이드된 스킬 반영
    /// </summary>
    private void UpdateSkillInManager(UpgradedSkillData upgradeData)
    {
        // 실행 중인 스킬이 있으면 업데이트
        var activeSkills = skillManager.GetSkillsById(upgradeData.baseSkill.skillId);
        foreach (var activeSkill in activeSkills)
        {
            // 런타임 스킬 데이터 업데이트
            activeSkill.UpdateSkillData(upgradeData.currentSkill);
        }
    }

    /// <summary>
    /// 시각 효과 업그레이드
    /// </summary>
    private void ApplyVisualUpgrade(UpgradedSkillData upgradeData, BPUpgradeModifier modifier)
    {
        // 이펙트 프리팹 변경
        if (modifier.upgradedEffectPrefab != null)
        {
            upgradeData.currentSkill.effectPrefab = modifier.upgradedEffectPrefab;
        }

        // TODO: 색상, 크기 등 시각 효과 수정
    }

    private UpgradedSkillData CreateUpgradedSkillData(int skillId)
    {
        // 기본 스킬 데이터 가져오기
        var baseSkillData = GetBaseSkillData(skillId);
        if (baseSkillData == null)
        {
            Debug.LogError($"[BPManager] Base skill data not found for ID {skillId}");
            return null;
        }

        return new UpgradedSkillData
        {
            baseSkill = baseSkillData,
            currentSkill = baseSkillData.Clone(),
            currentLevel = BPUpgradeLevel.Base,
            totalBPUsed = 0
        };
    }

    private AdvancedSkillData GetBaseSkillData(int skillId)
    {
        // TODO: 실제 데이터베이스에서 로드
        // 임시로 owner의 스킬 매니저에서 가져오기
        if (owner != null && owner.SkillManager != null)
        {
            var skills = owner.SkillManager.GetAllActiveSkills();
            var skill = skills.FirstOrDefault(s => s.SkillID == skillId);
            return skill?.SkillData;
        }
        return null;
    }
    #endregion

    #region Query Methods
    /// <summary>
    /// 스킬의 현재 업그레이드 레벨 가져오기
    /// </summary>
    public BPUpgradeLevel GetSkillUpgradeLevel(int skillId)
    {
        if (upgradedSkills.TryGetValue(skillId, out var upgradeData))
        {
            return upgradeData.currentLevel;
        }
        return BPUpgradeLevel.Base;
    }

    /// <summary>
    /// 업그레이드된 스킬 데이터 가져오기
    /// </summary>
    public AdvancedSkillData GetUpgradedSkillData(int skillId, int bpToUse = -1)
    {
        // BP 사용량 자동 결정
        if (bpToUse < 0)
        {
            bpToUse = currentBP; // 현재 가진 BP 모두 사용
        }

        // 업그레이드 경로 확인
        if (!upgradePaths.TryGetValue(skillId, out var path))
        {
            // 경로가 없으면 원본 반환
            return skillManager?.GetSkillById(skillId)?.SkillData;
        }

        // BP 레벨에 맞는 업그레이드 가져오기
        var upgradeLevel = path.GetUpgradeLevel(bpToUse);
        var modifier = path.GetUpgradeForBP(bpToUse);

        if (modifier == null)
        {
            return skillManager?.GetSkillById(skillId)?.SkillData;
        }

        // 업그레이드 적용
        var upgradeData = new UpgradedSkillData();
        upgradeData.baseSkill = skillManager.GetSkillById(skillId).SkillData;
        upgradeData.ApplyUpgrade(modifier);

        // BP 소비
        UseBP(bpToUse);

        return upgradeData.currentSkill ?? upgradeData.baseSkill;
    }

    /// <summary>
    /// 다음 업그레이드 정보 가져오기
    /// </summary>
    public string GetNextUpgradeInfo(int skillId)
    {
        if (!upgradePaths.TryGetValue(skillId, out var path))
            return "업그레이드 경로 없음";

        if (!upgradedSkills.TryGetValue(skillId, out var upgradeData))
        {
            // 아직 업그레이드하지 않은 경우
            if (path.upgrade1 != null)
            {
                return $"[BP 1] {path.upgrade1.upgradeName}: {path.upgrade1.description}";
            }
        }
        else
        {
            return upgradeData.GetNextUpgradePreview(path);
        }

        return "업그레이드 정보 없음";
    }

    /// <summary>
    /// 업그레이드 가능한 스킬 목록
    /// </summary>
    public List<int> GetUpgradeableSkills()
    {
        var result = new List<int>();

        foreach (var kvp in upgradePaths)
        {
            int skillId = kvp.Key;
            var path = kvp.Value;

            // 현재 레벨 확인
            var currentLevel = GetSkillUpgradeLevel(skillId);
            if (currentLevel == BPUpgradeLevel.MAX)
                continue;

            // 필요 BP 확인
            int currentBPUsed = upgradedSkills.TryGetValue(skillId, out var data) ? data.totalBPUsed : 0;
            int requiredBP = path.GetNextUpgradeBPCost(currentBPUsed) - currentBPUsed;

            if (currentBP >= requiredBP)
            {
                result.Add(skillId);
            }
        }

        return result;
    }


    /// <summary>
    /// 스킬 매니저 추가 헬퍼 메서드
    /// </summary>
    public void AddSkillRuntime(AdvancedSkillRuntime runtime)
    {
        if (skillManager == null)
        {
            skillManager = owner?.SkillManager;
        }

        if (skillManager != null)
        {
            // BattleSkillManager에 AddSkillRuntime 메서드가 필요
            // 없다면 대체 방법 사용
            var skills = skillManager.GetAllActiveSkills();
            if (!skills.Contains(runtime))
            {
                // 리플렉션으로 private 리스트에 추가
                var field = typeof(BattleSkillManager).GetField("activeSkills",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    var list = field.GetValue(skillManager) as List<AdvancedSkillRuntime>;
                    list?.Add(runtime);
                }
            }
        }
    }





    #endregion

    #region History & Debug
    private void RecordBPUsage(int amount, string reason)
    {
        bpHistory.Add(new BPUsageRecord
        {
            timestamp = Time.time,
            amount = amount,
            reason = reason,
            remainingBP = currentBP
        });
    }

    public void PrintBPStatus()
    {
        Debug.Log($"=== BP Status ===");
        Debug.Log($"Current BP: {currentBP}/{maxBP}");
        Debug.Log($"Total Used: {totalBPUsedThisBattle}");
        Debug.Log($"Upgraded Skills: {upgradedSkills.Count}");

        foreach (var kvp in upgradedSkills)
        {
            var data = kvp.Value;
            Debug.Log($"  - Skill {kvp.Key}: Level {data.currentLevel}, BP Used: {data.totalBPUsed}");
        }
    }
    #endregion

    #region Sample Data Creation (테스트용)
    /// <summary>
    /// 샘플 업그레이드 경로 생성 (생명 약탈 예시)
    /// </summary>
    public static SkillUpgradePath CreateSampleUpgradePath_LifeDrain()
    {
        var path = new SkillUpgradePath
        {
            baseSkillId = 1001,
            pathName = "생명 약탈 진화",
            pathDescription = "단일 대상 흡수에서 전체 흡수로 진화"
        };

        // Upgrade 1 (BP 1-2): 흡수율 증가
        path.upgrade1 = new BPUpgradeModifier
        {
            level = BPUpgradeLevel.Upgrade1,
            requiredBP = 1,
            upgradeName = "강화 흡수",
            description = "흡수율 +5% per BP",
            type = UpgradeType.Numerical,
            valueModifiers = new List<ValueModifier>
            {
                new ValueModifier
                {
                    targetField = "value",
                    operation = ValueModifier.ModifierOperation.Add,
                    value = 5,
                    isPercentage = true
                }
            }
        };

        // Upgrade 2 (BP 3-4): 범위 공격 전환
        path.upgrade2 = new BPUpgradeModifier
        {
            level = BPUpgradeLevel.Upgrade2,
            requiredBP = 3,
            upgradeName = "광역 흡수",
            description = "모든 적에게서 HP 흡수",
            type = UpgradeType.Mechanical,
            targetChange = new TargetTypeChange
            {
                enabled = true,
                fromType = SkillSystem.TargetType.EnemySingle,
                toType = SkillSystem.TargetType.EnemyAll
            }
        };

        // MAX (BP 5): 완전 포식
        path.upgradeMax = new BPUpgradeModifier
        {
            level = BPUpgradeLevel.MAX,
            requiredBP = 5,
            upgradeName = "완전 포식",
            description = "적 전체 HP 30% 흡수 + 아군 전체 분배 + 2턴간 재생",
            type = UpgradeType.Transform,
            isCompleteOverride = true,
            transformedSkill = new AdvancedSkillData
            {
                skillId = 1001,
                skillName = "완전 포식",
                description = "적 전체의 생명력을 흡수하여 아군 전체를 치유",
                category = SkillSystem.SkillCategory.SpecialActive,
                cooldown = 5,
                effects = new List<AdvancedSkillEffect>
                {
                    // 적 전체 30% 흡수
                    new AdvancedSkillEffect
                    {
                        type = SkillSystem.EffectType.LifeSteal,
                        targetType = SkillSystem.TargetType.EnemyAll,
                        value = 30f,
                        chance = 1f
                    },
                    // 아군 전체 회복
                    new AdvancedSkillEffect
                    {
                        type = SkillSystem.EffectType.Heal,
                        targetType = SkillSystem.TargetType.AllyAll,
                        value = 15f,
                        chance = 1f
                    },
                    // 재생 버프
                    new AdvancedSkillEffect
                    {
                        type = SkillSystem.EffectType.HealOverTime,
                        targetType = SkillSystem.TargetType.AllyAll,
                        value = 5f,
                        duration = 2,
                        chance = 1f
                    }
                }
            }
        };

        return path;
    }
    #endregion

    #region Nested Classes
    [Serializable]
    private class BPUsageRecord
    {
        public float timestamp;
        public int amount;
        public string reason;
        public int remainingBP;
    }
    #endregion
}