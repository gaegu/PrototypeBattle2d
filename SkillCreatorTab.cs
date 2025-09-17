// =====================================================
// SkillCreatorTab.cs
// 스킬 제작 탭 부분 - Editor 폴더에 저장
// =====================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR

namespace SkillSystem.Editor
{
    public partial class AdvancedSkillEditorWindow
    {
        // =====================================================
        // 스킬 제작 탭
        // =====================================================

        private void DrawSkillCreatorTab()
        {
            if (currentSkill == null)
            {
                currentSkill = new AdvancedSkillData();
            }

            EditorGUILayout.BeginHorizontal();

            // 왼쪽: 스킬 편집
            EditorGUILayout.BeginVertical(GUILayout.Width(500));
            DrawAdvancedSkillEditor();
            EditorGUILayout.EndVertical();

            // 중앙: 효과 편집
            EditorGUILayout.BeginVertical();
            DrawEffectEditor();
            EditorGUILayout.EndVertical();

            // 오른쪽: 실시간 미리보기
            EditorGUILayout.BeginVertical(GUILayout.Width(350));
            DrawEnhancedPreview();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawAdvancedSkillEditor()
        {
            EditorGUILayout.LabelField("⚙️ 스킬 설정", EditorStyles.boldLabel);

            // 기본 정보
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("기본 정보", EditorStyles.boldLabel);

            // ID 필드 수정 - 읽기 전용으로 표시
            EditorGUI.BeginDisabledGroup(true);
            currentSkill.skillId = EditorGUILayout.IntField("ID (자동)", currentSkill.skillId);
            EditorGUI.EndDisabledGroup();

            // ID 재생성 버튼 추가
            if (GUILayout.Button("ID 재생성", GUILayout.Width(80)))
            {
                currentSkill.skillId = database.GetNextId(currentSkill.category);
            }

            currentSkill.skillName = EditorGUILayout.TextField("이름", currentSkill.skillName);
            currentSkill.description = EditorGUILayout.TextArea(currentSkill.description, GUILayout.Height(40));

            // 카테고리 변경시 ID도 변경
            var prevCategory = currentSkill.category;
            currentSkill.category = (SkillCategory)EditorGUILayout.EnumPopup("카테고리", currentSkill.category);

            if (prevCategory != currentSkill.category && currentSkill.skillId == 0)
            {
                // 카테고리 변경시 새 ID 할당
                currentSkill.skillId = database.GetNextId(currentSkill.category);
            }
            currentSkill.characterClass = (CharacterClass)EditorGUILayout.EnumPopup("클래스", currentSkill.characterClass);
            currentSkill.tier = EditorGUILayout.IntSlider("티어", currentSkill.tier, 0, 5);

            EditorGUILayout.EndVertical();

            // 코스트 설정
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("코스트", EditorStyles.boldLabel);

            currentSkill.cooldown = EditorGUILayout.IntSlider("쿨다운", currentSkill.cooldown, 0, 10);

            if (currentSkill.category == SkillCategory.SpecialActive ||
                currentSkill.category == SkillCategory.SpecialPassive)
            {
                currentSkill.specialGauge = EditorGUILayout.IntSlider("스페셜 게이지", currentSkill.specialGauge, 0, 100);
            }

            EditorGUILayout.EndVertical();

            // 트리거 설정 (패시브용)
            if (currentSkill.category == SkillCategory.Passive ||
                currentSkill.category == SkillCategory.SpecialPassive)
            {
                EditorGUILayout.Space();
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("트리거", EditorStyles.boldLabel);

                currentSkill.triggerType = (TriggerType)EditorGUILayout.EnumPopup("발동 조건", currentSkill.triggerType);

                if (currentSkill.triggerType != TriggerType.Always)
                {
                    currentSkill.triggerChance = EditorGUILayout.Slider("발동 확률", currentSkill.triggerChance, 0f, 1f);
                    currentSkill.triggerCondition = EditorGUILayout.TextField("추가 조건", currentSkill.triggerCondition);
                }

                EditorGUILayout.EndVertical();
            }

            // 비주얼 설정
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("비주얼", EditorStyles.boldLabel);

            currentSkill.icon = (Sprite)EditorGUILayout.ObjectField("아이콘", currentSkill.icon, typeof(Sprite), false);
            currentSkill.effectPrefab = (GameObject)EditorGUILayout.ObjectField("이펙트", currentSkill.effectPrefab, typeof(GameObject), false);
            currentSkill.soundEffect = (AudioClip)EditorGUILayout.ObjectField("사운드", currentSkill.soundEffect, typeof(AudioClip), false);

            EditorGUILayout.EndVertical();

            // 태그
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("태그", EditorStyles.boldLabel);
            currentSkill.tags = EditorGUILayout.TextField("태그 (쉼표로 구분)", currentSkill.tags);
            EditorGUILayout.EndVertical();

            // BP 관련 필드 추가
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("BP 업그레이드 정보", EditorStyles.boldLabel);

            currentSkill.bpLevel = EditorGUILayout.IntField("BP 레벨", currentSkill.bpLevel);
            currentSkill.parentSkillId = EditorGUILayout.IntField("부모 스킬 ID", currentSkill.parentSkillId);

            if (currentSkill.bpLevel > 0)
            {
                currentSkill.bpUpgradeName = EditorGUILayout.TextField("업그레이드 이름",
                    currentSkill.bpUpgradeName);
            }

            // BP 업그레이드 경로 빠른 생성
            if (currentSkill.bpLevel == 0 && GUILayout.Button("BP 업그레이드 생성"))
            {
                CreateBPUpgradesForSkill(currentSkill);
            }

            EditorGUILayout.EndVertical();

        }

        private void CreateBPUpgradesForSkill(AdvancedSkillData baseSkill)
        {
            // BP 1-2 버전 생성
            var bp1Skill = baseSkill.Clone();
            bp1Skill.skillId = baseSkill.skillId + 1000;
            bp1Skill.skillName = $"{baseSkill.skillName} +";
            bp1Skill.bpLevel = 1;
            bp1Skill.parentSkillId = baseSkill.skillId;
            bp1Skill.bpUpgradeName = "강화";

            // 효과 25% 증가
            foreach (var effect in bp1Skill.effects)
            {
                effect.value *= 1.25f;
            }

            // BP 3-4 버전 생성  
            var bp3Skill = baseSkill.Clone();
            bp3Skill.skillId = baseSkill.skillId + 2000;
            bp3Skill.skillName = $"{baseSkill.skillName} ++";
            bp3Skill.bpLevel = 3;
            bp3Skill.parentSkillId = baseSkill.skillId;
            bp3Skill.bpUpgradeName = "대폭 강화";

            // 효과 50% 증가
            foreach (var effect in bp3Skill.effects)
            {
                effect.value *= 1.5f;
            }

            // BP 5 버전 생성
            var bp5Skill = baseSkill.Clone();
            bp5Skill.skillId = baseSkill.skillId + 3000;
            bp5Skill.skillName = $"{baseSkill.skillName} MAX";
            bp5Skill.bpLevel = 5;
            bp5Skill.parentSkillId = baseSkill.skillId;
            bp5Skill.bpUpgradeName = "극대화";

            // 효과 100% 증가
            foreach (var effect in bp5Skill.effects)
            {
                effect.value *= 2f;
            }

            // 데이터베이스에 추가
            database.AddOrUpdateSkill(bp1Skill);
            database.AddOrUpdateSkill(bp3Skill);
            database.AddOrUpdateSkill(bp5Skill);

            SaveDatabase();

            EditorUtility.DisplayDialog("BP 업그레이드 생성",
                $"{baseSkill.skillName}의 BP 업그레이드 3개가 생성되었습니다.", "확인");
        }



        private void DrawEffectEditor()
        {
            EditorGUILayout.LabelField("✨ 효과 편집", EditorStyles.boldLabel);

            // 빠른 효과 추가 버튼들
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ 데미지")) AddEffectFromPreset("damage_basic");
            if (GUILayout.Button("+ 회복")) AddEffectFromPreset("heal_basic");
            if (GUILayout.Button("+ 버프")) AddEffectFromPreset("buff_attack");
            if (GUILayout.Button("+ 디버프")) AddEffectFromPreset("debuff_defense");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ 상태이상")) AddEffectFromPreset("status_stun");
            if (GUILayout.Button("+ 보호막")) AddEffectFromPreset("shield_basic");
            if (GUILayout.Button("+ 반사")) AddEffectFromPreset("reflect_basic");
            if (GUILayout.Button("+ 해제")) AddEffectFromPreset("dispel_basic");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // 효과 목록
            effectScrollPosition = EditorGUILayout.BeginScrollView(effectScrollPosition);

            for (int i = 0; i < currentSkill.effects.Count; i++)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                DrawSingleEffect(currentSkill.effects[i], i);
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();

            // 저장 버튼
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("💾 저장", GUILayout.Height(35)))
            {
                SaveSkill();
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("❌ 취소", GUILayout.Height(35)))
            {
                if (EditorUtility.DisplayDialog("확인", "변경사항을 취소하시겠습니까?", "예", "아니오"))
                {
                    currentSkill = null;
                    currentTab = EditorTab.SkillList;
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSingleEffect(AdvancedSkillEffect effect, int index)
        {
            EditorGUILayout.BeginHorizontal();

            effect.isExpanded = EditorGUILayout.Foldout(effect.isExpanded,
                $"효과 {index + 1}: {GetEffectIcon(effect.type)} {effect.GetShortDescription()}", true);

            if (GUILayout.Button("↑", GUILayout.Width(25)) && index > 0)
            {
                SwapEffects(index, index - 1);
            }

            if (GUILayout.Button("↓", GUILayout.Width(25)) && index < currentSkill.effects.Count - 1)
            {
                SwapEffects(index, index + 1);
            }

            if (GUILayout.Button("복제", GUILayout.Width(40)))
            {
                currentSkill.effects.Insert(index + 1, effect.Clone());
            }

            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                currentSkill.effects.RemoveAt(index);
                return;
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            if (effect.isExpanded)
            {
                EditorGUI.indentLevel++;

                effect.type = (EffectType)EditorGUILayout.EnumPopup("타입", effect.type);
                effect.targetType = (TargetType)EditorGUILayout.EnumPopup("타겟", effect.targetType);

                if (effect.targetType == TargetType.Multiple)
                {
                    effect.targetCount = EditorGUILayout.IntSlider("타겟 수", effect.targetCount, 1, 5);
                }

                effect.chance = EditorGUILayout.Slider("발동 확률", effect.chance, 0f, 1f);

                // 타입별 세부 설정
                switch (effect.type)
                {
                    case EffectType.Damage:
                    case EffectType.TrueDamage:
                        effect.value = EditorGUILayout.FloatField("데미지 %", effect.value);
                        effect.damageType = (DamageType)EditorGUILayout.EnumPopup("데미지 타입", effect.damageType);
                        effect.canCrit = EditorGUILayout.Toggle("치명타 가능", effect.canCrit);

                        if (effect.type == EffectType.Damage)
                        {
                            effect.damageBase = (DamageBase)EditorGUILayout.EnumPopup("데미지 기준", effect.damageBase);
                        }
                        break;

                    case EffectType.Heal:
                    case EffectType.HealOverTime:
                        effect.value = EditorGUILayout.FloatField("회복량 %", effect.value);
                        effect.healBase = (HealBase)EditorGUILayout.EnumPopup("회복 기준", effect.healBase);
                        if (effect.type == EffectType.HealOverTime)
                        {
                            effect.duration = EditorGUILayout.IntField("지속 턴", effect.duration);
                        }
                        break;

                    case EffectType.Buff:
                    case EffectType.Debuff:
                        effect.statType = (StatType)EditorGUILayout.EnumPopup("스탯", effect.statType);
                        effect.value = EditorGUILayout.FloatField("변화량 %", effect.value);
                        effect.duration = EditorGUILayout.IntField("지속 턴", effect.duration);
                        effect.isStackable = EditorGUILayout.Toggle("중첩 가능", effect.isStackable);
                        if (effect.isStackable)
                        {
                            effect.maxStacks = EditorGUILayout.IntField("최대 중첩", effect.maxStacks);
                        }
                        break;

                    case EffectType.StatusEffect:
                        effect.statusType = (StatusType)EditorGUILayout.EnumPopup("상태이상", effect.statusType);
                        effect.duration = EditorGUILayout.IntField("지속 턴", effect.duration);
                        if (effect.statusType == StatusType.Burn ||
                            effect.statusType == StatusType.Poison ||
                            effect.statusType == StatusType.Bleed)
                        {
                            effect.value = EditorGUILayout.FloatField("틱 데미지 %", effect.value);
                        }
                        break;

                    case EffectType.Shield:
                        effect.value = EditorGUILayout.FloatField("보호막 %", effect.value);
                        effect.shieldBase = (ShieldBase)EditorGUILayout.EnumPopup("보호막 기준", effect.shieldBase);
                        effect.duration = EditorGUILayout.IntField("지속 턴", effect.duration);
                        break;

                    case EffectType.Reflect:
                        effect.value = EditorGUILayout.FloatField("반사 %", effect.value);
                        effect.duration = EditorGUILayout.IntField("지속 턴", effect.duration);
                        break;

                    case EffectType.LifeSteal:
                        effect.value = EditorGUILayout.FloatField("흡혈 %", effect.value);
                        break;

                    case EffectType.Dispel:
                        effect.dispelType = (DispelType)EditorGUILayout.EnumPopup("해제 타입", effect.dispelType);
                        effect.dispelCount = EditorGUILayout.IntField("해제 개수", effect.dispelCount);
                        break;

                    case EffectType.Summon:
                        effect.summonId = EditorGUILayout.IntField("소환수 ID", effect.summonId);
                        effect.summonCount = EditorGUILayout.IntField("소환 수", effect.summonCount);
                        break;

                    case EffectType.Transform:
                        effect.transformId = EditorGUILayout.IntField("변신 ID", effect.transformId);
                        effect.duration = EditorGUILayout.IntField("지속 턴", effect.duration);
                        break;

                    case EffectType.DamageReduction:
                        effect.value = EditorGUILayout.FloatField("피해 감소 %", effect.value);
                        effect.duration = EditorGUILayout.IntField("지속 턴", effect.duration);
                        break;

                    case EffectType.Immunity:
                        effect.duration = EditorGUILayout.IntField("지속 턴", effect.duration);
                        break;

                    case EffectType.Invincible:
                        effect.duration = EditorGUILayout.IntField("지속 턴", effect.duration);
                        break;

                    case EffectType.Special:
                        effect.specialEffect = EditorGUILayout.TextField("특수 효과", effect.specialEffect);
                        effect.value = EditorGUILayout.FloatField("값", effect.value);
                        effect.duration = EditorGUILayout.IntField("지속 턴", effect.duration);
                        break;
                }

                // 조건부 효과
                effect.hasCondition = EditorGUILayout.Toggle("조건부 발동", effect.hasCondition);
                if (effect.hasCondition)
                {
                    effect.conditionType = (ConditionType)EditorGUILayout.EnumPopup("조건", effect.conditionType);
                    effect.conditionValue = EditorGUILayout.FloatField("조건 값", effect.conditionValue);
                }

                // 툴팁
                effect.tooltip = EditorGUILayout.TextField("툴팁", effect.tooltip);

                EditorGUI.indentLevel--;
            }
        }

        private void DrawEnhancedPreview()
        {
            EditorGUILayout.LabelField("👁️ 실시간 미리보기", EditorStyles.boldLabel);

            // 효율 계산 섹션 (새로 추가)
            DrawEfficiencySection();

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 스킬 요약
            EditorGUILayout.LabelField($"{GetCategoryIcon(currentSkill.category)} {currentSkill.skillName}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"클래스: {GetClassIcon(currentSkill.characterClass)} {currentSkill.characterClass}");
            EditorGUILayout.LabelField($"티어: {GetTierStars(currentSkill.tier)}");
            EditorGUILayout.LabelField($"쿨타운: {currentSkill.cooldown} MP");

            if (currentSkill.cooldown > 0)
            {
                EditorGUILayout.LabelField($"쿨다운: {currentSkill.cooldown} 턴");
            }

            if (currentSkill.specialGauge > 0)
            {
                EditorGUILayout.LabelField($"스페셜 게이지: {currentSkill.specialGauge}");
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("효과:", EditorStyles.boldLabel);

            foreach (var effect in currentSkill.effects)
            {
                EditorGUILayout.LabelField($"• {effect.GetFullDescription()}");
            }

            EditorGUILayout.EndVertical();

            // 시뮬레이션
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📊 시뮬레이션", EditorStyles.boldLabel);

            var attackPower = EditorGUILayout.IntSlider("공격력", 1000, 100, 5000);
            var defense = EditorGUILayout.IntSlider("방어력", 500, 0, 2000);
            var maxHp = EditorGUILayout.IntSlider("최대 HP", 10000, 1000, 50000);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("예상 결과:", EditorStyles.boldLabel);

            var simulation = SimulateSkill(currentSkill, attackPower, defense, maxHp);
            foreach (var result in simulation)
            {
                EditorGUILayout.LabelField(result);
            }

            EditorGUILayout.EndVertical();

            // 비교
            if (compareSkill != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("🔄 스킬 비교", EditorStyles.boldLabel);

                CompareSkills(currentSkill, compareSkill);

                if (GUILayout.Button("비교 종료"))
                {
                    compareSkill = null;
                }

                EditorGUILayout.EndVertical();
            }
        }

        // 효율 계산 섹션 (새로 추가)
        private void DrawEfficiencySection()
        {
            if (currentSkill == null || currentSkill.effects.Count == 0) return;

            var efficiency = SkillEfficiencyCalculator.CalculateEfficiency(currentSkill);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 헤더
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("⚖️ 효율 분석", EditorStyles.boldLabel);

            // 등급 표시
            var oldColor = GUI.color;
            GUI.color = efficiency.GetGradeColor();
            EditorGUILayout.LabelField($"등급: {efficiency.GetGradeText()}",
                EditorStyles.boldLabel, GUILayout.Width(80));
            GUI.color = oldColor;

            EditorGUILayout.EndHorizontal();

            // 총 점수
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"총 효율 점수: {efficiency.TotalScore:F1}");

            // 점수 바 그리기
            var rect = GUILayoutUtility.GetRect(100, 20);
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));
            var fillRect = new Rect(rect.x, rect.y, rect.width * Mathf.Min(efficiency.TotalScore / 200f, 1f), rect.height);
            EditorGUI.DrawRect(fillRect, efficiency.GetGradeColor());

            EditorGUILayout.EndHorizontal();

            // 밸런스 상태
            EditorGUILayout.Space();
            oldColor = GUI.color;
            GUI.color = efficiency.GetStatusColor();
            EditorGUILayout.LabelField(efficiency.BalanceMessage, EditorStyles.wordWrappedLabel);
            GUI.color = oldColor;

            // 추천 설정
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"현재 티어: {currentSkill.tier}", GUILayout.Width(100));

            if (currentSkill.tier != efficiency.RecommendedTier)
            {
                GUI.backgroundColor = Color.yellow;
                if (GUILayout.Button($"→ 티어 {efficiency.RecommendedTier} 적용", GUILayout.Width(120)))
                {
                    currentSkill.tier = efficiency.RecommendedTier;
                }
                GUI.backgroundColor = Color.white;
            }
            else
            {
                EditorGUILayout.LabelField("✓ 적정", GUILayout.Width(120));
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"현재 쿨타운: {currentSkill.cooldown}", GUILayout.Width(100));

            if (currentSkill.cooldown != efficiency.RecommendedCooldown)
            {
                GUI.backgroundColor = Color.yellow;
                if (GUILayout.Button($"→ 쿨다운 {efficiency.RecommendedCooldown} 적용", GUILayout.Width(120)))
                {
                    currentSkill.cooldown = efficiency.RecommendedCooldown;
                }
                GUI.backgroundColor = Color.white;
            }
            else
            {
                EditorGUILayout.LabelField("✓ 적정", GUILayout.Width(120));
            }
            EditorGUILayout.EndHorizontal();

            // 효과별 점수 분석 (접을 수 있는 섹션)
            EditorGUILayout.Space();
            showEfficiencyBreakdown = EditorGUILayout.Foldout(showEfficiencyBreakdown, "상세 점수 분석", true);

            if (showEfficiencyBreakdown)
            {
                EditorGUI.indentLevel++;

                foreach (var breakdown in efficiency.EfficiencyBreakdown)
                {
                    EditorGUILayout.BeginHorizontal();

                    // 효과 이름
                    EditorGUILayout.LabelField($"• {breakdown.EffectName}", GUILayout.Width(200));

                    // 점수
                    EditorGUILayout.LabelField($"{breakdown.FinalScore:F1}점", GUILayout.Width(60));

                    // 상세 정보
                    if (!string.IsNullOrEmpty(breakdown.Details))
                    {
                        EditorGUILayout.LabelField($"({breakdown.Details})", EditorStyles.miniLabel);
                    }

                    EditorGUILayout.EndHorizontal();

                    // 배수 정보
                    if (breakdown.ChanceMultiplier < 1f || breakdown.TargetMultiplier != 1f)
                    {
                        EditorGUI.indentLevel++;
                        var details = $"기본: {breakdown.BaseScore:F1}";
                        if (breakdown.ChanceMultiplier < 1f)
                            details += $" × 확률 {breakdown.ChanceMultiplier:F1}";
                        if (breakdown.TargetMultiplier != 1f)
                            details += $" × 타겟 {breakdown.TargetMultiplier:F1}";

                        EditorGUILayout.LabelField(details, EditorStyles.miniLabel);
                        EditorGUI.indentLevel--;
                    }
                }

                EditorGUI.indentLevel--;
            }

            // 개선 제안
            if (efficiency.Suggestions != null && efficiency.Suggestions.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("개선 제안:", EditorStyles.boldLabel);

                foreach (var suggestion in efficiency.Suggestions)
                {
                    EditorGUILayout.LabelField(suggestion, EditorStyles.wordWrappedMiniLabel);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private bool showEfficiencyBreakdown = true;

        // 헬퍼 메서드들
        private void AddEffectFromPreset(string presetId)
        {
            var preset = AdvancedEffectPresets.GetPresetById(presetId);
            if (preset != null && currentSkill != null)
            {
                currentSkill.effects.Add(preset.Clone());
            }
        }

        private void SwapEffects(int index1, int index2)
        {
            var temp = currentSkill.effects[index1];
            currentSkill.effects[index1] = currentSkill.effects[index2];
            currentSkill.effects[index2] = temp;
        }

        private List<string> SimulateSkill(AdvancedSkillData skill, int attackPower, int defense, int maxHp)
        {
            var results = new List<string>();

            foreach (var effect in skill.effects)
            {
                switch (effect.type)
                {
                    case EffectType.Damage:
                        var damage = CalculateDamage(effect, attackPower, defense);
                        results.Add($"💥 데미지: {damage:F0}");
                        break;

                    case EffectType.Heal:
                        var heal = CalculateHeal(effect, attackPower, maxHp);
                        results.Add($"💚 회복: {heal:F0}");
                        break;

                    case EffectType.Shield:
                        var shield = CalculateShield(effect, defense, maxHp);
                        results.Add($"🛡️ 보호막: {shield:F0}");
                        break;

                    default:
                        results.Add($"✨ {effect.GetShortDescription()}");
                        break;
                }
            }

            return results;
        }

        private float CalculateDamage(AdvancedSkillEffect effect, int attackPower, int defense)
        {
            float baseDamage = attackPower;

            if (effect.damageBase == DamageBase.Defense)
            {
                baseDamage = defense;
            }

            float damage = baseDamage * (effect.value / 100f);

            if (effect.type != EffectType.TrueDamage)
            {
                damage -= defense * 0.5f;
            }

            return Mathf.Max(1, damage);
        }

        private float CalculateHeal(AdvancedSkillEffect effect, int attackPower, int maxHp)
        {
            return effect.healBase switch
            {
                HealBase.MaxHP => maxHp * (effect.value / 100f),
                HealBase.Attack => attackPower * (effect.value / 100f),
                _ => maxHp * (effect.value / 100f)
            };
        }

        private float CalculateShield(AdvancedSkillEffect effect, int defense, int maxHp)
        {
            return effect.shieldBase switch
            {
                ShieldBase.MaxHP => maxHp * (effect.value / 100f),
                ShieldBase.Defense => defense * (effect.value / 100f),
                _ => maxHp * (effect.value / 100f)
            };
        }

        private void CompareSkills(AdvancedSkillData skill1, AdvancedSkillData skill2)
        {
            EditorGUILayout.LabelField($"현재: {skill1.skillName} vs {skill2.skillName}");
            EditorGUILayout.LabelField($"쿨다운: {skill1.cooldown} vs {skill2.cooldown}");
            EditorGUILayout.LabelField($"효과 수: {skill1.effects.Count} vs {skill2.effects.Count}");
            EditorGUILayout.LabelField($"티어: {skill1.tier} vs {skill2.tier}");
        }

        private void SaveSkill()
        {
            if (string.IsNullOrEmpty(currentSkill.skillName))
            {
                EditorUtility.DisplayDialog("오류", "스킬 이름을 입력해주세요.", "확인");
                return;
            }

            currentSkill.lastModified = DateTime.Now;
            database.AddOrUpdateSkill(currentSkill);
            SaveDatabase();

            EditorUtility.DisplayDialog("성공", "스킬이 저장되었습니다.", "확인");
            currentTab = EditorTab.SkillList;
        }
    }
}

#endif