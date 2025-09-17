using System;
using System.Collections.Generic;
using UnityEngine;
using SkillSystem;
using CharacterSystem;
using UnityEditor;

namespace BattleCharacterSystem
{
    /// <summary>
    /// 몬스터 데이터 ScriptableObject
    /// </summary>
    [CreateAssetMenu(fileName = "NewMonsterData", menuName = "Battle Character System/Monster Data", order = 2)]
    public class BattleMonsterDataSO : ScriptableObject
    {
        [Header("===== 기본 정보 =====")]
        [SerializeField] private int monsterId = 2000;
        [SerializeField] private string monsterName = "New Monster";
        [SerializeField] private string description = "";

        [Header("===== 분류 =====")]
        [SerializeField] private MonsterType monsterType = MonsterType.Normal;
        [SerializeField] private MonsterPattern behaviorPattern = MonsterPattern.Aggressive;
        [SerializeField] private EBattleElementType elementType = EBattleElementType.Power;
        [SerializeField] private EAttackType attackType = EAttackType.Physical;

        [Header("===== 몬스터 배율 =====")]
        [SerializeField][Range(0.5f, 5f)] private float statMultiplier = 1.2f; // 기본 몬스터 배율
        [SerializeField] private bool isBoss = false;
        [SerializeField][Range(1f, 10f)] private float bossMultiplier = 2.0f; // 보스 추가 배율

        [Header("===== 기본 스탯 (레벨 1 기준) =====")]
        [SerializeField] private BaseStats baseStats;
        [SerializeField] private FixedStats fixedStats;

        [Header("===== 면역 타입 (몬스터 전용) =====")]
        [SerializeField] private ImmunityTypeExt immunityType = ImmunityTypeExt.None;

        [Header("===== 수호석 시스템 =====")]
        [SerializeField] private bool hasGuardianStone = false;
        [SerializeField] private EBattleElementType[] guardianStoneElements = new EBattleElementType[0];

        [Header("===== 스킬 =====")]
        [SerializeField] private List<int> skillIds = new List<int>();

        [Header("===== 보스 페이즈 (보스 전용) =====")]
        [SerializeField] private List<BossPhaseData> bossPhases = new List<BossPhaseData>();

        [Header("===== 리소스 =====")]
        // 추가: 리소스 모드 선택
        [SerializeField] private bool useExistingCharacter = false;  // 기존 캐릭터 사용 여부
        [SerializeField] private int baseCharacterId = 0;            // 기존 캐릭터 ID (1000-1999 범위)

        // 기존 필드 유지 (신규 몬스터용)
        [SerializeField] private string prefabPath = "Monster/";
        [SerializeField] private string monsterResourceName = "";

        [SerializeField] private string addressableKey = "";

        [Header("===== 사운드/이펙트 =====")]
        [SerializeField] private string attackSound = "";
        [SerializeField] private string hitSound = "";
        [SerializeField] private string deathSound = "";
        [SerializeField] private string skillEffectPrefab = "";

        // ===== Properties =====
        public int MonsterId => monsterId;
        public string MonsterName => monsterName;
        public string Description => description;
        public MonsterType MonsterType => monsterType;
        public MonsterPattern BehaviorPattern => behaviorPattern;
        public EBattleElementType ElementType => elementType;
        public EAttackType AttackType => attackType;
        public float StatMultiplier => statMultiplier;
        public bool IsBoss => isBoss;
        public float BossMultiplier => bossMultiplier;
        public BaseStats BaseStats => baseStats;
        public FixedStats FixedStats => fixedStats;
        public ImmunityTypeExt ImmunityType => immunityType;
        public bool HasGuardianStone => hasGuardianStone;
        public EBattleElementType[] GuardianStoneElements => guardianStoneElements;
        public List<int> SkillIds => skillIds;
        public List<BossPhaseData> BossPhases => bossPhases;
        public string PrefabPath => prefabPath;
        public string MonsterResourceName => monsterResourceName;
        public string AddressableKey => addressableKey;
        public bool UseExistingCharacter => useExistingCharacter;
        public int BaseCharacterId => baseCharacterId;
        // ===== Methods =====

