#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameCore.Editor.Addressables
{



    [System.Serializable]
    public class MappingRule
    {
        public string pattern;
        public string groupNameTemplate;
        public bool isLocal;
        public string compression = "LZ4";
        public bool includeInBuild = false;
        public List<string> autoLabels = new List<string>();
        public string addressTemplate;
        public string labelTemplate;
        public int priority = 5;

        // ✨ 새로 추가할 필드 - 조건부 템플릿
        public Dictionary<string, string> conditionalTemplates = new Dictionary<string, string>();
    }

    [System.Serializable]
    public class MappingRuleSet
    {
        public List<MappingRule> rules = new List<MappingRule>();
        public Dictionary<string, string> variables = new Dictionary<string, string>();

        public static MappingRuleSet GetDefault()
        {
            return new MappingRuleSet
            {
                rules = new List<MappingRule>
                {
                    new MappingRule
                    {
                        pattern = "_Core/**/*",
                        groupNameTemplate = "Core_Local",
                        isLocal = true,
                        compression = "Uncompressed",
                        includeInBuild = true,
                        addressTemplate = "Core_{filename}_{ext}",
                        autoLabels = new List<string> { "core", "essential" }
                    },
                    new MappingRule
                    {
                        pattern = "_Core/Fonts/Kor/*",
                        groupNameTemplate = "Core_Local",
                        isLocal = true,
                        compression = "Uncompressed",
                        includeInBuild = true,
                        addressTemplate = "Fonts_Kor_{filename}_{ext}",
                        autoLabels = new List<string> { "core", "font", "kor" }
                    },
                     new MappingRule
                    {
                        pattern = "_Core/Fonts/Eng/*",
                        groupNameTemplate = "Core_Local",
                        isLocal = true,
                        compression = "Uncompressed",
                        includeInBuild = true,
                        addressTemplate = "Fonts_Eng_{filename}_{ext}",
                        autoLabels = new List<string> { "core", "font", "eng" }
                    },
                    new MappingRule
                    {
                        pattern = "_Shared/Effects/Battle/**/*",
                        groupNameTemplate = "Shared_Effects",
                        isLocal = false,
                        compression = "LZ4",
                        addressTemplate = "FX_{filename}_{ext}",
                        autoLabels = new List<string> { "fx", "battle", "shared" },
                        priority = 10  // 높은 우선순위
                    },
                    new MappingRule
                    {
                        pattern = "_Shared/Effects/Common/**/*",
                        groupNameTemplate = "Shared_Effects",
                        isLocal = false,
                        compression = "LZ4",
                        addressTemplate = "FX_{filename}_{ext}",
                        autoLabels = new List<string> { "fx", "common", "shared" },
                        priority = 9
                    },
                    new MappingRule
                    {
                        pattern = "_Shared/Effects/**/*",  // 나머지 Effects 파일들
                        groupNameTemplate = "Shared_Effects",
                        isLocal = false,
                        compression = "LZ4",
                        addressTemplate = "FX_{filename}_{ext}",
                        autoLabels = new List<string> { "fx", "shared" },
                        priority = 8  // 낮은 우선순위로 fallback
                    },

                    new MappingRule
                    {
                        pattern = "_Shared/**/*",
                        groupNameTemplate = "Shared_Common",
                        isLocal = false,
                        compression = "LZ4",
                        addressTemplate = "Shared_{filename}_{ext}",
                        autoLabels = new List<string> { "shared", "common" },
                        priority = 7  // 낮은 우선순위로 fallback
                    },

                    new MappingRule
                    {
                        pattern = "Characters/{CharName}/Effects/**/*",
                        groupNameTemplate = "Character_{CharName}",
                        isLocal = false,
                        compression = "LZ4",
                        addressTemplate = "Char_{filename}_{ext}",
                        autoLabels = new List<string> { "character", "{CharName}", "fx", "battle" },
                        priority = 8
                    },

                    new MappingRule
                    {
                        pattern = "Characters/{CharName}/**/*",
                        groupNameTemplate = "Character_{CharName}",
                        isLocal = false,
                        compression = "LZ4",
                        addressTemplate = "Char_{filename}_{ext}",
                        priority = 5,  // 낮은 우선순위 (fallback)

                        autoLabels = new List<string> { "character", "{CharName}" }
                    },




                    new MappingRule
                    {
                        pattern = "Characters/_MonthlyUpdate/{YYYY_MM}/**/*",
                        groupNameTemplate = "Monthly_{YYYY_MM}_Characters",
                        isLocal = false,
                        compression = "LZ4",
                        addressTemplate = "Char_{filename}_{ext}",
                        autoLabels = new List<string> { "monthly", "monthly_{YYYY_MM}" }
                    },
                    new MappingRule
                    {
                        pattern = "Scenes/EventCut/{Episode}/**/*",
                        groupNameTemplate = "EventCut_{Episode}",
                        isLocal = false,
                        compression = "LZ4",
                        addressTemplate = "{Episode}_{filename}_{ext}",
                        autoLabels = new List<string> { "eventcut", "{Episode}" }
                    },
                    new MappingRule
                    {
                        pattern = "Scenes/Battle/Stages/{StageName}/**/*",
                        groupNameTemplate = "Battle_{StageName}",
                        isLocal = false,
                        compression = "LZ4",
                        addressTemplate = "{filename}_{ext}",
                        autoLabels = new List<string> { "battle", "stage" }
                    },
                    new MappingRule
                    {
                        pattern = "UI/Windows/**/*",
                        groupNameTemplate = "UI_System",
                        isLocal = false,
                        compression = "LZ4",
                        addressTemplate = "UI_{filename}_{ext}",
                        autoLabels = new List<string> { "ui", "system" }
                    },
                    new MappingRule
                    {
                        pattern = "UI/Battle/**/*",
                        groupNameTemplate = "UI_Battle",
                        isLocal = false,
                        compression = "LZ4",
                        addressTemplate = "UI_{filename}_{ext}",
                        autoLabels = new List<string> { "ui", "battle" }
                    },
                    new MappingRule
                    {
                        pattern = "Audio/BGM/**/*",
                        groupNameTemplate = "Audio_BGM",
                        isLocal = false,
                        compression = "LZ4",
                        addressTemplate = "Audio_{filename}_{ext}",
                        autoLabels = new List<string> { "audio", "bgm" }
                    },
                    new MappingRule
                    {
                        pattern = "Audio/SFX/**/*",
                        groupNameTemplate = "Audio_SFX",
                        isLocal = false,
                        compression = "LZ4",
                        addressTemplate = "Audio_{filename}_{ext}",
                        autoLabels = new List<string> { "audio", "sfx" }
                    },
                    new MappingRule
                    {
                        pattern = "_Seasonal/{SeasonName}/**/*",
                        groupNameTemplate = "Seasonal_{SeasonName}",
                        isLocal = false,
                        compression = "LZ4",
                        addressTemplate = "{SeasonName}_{filename}_{ext}",
                        autoLabels = new List<string> { "seasonal", "{SeasonName}" }
                    },
                    new MappingRule
{
    pattern = "ScriptableObjects/Characters/*.asset",
    groupNameTemplate = "SO_Characters",
    isLocal = false,
    compression = "LZ4",
    includeInBuild = false,
    addressTemplate = "SO_{filename}_{ext}",
    autoLabels = new List<string> { "scriptableobject", "character_data" },
    priority = 3
},
new MappingRule
{
    pattern = "ScriptableObjects/Monsters/*.asset",
    groupNameTemplate = "SO_Monsters",
    isLocal = false,
    compression = "LZ4",
    includeInBuild = false,
    addressTemplate = "SO_{filename}_{ext}",
    autoLabels = new List<string> { "scriptableobject", "monster_data" },
    priority = 3
},
new MappingRule
{
    pattern = "ScriptableObjects/MonsterGroups/*.asset",
    groupNameTemplate = "SO_MonsterGroups",
    isLocal = false,
    compression = "LZ4",
    includeInBuild = false,
    addressTemplate = "SO_{filename}_{ext}",
    autoLabels = new List<string> { "scriptableobject", "monster_group" },
    priority = 3
},
new MappingRule
{
    pattern = "ScriptableObjects/Common/*.asset",  // 
    groupNameTemplate = "SO_Common",
    isLocal = false,
    compression = "LZ4",
    includeInBuild = false,
    addressTemplate = "SO_{filename}_{ext}",
    autoLabels = new List<string> { "scriptableobject", "data" },
    priority = 2
},




                }
            };
        }
    }

    [System.Serializable]
    public class CharacterTemplate
    {
        public string name;
        public List<string> folders = new List<string>
        {
            "Prefabs", "Atlas", "Textures", "Effects",
            "Animations", "Data"
        };
        public List<string> requiredFiles = new List<string>
        {
            "{name}_Battle.prefab",
            "{name}_Town.prefab",
            "{name}_Atlas.spriteatlas"
        };
    }

    [System.Serializable]
    public class ValidationResult
    {
        public bool isValid;
        public List<string> errors = new List<string>();
        public List<string> warnings = new List<string>();
        public List<string> suggestions = new List<string>();
        public Dictionary<string, long> groupSizes = new Dictionary<string, long>();

        public void Clear()
        {
            isValid = true;
            errors.Clear();
            warnings.Clear();
            suggestions.Clear();
            groupSizes.Clear();
        }
    }

    [System.Serializable]
    public class AutomationProfile
    {
        public string name;
        public string description;
        public MappingRuleSet rules;
        public List<string> defaultLabels;
        public Dictionary<string, object> settings = new Dictionary<string, object>();
    }

    public class DuplicateAsset
    {
        public string hash;
        public List<string> paths = new List<string>();
        public long size;
        public bool canMoveToShared;

        public float GetWastedSpace()
        {
            return size * (paths.Count - 1) / (1024f * 1024f); // MB
        }
    }

    public class OptimizationSuggestion
    {
        public string title;
        public string description;
        public SuggestionType type;
        public Action applyAction;
        public float estimatedSavingMB;
    }

    public enum SuggestionType
    {
        MoveToShared,
        CompressTexture,
        ReduceAtlasSize,
        SplitGroup,
        MergeGroups,
        RemoveUnused
    }

    public class SetupStatistics
    {
        public int groupsCreated;
        public int assetsProcessed;
        public int addressesUpdated;
        public int labelsApplied;
        public int duplicatesFound;
        public int errorsFixed;
        public float timeTaken;

        public void Reset()
        {
            groupsCreated = 0;
            assetsProcessed = 0;
            addressesUpdated = 0;
            labelsApplied = 0;
            duplicatesFound = 0;
            errorsFixed = 0;
            timeTaken = 0;
        }
    }

    public class GroupConfig
    {
        public string groupName;
        public bool isLocal;
        public string compression;
        public bool includeInBuild;
        public string buildPath;
        public string loadPath;

        public static GroupConfig CreateDefault(string name, bool local = false)
        {
            return new GroupConfig
            {
                groupName = name,
                isLocal = local,
                compression = "LZ4",
                includeInBuild = local,
                buildPath = local ? "Local.BuildPath" : "Remote.BuildPath",
                loadPath = local ? "Local.LoadPath" : "Remote.LoadPath"
            };
        }
    }

    public static class AddressableSetupConstants
    {
        public const string BASE_PATH = "Assets/Cosmos/ResourcesAddressable";
        public const string CORE_PATH = "Assets/Cosmos/ResourcesAddressable/_Core";
        public const string SHARED_PATH = "Assets/Cosmos/ResourcesAddressable/_Shared";
        public const string CHARACTERS_PATH = "Assets/Cosmos/ResourcesAddressable/Characters";
        public const string MONTHLY_PATH = "Assets/Cosmos/ResourcesAddressable/Characters/_MonthlyUpdate";
        public const string SCENES_PATH = "Assets/Cosmos/ResourcesAddressable/Scenes";
        public const string UI_PATH = "Assets/Cosmos/ResourcesAddressable/UI";
        public const string AUDIO_PATH = "Assets/Cosmos/ResourcesAddressable/Audio";
        public const string SEASONAL_PATH = "Assets/Cosmos/ResourcesAddressable/_Seasonal";

        // ScriptableObjects 경로 추가
        public const string SCRIPTABLE_OBJECTS_PATH = "Assets/Cosmos/ResourcesAddressable/ScriptableObjects";
        public const string SO_CHARACTERS_PATH = "Assets/Cosmos/ResourcesAddressable/ScriptableObjects/Characters";
        public const string SO_MONSTERS_PATH = "Assets/Cosmos/ResourcesAddressable/ScriptableObjects/Monsters";
        public const string SO_MONSTER_GROUPS_PATH = "Assets/Cosmos/ResourcesAddressable/ScriptableObjects/MonsterGroups";
        public const string SO_COMMON_PATH = "Assets/Cosmos/ResourcesAddressable/ScriptableObjects/Common";


        public const long CORE_SIZE_LIMIT = 30 * 1024 * 1024; // 30MB
        public const long SHARED_SIZE_LIMIT = 40 * 1024 * 1024; // 40MB
        public const long CHARACTER_SIZE_LIMIT = 20 * 1024 * 1024; // 20MB
        public const long EP_SIZE_LIMIT = 200 * 1024 * 1024; // 200MB
        public const long EP_SIZE_WARNING = 250 * 1024 * 1024; // 250MB
        public const long GROUP_SIZE_WARNING = 100 * 1024 * 1024; // 100MB

        public const string RULES_FILE = "Assets/Cosmos/AddressableAssetsData/MappingRules.json";
        public const string PROFILES_FILE = "Assets/Cosmos/AddressableAssetsData/AutomationProfiles.json";
    }
}
#endif