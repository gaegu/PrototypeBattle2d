using SkillSystem;
using System;
using UnityEngine;

namespace CharacterSystem
{
    /// <summary>
    /// 기본 스탯 구조체 (레벨 성장용)
    /// </summary>
    [Serializable]
    public struct BaseStats
    {
        [Header("레벨 1 기준 스탯")]
        public int hp;
        public int attack;
        public int defense;

        [Header("레벨당 성장값")]
        public float hpGrowth;
        public float attackGrowth;
        public float defenseGrowth;

        /// <summary>
        /// 특정 레벨에서의 스탯 계산
        /// </summary>
        public BaseStats GetStatsAtLevel(int level)
        {
            return new BaseStats
            {
                hp = Mathf.RoundToInt(hp + (level - 1) * hpGrowth),
                attack = Mathf.RoundToInt(attack + (level - 1) * attackGrowth),
                defense = Mathf.RoundToInt(defense + (level - 1) * defenseGrowth),
                hpGrowth = this.hpGrowth,
                attackGrowth = this.attackGrowth,
                defenseGrowth = this.defenseGrowth
            };
        }
    }

    /// <summary>
    /// 고정 스탯 구조체 (레벨 무관)
    /// </summary>
    [Serializable]
    public struct FixedStats
    {
        [Header("기본 스탯")]
        [Range(0, 5)] public int maxBP;              // 최대 BP
        [Range(0, 200)] public float turnSpeed;      // 행동 속도

        [Header("공격 관련")]
        [Range(0, 100)] public float critRate;       // 치명타 확률 (%)
        [Range(100, 300)] public float critDamage;   // 치명타 피해 (%)
        [Range(0, 100)] public float hitRate;        // 명중률 (%)
        [Range(0, 100)] public float penetration;    // 관통력 (%)

        [Header("방어 관련")]
        [Range(0, 100)] public float dodgeRate;      // 회피율 (%)
        [Range(0, 100)] public float blockRate;      // 방어율 (%)
        [Range(0, 100)] public float damageReduce;   // 피해 감소 (%)
        [Range(0, 100)] public float critResist;     // 치명타 저항 (%)

        [Header("특수 스탯")]
        [Range(0, 1000)] public int aggro;           // 어그로
        [Range(0, 100)] public float skillHitRate;   // 스킬 명중률 (%)
        [Range(0, 100)] public float skillResist;    // 스킬 저항률 (%)
        [Range(0, 100)] public float ccEnhance;      // CC 강화 (%)
        [Range(0, 100)] public float tenacity;       // CC 저항 (%)
        [Range(0, 100)] public float healPower;      // 치유력 (%)
        [Range(0, 50)] public float lifeSteal;       // 흡혈 (%)
        [Range(0, 100)] public float counter;        // 반격 (%)
        [Range(0, 100)] public float cooperation;    // 협동 공격 (%)
        [Range(0, 100)] public float variantDamage;  // 변동 피해 (%)

        /// <summary>
        /// 티어와 직업에 따른 기본값 설정
        /// </summary>
        public static FixedStats GetDefault(CharacterTier tier, ClassType charClass)
        {
            var stats = new FixedStats();

            // 티어별 기본값
            switch (tier)
            {
                case CharacterTier.XA:
                    stats.maxBP = 5;
                    stats.turnSpeed = 110;
                    stats.critRate = 20;
                    stats.critDamage = 180;
                    break;
                case CharacterTier.X:
                    stats.maxBP = 4;
                    stats.turnSpeed = 105;
                    stats.critRate = 18;
                    stats.critDamage = 170;
                    break;
                case CharacterTier.S:
                    stats.maxBP = 4;
                    stats.turnSpeed = 100;
                    stats.critRate = 15;
                    stats.critDamage = 160;
                    break;
                case CharacterTier.A:
                    stats.maxBP = 3;
                    stats.turnSpeed = 95;
                    stats.critRate = 12;
                    stats.critDamage = 150;
                    break;
            }

            // 직업별 속도 조정
            switch (charClass)
            {
                case ClassType.Slaughter:
                    stats.turnSpeed *= 1.1f;
                    break;
                case ClassType.Vanguard:
                    stats.turnSpeed *= 0.9f;
                    stats.aggro = 400; // 탱커는 기본 어그로 높음
                    break;
                case ClassType.Rewinder:
                    stats.turnSpeed *= 1.05f;
                    break;
                case ClassType.Jacker:
                    stats.turnSpeed *= 0.95f;
                    break;
            }

            // 공통 기본값
            stats.hitRate = 100;
            stats.dodgeRate = 5;
            stats.blockRate = 10;
            stats.skillHitRate = 100;
            stats.skillResist = 10;

            return stats;
        }
    }

    /// <summary>
    /// 티어별 기본 스탯 테이블
    /// </summary>
    public static class TierBaseStats
    {
        public static BaseStats GetBaseStats(CharacterTier tier, ClassType charClass)
        {
            BaseStats baseStats = new BaseStats();

            // 티어별 레벨 1 기본값
            switch (tier)
            {
                case CharacterTier.XA:
                    baseStats.hp = 3000;
                    baseStats.attack = 300;
                    baseStats.defense = 150;
                    break;
                case CharacterTier.X:
                    baseStats.hp = 2800;
                    baseStats.attack = 280;
                    baseStats.defense = 140;
                    break;
                case CharacterTier.S:
                    baseStats.hp = 2600;
                    baseStats.attack = 260;
                    baseStats.defense = 130;
                    break;
                case CharacterTier.A:
                    baseStats.hp = 2500;
                    baseStats.attack = 250;
                    baseStats.defense = 120;
                    break;
            }

            // 직업별 배율 적용
            switch (charClass)
            {
                case ClassType.Slaughter:
                    baseStats.hp = Mathf.RoundToInt(baseStats.hp * 0.9f);
                    baseStats.attack = Mathf.RoundToInt(baseStats.attack * 1.2f);
                    baseStats.defense = Mathf.RoundToInt(baseStats.defense * 0.8f);
                    break;
                case ClassType.Vanguard:
                    baseStats.hp = Mathf.RoundToInt(baseStats.hp * 1.3f);
                    baseStats.attack = Mathf.RoundToInt(baseStats.attack * 0.9f);
                    baseStats.defense = Mathf.RoundToInt(baseStats.defense * 1.3f);
                    break;
                case ClassType.Jacker:
                    baseStats.hp = Mathf.RoundToInt(baseStats.hp * 1.0f);
                    baseStats.attack = Mathf.RoundToInt(baseStats.attack * 1.1f);
                    baseStats.defense = Mathf.RoundToInt(baseStats.defense * 0.95f);
                    break;
                case ClassType.Rewinder:
                    baseStats.hp = Mathf.RoundToInt(baseStats.hp * 0.95f);
                    baseStats.attack = Mathf.RoundToInt(baseStats.attack * 0.95f);
                    baseStats.defense = Mathf.RoundToInt(baseStats.defense * 0.9f);
                    break;
            }

            // 성장 계수 설정 (모든 티어/직업 동일)
            baseStats.hpGrowth = baseStats.hp * 8f / 199f;  // 레벨 200에서 9배
            baseStats.attackGrowth = baseStats.attack * 6f / 199f;  // 레벨 200에서 7배
            baseStats.defenseGrowth = baseStats.defense * 6f / 199f;  // 레벨 200에서 7배

            return baseStats;
        }
    }
}