        /// <summary>
        /// 몬스터 타입에 따른 초기화
        /// </summary>
        public void InitializeFromType(MonsterType type, MonsterPattern pattern = MonsterPattern.Balanced)
        {
            this.monsterType = type;
            this.behaviorPattern = pattern;

            // 타입별 기본 스탯 설정
            SetDefaultStatsByType(type);

            // 타입별 배율 설정
            switch (type)
            {
                case MonsterType.Normal:
                    statMultiplier = 1.0f;
                    isBoss = false;
                    SetGuardianStones(0);
                    break;

                case MonsterType.Elite:
                    statMultiplier = 1.5f;
                    isBoss = false;
                    SetGuardianStones(2);
                    break;

                case MonsterType.MiniBoss:
                    statMultiplier = 2.0f;
                    isBoss = false;
                    SetGuardianStones(3);
                    break;

                case MonsterType.Boss:
                    statMultiplier = 1.0f; // 보스는 bossMultiplier 사용
                    isBoss = true;
                    bossMultiplier = 3.0f;
                    SetGuardianStones(4);
                    InitializeBossPhases();
                    break;
            }

            UpdateResourcePath();
        }

        /// <summary>
        /// 타입별 기본 스탯 설정
        /// </summary>
        private void SetDefaultStatsByType(MonsterType type)
        {
            switch (type)
            {
                case MonsterType.Normal:
                    baseStats = new BaseStats
                    {
                        hp = 1500,
                        attack = 150,
                        defense = 75,
                        hpGrowth = 60f,
                        attackGrowth = 6f,
                        defenseGrowth = 3f
                    };
                    fixedStats = new FixedStats
                    {
                        maxBP = 1,
                        turnSpeed = 90f,
                        critRate = 5f,
                        critDamage = 150f,
                        hitRate = 95f,
                        dodgeRate = 5f,
                        blockRate = 10f,
                        aggro = 100
                    };
                    break;

                case MonsterType.Elite:
                    baseStats = new BaseStats
                    {
                        hp = 3000,
                        attack = 250,
                        defense = 150,
                        hpGrowth = 120f,
                        attackGrowth = 10f,
                        defenseGrowth = 6f
                    };
                    fixedStats = new FixedStats
                    {
                        maxBP = 2,
                        turnSpeed = 95f,
                        critRate = 10f,
                        critDamage = 160f,
                        hitRate = 98f,
                        dodgeRate = 8f,
                        blockRate = 15f,
                        aggro = 200
                    };
                    break;

                case MonsterType.MiniBoss:
                    baseStats = new BaseStats
                    {
                        hp = 8000,
                        attack = 400,
                        defense = 300,
                        hpGrowth = 320f,
                        attackGrowth = 16f,
                        defenseGrowth = 12f
                    };
                    fixedStats = new FixedStats
                    {
                        maxBP = 3,
                        turnSpeed = 100f,
                        critRate = 15f,
                        critDamage = 170f,
                        hitRate = 100f,
                        dodgeRate = 10f,
                        blockRate = 20f,
                        aggro = 300
                    };
                    break;

                case MonsterType.Boss:
                    baseStats = new BaseStats
                    {
                        hp = 20000,
                        attack = 600,
                        defense = 400,
                        hpGrowth = 800f,
                        attackGrowth = 24f,
                        defenseGrowth = 16f
                    };
                    fixedStats = new FixedStats
                    {
                        maxBP = 5,
                        turnSpeed = 110f,
                        critRate = 20f,
                        critDamage = 180f,
                        hitRate = 100f,
                        dodgeRate = 15f,
                        blockRate = 25f,
                        aggro = 500
                    };
                    break;
            }
        }

        /// <summary>
        /// 수호석 설정
        /// </summary>
        public void SetGuardianStones(int count)
        {
            hasGuardianStone = count > 0;

            if (count == 0)
            {
                guardianStoneElements = new EBattleElementType[0];
                return;
            }

            // 기본 속성 배열로 균등 분배
            var elements = new EBattleElementType[]
            {
                EBattleElementType.Power,
                EBattleElementType.Plasma,
                EBattleElementType.Chemical,
                EBattleElementType.Bio
            };

            guardianStoneElements = new EBattleElementType[count];
            for (int i = 0; i < count; i++)
            {
                guardianStoneElements[i] = elements[i % elements.Length];
            }
        }

