using BattleCharacterSystem;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading.Tasks;

public static class BattleDataLoader
{
    // 캐시 추가 (성능 최적화)
    private static Dictionary<int, BattleCharacterDataSO> _characterCache = new Dictionary<int, BattleCharacterDataSO>();
    private static Dictionary<int, MonsterGroupDataSO> _monsterGroupCache = new Dictionary<int, MonsterGroupDataSO>();


    public static async Task<BattleCharacterDataSO> GetCharacterDataAsync(int characterId)
    {
        BattleCharacterDataSO characterData = null;

        // 1. 캐시 확인
        if (_characterCache.TryGetValue(characterId, out characterData))
        {
            Debug.Log($"[BattleDataLoader] Character {characterId} loaded from cache");
        }
        else
        {
            // characterId로 Key 가져와라 / 테이블 뒤져서.. EX> "SO_Ayumi(이름)_" + characterId +"_asset";
            string addressKey = characterId.ToString();

            // 2. Addressable로 로드
            try
            {
                // AddressableManager 사용
                if (GameCore.Addressables.AddressableManager.Instance != null)
                {
                    characterData = await GameCore.Addressables.AddressableManager.Instance.LoadAssetAsync<BattleCharacterDataSO>(addressKey);
                }
                else
                {
                    // AddressableManager 없으면 직접 로드
                    characterData = await ResourceLoadHelper.LoadAssetAsync<BattleCharacterDataSO>(addressKey);
                }

                if (characterData != null)
                {
                    _characterCache[characterId] = characterData;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[BattleDataLoader] Failed to load character {characterId}: {e.Message}");
            }
        }


        return characterData;
    }


    /// <summary>
    /// 비동기 캐릭터 데이터 로드 (권장)
    /// </summary>
    public static async Task<BattleCharInfoNew> LoadCharacterDataCharInfoAsync(int characterId, string addressKey, bool isAlly, int slotIndex)
    {
        BattleCharacterDataSO characterData = null;

        // 1. 캐시 확인
        if (_characterCache.TryGetValue(characterId, out characterData))
        {
            Debug.Log($"[BattleDataLoader] Character {characterId} loaded from cache");
        }
        else
        {
            // 2. Addressable로 로드
            try
            {
                // AddressableManager 사용
                if (GameCore.Addressables.AddressableManager.Instance != null)
                {
                    characterData = await GameCore.Addressables.AddressableManager.Instance.LoadAssetAsync<BattleCharacterDataSO>(addressKey);
                }
                else
                {
                    // AddressableManager 없으면 직접 로드
                    characterData = await ResourceLoadHelper.LoadAssetAsync<BattleCharacterDataSO>(addressKey);
                }

                if (characterData != null)
                {
                    _characterCache[characterId] = characterData;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[BattleDataLoader] Failed to load character {characterId}: {e.Message}");
            }
        }

        if (characterData != null)
        {
            var charInfo = new BattleCharInfoNew();
            charInfo.SetIsAlly(isAlly);
            charInfo.SetSlotIndex(slotIndex);
            characterData.ApplyToCharacterInfo(charInfo);

            // 프리팹 정보 설정 (중요!)
            charInfo.SetPrefabInfo(
                characterData.CharacterResourceName,
                characterData.CharacterName,
                characterData.AddressableKey,
                characterData.CharacterId
            );

            return charInfo;
        }

        Debug.LogError($"[BattleDataLoader] Character data not found: {characterId}");
        return null;
    }


    /// <summary>
    /// 비동기 몬스터 그룹 로드 (권장)
    /// </summary>
    public static async Task<List<BattleCharInfoNew>> LoadMonsterGroupCharInfoAsync(int groupId)
    {
        MonsterGroupDataSO groupData = null;

        // 런타임에서는 Addressable 사용
        // 1. 캐시 확인
        if (_monsterGroupCache.TryGetValue(groupId, out groupData))
        {
            Debug.Log($"[BattleDataLoader] Monster group {groupId} loaded from cache");
        }
        else
        {
            // 2. Addressable로 로드
            string addressKey = $"SO_MonsterGroup_{groupId}_asset";
            
            try
            {
                // AddressableManager 사용
                if (GameCore.Addressables.AddressableManager.Instance != null)
                {
                    groupData = await GameCore.Addressables.AddressableManager.Instance.LoadAssetAsync<MonsterGroupDataSO>(addressKey);
                }
                else
                {
                    // AddressableManager 없으면 직접 로드
                    groupData = await ResourceLoadHelper.LoadAssetAsync<MonsterGroupDataSO>(addressKey);
                }

                if (groupData != null)
                {
                    _monsterGroupCache[groupId] = groupData;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[BattleDataLoader] Failed to load monster group {groupId}: {e.Message}");
            }
        }

        if (groupData != null)
        {
            var monsters = new List<BattleCharInfoNew>();

            for (int i = 0; i < 5; i++)
            {
                var slot = groupData.MonsterSlots[i];
                if (!slot.isEmpty && slot.monsterData != null)
                {
                    var monsterInfo = new BattleCharInfoNew();
                    monsterInfo.SetIsAlly(false);
                    monsterInfo.SetSlotIndex(i);

                    monsterInfo.SetPrefabInfo(
                        slot.monsterData.GetActualResourceName(),
                        slot.monsterData.GetActualResourceRootName(),
                        slot.monsterData.GetActualAddressableKey(),
                        slot.monsterData.MonsterId
                    );

                    // 몬스터 데이터 적용
                    slot.monsterData.ApplyToCharacterInfo(monsterInfo);
                    monsters.Add(monsterInfo);
                }
            }

            return monsters;
        }

        Debug.LogError($"[BattleDataLoader] Monster group not found: {groupId}");
        return new List<BattleCharInfoNew>();
    }



    public static async Task<MonsterGroupDataSO> GetMonsterGroupDataAsync(int groupId)
    {
        MonsterGroupDataSO groupData = null;

        // 런타임에서는 Addressable 사용
        // 1. 캐시 확인
        if (_monsterGroupCache.TryGetValue(groupId, out groupData))
        {
            Debug.Log($"[BattleDataLoader] Monster group {groupId} loaded from cache");
        }
        else
        {
            // 2. Addressable로 로드
            string addressKey = $"SO_MonsterGroup_{groupId}_asset";

            try
            {
                // AddressableManager 사용
                if (GameCore.Addressables.AddressableManager.Instance != null)
                {
                    groupData = await GameCore.Addressables.AddressableManager.Instance.LoadAssetAsync<MonsterGroupDataSO>(addressKey);
                }
                else
                {
                    // AddressableManager 없으면 직접 로드
                    groupData = await ResourceLoadHelper.LoadAssetAsync<MonsterGroupDataSO>(addressKey);
                }

                if (groupData != null)
                {
                    _monsterGroupCache[groupId] = groupData;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[BattleDataLoader] Failed to load monster group {groupId}: {e.Message}");
            }
        }

        if (groupData != null)
        {
            return groupData;
        }

        return null;
    }


    /// <summary>
    /// 캐시 클리어
    /// </summary>
    public static void ClearCache()
    {
        _characterCache.Clear();
        _monsterGroupCache.Clear();
        Debug.Log("[BattleDataLoader] Cache cleared");
    }

    /// <summary>
    /// 특정 캐시 제거
    /// </summary>
    public static void RemoveFromCache(int characterId)
    {
        if (_characterCache.Remove(characterId))
        {
            Debug.Log($"[BattleDataLoader] Character {characterId} removed from cache");
        }
    }
}