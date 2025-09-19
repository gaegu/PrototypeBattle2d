using System;
using System.Collections.Generic;
using UnityEngine;
using BattleCharacterSystem;
using CharacterSystem;
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
        monsterTemplates.Add(new MonsterTemplate("Normal_Aggressive", BattleCharacterSystem.MonsterType.Normal, MonsterPattern.Aggressive)
        {
            description = "기본 공격형 몬스터"
        });

        monsterTemplates.Add(new MonsterTemplate("Normal_Defensive", BattleCharacterSystem.MonsterType.Normal, MonsterPattern.Defensive)
        {
            description = "기본 방어형 몬스터"
        });

        monsterTemplates.Add(new MonsterTemplate("Elite_Balanced", BattleCharacterSystem.MonsterType.Elite, MonsterPattern.Balanced)
        {
            description = "강화된 균형형 엘리트"
        });

        monsterTemplates.Add(new MonsterTemplate("Boss_Special", BattleCharacterSystem.MonsterType.Boss, MonsterPattern.Special)
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