        /// <summary>
        /// 보스 페이즈 초기화
        /// </summary>
        private void InitializeBossPhases()
        {
            bossPhases.Clear();

            // 기본 5페이즈 설정
            bossPhases.Add(new BossPhaseData
            {
                phaseName = "Phase 1 - Opening",
                hpTriggerPercent = 100f,
                attackMultiplier = 1.0f,
                defenseMultiplier = 1.0f,
                speedMultiplier = 1.0f
            });

            bossPhases.Add(new BossPhaseData
            {
                phaseName = "Phase 2 - Awakening",
                hpTriggerPercent = 80f,
                attackMultiplier = 1.2f,
                defenseMultiplier = 1.0f,
                speedMultiplier = 1.1f
            });

            bossPhases.Add(new BossPhaseData
            {
                phaseName = "Phase 3 - Rage",
                hpTriggerPercent = 60f,
                attackMultiplier = 1.5f,
                defenseMultiplier = 0.9f,
                speedMultiplier = 1.2f
            });

            bossPhases.Add(new BossPhaseData
            {
                phaseName = "Phase 4 - Desperate",
                hpTriggerPercent = 40f,
                attackMultiplier = 2.0f,
                defenseMultiplier = 0.8f,
                speedMultiplier = 1.3f
            });

            bossPhases.Add(new BossPhaseData
            {
                phaseName = "Phase 5 - Last Stand",
                hpTriggerPercent = 20f,
                attackMultiplier = 2.5f,
                defenseMultiplier = 0.7f,
                speedMultiplier = 1.5f
            });
        }

        /// <summary>
        /// 리소스 경로 업데이트
        /// </summary>
        private void UpdateResourcePath()
        {
            if (!string.IsNullOrEmpty(monsterName))
            {
                monsterResourceName = $"{monsterName}_Battle_Type01";
                prefabPath = $"Character/{monsterName}/Prefabs/";
                addressableKey = $"Char_{monsterResourceName}_prefab";
            }
        }

        /// <summary>
        /// 최종 스탯 계산 (배율 적용)
        /// </summary>
        public BaseStats GetFinalStats(int level = 1)
        {
            var stats = baseStats.GetStatsAtLevel(level);
            float finalMultiplier = isBoss ? bossMultiplier : statMultiplier;

            stats.hp = Mathf.RoundToInt(stats.hp * finalMultiplier);
            stats.attack = Mathf.RoundToInt(stats.attack * finalMultiplier);
            stats.defense = Mathf.RoundToInt(stats.defense * finalMultiplier);

            return stats;
        }

        /// <summary>
        /// 특정 페이즈의 스탯 가져오기
        /// </summary>
        public BaseStats GetPhaseStats(int phaseIndex, int level = 1)
        {
            if (!isBoss || phaseIndex < 0 || phaseIndex >= bossPhases.Count)
            {
                return GetFinalStats(level);
            }

            var stats = GetFinalStats(level);
            var phase = bossPhases[phaseIndex];

            stats.attack = Mathf.RoundToInt(stats.attack * phase.attackMultiplier);
            stats.defense = Mathf.RoundToInt(stats.defense * phase.defenseMultiplier);

            return stats;
        }

        /// <summary>
        /// BattleCharInfoNew와 호환되는 데이터 변환
        /// </summary>
        public void ApplyToCharacterInfo(BattleCharInfoNew info)
        {
            // 기본 정보
            info.SetName(monsterName);
            info.SetElementType(elementType);
            info.SetAttackType(attackType);

            // 최종 스탯 적용 (배율 포함)
            var finalStats = GetFinalStats(1);
            info.SetMaxHp(finalStats.hp);
            info.SetHp(finalStats.hp);
            info.SetAttack(finalStats.attack);
            info.SetDefence(finalStats.defense);

            // 고정 스탯
            info.SetMaxBp(fixedStats.maxBP);
            info.SetTurnSpeed(fixedStats.turnSpeed);
            info.SetCriRate(fixedStats.critRate);
            info.SetCriDmg(fixedStats.critDamage);
            info.SetHitRate(fixedStats.hitRate);
            info.SetDodgeRate(fixedStats.dodgeRate);
            info.SetBlockRate(fixedStats.blockRate);
            info.SetAggro(fixedStats.aggro);

            // 몬스터 전용 설정
            info.SetIsMonster(true);
            info.SetIsBoss(isBoss);

            // 수호석 설정
            if (hasGuardianStone && guardianStoneElements.Length > 0)
            {
                info.GuardianStoneElement = guardianStoneElements;
            }
        }

