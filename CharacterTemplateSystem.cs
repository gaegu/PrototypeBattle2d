using System;
using System.Collections.Generic;
using UnityEngine;
using BattleCharacterSystem;
using CharacterSystem;

namespace BattleCharacterSystem
{
    /// <summary>
    /// 캐릭터 템플릿 데이터
    /// </summary>
    [Serializable]
    public class CharacterTemplate
    {
        public string templateName;
        public CharacterTier tier;
        public ClassType characterClass;
        public EBattleElementType preferredElement;
        public string description;

        // 기본 스탯 프리셋
        public BaseStats baseStatsPreset;
        public FixedStats fixedStatsPreset;

        // 추천 스킬
        public List<int> recommendedActiveSkills = new List<int>();
        public List<int> recommendedPassiveSkills = new List<int>();

        // 리소스 템플릿
        public string prefabTemplate;
        public string animationSetTemplate;

        public CharacterTemplate(string name, CharacterTier tier, ClassType charClass)
        {
            this.templateName = name;
            this.tier = tier;
            this.characterClass = charClass;

            // 기본값 설정
            baseStatsPreset = TierBaseStats.GetBaseStats(tier, charClass);
            fixedStatsPreset = FixedStats.GetDefault(tier, charClass);
        }
    }

    /// <summary>
    /// 몬스터 템플릿 데이터
    /// </summary>
    [Serializable]
    public class MonsterTemplate
    {
        public string templateName;
        public MonsterType monsterType;
        public MonsterPattern pattern;
        public string description;

        // 기본 스탯 범위
        public int minHp;
        public int maxHp;
        public int minAttack;
        public int maxAttack;
        public int minDefense;
        public int maxDefense;

        // 수호석 프리셋
        public int guardianStoneCount;
        public List<EBattleElementType> defaultStoneElements;

        // AI 패턴
        public List<string> behaviorTags;

        public MonsterTemplate(string name, MonsterType type, MonsterPattern pattern)
        {
            this.templateName = name;
            this.monsterType = type;
            this.pattern = pattern;

            // 타입별 기본값 설정
            SetDefaultStatsByType(type);
        }

        private void SetDefaultStatsByType(MonsterType type)
        {
            switch (type)
            {
                case MonsterType.Normal:
                    minHp = 1000; maxHp = 2000;
                    minAttack = 100; maxAttack = 200;
                    minDefense = 50; maxDefense = 100;
                    guardianStoneCount = 0;
                    break;

                case MonsterType.Elite:
                    minHp = 3000; maxHp = 5000;
                    minAttack = 250; maxAttack = 400;
                    minDefense = 150; maxDefense = 250;
                    guardianStoneCount = 2;
                    break;

                case MonsterType.MiniBoss:
                    minHp = 8000; maxHp = 12000;
                    minAttack = 400; maxAttack = 600;
                    minDefense = 300; maxDefense = 400;
                    guardianStoneCount = 3;
                    break;

                case MonsterType.Boss:
                    minHp = 20000; maxHp = 50000;
                    minAttack = 600; maxAttack = 1000;
                    minDefense = 400; maxDefense = 600;
                    guardianStoneCount = 4;
                    break;
            }
        }
    }

    /// <summary>
    /// 몬스터 그룹 템플릿
    /// </summary>
    [Serializable]
    public class MonsterGroupTemplate
    {
        public string templateName;
        public string description;
        public string purpose; // "Tutorial", "Normal", "Boss", "Special"

        [Serializable]
        public class MonsterSlot
        {
            public int slotIndex; // 0-4
            public MonsterType preferredType;
            public MonsterPattern preferredPattern;
            public bool isEmpty;

            public MonsterSlot(int index)
            {
                this.slotIndex = index;
                this.isEmpty = true;
            }
        }

        public MonsterSlot[] slots = new MonsterSlot[5];
        public FormationType formationType = FormationType.DefensiveBalance;

        public MonsterGroupTemplate(string name)
        {
            this.templateName = name;
            for (int i = 0; i < 5; i++)
            {
                slots[i] = new MonsterSlot(i);
            }
        }

