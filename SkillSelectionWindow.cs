using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System;

public class SkillSelectionWindow : EditorWindow
{
    public enum SkillSelectMode
    {
        Active,
        Passive
    }

    private SkillSelectMode selectMode;
    private System.Action<int> onSkillSelected;

    // 스킬 데이터
    private List<SkillSystem.AdvancedSkillData> allSkills = new List<SkillSystem.AdvancedSkillData>();
    private List<SkillSystem.AdvancedSkillData> filteredSkills = new List<SkillSystem.AdvancedSkillData>();

    // 필터링
    private string searchString = "";
    private SkillSystem.SkillCategory filterCategory = SkillSystem.SkillCategory.All;
    private SkillSystem.CharacterClass filterClass = SkillSystem.CharacterClass.All;
    private int filterTier = -1;
    private bool showOnlyPassive = false;
    private bool showOnlyActive = false;

    // UI
    private Vector2 scrollPos;
    private SkillSystem.AdvancedSkillData selectedSkill;
    private bool showPreview = true;

    // 스타일
    private GUIStyle headerStyle;
    private GUIStyle selectedStyle;
    private Texture2D selectedTexture;

    private bool needsFilter = false;  // 필터링 플래그 추가
    private bool isInitialized = false;  // 초기화 플래그 추가
    public static void ShowWindow(SkillSelectMode mode, System.Action<int> callback)
    {
        var window = GetWindow<SkillSelectionWindow>(true, mode == SkillSelectMode.Active ? "액티브 스킬 선택" : "패시브 스킬 선택");
        window.minSize = new Vector2(800, 600);
        window.selectMode = mode;
        window.onSkillSelected = callback;
        window.Initialize();
        window.Show();
    }

    private void Initialize()
    {
        if (isInitialized) return;  // 중복 초기화 방지

        isInitialized = true;

        LoadAllSkills();
        InitStyles();

        // 모드에 따라 기본 필터 설정
        if (selectMode == SkillSelectMode.Passive)
        {
            showOnlyPassive = true;
            showOnlyActive = false;
        }
        else
        {
            showOnlyActive = true;
            showOnlyPassive = false;
        }

        needsFilter = true;  // 필터링 예약
    }
    private void OnEnable()
    {
        isInitialized = false;
    }

    private void InitStyles()
    {
        headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter
        };

        selectedTexture = new Texture2D(1, 1);
        selectedTexture.SetPixel(0, 0, new Color(0.3f, 0.5f, 1f, 0.3f));
        selectedTexture.Apply();

