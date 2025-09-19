using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 전투 밸런스 공식을 관리하는 정적 헬퍼 클래스
/// Lua 공식 포팅 + 기존 시스템 통합
/// </summary>
public static class BattleFormularHelper
{
    #region Constants - 밸런스 상수값 (Lua에서 포팅)

    // === Lua 데미지 계산 상수 ===
    private const float MISS_DAMAGE_RATE = 0f;             // Miss 시 데미지 0% (Lua는 75%지만 일반적으로 0)
    private const float DODGE_DAMAGE_RATE = 0f;            // Dodge 시 데미지 0%
    private const float BLOCK_DAMAGE_REDUCTION = 0.5f;     // Block 시 데미지 50% 감소
    private const float STRIKE_DAMAGE_RATE = 1.3f;         // Strike 시 데미지 130%
    private const float STRIKE_CHANCE = 0.3f;              // Strike 확률 30%


    // === 속성 상성 상수 ===
    private const float ELEMENT_ADVANTAGE_DAMAGE_RATE = 1.1f;      // 속성 우위 데미지 110%
    private const float ELEMENT_ADVANTAGE_CRITICAL_BONUS = 0.15f;  // 속성 우위 크리티컬 보너스
    private const float ELEMENT_DISADVANTAGE_HIT_PENALTY = 0.5f;   // 속성 열세 명중 페널티

    // === 방어력 계산 상수 (Lua 공식) ===
    private const float DEFENSE_BASE_VALUE = 2600f;        // 방어력 기준값
    private const float DEFENSE_REDUCTION_RATE = 0.7f;     // 방어력 감소율


    // === BP 보너스 (기존) ===
    public const float BP_DAMAGE_BONUS_PER_POINT = 0.5f;   // BP당 데미지 증가율
    public const float BP_CRIT_BONUS_PER_POINT = 0.1f;     // BP당 크리티컬 증가율
                                                           // === 새로운 스탯 관련 상수 추가 ===

    private const float MAX_PENETRATION = 100f;          // 최대 관통률 100%
    private const float MAX_DAMAGE_REDUCE = 90f;         // 최대 피해감소 90%
    private const float DEFAULT_BLOCK_POWER = 0.5f;      // 기본 블록 피해감소 50%
    private const float ELEMENTAL_RES_EFFICIENCY = 0.5f; // 속성 저항 효율 50%


    #endregion

    #region Enums

    /// <summary>
    /// 판정 타입 (Lua + 기존 통합)
    /// </summary>
    [Flags]
    public enum JudgementType
    {
        None = 0,
        Hit = 1,
        Miss = 2,
        Critical = 4,
        Block = 8,
        Dodge = 16,
        Strike = 32,        // Lua에서 추가
        Weakness = 64,      // 약점 공격
        Resist = 128,       // 저항
        Immune = 256        // 면역
    }

    [Flags]
    public enum DamageOption
    {
        None = 0,
        IgnoreDefense = 1,      // 방어 무시
        TrueDamage = 2,         // 고정 데미지
        PercentDamage = 4,      // 퍼센트 데미지
        Penetrating = 8,        // 관통
        Splash = 16,            // 범위
        Drain = 32,             // 흡혈
        IgnoreDodge = 64,       // 회피 무시
        IgnoreBlock = 128,      // 블록 무시
        CannotMiss = 256,        // 필중
        IgnoreShield = 512,      //쉴드 무시
    }

    #endregion

    #region Data Structures

    /// <summary>
    /// 데미지 계산 결과
    /// </summary>
    public class DamageResult
    {
        public JudgementType Judgement { get; set; }
        public DamageOption Options { get; set; }
        public int FinalDamage { get; set; }
        public int OriginalDamage { get; set; }
        public int CriticalBonus { get; set; }
        public int Defense { get; set; }
        public int BlockedDamage { get; set; }
        public bool IsCritical { get; set; }
        public bool IsBlocked { get; set; }
        public bool IsDodged { get; set; }
        public bool IsMissed { get; set; }
        public bool IsResisted { get; set; }     // 스킬 효과 저항
        public bool IsImmune { get; set; }       // 면역
        public float ElementMultiplier { get; set; } = 1f;
        public int HitCount { get; set; } = 1;
        public int ShieldAbsorbed { get; set; }  // Shield가 흡수한 데미지 (새로 추가)

        // 디버그용 상세 정보
        public Dictionary<string, float> DebugInfo { get; set; } = new Dictionary<string, float>();
        public int LifeStealAmount { get; set; }      // 흡혈량
        public bool HasLifeSteal { get; set; }        // 흡혈 여부
        public float LifeStealPercent { get; set; }   // 흡혈 비율

