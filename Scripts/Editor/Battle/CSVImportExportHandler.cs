using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using BattleCharacterSystem;
using CharacterSystem;

namespace BattleCharacterSystem
{
    /// <summary>
    /// CSV Import/Export 처리 클래스
    /// </summary>
    public static class CSVImportExportHandler
    {
        // CSV 헤더 정의
        private static readonly string[] CHARACTER_CSV_HEADERS = new string[]
        {
            "ID", "Name", "Description", "Tier", "Class", "Element", "AttackType",
            "HP", "Attack", "Defense", "HPGrowth", "AttackGrowth", "DefenseGrowth",
            "MaxBP", "Speed", "CritRate", "CritDamage", "HitRate", "DodgeRate", "BlockRate",
            "Aggro", "SkillHitRate", "SkillResist", "ActiveSkillID", "PassiveSkillID",
            "FormationPreference", "PrefabPath", "ResourceName"
        };

        /// <summary>
        /// 캐릭터 데이터를 CSV로 내보내기
        /// </summary>
        public static void ExportCharactersToCSV(List<BattleCharacterDataSO> characters)
        {
            if (characters == null || characters.Count == 0)
            {
                EditorUtility.DisplayDialog("Export Failed", "No characters to export.", "OK");
                return;
            }

            string path = EditorUtility.SaveFilePanel(
                "Export Characters to CSV",
                Application.dataPath,
                "CharacterData_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"),
                "csv"
            );

            if (string.IsNullOrEmpty(path)) return;

