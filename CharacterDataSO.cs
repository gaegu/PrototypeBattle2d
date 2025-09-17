using System;
using System.Collections.Generic;
using UnityEngine;
using SkillSystem;
using CharacterSystem;

namespace BattleCharacterSystem
{
    /// <summary>
    /// 캐릭터 데이터 ScriptableObject
    /// 기존 프로젝트의 enum들을 참조하여 호환성 유지
    /// </summary>
    [CreateAssetMenu(fileName = "NewCharacterData", menuName = "Battle Character System/Character Data", order = 1)]
    public class BattleCharacterDataSO : ScriptableObject
    {
        [Header("===== 기본 정보 =====")]
        [SerializeField] private int characterId = 0;
        [SerializeField] private string characterName = "New Character";
        [SerializeField] private string description = "";

        [Header("===== 분류 =====")]
        [SerializeField] private CharacterTier tier = CharacterTier.A;
        [SerializeField] private ClassType characterClass = ClassType.Slaughter;  // 기존 enum 사용
        [SerializeField] private EBattleElementType elementType = EBattleElementType.Power;  // 기존 enum 사용
        [SerializeField] private EAttackType attackType = EAttackType.Physical;  // 기존 enum 사용
        [SerializeField] private EAttackRange attackRange = EAttackRange.Melee;  // 기존 enum 사용

        [Header("===== 스탯 =====")]
        [SerializeField] private BaseStats baseStats;
        [SerializeField] private FixedStats fixedStats;
        [SerializeField] private bool useCustomStats = false; // 커스텀 스탯 사용 여부

        [Header("===== 스킬 =====")]
        [SerializeField] private int activeSkillId;  // 액티브 스킬 ID
        [SerializeField] private int passiveSkillId; // 패시브 스킬 ID

        [Header("===== 진형 =====")]
        [SerializeField] private FormationPreference formationPreference = FormationPreference.Flexible;

        [Header("===== 리소스 =====")]
        [SerializeField] private string prefabPath = "Character/";
        [SerializeField] private string characterResourceName = "";
        [SerializeField] private string addressableKey = "";

        [SerializeField] private bool hasCostume = false;
        [SerializeField] private List<string> costumeNames = new List<string>();

        [Header("===== 사운드/이펙트 =====")]
        [SerializeField] private string attackSound = "";
        [SerializeField] private string hitSound = "";
        [SerializeField] private string deathSound = "";
        [SerializeField] private string skillEffectPrefab = "";

        // ===== Properties =====
        public int CharacterId => characterId;

        public void SetCharacterId( int id )
        {
            characterId = id;
        }

        public string CharacterName => characterName;
        public string Description => description;
        public CharacterTier Tier => tier;
        public ClassType CharacterClass => characterClass;
        public EBattleElementType ElementType => elementType;
        public EAttackType AttackType => attackType;
        public BaseStats BaseStats => baseStats;
        public FixedStats FixedStats => fixedStats;
        public int ActiveSkillId => activeSkillId;
        public int PassiveSkillId => passiveSkillId;
        public FormationPreference FormationPreference => formationPreference;
        public string PrefabPath => prefabPath;
        public string CharacterResourceName => characterResourceName;

        public string AddressableKey => addressableKey;

        // ===== Methods =====

        /// <summary>
        /// 템플릿에서 초기화
        /// </summary>
        public void InitializeFromTemplate(CharacterTier tier, ClassType charClass)
        {
            this.tier = tier;
            this.characterClass = charClass;

            // 티어와 직업에 따른 기본 스탯 설정
            if (!useCustomStats)
            {
                baseStats = TierBaseStats.GetBaseStats(tier, charClass);
                fixedStats = FixedStats.GetDefault(tier, charClass);
            }

            // 진형 선호도 설정
            SetDefaultFormationPreference();

            // 리소스 경로 자동 설정
            UpdateResourcePath();
        }

        /// <summary>
        /// 직업별 기본 진형 선호도 설정
        /// </summary>
        private void SetDefaultFormationPreference()
        {
            switch (characterClass)
            {
                case ClassType.Vanguard:
                    formationPreference = FormationPreference.FrontOnly;
                    break;
                case ClassType.Slaughter:
                    formationPreference = FormationPreference.PreferBack; // 뒷열에서 공격력 버프
                    break;
                case ClassType.Jacker:
                    formationPreference = FormationPreference.PreferBack;
                    break;
                case ClassType.Rewinder:
                    formationPreference = FormationPreference.Flexible; // 상황에 따라
                    break;
            }
        }