        public override string ToString()
        {
            string status = "";
            if (IsMissed) status += "MISS ";
            if (IsDodged) status += "DODGE ";
            if (IsBlocked) status += "BLOCK ";
            if (IsCritical) status += "CRITICAL ";
            if (IsResisted) status += "RESIST ";
            if (IsImmune) status += "IMMUNE ";
            if (ShieldAbsorbed > 0) status += $"SHIELD({ShieldAbsorbed}) ";  // 추가
            if (HasLifeSteal) status += $"LIFESTEAL({LifeStealAmount}) ";  // 추가


            return $"[DamageResult] {status}Damage:{FinalDamage}(Original:{OriginalDamage}) " +
                   $"Defense:{Defense} Element:{ElementMultiplier:F2}x";
        }
    }

    /// <summary>
    /// 전투 컨텍스트 - 계산에 필요한 모든 정보
    /// </summary>
    public class BattleContext
    {
        public BattleActor Attacker { get; set; }
        public BattleActor Defender { get; set; }
        public int SkillPower { get; set; } = 100;
        public int UsedBP { get; set; } = 0;
        public DamageOption DamageOptions { get; set; } = DamageOption.None;
        public EAttackType AttackType { get; set; } = EAttackType.Physical;
        public bool IsSkillAttack { get; set; } = false;
        public Dictionary<string, object> ExtraParams { get; set; } = new Dictionary<string, object>();
      
        // === 새로 추가 (옵션) ===
        public float LifeStealPercent { get; set; }  // 흡혈률
        public bool EnableLifeSteal { get; set; }     // 흡혈 활성화 여부
    }

    #endregion

    #region Main Calculation Methods 

    /// <summary>
    /// 전체 데미지 계산 - Lua 공식 적용
    /// </summary>
    public static DamageResult CalculateDamage(BattleContext context)
    {
        var result = new DamageResult();

        // 1. 면역 체크 (최우선)
        if (CheckImmunity(context))
        {
            result.Judgement = JudgementType.Immune;
            result.IsImmune = true;
            result.FinalDamage = 0;
            Debug.Log($"[Battle] {context.Defender.name} is IMMUNE to {context.AttackType}!");
            return result;
        }


        // 2. 판정 체크 (Miss, Dodge, Block, Critical, Strike)
        result.Judgement = CheckJudgement(context);

        // 3. Miss나 Dodge인 경우 데미지 0
        if ((result.Judgement & JudgementType.Miss) != 0)
        {
            result.IsMissed = true;
            result.FinalDamage = 0;
            Debug.Log($"[Battle] Attack MISSED!");
            return result;
        }

        if ((result.Judgement & JudgementType.Dodge) != 0)
        {
            result.IsDodged = true;
            result.FinalDamage = 0;
            Debug.Log($"[Battle] Attack DODGED!");
            return result;
        }

        // 4. 스킬 효과 저항 체크 (스킬 공격인 경우)
        if (context.IsSkillAttack && CheckResist(context))
        {
            result.Judgement |= JudgementType.Resist;
            result.IsResisted = true;
            Debug.Log($"[Battle] Skill effect RESISTED!");
            // 데미지는 입지만 추가 효과는 무효
        }



        // 5. 데미지 계산
        result = CalculateDamageAmount(result.Judgement, context);

        // 6. Block 처리
        if ((result.Judgement & JudgementType.Block) != 0)
        {
            result.IsBlocked = true;
            int originalDamage = result.FinalDamage;
            result.FinalDamage = ApplyBlock(result.FinalDamage, context);
            result.BlockedDamage = originalDamage - result.FinalDamage;
            Debug.Log($"[Battle] Damage BLOCKED! {originalDamage} -> {result.FinalDamage}");
        }


        // LifeSteal 계산 (패시브 흡혈)
        if (context.Attacker != null && result.FinalDamage > 0 && !result.IsMissed && !result.IsDodged)
        {
            // 1. 공격자의 LifeSteal 스탯 확인
            float lifeStealPercent = 0f;

            // CharacterStatType에 LifeSteal이 있는 경우
            // lifeStealPercent = context.Attacker.GetStatFloat(CharacterStatType.LifeSteal);

            // 또는 BattleActorInfo에서 직접 가져오기
            if (context.ExtraParams != null && context.ExtraParams.ContainsKey("LifeStealPercent"))
            {
                lifeStealPercent = (float)context.ExtraParams["LifeStealPercent"];
            }

            // 2. 흡혈량 계산
            if (lifeStealPercent > 0)
            {
                int lifeStealAmount = Mathf.RoundToInt(result.FinalDamage * (lifeStealPercent / 100f));

                // 실제로 회복 가능한 양 계산 (최대 HP 제한)
                int currentHP = context.Attacker.BattleActorInfo.Hp;
                int maxHP = context.Attacker.BattleActorInfo.MaxHp;
                int maxHeal = maxHP - currentHP;

                lifeStealAmount = Mathf.Min(lifeStealAmount, maxHeal);

                // 결과에 저장
                result.HasLifeSteal = true;
                result.LifeStealPercent = lifeStealPercent;
                result.LifeStealAmount = lifeStealAmount;

                Debug.Log($"[LifeSteal] Calculated {lifeStealAmount} HP steal ({lifeStealPercent}% of {result.FinalDamage})");
            }
        }



        // 3. 디버그 정보 저장
        LogDebugInfo(result, context);

        return result;
    }