        /// <summary>
        /// 프리셋 템플릿 생성
        /// </summary>
        public static MonsterGroupTemplate CreatePreset(string presetType)
        {
            var template = new MonsterGroupTemplate(presetType);

            switch (presetType)
            {
                case "Balanced":
                    // 탱커 1, 딜러 2, 서포터 1 (슬롯 0은 비움)
                    template.slots[0].isEmpty = true;
                    template.slots[1] = new MonsterSlot(1) { preferredType = MonsterType.Elite, preferredPattern = MonsterPattern.Defensive, isEmpty = false };
                    template.slots[2] = new MonsterSlot(2) { preferredType = MonsterType.Normal, preferredPattern = MonsterPattern.Aggressive, isEmpty = false };
                    template.slots[3] = new MonsterSlot(3) { preferredType = MonsterType.Normal, preferredPattern = MonsterPattern.Aggressive, isEmpty = false };
                    template.slots[4] = new MonsterSlot(4) { preferredType = MonsterType.Normal, preferredPattern = MonsterPattern.Support, isEmpty = false };
                    template.formationType = FormationType.DefensiveBalance;
                    template.description = "균형잡힌 기본 구성";
                    break;

                case "Aggressive":
                    // 올 딜러 구성
                    template.slots[0] = new MonsterSlot(0) { preferredType = MonsterType.Normal, preferredPattern = MonsterPattern.Aggressive, isEmpty = false };
                    template.slots[1] = new MonsterSlot(1) { preferredType = MonsterType.Elite, preferredPattern = MonsterPattern.Aggressive, isEmpty = false };
                    template.slots[2] = new MonsterSlot(2) { preferredType = MonsterType.Normal, preferredPattern = MonsterPattern.Aggressive, isEmpty = false };
                    template.slots[3] = new MonsterSlot(3) { preferredType = MonsterType.Normal, preferredPattern = MonsterPattern.Aggressive, isEmpty = false };
                    template.slots[4] = new MonsterSlot(4) { preferredType = MonsterType.Normal, preferredPattern = MonsterPattern.Aggressive, isEmpty = false };
                    template.formationType = FormationType.Offensive;
                    template.description = "고화력 공격 구성";
                    break;

                case "Defensive":
                    // 방어 중심 구성
                    template.slots[0] = new MonsterSlot(0) { preferredType = MonsterType.Elite, preferredPattern = MonsterPattern.Defensive, isEmpty = false };
                    template.slots[1] = new MonsterSlot(1) { preferredType = MonsterType.Elite, preferredPattern = MonsterPattern.Defensive, isEmpty = false };
                    template.slots[2] = new MonsterSlot(2) { preferredType = MonsterType.Normal, preferredPattern = MonsterPattern.Support, isEmpty = false };
                    template.slots[3] = new MonsterSlot(3) { preferredType = MonsterType.Normal, preferredPattern = MonsterPattern.Support, isEmpty = false };
                    template.slots[4] = new MonsterSlot(4) { preferredType = MonsterType.Normal, preferredPattern = MonsterPattern.Defensive, isEmpty = false };
                    template.formationType = FormationType.Defensive;
                    template.description = "높은 생존력 구성";
                    break;

                case "Boss":
                    // 보스전 구성
                    template.slots[0].isEmpty = true;
                    template.slots[1].isEmpty = true;
                    template.slots[2] = new MonsterSlot(2) { preferredType = MonsterType.Boss, preferredPattern = MonsterPattern.Special, isEmpty = false };
                    template.slots[3] = new MonsterSlot(3) { preferredType = MonsterType.Elite, preferredPattern = MonsterPattern.Support, isEmpty = false };
                    template.slots[4] = new MonsterSlot(4) { preferredType = MonsterType.Elite, preferredPattern = MonsterPattern.Support, isEmpty = false };
                    template.formationType = FormationType.Defensive;
                    template.description = "보스 중심 구성";
                    break;

                case "Tutorial":
                    // 튜토리얼용 약한 구성
                    template.slots[0].isEmpty = true;
                    template.slots[1].isEmpty = true;
                    template.slots[2] = new MonsterSlot(2) { preferredType = MonsterType.Normal, preferredPattern = MonsterPattern.Balanced, isEmpty = false };
                    template.slots[3].isEmpty = true;
                    template.slots[4].isEmpty = true;
                    template.formationType = FormationType.Offensive;
                    template.description = "튜토리얼용 간단한 구성";
                    break;
            }

            template.purpose = presetType;
            return template;
        }
    }