        /// <summary>
        /// 리소스 경로 업데이트
        /// </summary>
        private void UpdateResourcePath()
        {
            if (!string.IsNullOrEmpty(characterName))
            {
                characterResourceName = $"{characterName}_Battle_Type01";
                prefabPath = $"Character/{characterName}/Prefabs/";
                addressableKey = $"Char_{characterResourceName}_prefab";
            }
        }

        /// <summary>
        /// 레벨별 스탯 가져오기
        /// </summary>
        public BaseStats GetStatsAtLevel(int level)
        {
            return baseStats.GetStatsAtLevel(level);
        }

        /// <summary>
        /// BattleCharInfoNew와 호환되는 데이터 변환
        /// </summary>
        public void ApplyToCharacterInfo(BattleCharInfoNew info)
        {
            // 기본 스탯 설정
            info.SetMaxHp(baseStats.hp);
            info.SetHp(baseStats.hp);
            info.SetAttack(baseStats.attack);
            info.SetDefence(baseStats.defense);

            // 고정 스탯 설정
            info.SetMaxBp(fixedStats.maxBP);
            info.SetTurnSpeed(fixedStats.turnSpeed);
            info.SetCriRate(fixedStats.critRate);
            info.SetCriDmg(fixedStats.critDamage);
            info.SetHitRate(fixedStats.hitRate);
            info.SetDodgeRate(fixedStats.dodgeRate);
            info.SetBlockRate(fixedStats.blockRate);
            info.SetAggro(fixedStats.aggro);
            info.SetSkillHitRate(fixedStats.skillHitRate);
            info.SetSkillResistRate(fixedStats.skillResist);

            // 기타 정보
            info.SetName(characterName);
            info.SetCharacterTier(tier);
            info.SetClassType(characterClass);
            info.SetElementType(elementType);
            info.SetAttackType(attackType);
        }

        /// <summary>
        /// CSV 내보내기용 문자열 생성
        /// </summary>
        public string ToCSV()
        {
            return $"{characterId},{characterName},{tier},{characterClass},{elementType}," +
                   $"{baseStats.hp},{baseStats.attack},{baseStats.defense}," +
                   $"{fixedStats.maxBP},{fixedStats.turnSpeed},{fixedStats.critRate},{fixedStats.critDamage}," +
                   $"{activeSkillId},{passiveSkillId},{formationPreference}";
        }

        /// <summary>
        /// CSV에서 데이터 파싱
        /// </summary>
        public static BattleCharacterDataSO FromCSV(string csvLine)
        {
            var data = CreateInstance<BattleCharacterDataSO>();
            var values = csvLine.Split(',');

            if (values.Length < 15) return null;

            data.characterId = int.Parse(values[0]);
            data.characterName = values[1];
            data.tier = (CharacterTier)Enum.Parse(typeof(CharacterTier), values[2]);
            data.characterClass = (ClassType)Enum.Parse(typeof(ClassType), values[3]);
            data.elementType = (EBattleElementType)Enum.Parse(typeof(EBattleElementType), values[4]);

            data.baseStats = new BaseStats
            {
                hp = int.Parse(values[5]),
                attack = int.Parse(values[6]),
                defense = int.Parse(values[7])
            };

            // 성장 계수 재계산
            data.baseStats.hpGrowth = data.baseStats.hp * 8f / 199f;
            data.baseStats.attackGrowth = data.baseStats.attack * 6f / 199f;
            data.baseStats.defenseGrowth = data.baseStats.defense * 6f / 199f;

            data.fixedStats = new FixedStats
            {
                maxBP = int.Parse(values[8]),
                turnSpeed = float.Parse(values[9]),
                critRate = float.Parse(values[10]),
                critDamage = float.Parse(values[11])
            };

            data.activeSkillId = int.Parse(values[12]);
            data.passiveSkillId = int.Parse(values[13]);
            data.formationPreference = (FormationPreference)Enum.Parse(typeof(FormationPreference), values[14]);

            data.UpdateResourcePath();

            return data;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 에디터에서 값 변경 시 호출
        /// </summary>
        private void OnValidate()
        {
            // ID 범위 체크
            if (characterId < 1000 || characterId >= 2000)
            {
                characterId = Mathf.Clamp(characterId, 1000, 1999);
            }

            // 커스텀 스탯 사용하지 않을 때 자동 계산
            if (!useCustomStats)
            {
                baseStats = TierBaseStats.GetBaseStats(tier, characterClass);
                fixedStats = FixedStats.GetDefault(tier, characterClass);
            }

            UpdateResourcePath();
        }
#endif
    }
}