    /// <summary>
    /// 강화된 판정 체크
    /// </summary>
    public static JudgementType CheckJudgement(BattleContext context)
    {
        var attacker = context.Attacker.BattleActorInfo;
        var defender = context.Defender.BattleActorInfo;
        JudgementType result = JudgementType.None;

        // 필중 옵션이 있으면 Miss와 Dodge 무시
        bool cannotMiss = context.DamageOptions.HasFlag(DamageOption.CannotMiss);
        bool ignoreDodge = context.DamageOptions.HasFlag(DamageOption.IgnoreDodge);
        bool ignoreBlock = context.DamageOptions.HasFlag(DamageOption.IgnoreBlock);

        // 1. Miss 체크 (필중이 아닌 경우)
        if (!cannotMiss)
        {
            float hitValue = attacker.HitRate / 100f;
            float dodgeValue = defender.DodgeRate / 100f;
            float disadvantageValue = 0f;

            // 속성 열세 체크
            if (attacker.IsDisadvantage(attacker.Element, defender.Element))
            {
                disadvantageValue = ELEMENT_DISADVANTAGE_HIT_PENALTY;
            }

            // Lua 공식: hit_value + 1.0 - dodge_value - disadvantage_value
            float hitRate = hitValue + 1.0f - dodgeValue - disadvantageValue;
            hitRate = Mathf.Clamp01(hitRate);

            float randomValue = UnityEngine.Random.value;

            Debug.Log($"[Hit Check] Rate:{hitRate:F2} (Hit:{hitValue:F2} - Dodge:{dodgeValue:F2} - Penalty:{disadvantageValue:F2}) Roll:{randomValue:F2}");

            if (randomValue >= hitRate)
            {
                return JudgementType.Miss;
            }
        }

        // 2. Dodge 체크 (회피 무시가 아닌 경우)
        if (!ignoreDodge && !cannotMiss)
        {
            float dodgeChance = defender.DodgeRate / 100f;

            // 속성 우위시 회피율 감소
            if (attacker.IsAdvantage(attacker.Element, defender.Element))
            {
                dodgeChance *= 0.5f;  // 속성 우위시 회피율 절반
            }

            float randomValue = UnityEngine.Random.value;

            Debug.Log($"[Dodge Check] Chance:{dodgeChance:F2} Roll:{randomValue:F2}");

            if (randomValue < dodgeChance)
            {
                return JudgementType.Dodge;
            }
        }

        // 이제 기본적으로 Hit
        result = JudgementType.Hit;

        // 3. Block 체크 (블록 무시가 아닌 경우)
        if (!ignoreBlock)
        {
            float blockChance = defender.BlockRate / 100f;
            float randomValue = UnityEngine.Random.value;

            Debug.Log($"[Block Check] Chance:{blockChance:F2} Roll:{randomValue:F2}");

            if (randomValue < blockChance)
            {
                result |= JudgementType.Block;
            }
        }

        // 4. Critical 체크
        float criticalValue = Mathf.Clamp01(attacker.CriRate);
        float resistValue = defender.CriticalResist;
        float advantageValue = 0f;

        // 속성 우위 체크
        if (attacker.IsAdvantage(attacker.Element, defender.Element))
        {
            advantageValue = ELEMENT_ADVANTAGE_CRITICAL_BONUS;
        }

        // BP 보너스
        if (context.UsedBP > 0)
        {
            criticalValue += context.UsedBP * BP_CRIT_BONUS_PER_POINT;
        }

        float criticalRate = Mathf.Clamp01(criticalValue - resistValue + advantageValue);
        float critRandom = UnityEngine.Random.value;

        Debug.Log($"[Critical Check] Rate:{criticalRate:F2} (Base:{criticalValue:F2} - Resist:{resistValue:F2} + Advantage:{advantageValue:F2}) Roll:{critRandom:F2}");

        if (critRandom < criticalRate)
        {
            result |= JudgementType.Critical;
        }
        // Critical이 아닐 때만 Strike 체크
        else
        {
            // 5. Strike 체크
            float strikeRandom = UnityEngine.Random.value;

            Debug.Log($"[Strike Check] Rate:{STRIKE_CHANCE:F2} Roll:{strikeRandom:F2}");

            if (strikeRandom < STRIKE_CHANCE)
            {
                result |= JudgementType.Strike;
            }
        }

        return result;
    }


