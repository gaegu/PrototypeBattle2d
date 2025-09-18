// =====================================================
// SkillDataClasses.cs
// 스킬 데이터 구조 - Scripts/Battle/Skills/Core 폴더에 저장
// =====================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkillSystem
{
    // =====================================================
    // 메인 스킬 데이터 클래스
    // =====================================================

    [System.Serializable]
    public class AdvancedSkillData
    {
        [Header("BP 업그레이드 정보")]
        public int bpLevel = 0;        // 0: Base, 1: BP1-2, 3: BP3-4, 5: BP5(MAX)
        public int parentSkillId = 0;  // 부모 스킬 ID (0이면 베이스 스킬)
        public string bpUpgradeName = "";  // 업그레이드 시 변경될 이름


        [Header("기본 정보")]
        public int skillId;
        public string skillName = "새 스킬";
        public string description = "";

        [Header("분류")]
        public SkillCategory category = SkillCategory.Active;
        public CharacterClass characterClass = CharacterClass.All;
        public int tier = 1;

        [Header("코스트")]
        public int cooldown = 2;
        public int specialGauge = 0;

        [Header("패시브 트리거")]
        public TriggerType triggerType = TriggerType.Always;
        public float triggerChance = 1f;
        public string triggerCondition = "";

        [Header("효과")]
        public List<AdvancedSkillEffect> effects = new List<AdvancedSkillEffect>();

        [Header("비주얼")]
        public Sprite icon;
        public GameObject effectPrefab;
        public AudioClip soundEffect;

        [Header("메타 데이터")]
        public DateTime lastModified;
        public string tags = "";


        // BP 업그레이드 관계
        [NonSerialized]
        public AdvancedSkillData baseSkill;  // 베이스 스킬 참조
        [NonSerialized]
        public Dictionary<int, AdvancedSkillData> bpUpgrades = new Dictionary<int, AdvancedSkillData>();


        public AdvancedSkillData Clone()
        {
            var clone = new AdvancedSkillData
            {
                skillId = this.skillId,
                skillName = this.skillName,
                description = this.description,
                category = this.category,
                characterClass = this.characterClass,
                tier = this.tier,
                cooldown = this.cooldown,
                specialGauge = this.specialGauge,
                triggerType = this.triggerType,
                triggerChance = this.triggerChance,
                triggerCondition = this.triggerCondition,
                icon = this.icon,
                effectPrefab = this.effectPrefab,
                soundEffect = this.soundEffect,
                tags = this.tags,
                effects = new List<AdvancedSkillEffect>()
            };

            foreach (var effect in effects)
            {
                clone.effects.Add(effect.Clone());
            }

            return clone;
        }

        public string ToCSV()
        {
            var csv = new System.Text.StringBuilder();
            csv.Append($"{skillId},{skillName},{(int)category},{(int)characterClass},{tier},{cooldown}");

            // 효과들을 CSV 형식으로 변환
            for (int i = 0; i < 3; i++)
            {
                if (i < effects.Count)
                {
                    var effect = effects[i];
                    csv.Append($",{effect.targetType},{effect.GetCSVDescription()}");
                }
                else
                {
                    csv.Append(",X,X");
                }
            }

            // BP 정보 추가
            csv.Append($",{bpLevel},{parentSkillId}");

            return csv.ToString();
        }
    }

    // =====================================================
    // 스킬 효과 클래스
    // =====================================================

    [System.Serializable]
    public class AdvancedSkillEffect
    {
        [Header("기본 설정")]
        public string name = "새 효과";
        public EffectType type = EffectType.Damage;
        public TargetType targetType = TargetType.EnemySingle;
        public int targetCount = 1;

        [Header("발동")]
        public float chance = 1f;
        public float value = 100f;
        public int duration = 0;

        [Header("데미지 설정")]
        public DamageType damageType = DamageType.Physical;
        public DamageBase damageBase = DamageBase.Attack;
        public bool canCrit = true;


        [Header("버프/디버프 설정")]
        public StatType statType = StatType.Attack;
        public StatusType statusType = StatusType.Stun;


        [Header("중첩 설정")]
        public bool isStackable = false;
        public int maxStacks = 3;

        [Header("특수 효과")]
        public int summonId = 0;
        public int summonCount = 1;
        public int transformId = 0;
        public string specialEffect = "";

        [Header("조건부")]
        public bool hasCondition = false;
        public ConditionType conditionType = ConditionType.None;
        public float conditionValue = 0;

        [Header("UI")]
        public bool isExpanded = false;
        public string tooltip = "";

        // ===== 데미지 타입 전용 추가 필드 =====
        // 기존 필드들 다음에 추가하세요

        [Header("데미지 계산 상세 설정 (Damage Type 전용)")]

        [Tooltip("스킬 계수를 별도로 지정 (value와 별개로 사용하려면)")]
        public bool useCustomSkillPower = false;

        [Tooltip("커스텀 스킬 계수 (%). 100 = 공격력의 100%")]
        [Range(0, 1000)]
        public float customSkillPower = 100f;

        [Tooltip("방어 무시 비율 (%)")]
        [Range(0, 100)]
        public float defenseIgnorePercent = 0f;

        [Tooltip("쉴드 무시 여부")]
        public bool ignoreShield = false;

        [Tooltip("속성 보너스 배율 (1.0 = 기본)")]
        [Range(0.5f, 2.0f)]
        public float elementalBonusMultiplier = 1.0f;

        [Header("힐 계산 상세 설정 (Heal Type 전용)")]

        [Tooltip("힐 계수를 별도로 지정 (value와 별개)")]
        public bool useCustomHealPower = false;

        [Tooltip("커스텀 힐 계수 (%)")]
        [Range(0, 500)]
        public float customHealPower = 100f;

        [Tooltip("오버힐 허용 (최대 HP 초과 회복)")]
        public bool allowOverheal = false;

        [Tooltip("오버힐 최대 비율 (% of MaxHP)")]
        [Range(100, 200)]
        public float overhealMaxPercent = 120f;

        [Tooltip("힐 감소 효과 무시")]
        public bool ignoreHealReduction = false;

        [Tooltip("크리티컬 힐 가능")]
        public bool canCriticalHeal = false;

        [Tooltip("추가 힐 보너스 (고정값)")]
        public float bonusHealFlat = 0f;

        [Header("버프/디버프 상세 설정 (Buff/Debuff Type 전용)")]

        [Tooltip("고정값으로 적용 (true: 고정값, false: 퍼센트)")]
        public bool isFixedValue = false;

        [Tooltip("중첩 가능 여부")]
        public bool canStack = false;

        [Tooltip("최대 중첩 수")]
        [Range(1, 10)]
        public int maxStackCount = 3;

        [Tooltip("중첩시 지속시간 갱신")]
        public bool refreshDurationOnStack = true;

        [Tooltip("중첩시 효과 증폭 (1.0 = 선형 증가)")]
        [Range(0.5f, 2.0f)]
        public float stackMultiplier = 1.0f;

        [Tooltip("디버프 저항 무시")]
        public bool ignoreResistance = false;

        [Tooltip("해제 불가능")]
        public bool cannotDispel = false;

        [Tooltip("죽어도 유지")]
        public bool persistOnDeath = false;

        [Tooltip("턴 종료시 효과 감소 (점진적 약화)")]
        public bool decayOverTime = false;

        [Tooltip("감소율 (% per turn)")]
        [Range(0, 50)]
        public float decayRate = 10f;



        [Header("상태이상 상세 설정 (StatusEffect Type 전용)")]

        [Tooltip("DoT 틱 간격 (0 = 매 턴)")]
        public int dotInterval = 0;

        [Tooltip("DoT 데미지 계산 방식")]
        public DotCalculationType dotCalcType = DotCalculationType.AttackBased;

        [Tooltip("상태이상 중첩 가능")]
        public bool statusCanStack = false;

        [Tooltip("최대 중첩 수")]
        [Range(1, 5)]
        public int statusMaxStacks = 1;

        [Tooltip("CC 저항 무시")]
        public bool ignoreCCResist = false;

        [Tooltip("보스 면역 무시")]
        public bool ignoreBossImmunity = false;

        [Tooltip("해제 불가")]
        public bool cannotCleanse = false;

        [Tooltip("행동 제한 타입")]
        public ActionRestriction actionRestriction = ActionRestriction.None;



        [Header("회복/보호막 설정")]
        public HealBase healBase = HealBase.MaxHP;
        public ShieldBase shieldBase = ShieldBase.MaxHP;

        public bool shieldCanStack = false;  // Shield 누적 가능 여부
        public int shieldRemoveAmount = 0;   // Shield 제거량 (0이면 전체 제거)


        // Dispel 관련

        [Header("해제 설정")]
        public DispelType dispelType = DispelType.Debuff;
        public int dispelCount = 1;
        public DispelPriority dispelPriority = DispelPriority.Random;  // 새로 추가


        // FixedDamage 관련 (새로 추가)
        [Header("Fixed Damage Settings")]
        public int minFixedDamage = 0;           // 최소 고정 데미지
        public int maxFixedDamage = 0;           // 최대 고정 데미지 (0 = 무제한)
        public float fixedDamageMultiplier = 1f; // 고정 데미지 배율
        public bool fixedDamageCanCrit = false;  // 고정 데미지 크리티컬 가능 여부



        // Reflect関連
        public int maxReflectDamage = 0;  // 反射ダメージ上限（0 = 無制限）
        public bool reflectOnlyPhysical = false;  // 物理ダメージのみ反射
        public bool reflectOnlyMagical = false;   // 魔法ダメージのみ反射


        // LifeSteal 관련 (새로 추가)
        [Header("Life Steal Settings")]
        public int minLifeSteal = 0;         // 최소 흡혈량
        public int maxLifeSteal = 0;         // 최대 흡혈량 (0 = 무제한)
        public bool lifeStealFromShield = false;  // Shield 데미지도 흡혈 가능

        [Header("추가 공격")]
        public int extraAttackCount = 1;          // 추가 공격 횟수
        public float extraAttackDamageModifier = 1.0f;  // 추가 공격 데미지 배율
        public float extraAttackDelay = 0.3f;     // 추가 공격 간 딜레이
       
        
        [Header("재생(Regen) 설정")]
        public bool applyFirstTickImmediately = false;  // 첫 틱을 즉시 적용
        public bool showTickNumbers = true;             // 매 틱마다 숫자 표시


        /// <summary>
        /// 실제 사용할 스킬 계수 반환
        /// </summary>
        public float GetSkillPower()
        {
            return useCustomSkillPower ? customSkillPower : value;
        }

        /// <summary>
        /// 데미지 타입인지 확인
        /// </summary>
        public bool IsDamageType()
        {
            return type == EffectType.Damage || type == EffectType.TrueDamage;
        }


        // Helper 메서드
        public float GetHealPower()
        {
            return useCustomHealPower ? customHealPower : value;
        }

        public bool IsHealType()
        {
            return type == EffectType.Heal || type == EffectType.HealOverTime;
        }


        // Helper 메서드
        public bool IsBuffType()
        {
            return type == EffectType.Buff;
        }

        public bool IsDebuffType()
        {
            return type == EffectType.Debuff;
        }


        // Helper
        public bool IsDoT()
        {
            return statusType == StatusType.Poison ||
                   statusType == StatusType.Burn ||
                   statusType == StatusType.Bleed ||
                   statusType == StatusType.Bleeding;
        }

        public bool IsCC()
        {
            return statusType == StatusType.Stun ||
                   statusType == StatusType.Freeze ||
                   statusType == StatusType.Paralyze ||
                   statusType == StatusType.Sleep ||
                   statusType == StatusType.Petrify;
        }
      


        public string GetShortDescription()
        {
            var chanceText = chance < 1 ? $"{chance * 100:F0}% " : "";

            return type switch
            {
                EffectType.Damage => $"{chanceText}{value}% 데미지",
                EffectType.TrueDamage => $"{chanceText}{value}% 고정 데미지",
                EffectType.FixedDamage => $"{chanceText}{value} 고정 데미지",
                EffectType.Execute => $"HP {value}% 이하 즉사",
                EffectType.Heal => $"{value}% 회복",
                EffectType.HealOverTime => $"{value}% 지속 회복",
                EffectType.Buff => $"{statType} +{value}%",
                EffectType.Debuff => $"{statType} -{value}%",
                EffectType.StatusEffect => $"{chanceText}{GetStatusName(statusType)}",
                EffectType.Shield => $"{value}% 보호막",
                EffectType.Reflect => $"{value}% 반사",
                EffectType.LifeSteal => $"{value}% 흡혈",
                EffectType.Dispel => $"{dispelType} 해제",
                EffectType.DamageReduction => $"{value}% 피해 감소",
                EffectType.Immunity => "면역",
                EffectType.Invincible => "무적",
                EffectType.Summon => $"소환 (ID:{summonId})",
                EffectType.Clone => $"분신 생성 ({value}% 능력치)",
                EffectType.Transform => $"변신 (ID:{transformId})",
                EffectType.TimeStop => $"시간 정지 {duration}턴",
                EffectType.TimeAccelerate => "시간 가속",
                EffectType.MindControl => $"정신 지배 {duration}턴",
                EffectType.ChainAttack => $"연쇄 {targetCount}회",
                EffectType.StealBuff => $"버프 {dispelCount}개 훔치기",
                EffectType.CopyBuff => $"버프 {dispelCount}개 복사",
                EffectType.Special => specialEffect,
                _ => type.ToString()
            };
        }

        public string GetFullDescription()
        {
            var desc = GetShortDescription();

            if (duration > 0)
            {
                desc += $" ({duration}턴)";
            }

            if (hasCondition)
            {
                desc += $" [조건: {GetConditionDescription()}]";
            }

            if (targetType != TargetType.Self && targetType != TargetType.EnemySingle)
            {
                desc += $" [{GetTargetDescription()}]";
            }

            return desc;
        }

        public string GetCSVDescription()
        {
            var chanceText = chance < 1 ? $"{chance * 100:F0}% 확률로 " : "";

            return type switch
            {
                EffectType.Damage => $"{value}% 데미지",
                EffectType.FixedDamage => $"{value} 고정 데미지",
                EffectType.Execute => $"HP {value}% 이하 즉사",
                EffectType.Heal => $"최대 체력의 {value}% 만큼 회복",
                EffectType.Buff => $"{chanceText}{duration}턴간 {GetStatName(statType)} {value}% 상승",
                EffectType.Debuff => $"{chanceText}{duration}턴간 {GetStatName(statType)} {value}% 감소",
                EffectType.StatusEffect => GetStatusEffectCSV(),
                EffectType.Shield => $"{duration}턴간 자신 최대 체력의 {value}% 보호막",
                EffectType.Reflect => $"{duration}턴간 받는 피해 {value}% 반사",
                EffectType.LifeSteal => $"피해량의 {value}% 회복",
                EffectType.Dispel => $"약화 효과 {dispelCount}개 해제",
                EffectType.DamageReduction => $"{duration}턴간 받는 피해 {value}% 감소",
                EffectType.Immunity => $"{duration}턴간 면역",
                EffectType.TimeStop => $"{duration}턴간 시간 정지",
                EffectType.ChainAttack => $"{targetCount}회 연쇄",
                EffectType.StealBuff => $"버프 {dispelCount}개 훔치기",
                EffectType.Special => specialEffect,
                _ => ""
            };
        }

        private string GetStatusEffectCSV()
        {
            var chanceText = chance < 1 ? $"{chance * 100:F0}% 확률로 " : "";

            return statusType switch
            {
                StatusType.Stun => $"{chanceText}{duration}턴간 기절",
                StatusType.Paralyze => $"{chanceText}{duration}턴간 마비",
                StatusType.Silence => $"{chanceText}{duration}턴간 침묵",
                StatusType.Bind => $"{chanceText}{duration}턴간 속박",
                StatusType.Taunt => $"{chanceText}{duration}턴간 도발",
                StatusType.Fear => $"{chanceText}{duration}턴간 공포",
                StatusType.Burn => $"{chanceText}{duration}턴 화상을 입혀 매 턴 {value}% 피해",
                StatusType.Poison => $"{chanceText}{duration}턴간 중독시켜 {value}% 피해",
                StatusType.Bleed => $"{chanceText}{duration}턴간 출혈시켜 매 턴 {value}% 피해",
                StatusType.InternalInjury => $"{chanceText}{duration}턴간 내상 (회복 불가)",
                StatusType.Infection => $"{chanceText}감염 ({duration}턴)",
                _ => $"{chanceText}{duration}턴간 {statusType}"
            };
        }

        private string GetConditionDescription()
        {
            return conditionType switch
            {
                ConditionType.HPBelow => $"HP {conditionValue}% 이하",
                ConditionType.HPAbove => $"HP {conditionValue}% 이상",
                ConditionType.TargetHPBelow => $"대상 HP {conditionValue}% 이하",
                ConditionType.TargetHPAbove => $"대상 HP {conditionValue}% 이상",
                ConditionType.MPBelow => $"MP {conditionValue}% 이하",
                ConditionType.MPAbove => $"MP {conditionValue}% 이상",
                ConditionType.BuffCount => $"버프 {conditionValue}개 이상",
                ConditionType.DebuffCount => $"디버프 {conditionValue}개 이상",
                ConditionType.EnemyCount => $"적 {conditionValue}명 이상",
                ConditionType.AllyCount => $"아군 {conditionValue}명 이상",
                ConditionType.TurnCount => $"{conditionValue}턴 이후",
                ConditionType.HasBuff => "버프 보유 시",
                ConditionType.HasDebuff => "디버프 보유 시",
                ConditionType.TurnNumber => $"{(conditionValue == 0 ? "짝수" : "홀수")} 턴",
                _ => ""
            };
        }

        private string GetTargetDescription()
        {
            return targetType switch
            {
                TargetType.Self => "자신",
                TargetType.EnemySingle => "적 단일",
                TargetType.EnemyAll => "적 전체",
                TargetType.EnemyRow => "적 전/후열",
                TargetType.AllySingle => "아군 단일",
                TargetType.AllyAll => "아군 전체",
                TargetType.AllyRow => "아군 전/후열",
                TargetType.Random => "무작위",
                TargetType.Multiple => $"다수({targetCount})",
                TargetType.Lowest => "가장 낮은",
                TargetType.Highest => "가장 높은",
                TargetType.Adjacent => "인접한",
                TargetType.Line => "직선",
                TargetType.Cross => "십자",
                TargetType.Fan => "부채꼴",
                TargetType.BackRow => "후열",
                TargetType.FrontRow => "전열",
                TargetType.Chain => "연쇄",
                TargetType.Pierce => "관통",
                _ => targetType.ToString()
            };
        }

        private string GetStatusName(StatusType status)
        {
            return status switch
            {
                StatusType.Stun => "기절",
                StatusType.Paralyze => "마비",
                StatusType.Silence => "침묵",
                StatusType.Bind => "속박",
                StatusType.Suppress => "제압",
                StatusType.Taunt => "도발",
                StatusType.Fear => "공포",
                StatusType.Freeze => "동결",
                StatusType.Burn => "화상",
                StatusType.Poison => "중독",
                StatusType.Bleed => "출혈",
                StatusType.InternalInjury => "내상",
                StatusType.Sleep => "수면",
                StatusType.Confuse => "혼란",
                StatusType.Petrify => "석화",
                StatusType.Blind => "실명",
                StatusType.Slow => "둔화",
                StatusType.Weaken => "약화",
                StatusType.Curse => "저주",
                StatusType.TimeStop => "시간정지",
                StatusType.SystemCrash => "시스템마비",
                StatusType.BuffBlock => "버프차단",
                StatusType.SkillSeal => "스킬봉인",
                StatusType.HealBlock => "치유차단",
                StatusType.Infection => "감염",
                StatusType.Erosion => "부식",
                StatusType.Mark => "표식",
                _ => status.ToString()
            };
        }

        private string GetStatName(StatType stat)
        {
            return stat switch
            {
                StatType.Attack => "공격력",
                StatType.Defense => "방어력",
                StatType.Speed => "속도",
                StatType.CritRate => "치명 확률",
                StatType.CritDamage => "치명 피해",
                StatType.Accuracy => "적중",
                StatType.Evasion => "회피",
                StatType.EffectHit => "효과 적중",
                StatType.EffectResist => "효과 저항",
                StatType.HealBlock => "회복 불가",
                StatType.HealReduction => "회복량 감소",
                StatType.MaxHP => "최대 체력",
                _ => stat.ToString()
            };
        }

        public AdvancedSkillEffect Clone()
        {
            return JsonUtility.FromJson<AdvancedSkillEffect>(JsonUtility.ToJson(this));
        }
    }

    // =====================================================
    // 열거형 정의
    // =====================================================

    public enum SkillCategory
    {
        All,
        Active,
        Passive,
        SpecialActive,
        SpecialPassive
    }

    public enum CharacterClass
    {
        All,
        Vanguard,    // 뱅가드 (방어)
        Slaughter,   // 슬로터 (공격)
        Jacker,      // 재커 (민첩)
        Rewinder     // 리와인더 (지원)
    }

    public enum EffectType
    {
        None,
        // 데미지
        Damage,
        TrueDamage,
        FixedDamage,        // 고정 수치 데미지
        Execute,            // 즉사 (조건부)

        // 회복
        Heal,
        HealOverTime,
        LifeSteal,

        // 버프/디버프
        Buff,
        Debuff,

        // 상태이상
        StatusEffect,

        // 보호
        Shield,
        Reflect,
        DamageReduction,
        Immunity,
        Invincible,

        // 해제
        Dispel,

        // 추가 필요
        ExtraAttack,        // 추가 공격
        Counter,            // 반격
        ConditionalDamage,  // 조건부 데미지

        ConditionalBuff,    // 조건부 버프


        // 소환/변신
        Summon,
        Clone,              // 분신 생성
        Transform,
        Metamorphosis,      // 영구 변신/각성

        // 시간 조작
        TimeStop,           // 시간 정지
        TimeAccelerate,     // 시간 가속 (즉시 행동)
        TimeRewind,         // 시간 되돌리기
        Paradox,            // 타임 패러독스 (2배 행동)

        // 제어
        MindControl,        // 정신 지배
        PositionSwap,       // 위치 교환
        Banish,             // 추방

        // 연쇄/확산
        ChainAttack,        // 연쇄 공격
        Spread,             // 전염/확산
        Penetrate,          // 관통

        // 흡수/변환
        StealBuff,          // 버프 훔치기
        CopyBuff,           // 버프 복사
        ConvertDebuff,      // 디버프→버프 전환
        AbsorbDamage,       // 데미지 흡수

        // 특수 메커니즘
        ShareDamage,        // 피해 분담
        Revenge,            // 복수/반격 강화 (HP 낮을수록 강해짐)
        Berserk,            // 광폭화
        RandomEffect,       // 랜덤 효과
        Special,            // 기타 특수 효과

        // 기존 추가분
        InstantAction,      // 즉시 행동 부여
        CooldownReset,      // 쿨다운 초기화
        ActionGauge,        // 행동 게이지 조작
        ExtraTurn,          // 추가 턴 부여
        SkipTurn,           // 턴 스킵

        StateRestore,       // HP/버프 상태 복원
        Execution,          // 조건부 즉사 (HP % 이하)
        DamageShare,        // 피해 분산/공유
        Sacrifice,          // HP 소모하여 효과 발동

    }

    public enum TargetType
    {
        None,
        Self,               // 자신
        EnemySingle,        // 적 단일
        EnemyAll,           // 적 전체
        EnemyRow,           // 적 전열/후열
        AllySingle,         // 아군 단일
        AllyAll,            // 아군 전체
        AllyRow,            // 아군 전열/후열
        Random,             // 무작위
        Multiple,           // 다수 (targetCount 사용)
        Lowest,             // 가장 낮은 (HP/공격력 등)
        Highest,            // 가장 높은

        // 새로 추가된 범위 타입
        Adjacent,           // 인접한 대상들
        Line,               // 직선
        Cross,              // 십자 범위
        Fan,                // 부채꼴
        Circle,             // 원형 범위

        // 조건부 타겟
        BackRow,            // 후열만
        FrontRow,           // 전열만
        Marked,             // 표식된 대상
        Infected,           // 감염된 대상
        Buffed,             // 버프 보유 대상
        Debuffed,           // 디버프 보유 대상

        // 특수
        Chain,              // 연쇄 (가까운 순서대로)
        Pierce,             // 관통 (직선상 모두)
    }

    public enum TriggerType
    {
        Always,             // 항상
        OnBattleStart,      // 전투 시작 시
        OnTurnStart,        // 턴 시작 시
        OnTurnEnd,          // 턴 종료 시
        OnAttack,           // 공격 시
        OnDamaged,          // 피격 시
        OnKill,             // 처치 시
        OnDeath,            // 사망 시
        OnCrit,             // 크리티컬 시
        OnBlock,            // 블록 시
        OnEvade,            // 회피 시
        OnHit,              // 공격 타격시
        Conditional         // 조건부
    }

    public enum StatType
    {
        None,
        Attack,             // 공격력
        Defense,            // 방어력
        Speed,              // 속도
        CritRate,           // 치명타율
        CritDamage,         // 치명타 피해
        Accuracy,           // 적중
        Evasion,            // 회피
        EffectHit,          // 효과 적중
        EffectResist,       // 효과 저항
        HealBlock,          // 회복 불가
        HealReduction,      // 회복량 감소
        MaxHP,              // 최대 HP
    }

    public enum StatusType
    {
        None,
        // 기존 상태이상
        Stun,               // 기절
        Silence,            // 침묵
        Taunt,              // 도발
        Freeze,             // 동결
        Burn,               // 화상
        Poison,             // 중독
        Bleed,              // 출혈
        Sleep,              // 수면
        Confuse,            // 혼란
        Petrify,            // 석화
        Blind,              // 실명
        Slow,               // 둔화
        Weaken,             // 약화
        Curse,              // 저주
        Bleeding,           // 지속 출혈

        // 새로 추가된 상태이상
        Paralyze,           // 마비 (전자기 마비 등)
        Bind,               // 속박/구속 (체포, 강제 단속)
        Suppress,           // 제압 (제압 사격)
        Fear,               // 공포
        InternalInjury,     // 내상
        TimeStop,           // 시간 정지 상태
        SystemCrash,        // 시스템 마비/다운
        BuffBlock,          // 버프 금지/차단
        SkillSeal,          // 스킬 봉인
        HealBlock,          // 치유 불가/차단
        Infection,          // 감염 (바이러스 확산)
        Erosion,            // 부식 (산성 효과)
        Mark,               // 표식

        // 특수 버프성 상태
        Resurrect,          // 부활 버프 (죽을 때 1회 부활)
        Stealth,            // 은신 (타겟 불가)
        Immortal,           // 불사 (HP 1 이하로 안 떨어짐)
    }

    public enum DamageType
    {
        Physical,           // 물리
        Magical,            // 마법
        True,               // 고정
        Proportional        // 비례
    }

    public enum DamageBase
    {
        Attack,             // 공격력 기준
        Defense,            // 방어력 기준
        CurrentHP,          // 현재 HP 기준
        MaxHP,              // 최대 HP 기준
        LostHP              // 잃은 HP 기준
    }

    public enum HealBase
    {
        MaxHP,              // 최대 HP 기준
        CurrentHP,          // 현재 HP 기준
        Attack,             // 공격력 기준
        Defense,             // 방어력 기준
        Fixed,
        LostHP

    }

    public enum ShieldBase
    {
        MaxHP,              // 최대 HP 기준
        Defense,            // 방어력 기준
        Attack              // 공격력 기준
    }

    public enum DispelType
    {
        Buff,               // 버프 제거
        Debuff,             // 디버프 제거
        StatusEffect,       // 상태이상 제거
        Shield,
        All                 // 모두 제거
    }

    public enum ConditionType
    {
        None,
        HPBelow,            // 자신 HP가 X% 이하
        HPAbove,            // 자신 HP가 X% 이상
        StatusEffect,   // 특정 상태이상 보유
        OnHit,          // 피격시
        OnKill,         // 처치시
        TargetHPBelow,      // 대상 HP가 X% 이하
        TargetHPAbove,      // 대상 HP가 X% 이상
        MPBelow,            // MP가 X% 이하
        MPAbove,            // MP가 X% 이상
        BuffCount,          // 버프 개수
        DebuffCount,        // 디버프 개수
        EnemyCount,         // 적 수
        AllyCount,          // 아군 수
        TurnCount,          // 턴 수
        HasBuff,            // 버프 보유 여부
        HasDebuff,          // 디버프 보유 여부
        TurnNumber,          // 특정 턴 (홀수/짝수)
      
    }

    public enum BatchEditOperation
    {
        None,
        ChangeTier,
        AddEffect,
        RemoveEffect,
        ScaleValues,
        ChangeClass,
        ChangeCategory
    }

    public enum DotCalculationType
    {
        AttackBased,     // 시전자 공격력 기반
        MaxHpBased,      // 대상 최대 HP 기반
        CurrentHpBased,  // 대상 현재 HP 기반
        FixedDamage      // 고정 데미지
    }

    public enum ActionRestriction
    {
        None,            // 제한 없음
        FullRestriction, // 완전 행동 불가 (기절)
        SkillOnly,       // 스킬만 불가 (침묵)
        AttackOnly,      // 일반 공격만 불가
        MovementOnly,    // 이동만 불가
        Confused         // 혼란 (랜덤 행동)
    }

    /// <summary>
    /// Dispel 우선순위
    /// </summary>
    public enum DispelPriority
    {
        Random,      // 무작위
        Newest,      // 최신 효과부터
        Oldest,      // 오래된 효과부터
        Strongest,   // 강한 효과부터
        Weakest,     // 약한 효과부터
    }


}