        selectedStyle = new GUIStyle(EditorStyles.helpBox);
        selectedStyle.normal.background = selectedTexture;
    }

    private void OnDestroy()
    {
        if (selectedTexture != null)
        {
            DestroyImmediate(selectedTexture);
        }
    }

    // LoadAllSkills 메서드도 안전하게 수정
    private void LoadAllSkills()
    {
        try
        {
            allSkills.Clear();

            // SkillDatabase 방식으로 로드 - 에러 처리 추가
            var database = SkillSystem.AdvancedSkillDatabase.Load();
            if (database != null)
            {
                var skills = database.GetAllSkills();
                if (skills != null)
                {
                    allSkills.AddRange(skills);
                }
            }

            // 테스트용 더미 데이터 생성 (스킬이 없을 경우)
            if (allSkills.Count == 0)
            {
                CreateDummySkills();
            }

            allSkills = allSkills.OrderBy(s => s.skillId).ToList();
        }
        catch (Exception e)
        {
            Debug.LogError($"스킬 로드 중 오류: {e.Message}");
            allSkills = new List<SkillSystem.AdvancedSkillData>();
            CreateDummySkills();  // 오류 시 더미 데이터로 대체
        }
    }


    private void CreateDummySkills()
    {
        // 액티브 스킬 예제
        allSkills.Add(new SkillSystem.AdvancedSkillData
        {
            skillId = 1001,
            skillName = "파워 스트라이크",
            description = "강력한 일격으로 적에게 큰 피해를 입힙니다.",
            category = SkillSystem.SkillCategory.Active,
            characterClass = SkillSystem.CharacterClass.Slaughter,
            tier = 1,
            cooldown = 3,
            effects = new List<SkillSystem.AdvancedSkillEffect>
            {
                new SkillSystem.AdvancedSkillEffect
                {
                    type = SkillSystem.EffectType.Damage,
                    targetType = SkillSystem.TargetType.EnemySingle,
                    value = 150f
                }
            }
        });

        allSkills.Add(new SkillSystem.AdvancedSkillData
        {
            skillId = 1002,
            skillName = "가디언 실드",
            description = "아군을 보호하는 방어막을 생성합니다.",
            category = SkillSystem.SkillCategory.Active,
            characterClass = SkillSystem.CharacterClass.Vanguard,
            tier = 1,
            cooldown = 4,
            effects = new List<SkillSystem.AdvancedSkillEffect>
            {
                new SkillSystem.AdvancedSkillEffect
                {
                    type = SkillSystem.EffectType.Shield,
                    targetType = SkillSystem.TargetType.AllySingle,
                    value = 100f,
                    duration = 3
                }
            }
        });

        // 패시브 스킬 예제
        allSkills.Add(new SkillSystem.AdvancedSkillData
        {
            skillId = 2001,
            skillName = "전투의 열정",
            description = "공격력이 영구적으로 증가합니다.",
            category = SkillSystem.SkillCategory.Passive,
            characterClass = SkillSystem.CharacterClass.All,
            tier = 1,
            effects = new List<SkillSystem.AdvancedSkillEffect>
            {
                new SkillSystem.AdvancedSkillEffect
                {
                    type = SkillSystem.EffectType.Buff,
                    targetType = SkillSystem.TargetType.Self,
                    statType = SkillSystem.StatType.Attack,
                    value = 20f
                }
            }
        });

        allSkills.Add(new SkillSystem.AdvancedSkillData
        {
            skillId = 2002,
            skillName = "민첩한 회피",
            description = "회피율이 영구적으로 증가합니다.",
            category = SkillSystem.SkillCategory.Passive,
            characterClass = SkillSystem.CharacterClass.Jacker,
            tier = 1,
            effects = new List<SkillSystem.AdvancedSkillEffect>
            {
                new SkillSystem.AdvancedSkillEffect
                {
                    type = SkillSystem.EffectType.Buff,
                    targetType = SkillSystem.TargetType.Self,
                    statType = SkillSystem.StatType.Evasion,
                    value = 15f
                }
            }
        });
    }

    private void OnGUI()
    {
        // 초기화 체크
        if (!isInitialized)
        {
            Initialize();
            return;
        }

        // 필터링이 필요한 경우 한 번만 수행
        if (needsFilter)
        {
            needsFilter = false;
            ApplyFilters();
        }


        EditorGUILayout.Space(5);

        // 헤더
        EditorGUILayout.LabelField(selectMode == SkillSelectMode.Active ? "🎯 액티브 스킬 선택" : "✨ 패시브 스킬 선택", headerStyle);
        EditorGUILayout.Space(10);

        // 메인 레이아웃
        EditorGUILayout.BeginHorizontal();

        // 왼쪽: 필터 및 리스트
        EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.5f));
        DrawFilterSection();
        DrawSkillList();
        EditorGUILayout.EndVertical();

        // 오른쪽: 미리보기
        if (showPreview)
        {
            EditorGUILayout.BeginVertical();
            DrawSkillPreview();
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndHorizontal();

        // 하단 버튼
        DrawBottomButtons();
    }

    private void DrawFilterSection()
    {
        EditorGUILayout.LabelField("필터", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            // 검색 - Event 사용으로 변경
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("검색:", GUILayout.Width(50));

            EditorGUI.BeginChangeCheck();
            searchString = EditorGUILayout.TextField(searchString);
            if (EditorGUI.EndChangeCheck())  // 값이 실제로 변경되었을 때만
            {
                needsFilter = true;  // 다음 프레임에 필터링
            }

            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                searchString = "";
                needsFilter = true;
            }
            EditorGUILayout.EndHorizontal();

            // 카테고리 필터
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("카테고리:", GUILayout.Width(50));

            EditorGUI.BeginChangeCheck();
            filterCategory = (SkillSystem.SkillCategory)EditorGUILayout.EnumPopup(filterCategory);
            if (EditorGUI.EndChangeCheck())
            {
                needsFilter = true;
            }
            EditorGUILayout.EndHorizontal();

            // 클래스 필터
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("클래스:", GUILayout.Width(50));

            EditorGUI.BeginChangeCheck();
            filterClass = (SkillSystem.CharacterClass)EditorGUILayout.EnumPopup(filterClass);
            if (EditorGUI.EndChangeCheck())
            {
                needsFilter = true;
            }
            EditorGUILayout.EndHorizontal();

            // 티어 필터
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("티어:", GUILayout.Width(50));
            string[] tierOptions = new string[] { "All", "0", "1", "2", "3", "4", "5" };
            int tierIndex = filterTier + 1;

            EditorGUI.BeginChangeCheck();
            tierIndex = EditorGUILayout.Popup(tierIndex, tierOptions);
            if (EditorGUI.EndChangeCheck())
            {
                filterTier = tierIndex - 1;
                needsFilter = true;
            }
            EditorGUILayout.EndHorizontal();

            // 스킬 타입 필터
            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginChangeCheck();
            showOnlyActive = EditorGUILayout.Toggle("액티브", showOnlyActive);
            showOnlyPassive = EditorGUILayout.Toggle("패시브", showOnlyPassive);
            if (EditorGUI.EndChangeCheck())
            {
                needsFilter = true;
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.LabelField($"검색 결과: {filteredSkills.Count}개", EditorStyles.miniLabel);
    }


    private void DrawSkillList()
    {
        EditorGUILayout.LabelField("스킬 목록", EditorStyles.boldLabel);

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        foreach (var skill in filteredSkills)
        {
            bool isSelected = selectedSkill == skill;

            using (new EditorGUILayout.VerticalScope(isSelected ? selectedStyle : EditorStyles.helpBox))
            {
                // 스킬 헤더
                EditorGUILayout.BeginHorizontal();

                // 스킬 ID와 이름
                string skillIcon = GetSkillCategoryIcon(skill.category);
                EditorGUILayout.LabelField($"{skillIcon} [{skill.skillId}] {skill.skillName}",
                    isSelected ? EditorStyles.whiteBoldLabel : EditorStyles.boldLabel);

                // 선택 버튼
                if (GUILayout.Button("선택", GUILayout.Width(50)))
                {
                    SelectSkill(skill);
                }

                EditorGUILayout.EndHorizontal();

                // 스킬 정보
                EditorGUILayout.BeginHorizontal();

                // 카테고리
                GUI.backgroundColor = GetCategoryColor(skill.category);
                EditorGUILayout.LabelField(skill.category.ToString(), GUILayout.Width(80));
                GUI.backgroundColor = Color.white;

                // 클래스
                if (skill.characterClass != SkillSystem.CharacterClass.All)
                {
                    EditorGUILayout.LabelField($"클래스: {skill.characterClass}", GUILayout.Width(120));
                }

                // 티어
                if (skill.tier > 0)
                {
                    EditorGUILayout.LabelField($"Tier {skill.tier}", GUILayout.Width(50));
                }

                // 쿨다운
                if (skill.cooldown > 0)
                {
                    EditorGUILayout.LabelField($"CD: {skill.cooldown}", GUILayout.Width(50));
                }

                EditorGUILayout.EndHorizontal();

                // 설명
                if (!string.IsNullOrEmpty(skill.description))
                {
                    EditorGUILayout.LabelField(skill.description, EditorStyles.wordWrappedMiniLabel);
                }

                // 주요 효과 표시
                if (skill.effects != null && skill.effects.Count > 0)
                {
                    string effectSummary = "효과: ";
                    int count = 0;
                    foreach (var effect in skill.effects)
                    {
                        if (count > 0) effectSummary += ", ";
                        effectSummary += GetEffectSummary(effect);
                        count++;
                        if (count >= 3) break;
                    }
                    if (skill.effects.Count > 3)
                    {
                        effectSummary += $" 외 {skill.effects.Count - 3}개";
                    }
                    EditorGUILayout.LabelField(effectSummary, EditorStyles.miniLabel);
                }

                if (Event.current.type == EventType.MouseDown &&
                    GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                {
                    selectedSkill = skill;
                    Event.current.Use();
                }
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawSkillPreview()
    {
        EditorGUILayout.LabelField("스킬 상세 정보", EditorStyles.boldLabel);

        if (selectedSkill == null)
        {
            EditorGUILayout.HelpBox("스킬을 선택하면 상세 정보가 표시됩니다.", MessageType.Info);
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            // 기본 정보
            EditorGUILayout.LabelField("기본 정보", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"ID: {selectedSkill.skillId}");
            EditorGUILayout.LabelField($"이름: {selectedSkill.skillName}");
            EditorGUILayout.LabelField($"카테고리: {selectedSkill.category}");
            if (!string.IsNullOrEmpty(selectedSkill.description))
            {
                EditorGUILayout.LabelField($"설명: {selectedSkill.description}", EditorStyles.wordWrappedLabel);
            }

            EditorGUILayout.Space(10);

            // 요구사항
            EditorGUILayout.LabelField("요구사항", EditorStyles.boldLabel);
            if (selectedSkill.characterClass != SkillSystem.CharacterClass.All)
            {
                EditorGUILayout.LabelField($"클래스: {selectedSkill.characterClass}");
            }
            if (selectedSkill.cooldown > 0)
            {
                EditorGUILayout.LabelField($"쿨다운: {selectedSkill.cooldown}턴");
            }
            if (selectedSkill.specialGauge > 0)
            {
                EditorGUILayout.LabelField($"SP 게이지: {selectedSkill.specialGauge}");
            }

            EditorGUILayout.Space(10);

            // 효과 목록
            if (selectedSkill.effects != null && selectedSkill.effects.Count > 0)
            {
                EditorGUILayout.LabelField($"효과 ({selectedSkill.effects.Count}개)", EditorStyles.boldLabel);

                foreach (var effect in selectedSkill.effects)
                {
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        DrawEffectDetails(effect);
                    }
                }
            }
        }
    }

    private void DrawEffectDetails(SkillSystem.AdvancedSkillEffect effect)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(GetEffectIcon(effect.type), GUILayout.Width(20));
        EditorGUILayout.LabelField($"{effect.type}", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"→ {effect.targetType}");
        EditorGUILayout.EndHorizontal();

        // 효과 세부사항
        if (effect.value > 0)
        {
            EditorGUILayout.LabelField($"수치: {effect.value:F1}%");
        }

        if (effect.duration > 0)
        {
            EditorGUILayout.LabelField($"지속: {effect.duration}턴");
        }

        if (effect.chance < 1f)
        {
            EditorGUILayout.LabelField($"확률: {effect.chance * 100:F0}%");
        }

        if (effect.statusType != SkillSystem.StatusType.None)
        {
            EditorGUILayout.LabelField($"상태: {GetStatusName(effect.statusType)}");
        }

        if (effect.statType != SkillSystem.StatType.None &&
            (effect.type == SkillSystem.EffectType.Buff || effect.type == SkillSystem.EffectType.Debuff))
        {
            EditorGUILayout.LabelField($"스탯: {GetStatName(effect.statType)}");
        }
    }

    private void DrawBottomButtons()
    {
        EditorGUILayout.Space(10);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();

            GUI.enabled = selectedSkill != null;
            GUI.backgroundColor = new Color(0.3f, 1f, 0.3f);
            if (GUILayout.Button("✓ 확인", GUILayout.Height(30), GUILayout.Width(100)))
            {
                SelectSkill(selectedSkill);
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;

            if (GUILayout.Button("취소", GUILayout.Height(30), GUILayout.Width(100)))
            {
                Close();
            }

            GUILayout.FlexibleSpace();
        }
    }

    private void SelectSkill(SkillSystem.AdvancedSkillData skill)
    {
        if (skill != null && onSkillSelected != null)
        {
            onSkillSelected(skill.skillId);
            Close();
        }
    }

    private void ApplyFilters()
    {
        if (allSkills == null || allSkills.Count == 0)
        {
            filteredSkills = new List<SkillSystem.AdvancedSkillData>();
            return;
        }

        try
        {
            filteredSkills = allSkills.Where(skill =>
            {
                if (skill == null) return false;  // null 체크

                // 검색어 필터
                if (!string.IsNullOrEmpty(searchString))
                {
                    bool matches = false;
                    if (!string.IsNullOrEmpty(skill.skillName))
                        matches = skill.skillName.ToLower().Contains(searchString.ToLower());
                    matches = matches || skill.skillId.ToString().Contains(searchString);
                    if (!string.IsNullOrEmpty(skill.description))
                        matches = matches || skill.description.ToLower().Contains(searchString.ToLower());
                    if (!matches) return false;
                }

                // ... 기존 필터 로직 ...

                return true;
            }).ToList();
        }
        catch (Exception e)
        {
            Debug.LogError($"필터링 중 오류 발생: {e.Message}");
            filteredSkills = new List<SkillSystem.AdvancedSkillData>();
        }
    }

    // 헬퍼 메서드들
    private string GetSkillCategoryIcon(SkillSystem.SkillCategory category)
    {
        switch (category)
        {
            case SkillSystem.SkillCategory.Active: return "⚔️";
            case SkillSystem.SkillCategory.Passive: return "🛡️";
            case SkillSystem.SkillCategory.SpecialActive: return "⭐";
            case SkillSystem.SkillCategory.SpecialPassive: return "💫";
            default: return "📋";
        }
    }

    private string GetEffectIcon(SkillSystem.EffectType type)
    {
        switch (type)
        {
            case SkillSystem.EffectType.Damage:
            case SkillSystem.EffectType.TrueDamage:
            case SkillSystem.EffectType.FixedDamage:
                return "💥";
            case SkillSystem.EffectType.Heal:
            case SkillSystem.EffectType.HealOverTime:
                return "💚";
            case SkillSystem.EffectType.Shield:
                return "🛡️";
            case SkillSystem.EffectType.Buff:
                return "⬆️";
            case SkillSystem.EffectType.Debuff:
                return "⬇️";
            case SkillSystem.EffectType.StatusEffect:
                return "🔮";
            /*case SkillSystem.EffectType.Stun:
                return "⚡";
            case SkillSystem.EffectType.Silence:
                return "🔇";*/
            default:
                return "•";
        }
    }

    private Color GetCategoryColor(SkillSystem.SkillCategory category)
    {
        switch (category)
        {
            case SkillSystem.SkillCategory.Active:
                return new Color(1f, 0.3f, 0.3f, 0.5f);
            case SkillSystem.SkillCategory.Passive:
                return new Color(0.3f, 0.5f, 1f, 0.5f);
            case SkillSystem.SkillCategory.SpecialActive:
                return new Color(1f, 0.8f, 0.3f, 0.5f);
            case SkillSystem.SkillCategory.SpecialPassive:
                return new Color(0.8f, 0.3f, 1f, 0.5f);
            default:
                return new Color(0.7f, 0.7f, 0.7f, 0.5f);
        }
    }

    private string GetEffectSummary(SkillSystem.AdvancedSkillEffect effect)
    {
        string summary = "";

        switch (effect.type)
        {
            case SkillSystem.EffectType.Damage:
            case SkillSystem.EffectType.TrueDamage:
            case SkillSystem.EffectType.FixedDamage:
                summary = $"데미지 {effect.value:F0}%";
                break;
            case SkillSystem.EffectType.Heal:
            case SkillSystem.EffectType.HealOverTime:
                summary = $"회복 {effect.value:F0}%";
                break;
            case SkillSystem.EffectType.Shield:
                summary = $"보호막 {effect.value:F0}";
                break;
            case SkillSystem.EffectType.Buff:
                summary = $"버프 {GetStatName(effect.statType)}";
                break;
            case SkillSystem.EffectType.Debuff:
                summary = $"디버프 {GetStatName(effect.statType)}";
                break;
            case SkillSystem.EffectType.StatusEffect:
                summary = GetStatusName(effect.statusType);
                break;
            default:
                summary = effect.type.ToString();
                break;
        }

        return summary;
    }

    private string GetStatName(SkillSystem.StatType stat)
    {
        switch (stat)
        {
            case SkillSystem.StatType.Attack: return "공격력";
            case SkillSystem.StatType.Defense: return "방어력";
            case SkillSystem.StatType.Speed: return "속도";
            case SkillSystem.StatType.CritRate: return "치명타 확률";
            case SkillSystem.StatType.CritDamage: return "치명타 피해";
            case SkillSystem.StatType.Accuracy: return "명중";
            case SkillSystem.StatType.Evasion: return "회피";
            case SkillSystem.StatType.EffectHit: return "효과 적중";
            case SkillSystem.StatType.EffectResist: return "효과 저항";
            case SkillSystem.StatType.MaxHP: return "최대 체력";
            case SkillSystem.StatType.HealBlock: return "회복 차단";
            case SkillSystem.StatType.HealReduction: return "회복량 감소";
            case SkillSystem.StatType.None: return "없음";
            default: return stat.ToString();
        }
    }

    private string GetStatusName(SkillSystem.StatusType status)
    {
        switch (status)
        {
            case SkillSystem.StatusType.None: return "없음";
            case SkillSystem.StatusType.Stun: return "기절";
            case SkillSystem.StatusType.Silence: return "침묵";
            case SkillSystem.StatusType.Taunt: return "도발";
            case SkillSystem.StatusType.Freeze: return "빙결";
            case SkillSystem.StatusType.Burn: return "화상";
            case SkillSystem.StatusType.Poison: return "중독";
            case SkillSystem.StatusType.Bleeding: return "출혈";
            case SkillSystem.StatusType.Sleep: return "수면";
            case SkillSystem.StatusType.Confuse: return "혼란";
            case SkillSystem.StatusType.Petrify: return "석화";
            case SkillSystem.StatusType.Blind: return "실명";
            case SkillSystem.StatusType.Slow: return "둔화";
            case SkillSystem.StatusType.Weaken: return "약화";
            case SkillSystem.StatusType.Curse: return "저주";
            case SkillSystem.StatusType.Fear: return "공포";
            case SkillSystem.StatusType.Immortal: return "불사";
            case SkillSystem.StatusType.Stealth: return "은신";
            case SkillSystem.StatusType.TimeStop: return "시간정지";
            case SkillSystem.StatusType.SystemCrash: return "시스템마비";
            case SkillSystem.StatusType.BuffBlock: return "버프차단";
            case SkillSystem.StatusType.SkillSeal: return "스킬봉인";
            case SkillSystem.StatusType.HealBlock: return "치유차단";
            case SkillSystem.StatusType.Infection: return "감염";
            case SkillSystem.StatusType.Erosion: return "부식";
            case SkillSystem.StatusType.Mark: return "표식";
            default: return status.ToString();
        }
    }
}