    /// <summary>
    /// 면역 체크
    /// </summary>
    private static bool CheckImmunity(BattleContext context)
    {
        var defender = context.Defender.BattleActorInfo;

        // IMM_TYPE이 있다면 체크 (CharacterTableData에서)
        // 예: "Physical", "Magic", "Fire", "Ice" 등
        if (!string.IsNullOrEmpty(defender.ImmunityType))
        {
            string[] immunities = defender.ImmunityType.Split(',');
            foreach (string immunity in immunities)
            {
                if (immunity.Trim().Equals(context.AttackType.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // 속성 면역 체크
                if (immunity.Trim().Equals(context.Attacker.BattleActorInfo.Element.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        // 상태이상 면역 체크 (예: 보스는 특정 상태이상 면역)
        if (defender.IsBoss && context.AttackType == EAttackType.Special)
        {
            return true;  // 보스는 상태이상 면역
        }

        //스킬 이뮨 체크




        return false;
    }




    /// <summary>
    /// 블록 데미지 감소 적용
    /// </summary>
    private static int ApplyBlock(int damage, BattleContext context)
    {
        // ===== 수정: BlockPower 스탯 적용 =====
        float blockPower = context.Defender.BattleActorInfo.GetStatFloat(CharacterStatType.BlockPower);

        // BlockPower가 0이면 기본값 사용
        if (blockPower <= 0)
        {
            blockPower = BLOCK_DAMAGE_REDUCTION * 100f; // 50%
        }

        float reduction = blockPower / 100f;
        return Mathf.Max(1, Mathf.RoundToInt(damage * (1f - reduction)));
    }


    /// <summary>
    /// 저항 체크 (스킬 효과)
    /// </summary>
    private static bool CheckResist(BattleContext context)
    {
        if (!context.IsSkillAttack)
            return false;

        var attacker = context.Attacker.BattleActorInfo;
        var defender = context.Defender.BattleActorInfo;

        // 기존 SkillHit, SkillResist 사용
        float skillHitRate = attacker.SkillHitRate;
        float skillResistRate = defender.SkillResistRate;

        // ===== 새로 추가: CCEnhance로 적중률 보정 =====
        float ccEnhance = attacker.GetStatFloat(CharacterStatType.CCEnhance);
        if (ccEnhance > 0)
        {
            skillHitRate += ccEnhance;
        }

        // ===== 새로 추가: Tenacity로 저항률 보정 =====  
        float tenacity = defender.GetStatFloat(CharacterStatType.Tenacity);
        if (tenacity > 0)
        {
            skillResistRate += tenacity;
        }

        float finalResistChance = Mathf.Clamp01(skillResistRate - skillHitRate);
        return UnityEngine.Random.value < finalResistChance;
    }

    

    /// <summary>
    /// 데미지 양 계산 - Lua CalculateDamageAmount 포팅
    /// </summary>
    private static DamageResult CalculateDamageAmount(JudgementType judgement, BattleContext context)
    {
        var result = new DamageResult { Judgement = judgement };
        var attacker = context.Attacker.BattleActorInfo;
        var defender = context.Defender.BattleActorInfo;

        // 기본 데미지 결정
        int origin = GetBaseDamage(attacker);
        float damage = origin;


        // ===== 스킬 계수 적용 (가장 먼저 적용) =====
        if (context.IsSkillAttack && context.SkillPower > 0)
        {
            // SkillPower를 퍼센트로 적용 (100 = 100% = 1.0배)
            float skillMultiplier = context.SkillPower / 100f;
            damage = damage * skillMultiplier;

            Debug.Log($"[Damage] Skill Power Applied: {origin} * {skillMultiplier:F2} = {damage:F0}");
        }

        // 1. 판정별 데미지 계산
        if ((judgement & JudgementType.Critical) != 0)
        {
            float criticalRate = Mathf.Clamp(attacker.CriDmg, 1f, 3.5f);
            damage = Mathf.Ceil(origin * criticalRate);
            result.CriticalBonus = (int)(damage - origin);
            result.IsCritical = true;
            Debug.Log($"[Damage] CRITICAL: {origin} * {criticalRate:F2} = {damage}");
        }
        else if ((judgement & JudgementType.Strike) != 0)
        {
            damage = Mathf.Ceil(origin * STRIKE_DAMAGE_RATE);
            Debug.Log($"[Damage] STRIKE: {origin} * {STRIKE_DAMAGE_RATE} = {damage}");
        }
        else
        {
            Debug.Log($"[Damage] Normal: {origin}");
        }


        // 크리티컬 무효화 체크
        if (result.IsCritical && defender.CheckAndNullifyCrit())
        {
            result.IsCritical = false;

            // UI 피드백
            if (BattleUIManager.Instance != null)
            {
               // BattleUIManager.Instance.ShowCustomText(target, "치명타 무효!", Color.yellow);
            }
        }



        // 2. 속성 상성 (기존 코드 + 새 스탯 추가)
        if (attacker.IsAdvantage(attacker.Element, defender.Element))
        {
            origin = (int)damage;
            float advantageRate = ELEMENT_ADVANTAGE_DAMAGE_RATE;

            // ===== 새로 추가: ElementalAtk 적용 =====
            float elementalAtk = attacker.GetStatFloat(CharacterStatType.ElementalAtk);
            if (elementalAtk > 0)
            {
                advantageRate += (elementalAtk / 100f);
            }

            damage = Mathf.Ceil(origin * advantageRate);
            result.ElementMultiplier = advantageRate;
        }
        else if (attacker.IsDisadvantage(attacker.Element, defender.Element))
        {
            origin = (int)damage;
            float disadvantageRate = 1f / ELEMENT_ADVANTAGE_DAMAGE_RATE;

            // ===== 새로 추가: ElementalRes 적용 =====
            float elementalRes = defender.GetStatFloat(CharacterStatType.ElementalRes);
            if (elementalRes > 0)
            {
                // 속성 저항이 역상성 피해를 더 줄임
                disadvantageRate *= (1f - (elementalRes / 100f * ELEMENTAL_RES_EFFICIENCY));
            }

            damage = Mathf.Ceil(origin * disadvantageRate);
            result.ElementMultiplier = disadvantageRate;
        }



        // ===== 추가: Range 타입 페널티 적용 =====
        // 3. Range 공격 페널티
        float rangeModifier = attacker.GetRangeDamageModifier(context.Defender.GetComponent<BattleActor>());
        if (rangeModifier < 1.0f)
        {
            damage *= rangeModifier;
            Debug.Log($"[Damage] Range penalty applied: x{rangeModifier:F2}");
        }
        // ========================================



        // 3. 방어력 적용 (기존 코드 + Penetration 추가)
        if (!context.DamageOptions.HasFlag(DamageOption.IgnoreDefense) &&
            !context.DamageOptions.HasFlag(DamageOption.TrueDamage))
        {
            origin = (int)damage;
            float defenceValue = defender.Defence;

            // ===== 새로 추가: Penetration 적용 =====
            float penetration = attacker.GetStatFloat(CharacterStatType.Penetration);
            if (penetration > 0)
            {
                penetration = Mathf.Clamp(penetration, 0, MAX_PENETRATION);
                defenceValue *= (1f - penetration / 100f);
                defenceValue = Mathf.Max(0, defenceValue);
            }

            // 기존 방어력 공식 그대로
            float defenceReduction = 1f - (defenceValue / (defenceValue + DEFENSE_BASE_VALUE)) * DEFENSE_REDUCTION_RATE;
            defenceReduction = Mathf.Clamp01(defenceReduction);

            damage = Mathf.Floor(origin * defenceReduction);
            damage = Mathf.Max(1, damage);

            result.Defense = origin - (int)damage;
        }



        // 4. 데미지 변동률 적용
        float variantRate = attacker.VariantDamage;
        origin = (int)damage;

        float variance = 1f + UnityEngine.Random.Range(-variantRate, variantRate);
        damage = damage * variance;

        Debug.Log($"[Damage] Variance: {origin} * {variance:F2} = {damage}");


        // ===== 새로 추가: DamageReduce 최종 적용 =====
        float damageReduce = defender.GetStatFloat(CharacterStatType.DamageReduce);
        if (damageReduce > 0)
        {
            damageReduce = Mathf.Clamp(damageReduce, 0, MAX_DAMAGE_REDUCE);
            damage *= (1f - damageReduce / 100f);
        }




        // 5. BP 보너스 적용
        if (context.UsedBP > 0)
        {
            float bpBonus = 1f + (context.UsedBP * BP_DAMAGE_BONUS_PER_POINT);
            damage *= bpBonus;
            Debug.Log($"[Damage] BP Bonus: x{bpBonus:F2} (Used BP: {context.UsedBP})");
        }

        // 6. 고정 데미지 처리
        if (context.DamageOptions.HasFlag(DamageOption.TrueDamage))
        {
            // 고정 데미지는 방어력과 감소 효과 무시
            damage = origin;
            Debug.Log($"[Damage] True Damage: {damage}");
        }


        // 7. Shield 처리 (새로 추가)
        if (!context.DamageOptions.HasFlag(DamageOption.IgnoreShield) && defender.Shield > 0)
        {
            int shieldBefore = defender.Shield;
            int damageToShield = Mathf.Min(defender.Shield, (int)damage);

            // Shield가 데미지 흡수
            defender.Shield -= damageToShield;
            damage = damage - damageToShield;

            // Shield 파괴 시 남은 데미지만 적용
            result.ShieldAbsorbed = damageToShield;

            Debug.Log($"[Shield] Absorbed {damageToShield} damage. Shield: {shieldBefore} -> {defender.Shield}");

            // Shield가 완전히 파괴되었는지 체크
            if (defender.Shield <= 0)
            {
                Debug.Log($"[Shield] Shield broken!");
                // Shield 파괴 이펙트 (필요시)
                if (BattleEffectManager.Instance != null)
                {
                    //BattleEffectManager.Instance.PlayEffect("ShieldBreak", context.Defender.GetComponent<BattleActor>()).Forget();
                }
            }
        }




        // 결과 설정
        result.OriginalDamage = GetBaseDamage(attacker);
        result.FinalDamage = Mathf.Max(0, Mathf.RoundToInt(damage));
        result.Options = context.DamageOptions;

        // ===== 새로 추가: 흡혈 처리 =====
        float lifesteal = attacker.GetStatFloat(CharacterStatType.LifeSteal);
        if (lifesteal > 0 && result.FinalDamage > 0)
        {
            int healAmount = Mathf.RoundToInt(result.FinalDamage * lifesteal / 100f);
            attacker.AddHp(healAmount);
        }

        // 디버그 정보 저장
        result.DebugInfo["SkillPower"] = context.SkillPower;
        result.DebugInfo["SkillMultiplier"] = context.SkillPower / 100f;


        Debug.Log(result.ToString());

        return result;
    }

    /// <summary>
    /// 기본 데미지 가져오기 (Min/Max 또는 Attack)
    /// </summary>
    private static int GetBaseDamage(BattleCharInfoNew attacker)
    {
        // Min/Max 데미지가 설정되어 있으면 랜덤
        if (attacker.MinDamage > 0 && attacker.MaxDamage > 0)
        {
            return UnityEngine.Random.Range(attacker.MinDamage, attacker.MaxDamage + 1);
        }

        // 아니면 Attack 값 사용
        return attacker.Attack;
    }

    #endregion

    #region Skill Effect Application

    /// <summary>
    /// 스킬 효과 적용 가능 여부 체크
    /// </summary>
    public static bool CanApplySkillEffect(BattleActor target, StatusType effectType, BattleContext context = null)
    {
        // 면역 체크
        if (context != null)
        {
            context.AttackType = EAttackType.Special;
            if (CheckImmunity(context))
            {
                Debug.Log($"[Skill] {target.name} is IMMUNE to {effectType}!");
                return false;
            }
        }

        // 저항 체크
        if (context != null && context.IsSkillAttack)
        {
            if (CheckResist(context))
            {
                Debug.Log($"[Skill] {target.name} RESISTED {effectType}!");
                return false;
            }
        }

        // 특정 상태이상별 추가 체크
        switch (effectType)
        {
            case StatusType.Stun:
            case StatusType.Freeze:
            case StatusType.Sleep:
                // 보스는 행동불능 상태이상 면역
                if (target.BattleActorInfo.IsBoss)
                {
                    Debug.Log($"[Skill] Boss is immune to {effectType}!");
                    return false;
                }
                break;
        }

        return true;
    }

    #endregion



    #region Cooperation & Counter (Lua 포팅)

    /// <summary>
    /// 협공 체크 - Lua CheckCooperation 포팅
    /// </summary>
    public static bool CheckCooperation(BattleActor character, BattleActor target)
    {
        float randomValue = UnityEngine.Random.value;
        float cooperationRate = character.BattleActorInfo.Cooperation;

        Debug.Log($"CheckCooperation -> {character.BattleActorInfo.Name}({character.BattleActorInfo.IsAlly}) cooperation_rate: {cooperationRate} / random_value: {randomValue}");

        return randomValue < cooperationRate;
    }

    /// <summary>
    /// 반격 체크 - 
    /// </summary>
    public static bool CheckCounter(BattleActor sender, BattleActor receiver)
    {
        // 반격 불가 상태 체크
        if (receiver.IsDead || receiver.BattleActorInfo.Hp <= 0)
            return false;

        // 행동불능 상태 체크
        if (receiver.SkillManager != null)
        {
            if (receiver.SkillManager.IsBattleSkillBase(SkillSystem.StatusType.Stun) ||
                receiver.SkillManager.IsBattleSkillBase(SkillSystem.StatusType.Freeze) ||
                receiver.SkillManager.IsBattleSkillBase(SkillSystem.StatusType.Sleep))
            {
                return false;
            }
        }

        float randomValue = UnityEngine.Random.value;
        float counterRate = receiver.BattleActorInfo.Counter;

        Debug.Log($"CheckCounter -> {receiver.BattleActorInfo.Name}({receiver.BattleActorInfo.IsBoss}) counter_rate: {counterRate} / random_value: {randomValue}");

        return randomValue < counterRate;
    }

    #endregion


    #region Helper Methods

    /// <summary>
    /// 디버그 정보 로깅
    /// </summary>
    private static void LogDebugInfo(DamageResult result, BattleContext context)
    {
        result.DebugInfo["AttackerAtk"] = context.Attacker.BattleActorInfo.Attack;
        result.DebugInfo["DefenderDef"] = context.Defender.BattleActorInfo.Def;
        result.DebugInfo["SkillPower"] = context.SkillPower;
        result.DebugInfo["UsedBP"] = context.UsedBP;

        if (result.IsCritical)
        {
            result.DebugInfo["CritMultiplier"] = context.Attacker.BattleActorInfo.CriDmg;
        }
    }

    #endregion

    #region Legacy Support (기존 메서드들 유지)

    /// <summary>
    /// 다단히트 데미지 분배 (기존)
    /// </summary>
    public static int[] DistributeHitDamage(int totalDamage, int hitCount, int maxHitsPerDistribution = 10)
    {
        if (hitCount <= 0) return new int[] { totalDamage };

        List<int> damages = new List<int>();
        int actualHits = Mathf.Min(hitCount, maxHitsPerDistribution);

        float totalRatio = (actualHits * (actualHits + 1)) / 2f;
        int accumulated = 0;

        for (int i = 0; i < actualHits - 1; i++)
        {
            int ratio = i + 1;
            int damage = Mathf.RoundToInt(totalDamage * (ratio / totalRatio));
            damages.Add(damage);
            accumulated += damage;
        }

        damages.Add(totalDamage - accumulated);
        return damages.ToArray();
    }

    /// <summary>
    /// 힐 계산 (기존)
    /// </summary>
    public static int CalculateHeal(BattleActor healer, BattleActor target, int skillPower = 100)
    {
        // 기존 CalculateHeal 로직 활용
        float baseHeal = healer.BattleActorInfo.Attack * 0.5f;
        float healAmount = baseHeal * (skillPower / 100f);

        // ===== 새로 추가: HealPower 적용 =====
        float healPower = healer.BattleActorInfo.GetStatFloat(CharacterStatType.HealPower);
        if (healPower != 0)
        {
            healAmount *= (1f + healPower / 100f);
        }

        // 기존 variance 적용
        float variance = UnityEngine.Random.Range(0.9f, 1.1f);
        healAmount *= variance;

        int maxHealable = target.BattleActorInfo.MaxHp - target.BattleActorInfo.Hp;
        return Mathf.Min(Mathf.RoundToInt(healAmount), maxHealable);
    }



    /// <summary>
    /// CC 지속시간 계산 (CCEnhance, Tenacity 적용)
    /// </summary>
    public static int CalculateCCDuration(BattleActor caster, BattleActor target, int baseDuration)
    {
        float duration = baseDuration;

        // CCEnhance 적용
        float ccEnhance = caster.BattleActorInfo.GetStatFloat(CharacterStatType.CCEnhance);
        if (ccEnhance > 0)
        {
            duration *= (1f + ccEnhance / 100f);
        }

        // Tenacity 적용
        float tenacity = target.BattleActorInfo.GetStatFloat(CharacterStatType.Tenacity);
        if (tenacity > 0)
        {
            duration *= (1f - tenacity / 100f);
        }

        return Mathf.Max(1, Mathf.RoundToInt(duration));
    }

    /// <summary>
    /// 반격 데미지 계산 (Counter 스탯 활용)
    /// </summary>
    public static int CalculateCounterDamage(BattleActor defender, int originalDamage)
    {
        float counterChance = defender.BattleActorInfo.Counter;

        if (UnityEngine.Random.Range(0, 100) < counterChance)
        {
            // 반격 데미지 = 원본 데미지의 50%
            return Mathf.RoundToInt(originalDamage * 0.5f);
        }

        return 0;
    }






    // BattleFormularHelper.cs에 추가할 GetElementalMultiplier 함수
    // 기존 BattleElementHelper의 로직을 활용

    /// <summary>
    /// 속성 상성 배율 계산 (AI 시스템용)
    /// BattleElementHelper.GetElementMultiplier를 래핑
    /// </summary>
    /// <param name="attackerElement">공격자 속성</param>
    /// <param name="defenderElement">방어자 속성</param>
    /// <returns>데미지 배율 (1.0 = 100%)</returns>
    public static float GetElementalMultiplier(EBattleElementType attackerElement, EBattleElementType defenderElement)
    {
        // BattleElementHelper의 기존 함수 호출
        return BattleElementHelper.GetElementMultiplier(attackerElement, defenderElement);
    }

    /// <summary>
    /// 속성 상성 배율 계산 (오버로드 - int 타입용)
    /// </summary>
    public static float GetElementalMultiplier(int attackerElement, int defenderElement)
    {
        return GetElementalMultiplier(
            (EBattleElementType)attackerElement,
            (EBattleElementType)defenderElement
        );
    }

    // ============================================================
    // 만약 BattleElementHelper가 없다면 아래 코드를 BattleFormularHelper.cs에 직접 추가
    // ============================================================

    #region Elemental System (BattleElementHelper가 없는 경우)

    /// <summary>
    /// 속성 상성 배율 계산 - 직접 구현
    /// 기존 프로젝트의 상성 관계를 따름
    /// </summary>
    public static float GetElementalMultiplier_Direct(EBattleElementType attackerElement, EBattleElementType defenderElement)
    {
        // None이나 Max는 중립
        if (attackerElement == EBattleElementType.None ||
            attackerElement == EBattleElementType.Max ||
            defenderElement == EBattleElementType.None ||
            defenderElement == EBattleElementType.Max)
        {
            return 1.0f;
        }

        // 같은 속성끼리는 상성 없음
        if (attackerElement == defenderElement)
        {
            return 1.0f;
        }

        // 기존 프로젝트의 상성 관계 (BattleElementHelper 참조)
        // 순환 상성: Plasma → Power → Chemical → Bio → Plasma

        // 강한 상성 (1.5배)
        if ((attackerElement == EBattleElementType.Plasma && defenderElement == EBattleElementType.Power) ||
            (attackerElement == EBattleElementType.Power && defenderElement == EBattleElementType.Chemical) ||
            (attackerElement == EBattleElementType.Chemical && defenderElement == EBattleElementType.Bio) ||
            (attackerElement == EBattleElementType.Bio && defenderElement == EBattleElementType.Plasma))
        {
            return 1.5f;
        }

        // 약한 상성 (0.77배 = 1/1.3)
        if ((attackerElement == EBattleElementType.Power && defenderElement == EBattleElementType.Plasma) ||
            (attackerElement == EBattleElementType.Chemical && defenderElement == EBattleElementType.Power) ||
            (attackerElement == EBattleElementType.Bio && defenderElement == EBattleElementType.Chemical) ||
            (attackerElement == EBattleElementType.Plasma && defenderElement == EBattleElementType.Bio))
        {
            return 0.77f;
        }

        // Electrical과 Network는 서로에게 강함
        if ((attackerElement == EBattleElementType.Electrical && defenderElement == EBattleElementType.Network) ||
            (attackerElement == EBattleElementType.Network && defenderElement == EBattleElementType.Electrical))
        {
            return 1.5f;
        }

        // 정의되지 않은 관계는 중립
        return 1.0f;
    }

    /// <summary>
    /// BattleCharInfoNew의 IsAdvantage 메서드와 호환
    /// </summary>
    public static bool IsAdvantage(EBattleElementType attackerElement, EBattleElementType defenderElement)
    {
        return GetElementalMultiplier(attackerElement, defenderElement) > 1.0f;
    }

    /// <summary>
    /// BattleCharInfoNew의 IsDisadvantage 메서드와 호환
    /// </summary>
    public static bool IsDisadvantage(EBattleElementType attackerElement, EBattleElementType defenderElement)
    {
        return GetElementalMultiplier(attackerElement, defenderElement) < 1.0f;
    }

    #endregion

    // ============================================================
    // 추가 유틸리티 함수들 (AI 시스템에서 활용)
    // ============================================================

    /// <summary>
    /// 속성별 색상 반환 (UI용)
    /// </summary>
    public static Color GetElementColor(EBattleElementType element)
    {
        return element switch
        {
            EBattleElementType.Power => new Color(0.9f, 0.2f, 0.2f),        // 빨간색
            EBattleElementType.Plasma => new Color(0.4f, 0.8f, 1f),         // 하늘색
            EBattleElementType.Bio => new Color(0.2f, 0.8f, 0.3f),          // 초록색
            EBattleElementType.Chemical => new Color(0.7f, 0.3f, 0.9f),     // 보라색
            EBattleElementType.Electrical => new Color(1f, 0.9f, 0.1f),     // 노란색
            EBattleElementType.Network => new Color(0.1f, 0.1f, 0.1f),      // 검은색
            _ => Color.white
        };
    }

    /// <summary>
    /// 속성 상성 설명 (디버깅용)
    /// </summary>
    public static string GetElementalRelationDescription(EBattleElementType attackerElement, EBattleElementType defenderElement)
    {
        float multiplier = GetElementalMultiplier(attackerElement, defenderElement);

        if (multiplier >= 1.5f)
            return $"{attackerElement} → {defenderElement}: 효과적! (x{multiplier:F2})";
        else if (multiplier <= 0.77f)
            return $"{attackerElement} → {defenderElement}: 효과적이지 않음... (x{multiplier:F2})";
        else if (attackerElement == defenderElement)
            return $"{attackerElement} → {defenderElement}: 같은 속성 (x{multiplier:F2})";
        else
            return $"{attackerElement} → {defenderElement}: 보통 (x{multiplier:F2})";
    }

    /// <summary>
    /// 전체 속성 상성표 (디버깅용)
    /// 기존 프로젝트의 실제 상성 관계
    /// </summary>
    public static void PrintElementalChart()
    {
        Debug.Log("=== 속성 상성표 (실제 프로젝트 기준) ===");
        Debug.Log("순환 관계: Plasma → Power → Chemical → Bio → Plasma");
        Debug.Log("");

        Debug.Log("【강한 상성 (x1.5)】");
        Debug.Log("  Plasma → Power");
        Debug.Log("  Power → Chemical");
        Debug.Log("  Chemical → Bio");
        Debug.Log("  Bio → Plasma");
        Debug.Log("  Electrical ↔ Network (서로 강함)");
        Debug.Log("");

        Debug.Log("【약한 상성 (x0.77)】");
        Debug.Log("  Power → Plasma");
        Debug.Log("  Chemical → Power");
        Debug.Log("  Bio → Chemical");
        Debug.Log("  Plasma → Bio");
        Debug.Log("");

        Debug.Log("【중립 (x1.0)】");
        Debug.Log("  같은 속성끼리");
        Debug.Log("  정의되지 않은 관계");
        Debug.Log("  None 속성");
    }


    #endregion
}