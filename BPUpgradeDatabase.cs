using SkillSystem;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "BPUpgradeDatabase", menuName = "Battle/BP Upgrade Database")]
public class BPUpgradeDatabase : ScriptableObject
{
    [SerializeField] private List<SkillUpgradePathData> upgradePaths = new List<SkillUpgradePathData>();

    /// <summary>
    /// 스킬 ID로 업그레이드 경로 가져오기
    /// </summary>
    public SkillUpgradePath GetUpgradePath(int skillId)
    {
        var data = upgradePaths.FirstOrDefault(p => p.baseSkillId == skillId);
        return data?.ToUpgradePath();
    }

    /// <summary>
    /// 모든 업그레이드 경로 가져오기
    /// </summary>
    public Dictionary<int, SkillUpgradePath> GetAllUpgradePaths()
    {
        var result = new Dictionary<int, SkillUpgradePath>();
        foreach (var data in upgradePaths)
        {
            result[data.baseSkillId] = data.ToUpgradePath();
        }
        return result;
    }

    [Serializable]
    public class SkillUpgradePathData
    {
        public int baseSkillId;
        public string pathName;

        [Header("Level 1 (BP 1-2)")]
        public BPUpgradeModifierData upgrade1;

        [Header("Level 2 (BP 3-4)")]
        public BPUpgradeModifierData upgrade2;

        [Header("MAX (BP 5)")]
        public BPUpgradeModifierData upgradeMax;

        public SkillUpgradePath ToUpgradePath()
        {
            return new SkillUpgradePath
            {
                baseSkillId = baseSkillId,
                pathName = pathName,
                upgrade1 = upgrade1?.ToModifier(BPUpgradeLevel.Upgrade1, 1),
                upgrade2 = upgrade2?.ToModifier(BPUpgradeLevel.Upgrade2, 3),
                upgradeMax = upgradeMax?.ToModifier(BPUpgradeLevel.MAX, 5)
            };
        }
    }

    [Serializable]
    public class BPUpgradeModifierData
    {
        public string upgradeName;
        public string description;
        public UpgradeType type;

        [Header("수치 변경")]
        public List<ValueModifier> valueModifiers;

        [Header("메커니즘 변경")]
        public bool changeTargetType;
        public SkillSystem.TargetType newTargetType;
        public int additionalDuration;

        [Header("추가 효과")]
        public List<AdvancedSkillEffect> additionalEffects;

        [Header("시각 효과")]
        public GameObject upgradedEffectPrefab;
        public Color effectColorTint = Color.white;

        public BPUpgradeModifier ToModifier(BPUpgradeLevel level, int requiredBP)
        {
            var modifier = new BPUpgradeModifier
            {
                level = level,
                requiredBP = requiredBP,
                upgradeName = upgradeName,
                description = description,
                type = type,
                valueModifiers = valueModifiers ?? new List<ValueModifier>(),
                additionalEffects = additionalEffects ?? new List<AdvancedSkillEffect>(),
                upgradedEffectPrefab = upgradedEffectPrefab,
                effectColorTint = effectColorTint
            };

            if (changeTargetType)
            {
                modifier.targetChange = new TargetTypeChange
                {
                    enabled = true,
                    toType = newTargetType
                };
            }

            if (additionalDuration > 0)
            {
                modifier.durationChange = new DurationChange
                {
                    enabled = true,
                    additionalTurns = additionalDuration
                };
            }

            return modifier;
        }
    }
}