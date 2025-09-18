// =====================================================
// SkillDatabase.cs
// 스킬 데이터베이스 관리 - Scripts/Battle/Skills/Core 폴더에 저장
// =====================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SkillSystem
{
    [System.Serializable]
    public class AdvancedSkillDatabase
    {
        public static SkillDatabaseAsset skillDatabaseAsset = null;

        [SerializeField]
        private List<AdvancedSkillData> skills = new List<AdvancedSkillData>();

        // 새로운 ID 관리 필드
        [SerializeField]
        private int nextActiveId = 10000;    // Active 스킬: 10000번대
        [SerializeField]
        private int nextPassiveId = 20000;   // Passive 스킬: 20000번대
        [SerializeField]
        private int nextSpecialActiveId = 30000;  // Special Active: 30000번대
        [SerializeField]
        private int nextSpecialPassiveId = 40000; // Special Passive: 40000번대
                                                
        // 사용중인 ID 캐시
        private HashSet<int> usedIds = new HashSet<int>();

        // BP 관계 캐시
        private Dictionary<int, List<AdvancedSkillData>> bpGroupCache = new Dictionary<int, List<AdvancedSkillData>>();


        private const string SAVE_PATH = "Assets/Cosmos/ResourcesAddressable/ScriptableObjects/Common/SkillDatabase.asset";

        // =====================================================
        // 데이터 접근
        // =====================================================

        public List<AdvancedSkillData> GetAllSkills()
        {
            return skills;
        }

        public AdvancedSkillData GetSkillById(int id)
        {
            return skills.FirstOrDefault(s => s.skillId == id);
        }

        public List<AdvancedSkillData> GetSkillsByClass(CharacterClass charClass)
        {
            return skills.Where(s => s.characterClass == charClass || s.characterClass == CharacterClass.All).ToList();
        }

        public List<AdvancedSkillData> GetSkillsByCategory(SkillCategory category)
        {
            return skills.Where(s => s.category == category).ToList();
        }

        public List<AdvancedSkillData> GetSkillsByTier(int tier)
        {
            return skills.Where(s => s.tier == tier).ToList();
        }

        public List<AdvancedSkillData> GetRecentSkills(int count)
        {
            return skills.OrderByDescending(s => s.lastModified).Take(count).ToList();
        }

        public List<AdvancedSkillData> SearchSkills(string searchText)
        {
            searchText = searchText.ToLower();
            return skills.Where(s =>
                s.skillName.ToLower().Contains(searchText) ||
                s.description.ToLower().Contains(searchText) ||
                s.tags.ToLower().Contains(searchText)
            ).ToList();
        }

        // =====================================================
        // 데이터 수정
        // =====================================================

        public void AddSkill(AdvancedSkillData skill)
        {
            if (skill.skillId == 0)
            {
                // 카테고리에 따라 적절한 ID 자동 할당
                skill.skillId = GetNextId(skill.category);
            }
            else
            {
                // 수동으로 설정한 ID가 중복인지 체크
                if (!IsIdAvailable(skill.skillId))
                {
                    Debug.LogWarning($"[SkillDatabase] ID {skill.skillId} already exists! Assigning new ID.");
                    skill.skillId = GetNextId(skill.category);
                }
            }

            skill.lastModified = DateTime.Now;
            skills.Add(skill);
            usedIds.Add(skill.skillId);
        }

        public void AddOrUpdateSkill(AdvancedSkillData skill)
        {
            skill.lastModified = DateTime.Now;

            var existing = skills.FirstOrDefault(s => s.skillId == skill.skillId);
            if (existing != null)
            {
                skills.Remove(existing);
            }
            else if (skill.skillId == 0)
            {
                // 새 스킬인 경우 ID 자동 할당
                skill.skillId = GetNextId(skill.category);
            }

            skills.Add(skill);
            usedIds.Add(skill.skillId);
        }

        public void RemoveSkill(AdvancedSkillData skill)
        {
            skills.Remove(skill);
            usedIds.Remove(skill.skillId);
        }

        public void RemoveSkillById(int id)
        {
            skills.RemoveAll(s => s.skillId == id);
            usedIds.Remove(id);
        }

        public int GetNextId()
        {
            // 기본값으로 Active ID 반환 (기존 호환성)
            return GetNextId(SkillCategory.Active);
        }
        // 새로운 오버로드 메서드 추가
        public int GetNextId(SkillCategory category)
        {
            // 사용중인 ID 목록 업데이트
            if (usedIds.Count == 0)
            {
                RefreshUsedIds();
            }

            int nextId = 0;

            switch (category)
            {
                case SkillCategory.Active:
                    nextId = GetNextAvailableId(ref nextActiveId, 10000, 19999);
                    break;

                case SkillCategory.Passive:
                    nextId = GetNextAvailableId(ref nextPassiveId, 20000, 29999);
                    break;

                case SkillCategory.SpecialActive:
                    nextId = GetNextAvailableId(ref nextSpecialActiveId, 30000, 39999);
                    break;

                case SkillCategory.SpecialPassive:
                    nextId = GetNextAvailableId(ref nextSpecialPassiveId, 40000, 49999);
                    break;

                default:
                    nextId = GetNextAvailableId(ref nextActiveId, 10000, 19999);
                    break;
            }

            usedIds.Add(nextId);
            return nextId;
        }

        // 사용 가능한 다음 ID 찾기
        private int GetNextAvailableId(ref int currentNext, int rangeMin, int rangeMax)
        {
            while (usedIds.Contains(currentNext) && currentNext <= rangeMax)
            {
                currentNext++;
            }

            // 범위 초과시 빈 ID 찾기
            if (currentNext > rangeMax)
            {
                for (int id = rangeMin; id <= rangeMax; id++)
                {
                    if (!usedIds.Contains(id))
                    {
                        currentNext = id + 1;
                        return id;
                    }
                }
                // 모든 ID가 사용중이면 범위 끝값 + 1 반환 (경고 상황)
                Debug.LogWarning($"[SkillDatabase] ID range {rangeMin}-{rangeMax} is full!");
                return rangeMax + 1;
            }

            int result = currentNext;
            currentNext++;
            return result;
        }

        // 사용중인 ID 목록 새로고침
        private void RefreshUsedIds()
        {
            usedIds.Clear();
            foreach (var skill in skills)
            {
                usedIds.Add(skill.skillId);
            }
        }

        // ID 중복 체크
        public bool IsIdAvailable(int id)
        {
            if (usedIds.Count == 0)
            {
                RefreshUsedIds();
            }
            return !usedIds.Contains(id);
        }
        // =====================================================
        // 통계
        // =====================================================

        public Dictionary<string, int> GetStatistics()
        {
            var stats = new Dictionary<string, int>();

            // 전체 수
            stats["Total"] = skills.Count;

            // 카테고리별
            foreach (SkillCategory category in Enum.GetValues(typeof(SkillCategory)))
            {
                if (category != SkillCategory.All)
                {
                    stats[$"Category_{category}"] = skills.Count(s => s.category == category);
                }
            }

            // 클래스별
            foreach (CharacterClass charClass in Enum.GetValues(typeof(CharacterClass)))
            {
                if (charClass != CharacterClass.All)
                {
                    stats[$"Class_{charClass}"] = skills.Count(s => s.characterClass == charClass);
                }
            }

            // 티어별
            for (int tier = 0; tier <= 5; tier++)
            {
                stats[$"Tier_{tier}"] = skills.Count(s => s.tier == tier);
            }

            return stats;
        }

        // =====================================================
        // 저장/로드
        // =====================================================

#if UNITY_EDITOR
        public static AdvancedSkillDatabase Load( bool reload = true )
        {
            if (reload == false)
            {
                if (skillDatabaseAsset != null)
                    return skillDatabaseAsset.database;
            }

            skillDatabaseAsset = AssetDatabase.LoadAssetAtPath<SkillDatabaseAsset>(SAVE_PATH);

            if (skillDatabaseAsset == null)
            {
                // 새 데이터베이스 생성
                skillDatabaseAsset = ScriptableObject.CreateInstance<SkillDatabaseAsset>();
                skillDatabaseAsset.database = new AdvancedSkillDatabase();

                // 디렉토리 생성
                string directory = System.IO.Path.GetDirectoryName(SAVE_PATH);
                if (!AssetDatabase.IsValidFolder(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }

                AssetDatabase.CreateAsset(skillDatabaseAsset, SAVE_PATH);
                AssetDatabase.SaveAssets();

                Debug.Log($"새 스킬 데이터베이스 생성: {SAVE_PATH}");
            }

            return skillDatabaseAsset.database;
        }

        public void Save()
        {
            var asset = AssetDatabase.LoadAssetAtPath<SkillDatabaseAsset>(SAVE_PATH);

            if (asset != null)
            {
                asset.database = this;
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();

                Debug.Log($"스킬 데이터베이스 저장 완료: {skills.Count}개 스킬");
            }
        }
#endif

        // =====================================================
        // Import/Export
        // =====================================================

        public string ExportToCSV(List<AdvancedSkillData> skillsToExport = null)
        {
            if (skillsToExport == null)
            {
                skillsToExport = skills;
            }

            var csv = new System.Text.StringBuilder();

            // 헤더 - BP 컬럼 추가
            csv.AppendLine("ID,이름,카테고리,클래스,티어,쿨다운,타겟1,효과1,타겟2,효과2,타겟3,효과3,BP레벨,부모ID");

            // 데이터
            foreach (var skill in skillsToExport)
            {
                csv.AppendLine(skill.ToCSV());
            }

            return csv.ToString();
        }

        public void ImportFromCSV(string csvContent)
        {
            var lines = csvContent.Split('\n');
            int importedCount = 0;
            int skippedCount = 0;


            // 첫 번째 패스: 모든 스킬 파싱
            var parsedSkills = new List<AdvancedSkillData>();

            for (int i = 1; i < lines.Length; i++) // 헤더 스킵
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;

                try
                {
                    var skill = ParseCSVLineWithBP(lines[i]);
                    if (skill != null)
                    {
                        parsedSkills.Add(skill);
                        importedCount++;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"CSV 파싱 오류 (라인 {i}): {e.Message}");
                    skippedCount++;
                }
            }

            // 두 번째 패스: BP 관계 설정
            ProcessBPRelationships(parsedSkills);

            // 데이터베이스에 추가
            foreach (var skill in parsedSkills)
            {
                AddOrUpdateSkill(skill);
            }

            Debug.Log($"CSV 임포트 완료: {importedCount}개 스킬 성공, {skippedCount}개 스킵");
            Debug.Log($"BP 그룹: {bpGroupCache.Count}개");
        }


        /*private AdvancedSkillData ParseCSVLineWithBP(string line)
        {
            var values = SplitCSVLine(line);
        
            if (values.Length < 14) // BP 정보 포함하여 14개 컬럼
            {
                Debug.LogWarning($"컬럼 수 부족: {values.Length}개 (필요: 14개)");
                return null;
            }

            var skill = new AdvancedSkillData();

            // 기본 정보 파싱
            // ID 파싱 (숫자 또는 숫자-문자 형식 지원)
            string idStr = values[0].Trim();
            if (idStr.Contains("-"))
            {
                // 1001-1, 1001-M 형식 처리
                var parts = idStr.Split('-');
                if (int.TryParse(parts[0], out int baseId))
                {
                    // BP 레벨에 따른 ID 생성
                    if (parts[1] == "M" || parts[1] == "MAX")
                        skill.skillId = baseId * 10 + 5; // MAX는 5 추가
                    else if (int.TryParse(parts[1], out int bpSuffix))
                        skill.skillId = baseId * 10 + bpSuffix;
                    else
                        skill.skillId = baseId * 10;
                }
            }
            else if (int.TryParse(idStr, out int id))
            {
                skill.skillId = id;
            }

            skill.skillName = values[1].Trim();

            // 카테고리 파싱
            string categoryStr = values[2].Trim();
            // 문자열 직접 비교 (대소문자 구분 없이)
            if (categoryStr.Equals("Passive", StringComparison.OrdinalIgnoreCase))
            {
                skill.category = SkillCategory.Passive;
            }
            else if (categoryStr.Equals("Active", StringComparison.OrdinalIgnoreCase))
            {
                skill.category = SkillCategory.Active;
            }
            else if (categoryStr.Equals("SpecialActive", StringComparison.OrdinalIgnoreCase))
            {
                skill.category = SkillCategory.SpecialActive;
            }
            else if (categoryStr.Equals("SpecialPassive", StringComparison.OrdinalIgnoreCase))
            {
                skill.category = SkillCategory.SpecialPassive;
            }
            else
            {
                // 기본값
                skill.category = SkillCategory.Active;
                Debug.LogWarning($"Unknown category: '{categoryStr}' for skill {skill.skillName}");
            }

            // 클래스 파싱
            skill.characterClass = ParseCharacterClass(values[3]);

            // 티어와 쿨다운
            if (int.TryParse(values[4], out int tier))
                skill.tier = tier;
            if (int.TryParse(values[5], out int cooldown))
                skill.cooldown = cooldown;

            // 효과 파싱 (최대 3개)
            for (int i = 0; i < 3; i++)
            {
                int targetIndex = 6 + (i * 2);
                int effectIndex = 7 + (i * 2);

                if (effectIndex < values.Length &&
                    !string.IsNullOrEmpty(values[effectIndex]) &&
                    values[effectIndex].Trim() != "X" &&
                    values[effectIndex].Trim() != "")
                {
                    var effect = ParseEffect(values[effectIndex], values[targetIndex]);
                    if (effect != null)
                    {
                        skill.effects.Add(effect);
                    }
                }
            }


            // 패시브 스킬인 경우 첫 번째 효과에서 트리거 조건 추출
            if (skill.category == SkillCategory.Passive ||
                skill.category == SkillCategory.SpecialPassive)
            {
                if (skill.effects.Count > 0)
                {
                    // 첫 번째 효과 텍스트에서 트리거 파싱
                    string firstEffectText = values[7].Trim(); // 첫 번째 효과 컬럼
                    ParsePassiveTrigger(skill, firstEffectText);
                }

                // 패시브는 기본적으로 쿨다운 0
                skill.cooldown = 0;
            }


            // BP 정보 파싱
            if (values.Length > 12)
            {
                if (int.TryParse(values[12], out int bpLevel))
                    skill.bpLevel = bpLevel;
            }

            if (values.Length > 13)
            {
                if (int.TryParse(values[13], out int parentId))
                    skill.parentSkillId = parentId;
            }

            return skill;
        }*/

        private AdvancedSkillData ParseCSVLineWithBP(string line)
        {
            var values = SplitCSVLine(line);

            // 22개 컬럼 (기본 5개 + 효과당 5개 * 3 + BP 2개)
            if (values.Length < 22)
            {
                Debug.LogWarning($"컬럼 수 부족: {values.Length}개 (필요: 22개)");
                return null;
            }

            var skill = new AdvancedSkillData();

            try
            {
                // 기본 정보 (0-4)
                skill.skillId = int.Parse(values[0]);
                skill.skillName = values[1];
                skill.category = values[2].ToLower() == "passive" ?
                    SkillCategory.Passive : SkillCategory.Active;
                skill.description = values[3];
                skill.cooldown = int.Parse(values[4]);

                // 클래스와 티어는 기본값
                skill.characterClass = CharacterClass.All;
                skill.tier = 1;

                // 효과 파싱 (5-19: 3개 효과)
                for (int i = 0; i < 3; i++)
                {
                    int baseIndex = 5 + (i * 5);

                    string effectType = values[baseIndex];
                    string targetType = values[baseIndex + 1];

                    if (string.IsNullOrEmpty(effectType) || effectType == "None")
                        continue;

                    var effect = new AdvancedSkillEffect();

                    if (!ParseEffectType(effectType, ref effect))
                        continue;

                    effect.targetType = ParseTargetTypeSimple(targetType);
                    effect.value = float.Parse(values[baseIndex + 2]);
                    effect.duration = int.Parse(values[baseIndex + 3]);

                    float chance = float.Parse(values[baseIndex + 4]);
                    effect.chance = chance > 1 ? chance / 100f : chance;

                    effect.name = GenerateEffectName(effectType, effect.value);

                    skill.effects.Add(effect);
                }

                // BP 정보 파싱 (20-21)
                if (values.Length > 20)
                {
                    skill.bpLevel = int.Parse(values[20]);
                    skill.parentSkillId = int.Parse(values[21]);
                }

                return skill;
            }
            catch (Exception e)
            {
                Debug.LogError($"스킬 파싱 실패 [{skill.skillName}]: {e.Message}");
                return null;
            }
        }


        // 간단한 효과 타입 파싱
        // 간단한 효과 타입 파싱
        private bool ParseEffectType(string typeStr, ref AdvancedSkillEffect effect)
        {
            switch (typeStr.ToLower())
            {
                // 기본 효과들
                case "damage":
                    effect.type = EffectType.Damage;
                    effect.damageType = DamageType.Physical;
                    return true;

                case "heal":
                    effect.type = EffectType.Heal;
                    return true;

                // 특수 치유 - Heal로 처리하고 specialEffect에 저장
                case "heallowhp":
                case "heallowesthp":
                    effect.type = EffectType.Heal;
                    effect.specialEffect = "TargetLowestHP";
                    return true;

                case "shield":
                    effect.type = EffectType.Shield;
                    return true;

                case "buff":
                    effect.type = EffectType.Buff;
                    effect.statType = StatType.Attack; // 기본값
                    return true;

                case "debuff":
                    effect.type = EffectType.Debuff;
                    effect.statType = StatType.Attack;
                    return true;

                case "taunt":
                    effect.type = EffectType.StatusEffect;
                    effect.statusType = StatusType.Taunt;
                    return true;

                case "regen":
                    effect.type = EffectType.HealOverTime;
                    return true;

                case "counter":
                case "counterattack":
                    effect.type = EffectType.Counter;
                    return true;

                case "extraattack":
                    effect.type = EffectType.ExtraAttack;
                    effect.extraAttackCount = 1;  // 기본값
                    effect.extraAttackDamageModifier = 0.5f;  // 50% 데미지
                    return true;

                case "conditionaldamage":
                    effect.type = EffectType.Damage;
                    effect.hasCondition = true;
                    effect.conditionType = ConditionType.StatusEffect;
                    return true;

                case "conditionalbuff":
                    effect.type = EffectType.Buff;
                    effect.hasCondition = true;
                    effect.conditionType = ConditionType.HPBelow;
                    effect.conditionValue = 50;
                    effect.statType = StatType.CritRate; // 치명타율을 기본으로
                    return true;

                case "conditionalheal":
                    effect.type = EffectType.Heal;
                    effect.hasCondition = true;
                    effect.conditionType = ConditionType.HPBelow;
                    effect.conditionValue = 20;
                    return true;

                case "removedebuff":
                case "dispel":
                    effect.type = EffectType.Dispel;
                    effect.dispelCount = 1; // value로 덮어씌워짐
                    return true;

                case "periodicbuff":
                    effect.type = EffectType.Buff;
                    effect.specialEffect = "Periodic3Turn"; // 3턴마다 발동
                    effect.statType = StatType.Attack;
                    return true;

                case "critnullify":
                    effect.type = EffectType.Special;
                    effect.specialEffect = "CritNullify";
                    return true;

                case "killbuff":
                case "onkillbuff":
                    effect.type = EffectType.Special;
                    effect.specialEffect = "OnKillBuff";
                    return true;

                case "textdisplay":
                    effect.type = EffectType.Special;
                    effect.specialEffect = "TextDisplay";
                    return true;

                case "skilldisrupt":
                case "skillfail":
                    effect.type = EffectType.Special;
                    effect.specialEffect = "SkillDisrupt";
                    return true;

                case "damagereduction":
                    effect.type = EffectType.DamageReduction;
                    return true;

                case "none":

                case "":
                    return false; // 빈 효과는 false 반환

                default:
                    Debug.LogWarning($"Unknown effect type: {typeStr}");
                    return false;
            }
        }


        // 간단한 타겟 타입 파싱
        private TargetType ParseTargetTypeSimple(string targetStr)
        {
            switch (targetStr.ToLower())
            {
                case "self":
                    return TargetType.Self;
                case "singleenemy":
                    return TargetType.EnemySingle;
                case "allenemies":
                    return TargetType.EnemyAll;
                case "ally":
                case "singleally":
                    return TargetType.AllySingle;
                case "allallies":
                    return TargetType.AllyAll;
                case "any":
                    return TargetType.Random;
                default:
                    return TargetType.EnemySingle;
            }
        }

        // 효과 이름 자동 생성
        private string GenerateEffectName(string effectType, float value)
        {
            return $"{effectType} ({value:0}%)";
        }
        // =====================================================
        // BP 관계 처리
        // =====================================================

        private void ProcessBPRelationships(List<AdvancedSkillData> skills)
        {
            // BP 그룹 캐시 초기화
            bpGroupCache.Clear();

            // 베이스 스킬 찾기 (parentSkillId == 0)
            var baseSkills = skills.Where(s => s.parentSkillId == 0 && s.bpLevel == 0).ToList();

            foreach (var baseSkill in baseSkills)
            {
                var group = new List<AdvancedSkillData> { baseSkill };

                // 이 베이스 스킬의 모든 업그레이드 찾기
                var upgrades = skills.Where(s => s.parentSkillId == baseSkill.skillId)
                                    .OrderBy(s => s.bpLevel)
                                    .ToList();

                foreach (var upgrade in upgrades)
                {
                    upgrade.baseSkill = baseSkill;
                    baseSkill.bpUpgrades[upgrade.bpLevel] = upgrade;
                    group.Add(upgrade);
                }

                // 캐시에 저장
                bpGroupCache[baseSkill.skillId] = group;

                Debug.Log($"BP 그룹 생성: {baseSkill.skillName} - {group.Count}개 레벨");
            }
        }

        // =====================================================
        // BP 스킬 조회 메서드
        // =====================================================

        public AdvancedSkillData GetSkillByBPLevel(int baseSkillId, int bpLevel)
        {
            var baseSkill = GetSkillById(baseSkillId);
            if (baseSkill == null) return null;

            if (bpLevel == 0) return baseSkill;

            if (baseSkill.bpUpgrades.TryGetValue(bpLevel, out var upgrade))
            {
                return upgrade;
            }

            return null;
        }

        public List<AdvancedSkillData> GetAllBPVersions(int baseSkillId)
        {
            if (bpGroupCache.TryGetValue(baseSkillId, out var group))
            {
                return group;
            }

            var baseSkill = GetSkillById(baseSkillId);
            if (baseSkill != null)
            {
                return new List<AdvancedSkillData> { baseSkill };
            }

            return new List<AdvancedSkillData>();
        }

        public AdvancedSkillData GetAppropriateSkillForBP(int baseSkillId, int currentBP)
        {
            // 현재 BP에 맞는 적절한 스킬 반환
            if (currentBP >= 5) return GetSkillByBPLevel(baseSkillId, 5); // MAX
            if (currentBP >= 3) return GetSkillByBPLevel(baseSkillId, 3); // Upgrade2
            if (currentBP >= 1) return GetSkillByBPLevel(baseSkillId, 1); // Upgrade1
            return GetSkillByBPLevel(baseSkillId, 0); // Base
        }

        // =====================================================
        // 헬퍼 메서드
        // =====================================================

        private new string[] SplitCSVLine(string line)
        {
            var result = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            result.Add(current.ToString().Trim());
            return result.ToArray();
        }

        // 기존 파싱 메서드들은 그대로 사용
        private CharacterClass ParseCharacterClass(string value)
        {
            if (value.Contains("Vanguard") || value.Contains("뱅가드")) return CharacterClass.Vanguard;
            if (value.Contains("Slaughter") || value.Contains("슬로터")) return CharacterClass.Slaughter;
            if (value.Contains("Jacker") || value.Contains("재커")) return CharacterClass.Jacker;
            if (value.Contains("Rewinder") || value.Contains("리와인더")) return CharacterClass.Rewinder;
            return CharacterClass.All;
        }

        private void ParsePassiveTrigger(AdvancedSkillData skill, string effectText)
        {
            // 패시브만 처리
            if (skill.category != SkillCategory.Passive &&
                skill.category != SkillCategory.SpecialPassive)
                return;

            // 트리거 조건 파싱
            if (effectText.Contains("매턴") || effectText.Contains("매 턴"))
            {
                skill.triggerType = TriggerType.OnTurnStart;
            }
            else if (effectText.Contains("전투시작") || effectText.Contains("전투 시작"))
            {
                skill.triggerType = TriggerType.OnBattleStart;
            }
            else if (effectText.Contains("공격시") || effectText.Contains("공격 시"))
            {
                skill.triggerType = TriggerType.OnAttack;
            }
            else if (effectText.Contains("피격시") || effectText.Contains("피격 시"))
            {
                skill.triggerType = TriggerType.OnDamaged;
            }
            else if (effectText.Contains("처치시") || effectText.Contains("처치 시"))
            {
                skill.triggerType = TriggerType.OnKill;
            }
            else if (effectText.Contains("사망시") || effectText.Contains("사망 시"))
            {
                skill.triggerType = TriggerType.OnDeath;
            }
            else if (effectText.Contains("크리티컬시") || effectText.Contains("치명타시"))
            {
                skill.triggerType = TriggerType.OnCrit;
            }
            else if (effectText.Contains("회피시") || effectText.Contains("회피성공시"))
            {
                skill.triggerType = TriggerType.OnEvade;
            }
            else if (effectText.Contains("턴시작") || effectText.Contains("턴 시작"))
            {
                skill.triggerType = TriggerType.OnTurnStart;
            }
            else if (effectText.Contains("턴종료") || effectText.Contains("턴 종료"))
            {
                skill.triggerType = TriggerType.OnTurnEnd;
            }
            else if (effectText.Contains("HP") && (effectText.Contains("이하") || effectText.Contains("이상")))
            {
                skill.triggerType = TriggerType.Conditional;
                ParseTriggerCondition(skill, effectText);
            }
            else
            {
                skill.triggerType = TriggerType.Always;
            }

            // 트리거 확률 파싱
            var chanceMatch = System.Text.RegularExpressions.Regex.Match(effectText, @"(\d+)%\s*확률");
            if (chanceMatch.Success)
            {
                skill.triggerChance = float.Parse(chanceMatch.Groups[1].Value) / 100f;
            }
            else
            {
                skill.triggerChance = 1f;
            }
        }

        private void ParseTriggerCondition(AdvancedSkillData skill, string effectText)
        {
            // HP 조건
            if (effectText.Contains("HP"))
            {
                var hpMatch = System.Text.RegularExpressions.Regex.Match(effectText, @"HP\s*(\d+)%\s*(이하|이상)");
                if (hpMatch.Success)
                {
                    string value = hpMatch.Groups[1].Value;
                    string condition = hpMatch.Groups[2].Value;

                    if (condition == "이하")
                        skill.triggerCondition = $"HP < {value}";
                    else
                        skill.triggerCondition = $"HP > {value}";
                }
            }

            // 턴 조건
            if (effectText.Contains("홀수턴"))
            {
                skill.triggerCondition = "Turn % 2 == 1";
            }
            else if (effectText.Contains("짝수턴"))
            {
                skill.triggerCondition = "Turn % 2 == 0";
            }

            // 기타 조건들...
        }


        private AdvancedSkillData ParseCSVLine(string line)
        {
            var values = SplitCSVLine(line);
            if (values.Length < 6) return null;

            var skill = new AdvancedSkillData();

            // 기본 정보 파싱
            if (int.TryParse(values[0], out int id))
                skill.skillId = id;

            skill.skillName = values[1];

            // 카테고리 파싱
            if (values[2].Contains("스페셜"))
            {
                skill.category = values[2].Contains("패시브") ?
                    SkillCategory.SpecialPassive : SkillCategory.SpecialActive;
            }
            else
            {
                skill.category = values[2].Contains("패시브") ?
                    SkillCategory.Passive : SkillCategory.Active;
            }

            // 클래스 파싱
            skill.characterClass = ParseCharacterClass(values[3]);

            // 티어
            if (int.TryParse(values[4], out int tier))
                skill.tier = tier;

            // 쿨다운 파싱 (이전 Mana 위치 활용 또는 티어 기반 자동 설정)
            if (values.Length > 5 && int.TryParse(values[5], out int cooldown))
            {
                skill.cooldown = cooldown;
            }
            else
            {
                // CSV에 쿨다운 정보가 없으면 티어와 카테고리 기반으로 자동 설정
                skill.cooldown = SkillCooldownDefaults.GetDefaultCooldown(skill.tier, skill.category);
            }

            // 효과 파싱 (최대 3개)
            for (int i = 0; i < 3; i++)
            {
                int targetIndex = 6 + (i * 2);
                int effectIndex = 7 + (i * 2);

                if (effectIndex < values.Length &&
                    !string.IsNullOrEmpty(values[effectIndex]) &&
                    values[effectIndex] != "X")
                {
                    var effect = ParseEffect(values[effectIndex], values[targetIndex]);
                    if (effect != null)
                    {
                        skill.effects.Add(effect);
                    }
                }
            }

            return skill;
        }



        private AdvancedSkillEffect ParseEffect(string effectText, string targetText)
        {
            var effect = new AdvancedSkillEffect();

            // 타겟 파싱
            effect.targetType = ParseTargetType(targetText);

            // None/X/빈값 체크
            if (string.IsNullOrEmpty(effectText) || effectText == "X" || effectText == "None")
                return null;

            // 확률 파싱 (먼저 처리)
            var chanceMatch = System.Text.RegularExpressions.Regex.Match(effectText, @"(\d+)%\s*확률");
            if (chanceMatch.Success)
            {
                effect.chance = float.Parse(chanceMatch.Groups[1].Value) / 100f;
            }
            else
            {
                effect.chance = 1f;
            }

            // 지속시간 파싱
            var durationMatch = System.Text.RegularExpressions.Regex.Match(effectText, @"(\d+)턴");
            if (durationMatch.Success)
            {
                effect.duration = int.Parse(durationMatch.Groups[1].Value);
            }

            // "영구" 키워드
            if (effectText.Contains("영구"))
            {
                effect.duration = 999;
            }

            // 수치 파싱 (% 포함)
            var valueMatch = System.Text.RegularExpressions.Regex.Match(effectText, @"(\d+(?:\.\d+)?)%?");
            if (valueMatch.Success)
            {
                effect.value = float.Parse(valueMatch.Groups[1].Value.Replace("%", ""));
            }

            // ==================== 효과 타입 결정 ====================

            // 데미지 계열
            if (effectText.Contains("데미지"))
            {
                if (effectText.Contains("고정") && effectText.Contains("방어무시"))
                {
                    effect.type = EffectType.TrueDamage;
                }
                else if (effectText.Contains("고정"))
                {
                    effect.type = EffectType.FixedDamage;
                }
                else if (effectText.Contains("방어무시"))
                {
                    effect.type = EffectType.TrueDamage;
                }
                else
                {
                    effect.type = EffectType.Damage;
                }
            }
            // 즉사
            else if (effectText.Contains("즉사"))
            {
                effect.type = EffectType.Execute;
                // HP 임계값 파싱
                var hpMatch = System.Text.RegularExpressions.Regex.Match(effectText, @"HP\s*(\d+)%");
                if (hpMatch.Success)
                {
                    effect.value = float.Parse(hpMatch.Groups[1].Value);
                }
            }
            // 흡혈/흡수
            else if (effectText.Contains("흡혈") || effectText.Contains("흡수"))
            {
                effect.type = EffectType.LifeSteal;
            }
            // 회복 계열
            else if (effectText.Contains("회복") || effectText.Contains("치유"))
            {
                if (effectText.Contains("재생") || effectText.Contains("매턴"))
                {
                    effect.type = EffectType.HealOverTime;
                }
                else
                {
                    effect.type = EffectType.Heal;
                }
            }
            // 보호막/실드
            else if (effectText.Contains("보호막") || effectText.Contains("실드") || effectText.Contains("방어막"))
            {
                effect.type = EffectType.Shield;
            }
            // 반사
            else if (effectText.Contains("반사"))
            {
                effect.type = EffectType.Reflect;
            }
            // 반격
            else if (effectText.Contains("반격"))
            {
                effect.type = EffectType.Special;
                effect.specialEffect = "Counter";
            }
            // 피해 감소
            else if (effectText.Contains("받는") && effectText.Contains("피해") && (effectText.Contains("-") || effectText.Contains("감소")))
            {
                effect.type = EffectType.DamageReduction;
            }
            // 면역
            else if (effectText.Contains("면역"))
            {
                effect.type = EffectType.Immunity;
            }
            // 무적
            else if (effectText.Contains("무적"))
            {
                effect.type = EffectType.Invincible;
            }
            // 해제/정화
            else if (effectText.Contains("해제") || effectText.Contains("제거") || effectText.Contains("정화"))
            {
                effect.type = EffectType.Dispel;
                effect.dispelCount = (int)ExtractNumber(effectText, 1);

                if (effectText.Contains("모든") || effectText.Contains("전체"))
                    effect.dispelCount = 99;

                if (effectText.Contains("버프"))
                    effect.dispelType = DispelType.Buff;
                else if (effectText.Contains("디버프"))
                    effect.dispelType = DispelType.Debuff;
                else
                    effect.dispelType = DispelType.All;
            }
            // 분신
            else if (effectText.Contains("분신"))
            {
                effect.type = EffectType.Clone;
                var cloneMatch = System.Text.RegularExpressions.Regex.Match(effectText, @"(\d+)%\s*스탯");
                if (cloneMatch.Success)
                {
                    effect.value = float.Parse(cloneMatch.Groups[1].Value);
                }
            }
            // 변신/각성
            else if (effectText.Contains("변신") || effectText.Contains("각성"))
            {
                if (effectText.Contains("영구"))
                {
                    effect.type = EffectType.Metamorphosis;
                }
                else
                {
                    effect.type = EffectType.Transform;
                }
            }
            // 시간 조작
            else if (effectText.Contains("시간정지"))
            {
                effect.type = EffectType.TimeStop;
            }
            else if (effectText.Contains("시간가속") || effectText.Contains("즉시") && effectText.Contains("행동"))
            {
                effect.type = EffectType.TimeAccelerate;
            }
            else if (effectText.Contains("시간") && (effectText.Contains("되돌") || effectText.Contains("회귀") || effectText.Contains("복구")))
            {
                effect.type = EffectType.TimeRewind;
            }
            else if (effectText.Contains("패러독스") || effectText.Contains("2배 행동"))
            {
                effect.type = EffectType.Paradox;
            }
            // 추가턴/추가행동
            else if (effectText.Contains("추가턴") || effectText.Contains("추가공격") || effectText.Contains("추가행동"))
            {
                effect.type = EffectType.ExtraTurn;
            }
            // 쿨다운
            else if (effectText.Contains("쿨다운") || effectText.Contains("스킬") && effectText.Contains("초기화"))
            {
                effect.type = EffectType.CooldownReset;
            }
            // 정신지배/조종
            else if (effectText.Contains("정신지배") || effectText.Contains("조종"))
            {
                effect.type = EffectType.MindControl;
            }
            // 위치 관련
            else if (effectText.Contains("위치") && (effectText.Contains("셔플") || effectText.Contains("교환")))
            {
                effect.type = EffectType.PositionSwap;
            }
            else if (effectText.Contains("추방") || effectText.Contains("차원"))
            {
                effect.type = EffectType.Banish;
            }
            // 연쇄/확산
            else if (effectText.Contains("연쇄"))
            {
                effect.type = EffectType.ChainAttack;
                var chainMatch = System.Text.RegularExpressions.Regex.Match(effectText, @"(\d+)회");
                if (chainMatch.Success)
                {
                    effect.targetCount = int.Parse(chainMatch.Groups[1].Value);
                }
            }
            else if (effectText.Contains("전염") || effectText.Contains("확산"))
            {
                effect.type = EffectType.Spread;
            }
            else if (effectText.Contains("관통"))
            {
                effect.type = EffectType.Penetrate;
            }
            // 버프 훔치기/복사
            else if (effectText.Contains("훔치"))
            {
                effect.type = EffectType.StealBuff;
                effect.dispelCount = (int)ExtractNumber(effectText, 1);
            }
            else if (effectText.Contains("복사"))
            {
                effect.type = EffectType.CopyBuff;
                effect.dispelCount = (int)ExtractNumber(effectText, 1);
            }
            // 피해 공유/분담
            else if (effectText.Contains("피해") && (effectText.Contains("분담") || effectText.Contains("공유")))
            {
                effect.type = EffectType.ShareDamage;
            }
            // 랜덤
            else if (effectText.Contains("랜덤"))
            {
                effect.type = EffectType.RandomEffect;
            }
            // 상태이상
            else if (ParseStatusEffect(effectText, ref effect))
            {
                effect.type = EffectType.StatusEffect;
            }
            // 버프/디버프
            else if (ParseBuffDebuff(effectText, ref effect))
            {
                // type과 statType이 ParseBuffDebuff에서 설정됨
            }
            // 특수 효과
            else
            {
                effect.type = EffectType.Special;
                effect.specialEffect = effectText;
            }

            return effect;
        }


        // 상태이상 파싱 헬퍼
        private bool ParseStatusEffect(string text, ref AdvancedSkillEffect effect)
        {
            // 기절/마비
            if (text.Contains("기절"))
            {
                effect.statusType = StatusType.Stun;
                return true;
            }
            if (text.Contains("마비") || text.Contains("감전"))
            {
                effect.statusType = StatusType.Paralyze;
                return true;
            }
            // 침묵/봉인
            if (text.Contains("침묵"))
            {
                effect.statusType = StatusType.Silence;
                return true;
            }
            if (text.Contains("스킬봉인"))
            {
                effect.statusType = StatusType.SkillSeal;
                return true;
            }
            // 속박/제압
            if (text.Contains("속박"))
            {
                effect.statusType = StatusType.Bind;
                return true;
            }
            if (text.Contains("제압"))
            {
                effect.statusType = StatusType.Suppress;
                return true;
            }
            // 도발/공포
            if (text.Contains("도발"))
            {
                effect.statusType = StatusType.Taunt;
                return true;
            }
            if (text.Contains("공포"))
            {
                effect.statusType = StatusType.Fear;
                return true;
            }
            // 동결
            if (text.Contains("동결") || text.Contains("빙결"))
            {
                effect.statusType = StatusType.Freeze;
                return true;
            }
            // DoT 효과들
            if (text.Contains("화상") || text.Contains("불꽃"))
            {
                effect.statusType = StatusType.Burn;
                effect.value = ExtractNumber(text, 10);
                return true;
            }
            if (text.Contains("중독") || text.Contains("독"))
            {
                effect.statusType = StatusType.Poison;
                effect.value = ExtractNumber(text, 10);
                return true;
            }
            if (text.Contains("출혈"))
            {
                effect.statusType = StatusType.Bleed;
                effect.value = ExtractNumber(text, 10);
                return true;
            }
            if (text.Contains("내상"))
            {
                effect.statusType = StatusType.InternalInjury;
                return true;
            }
            if (text.Contains("감염"))
            {
                effect.statusType = StatusType.Infection;
                effect.value = ExtractNumber(text, 10);
                return true;
            }
            if (text.Contains("부식"))
            {
                effect.statusType = StatusType.Erosion;
                return true;
            }
            // 기타 상태이상
            if (text.Contains("수면"))
            {
                effect.statusType = StatusType.Sleep;
                return true;
            }
            if (text.Contains("혼란"))
            {
                effect.statusType = StatusType.Confuse;
                return true;
            }
            if (text.Contains("석화"))
            {
                effect.statusType = StatusType.Petrify;
                return true;
            }
            if (text.Contains("실명"))
            {
                effect.statusType = StatusType.Blind;
                return true;
            }
            if (text.Contains("둔화") || text.Contains("속도감소"))
            {
                effect.statusType = StatusType.Slow;
                return true;
            }
            if (text.Contains("약화"))
            {
                effect.statusType = StatusType.Weaken;
                return true;
            }
            if (text.Contains("저주"))
            {
                effect.statusType = StatusType.Curse;
                return true;
            }
            // 특수 상태
            if (text.Contains("은신"))
            {
                effect.statusType = StatusType.Stealth;
                return true;
            }
            if (text.Contains("부활"))
            {
                effect.statusType = StatusType.Resurrect;
                return true;
            }
            if (text.Contains("표식") || text.Contains("마크") || text.Contains("링크"))
            {
                effect.statusType = StatusType.Mark;
                return true;
            }
            if (text.Contains("시스템") && (text.Contains("마비") || text.Contains("다운")))
            {
                effect.statusType = StatusType.SystemCrash;
                return true;
            }
            if (text.Contains("버프") && (text.Contains("금지") || text.Contains("차단")))
            {
                effect.statusType = StatusType.BuffBlock;
                return true;
            }
            if (text.Contains("치유불가") || text.Contains("회복불가") || text.Contains("치유차단"))
            {
                effect.statusType = StatusType.HealBlock;
                return true;
            }

            return false;
        }

        // 버프/디버프 파싱 헬퍼
        private bool ParseBuffDebuff(string text, ref AdvancedSkillEffect effect)
        {
            bool isBuff = false;
            bool isDebuff = false;

            // 증가/감소 판단
            if (text.Contains("+") || text.Contains("증가") || text.Contains("상승") || text.Contains("강화"))
            {
                isBuff = true;
                effect.type = EffectType.Buff;
            }
            else if (text.Contains("-") || text.Contains("감소") || text.Contains("하락") || text.Contains("약화"))
            {
                isDebuff = true;
                effect.type = EffectType.Debuff;
            }

            if (!isBuff && !isDebuff)
                return false;

            // 스탯 타입 결정
            if (text.Contains("공격"))
            {
                effect.statType = StatType.Attack;
            }
            else if (text.Contains("방어"))
            {
                effect.statType = StatType.Defense;
            }
            else if (text.Contains("속도"))
            {
                effect.statType = StatType.Speed;
            }
            else if (text.Contains("치명타율") || text.Contains("크리티컬") || text.Contains("크리율"))
            {
                effect.statType = StatType.CritRate;
            }
            else if (text.Contains("치명타") && text.Contains("피해"))
            {
                effect.statType = StatType.CritDamage;
            }
            else if (text.Contains("명중"))
            {
                effect.statType = StatType.Accuracy;
            }
            else if (text.Contains("회피"))
            {
                effect.statType = StatType.Evasion;
            }
            else if (text.Contains("효과") && text.Contains("적중"))
            {
                effect.statType = StatType.EffectHit;
            }
            else if (text.Contains("효과") && text.Contains("저항"))
            {
                effect.statType = StatType.EffectResist;
            }
            else if (text.Contains("모든") && text.Contains("스탯"))
            {
                // 모든 스탯 처리 - 특수 효과로
                effect.specialEffect = "AllStats";
                effect.statType = StatType.Attack; // 임시
            }

            return true;
        }


        // 타겟 타입 파싱
        private TargetType ParseTargetType(string value)
        {
            // 특수 타겟
            if (value.Contains("가장 높은")) return TargetType.Highest;
            if (value.Contains("가장 낮은")) return TargetType.Lowest;
            if (value.Contains("무작위") || value.Contains("랜덤")) return TargetType.Random;
            if (value.Contains("다수")) return TargetType.Multiple;
            if (value.Contains("인접")) return TargetType.Adjacent;
            if (value.Contains("직선")) return TargetType.Line;
            if (value.Contains("십자")) return TargetType.Cross;
            if (value.Contains("부채꼴")) return TargetType.Fan;
            if (value.Contains("후열")) return TargetType.BackRow;
            if (value.Contains("전열")) return TargetType.FrontRow;
            if (value.Contains("연쇄")) return TargetType.Chain;
            if (value.Contains("관통")) return TargetType.Pierce;

            // 기본 타겟
            if (value.Contains("자신")) return TargetType.Self;
            if (value.Contains("적 전/후열")) return TargetType.EnemyRow;
            if (value.Contains("적") && value.Contains("단일")) return TargetType.EnemySingle;
            if (value.Contains("적") && value.Contains("전체")) return TargetType.EnemyAll;
            if (value.Contains("아군 전/후열")) return TargetType.AllyRow;
            if (value.Contains("아군") && value.Contains("단일")) return TargetType.AllySingle;
            if (value.Contains("아군") && value.Contains("전체")) return TargetType.AllyAll;

            return TargetType.EnemySingle; // 기본값
        }

        // 숫자 추출 헬퍼
        private float ExtractNumber(string text, float defaultValue = 0)
        {
            var match = System.Text.RegularExpressions.Regex.Match(text, @"(\d+(?:\.\d+)?)");
            if (match.Success)
            {
                return float.Parse(match.Groups[1].Value);
            }
            return defaultValue;
        }


        private StatType ParseStatType(string text)
        {
            if (text.Contains("공격")) return StatType.Attack;
            if (text.Contains("방어")) return StatType.Defense;
            if (text.Contains("속도")) return StatType.Speed;
            if (text.Contains("치명") && text.Contains("확률")) return StatType.CritRate;
            if (text.Contains("치명") && text.Contains("피해")) return StatType.CritDamage;
            if (text.Contains("적중")) return StatType.Accuracy;
            if (text.Contains("회피")) return StatType.Evasion;
            if (text.Contains("효과") && text.Contains("저항")) return StatType.EffectResist;
            if (text.Contains("효과") && text.Contains("적중")) return StatType.EffectHit;
            return StatType.Attack;
        }

        private int ExtractDuration(string text)
        {
            var match = System.Text.RegularExpressions.Regex.Match(text, @"(\d+)턴");
            if (match.Success)
            {
                return int.Parse(match.Groups[1].Value);
            }
            return 0;
        }
    }

 
}