    /// <summary>
    /// 템플릿 관리자
    /// </summary>
    [CreateAssetMenu(fileName = "TemplateDatabase", menuName = "Battle Character System/Template Database")]
    public class TemplateDatabase : ScriptableObject
    {
        [Header("Character Templates")]
        public List<CharacterTemplate> characterTemplates = new List<CharacterTemplate>();

        [Header("Monster Templates")]
        public List<MonsterTemplate> monsterTemplates = new List<MonsterTemplate>();

        [Header("Monster Group Templates")]
        public List<MonsterGroupTemplate> groupTemplates = new List<MonsterGroupTemplate>();

        /// <summary>
        /// 기본 템플릿 생성
        /// </summary>
        public void CreateDefaultTemplates()
        {
            // 캐릭터 템플릿 - 티어별 x 직업별
            characterTemplates.Clear();

            var tiers = new[] { CharacterTier.A, CharacterTier.S, CharacterTier.X, CharacterTier.XA };
            var classes = new[] { ClassType.Slaughter, ClassType.Vanguard, ClassType.Jacker, ClassType.Rewinder };

            foreach (var tier in tiers)
            {
                foreach (var charClass in classes)
                {
                    var template = new CharacterTemplate($"{tier}_{charClass}", tier, charClass);

                    // 직업별 설명 추가
                    switch (charClass)
                    {
                        case ClassType.Slaughter:
                            template.description = "높은 공격력의 딜러 캐릭터";
                            template.preferredElement = EBattleElementType.Power;
                            break;
                        case ClassType.Vanguard:
                            template.description = "높은 방어력의 탱커 캐릭터";
                            template.preferredElement = EBattleElementType.Power;
                            break;
                        case ClassType.Jacker:
                            template.description = "버프/디버프 특화 서포터";
                            template.preferredElement = EBattleElementType.Network;
                            break;
                        case ClassType.Rewinder:
                            template.description = "힐/유틸리티 특화 서포터";
                            template.preferredElement = EBattleElementType.Bio;
                            break;
                    }

                    characterTemplates.Add(template);
                }
            }

            // 몬스터 템플릿
            monsterTemplates.Clear();

            // 일반 몬스터 템플릿
            monsterTemplates.Add(new MonsterTemplate("Normal_Aggressive", MonsterType.Normal, MonsterPattern.Aggressive)
            {
                description = "기본 공격형 몬스터"
            });

            monsterTemplates.Add(new MonsterTemplate("Normal_Defensive", MonsterType.Normal, MonsterPattern.Defensive)
            {
                description = "기본 방어형 몬스터"
            });

            monsterTemplates.Add(new MonsterTemplate("Elite_Balanced", MonsterType.Elite, MonsterPattern.Balanced)
            {
                description = "강화된 균형형 엘리트"
            });

            monsterTemplates.Add(new MonsterTemplate("Boss_Special", MonsterType.Boss, MonsterPattern.Special)
            {
                description = "특수 패턴 보스"
            });

            // 몬스터 그룹 템플릿
            groupTemplates.Clear();
            groupTemplates.Add(MonsterGroupTemplate.CreatePreset("Balanced"));
            groupTemplates.Add(MonsterGroupTemplate.CreatePreset("Aggressive"));
            groupTemplates.Add(MonsterGroupTemplate.CreatePreset("Defensive"));
            groupTemplates.Add(MonsterGroupTemplate.CreatePreset("Boss"));
            groupTemplates.Add(MonsterGroupTemplate.CreatePreset("Tutorial"));

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssets();
#endif
        }

        /// <summary>
        /// 템플릿으로부터 캐릭터 생성
        /// </summary>
        public BattleCharacterDataSO CreateCharacterFromTemplate(CharacterTemplate template, string characterName)
        {
            var newCharacter = ScriptableObject.CreateInstance<BattleCharacterDataSO>();
            newCharacter.InitializeFromTemplate(template.tier, template.characterClass);
            // 추가 설정은 InitializeFromTemplate에서 처리

            return newCharacter;
        }
    }
}