        /// <summary>
        /// CSV 내보내기용 문자열 생성
        /// </summary>
        public string ToCSV()
        {
            return $"{monsterId},{monsterName},{monsterType},{behaviorPattern},{elementType}," +
                   $"{baseStats.hp},{baseStats.attack},{baseStats.defense}," +
                   $"{statMultiplier},{isBoss},{bossMultiplier}," +
                   $"{immunityType},{guardianStoneElements.Length}," +
                   $"{string.Join(";", skillIds)}";
        }


        // 메서드 추가
        public string GetActualPrefabPath()
        {
            if (useExistingCharacter && baseCharacterId > 0)
            {
                // 기존 캐릭터 경로 패턴 사용
                return $"Character/{GetCharacterNameById(baseCharacterId)}/Prefabs/";
            }
            return prefabPath;
        }

        public string GetActualResourceName()
        {
            if (useExistingCharacter && baseCharacterId > 0)
            {
                return $"{GetCharacterNameById(baseCharacterId)}_Battle_Type01";
            }
            return monsterResourceName;
        }

        public string GetActualResourceRootName()
        {
            if (useExistingCharacter && baseCharacterId > 0)
            {
                return $"{GetCharacterNameById(baseCharacterId)}";
            }
            return monsterName;
        }


        public string GetActualAddressableKey()
        {
            if (useExistingCharacter && baseCharacterId > 0)
            {
                return $"Char_{GetCharacterNameById(baseCharacterId)}_Battle_Type01_prefab";
            }
            return addressableKey;
        }

        private static Dictionary<int, string> characterNameCache = new Dictionary<int, string>();


        public static string GetCharacterNameById(int characterId)
        {
            if (characterNameCache.ContainsKey(characterId))
                return characterNameCache[characterId];

#if UNITY_EDITOR
            // 에디터에서는 실제 데이터 검색
            string[] guids = AssetDatabase.FindAssets("t:BattleCharacterDataSO");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var charData = AssetDatabase.LoadAssetAtPath<BattleCharacterDataSO>(path);
                if (charData != null && charData.CharacterId == characterId)
                {
                    characterNameCache[characterId] = charData.CharacterName;
                    return charData.CharacterName;
                }
            }
#endif

            return "Unknown";
        }

        public static void ClearCache()
        {
            characterNameCache.Clear();
        }


#if UNITY_EDITOR
        /// <summary>
        /// 에디터에서 값 변경 시 호출
        /// </summary>
        private void OnValidate()
        {
            // ID 범위 체크
            if (isBoss && (monsterId < 3000 || monsterId >= 4000))
            {
                monsterId = Mathf.Clamp(monsterId, 3000, 3999);
            }
            else if (!isBoss && (monsterId < 2000 || monsterId >= 3000))
            {
                monsterId = Mathf.Clamp(monsterId, 2000, 2999);
            }

            UpdateResourcePath();
        }


#endif
    }

    /// <summary>
    /// 보스 페이즈 데이터
    /// </summary>
    [Serializable]
    public class BossPhaseData
    {
        [Header("페이즈 기본 정보")]
        public string phaseName = "Phase 1";
        [Range(0, 100)] public float hpTriggerPercent = 100f;

        [Header("페이즈별 스탯 배율")]
        [Range(0.5f, 3f)] public float attackMultiplier = 1f;
        [Range(0.5f, 3f)] public float defenseMultiplier = 1f;
        [Range(0.5f, 2f)] public float speedMultiplier = 1f;

        [Header("페이즈별 스킬")]
        public List<int> phaseSkillIds = new List<int>();

        [Header("페이즈별 수호석")]
        public bool hasPhaseGuardianStone = false;
        public EBattleElementType[] phaseGuardianStones = new EBattleElementType[0];

        [Header("페이즈별 리소스 (선택)")]
        public bool useCustomPrefab = false;
        public string phasePrefabName = "";
    }
}