            try
            {
                StringBuilder csv = new StringBuilder();

                // 헤더 추가
                csv.AppendLine(string.Join(",", CHARACTER_CSV_HEADERS));

                // 데이터 추가
                foreach (var character in characters)
                {
                    if (character == null) continue;
                    csv.AppendLine(CharacterToCSVLine(character));
                }

                // 파일 저장
                File.WriteAllText(path, csv.ToString(), Encoding.UTF8);

                EditorUtility.DisplayDialog("Export Successful",
                    $"Exported {characters.Count} characters to:\n{path}", "OK");

                // 파일 탐색기에서 열기
                EditorUtility.RevealInFinder(path);
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Export Failed",
                    $"Failed to export CSV:\n{e.Message}", "OK");
            }
        }

        /// <summary>
        /// CSV에서 캐릭터 데이터 가져오기
        /// </summary>
        public static List<BattleCharacterDataSO> ImportCharactersFromCSV()
        {
            string path = EditorUtility.OpenFilePanel(
                "Import Characters from CSV",
                Application.dataPath,
                "csv"
            );

            if (string.IsNullOrEmpty(path)) return null;

            List<BattleCharacterDataSO> importedCharacters = new List<BattleCharacterDataSO>();

            try
            {
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);

                if (lines.Length < 2)
                {
                    EditorUtility.DisplayDialog("Import Failed",
                        "CSV file is empty or invalid.", "OK");
                    return null;
                }

                // 헤더 검증
                string[] headers = ParseCSVLine(lines[0]);
                if (!ValidateHeaders(headers))
                {
                    EditorUtility.DisplayDialog("Import Failed",
                        "CSV headers do not match expected format.", "OK");
                    return null;
                }

                int successCount = 0;
                int failCount = 0;
                List<string> errors = new List<string>();

                // 데이터 파싱
                for (int i = 1; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i])) continue;

                    try
                    {
                        var character = ParseCharacterFromCSV(lines[i]);
                        if (character != null)
                        {
                            importedCharacters.Add(character);
                            successCount++;
                        }
                    }
                    catch (Exception lineError)
                    {
                        failCount++;
                        errors.Add($"Line {i + 1}: {lineError.Message}");
                    }
                }

                // 결과 표시
                StringBuilder resultMessage = new StringBuilder();
                resultMessage.AppendLine($"Import completed!");
                resultMessage.AppendLine($"Success: {successCount} characters");

                if (failCount > 0)
                {
                    resultMessage.AppendLine($"Failed: {failCount} lines");
                    if (errors.Count > 0 && errors.Count <= 5)
                    {
                        resultMessage.AppendLine("\nErrors:");
                        foreach (var error in errors.Take(5))
                        {
                            resultMessage.AppendLine("- " + error);
                        }
                    }
                }

                EditorUtility.DisplayDialog("Import Result", resultMessage.ToString(), "OK");

                return importedCharacters;
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Import Failed",
                    $"Failed to import CSV:\n{e.Message}", "OK");
                return null;
            }
        }

        /// <summary>
        /// 캐릭터를 CSV 라인으로 변환
        /// </summary>
        private static string CharacterToCSVLine(BattleCharacterDataSO character)
        {
            var values = new List<string>
            {
                character.CharacterId.ToString(),
                EscapeCSVValue(character.CharacterName),
                EscapeCSVValue(character.Description),
                character.Tier.ToString(),
                character.CharacterClass.ToString(),
                character.ElementType.ToString(),
                character.AttackType.ToString(),
                
                // Base Stats
                character.BaseStats.hp.ToString(),
                character.BaseStats.attack.ToString(),
                character.BaseStats.defense.ToString(),
                character.BaseStats.hpGrowth.ToString("F2"),
                character.BaseStats.attackGrowth.ToString("F2"),
                character.BaseStats.defenseGrowth.ToString("F2"),
                
                // Fixed Stats
                character.FixedStats.maxBP.ToString(),
                character.FixedStats.turnSpeed.ToString("F1"),
                character.FixedStats.critRate.ToString("F1"),
                character.FixedStats.critDamage.ToString("F1"),
                character.FixedStats.hitRate.ToString("F1"),
                character.FixedStats.dodgeRate.ToString("F1"),
                character.FixedStats.blockRate.ToString("F1"),
                character.FixedStats.aggro.ToString(),
                character.FixedStats.skillHitRate.ToString("F1"),
                character.FixedStats.skillResist.ToString("F1"),
                
                // Skills
                character.ActiveSkillId.ToString(),
                character.PassiveSkillId.ToString(),
                
                // Other
                character.FormationPreference.ToString(),
                EscapeCSVValue(character.PrefabPath),
                EscapeCSVValue(character.CharacterResourceName)
            };

            return string.Join(",", values);
        }

        /// <summary>
        /// CSV 라인에서 캐릭터 파싱
        /// </summary>
        private static BattleCharacterDataSO ParseCharacterFromCSV(string csvLine)
        {
            string[] values = ParseCSVLine(csvLine);

            if (values.Length < CHARACTER_CSV_HEADERS.Length)
            {
                throw new Exception($"Invalid column count. Expected {CHARACTER_CSV_HEADERS.Length}, got {values.Length}");
            }

            var character = ScriptableObject.CreateInstance<BattleCharacterDataSO>();

            try
            {
                int index = 0;

                // 리플렉션을 사용해 private 필드 설정
                var type = character.GetType();
                var bindingFlags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

                // 기본 정보
                SetPrivateField(character, "characterId", int.Parse(values[index++]));
                SetPrivateField(character, "characterName", values[index++]);
                SetPrivateField(character, "description", values[index++]);
                SetPrivateField(character, "tier", Enum.Parse<CharacterTier>(values[index++]));
                SetPrivateField(character, "characterClass", Enum.Parse<ClassType>(values[index++]));
                SetPrivateField(character, "elementType", Enum.Parse<EBattleElementType>(values[index++]));
                SetPrivateField(character, "attackType", Enum.Parse<EAttackType>(values[index++]));

                // Base Stats
                var baseStats = new BaseStats
                {
                    hp = int.Parse(values[index++]),
                    attack = int.Parse(values[index++]),
                    defense = int.Parse(values[index++]),
                    hpGrowth = float.Parse(values[index++]),
                    attackGrowth = float.Parse(values[index++]),
                    defenseGrowth = float.Parse(values[index++])
                };
                SetPrivateField(character, "baseStats", baseStats);

                // Fixed Stats
                var fixedStats = new FixedStats
                {
                    maxBP = int.Parse(values[index++]),
                    turnSpeed = float.Parse(values[index++]),
                    critRate = float.Parse(values[index++]),
                    critDamage = float.Parse(values[index++]),
                    hitRate = float.Parse(values[index++]),
                    dodgeRate = float.Parse(values[index++]),
                    blockRate = float.Parse(values[index++]),
                    aggro = int.Parse(values[index++]),
                    skillHitRate = float.Parse(values[index++]),
                    skillResist = float.Parse(values[index++])
                };
                SetPrivateField(character, "fixedStats", fixedStats);
                SetPrivateField(character, "useCustomStats", true); // CSV에서 가져온 스탯 사용

                // Skills
                SetPrivateField(character, "activeSkillId", int.Parse(values[index++]));
                SetPrivateField(character, "passiveSkillId", int.Parse(values[index++]));

                // Other
                SetPrivateField(character, "formationPreference", Enum.Parse<FormationPreference>(values[index++]));
                SetPrivateField(character, "prefabPath", values[index++]);
                SetPrivateField(character, "characterResourceName", values[index++]);

                return character;
            }
            catch (Exception e)
            {
                UnityEngine.Object.DestroyImmediate(character);
                throw new Exception($"Failed to parse character data: {e.Message}");
            }
        }

        /// <summary>
        /// Private 필드 설정 헬퍼
        /// </summary>
        private static void SetPrivateField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (field != null)
            {
                field.SetValue(obj, value);
            }
        }

        /// <summary>
        /// CSV 값 이스케이프
        /// </summary>
        private static string EscapeCSVValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";

            // 쉼표, 줄바꿈, 따옴표가 포함된 경우 따옴표로 감싸기
            if (value.Contains(",") || value.Contains("\n") || value.Contains("\""))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }

            return value;
        }

        /// <summary>
        /// CSV 라인 파싱
        /// </summary>
        private static string[] ParseCSVLine(string line)
        {
            List<string> result = new List<string>();
            StringBuilder current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++; // 다음 따옴표 건너뛰기
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            result.Add(current.ToString());
            return result.ToArray();
        }

        /// <summary>
        /// 헤더 검증
        /// </summary>
        private static bool ValidateHeaders(string[] headers)
        {
            if (headers.Length != CHARACTER_CSV_HEADERS.Length) return false;

            for (int i = 0; i < headers.Length; i++)
            {
                if (headers[i].Trim() != CHARACTER_CSV_HEADERS[i])
                {
                    Debug.LogWarning($"Header mismatch at column {i}: expected '{CHARACTER_CSV_HEADERS[i]}', got '{headers[i]}'");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 가져온 캐릭터를 프로젝트에 저장
        /// </summary>
        public static void SaveImportedCharacters(List<BattleCharacterDataSO> characters)
        {
            if (characters == null || characters.Count == 0) return;

            string basePath = "Assets/Cosmos/ResourcesAddressable/ScriptableObjects/Characters";

            // 디렉토리 확인
            if (!AssetDatabase.IsValidFolder(basePath))
            {
                Directory.CreateDirectory(basePath);
            }

            int savedCount = 0;

            foreach (var character in characters)
            {
                if (character == null) continue;

                string fileName = $"{character.CharacterName}_{character.CharacterId}.asset";
                string fullPath = Path.Combine(basePath, fileName);

                // 기존 파일 체크
                if (File.Exists(fullPath))
                {
                    bool overwrite = EditorUtility.DisplayDialog("File Exists",
                        $"Character '{character.CharacterName}' already exists.\nOverwrite?",
                        "Overwrite", "Skip");

                    if (!overwrite) continue;
                }

                AssetDatabase.CreateAsset(character, fullPath);
                savedCount++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Import Complete",
                $"Saved {savedCount} characters to project.", "OK");
        }
    }
}