using Cysharp.Threading.Tasks;
using IronJade.Table.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using GameCore.Addressables;

public class BattleObjectPoolSupport : MonoBehaviour
{
    [SerializeField]
    private Transform actorParent = null;

    [SerializeField]
    private Transform effectParent = null;

    private Dictionary<string, List<GameObject>> activeEffectObjects = null;


    private void OnDisable()
    {
        TokenPool.Cancel(GetHashCode());
    }


    private void Update()
    {
        OnUpdateActorState();
        OnUpdateEffectState();
    }

    private void OnUpdateActorState()
    {
        // 아바타 위치 조정
        /*var actors = GetActiveObjects(HousingObjectType.Avatar);

        foreach (var actor in actors.Cast<HousingAvatar>().ToList())
        {
            if (!actor)
            {
                RemoveActor(HousingObjectType.Avatar, actor.Model.DataId);
                continue;
            }

            actor.SetPosition();
        }*/

    }

    private void OnUpdateEffectState()
    {
        /*if (followEffectObjects == null || followEffectObjects.Count == 0)
            return;

        // 이펙트 위치 조정
        List<Transform> removeObjects = null;

        foreach (var followEffectPair in followEffectObjects)
        {
            if (!followEffectPair.Key || !followEffectPair.Key.gameObject.activeSelf)
            {
                if (removeObjects == null)
                    removeObjects = new List<Transform>();

                removeObjects.Add(followEffectPair.Key);
                continue;
            }

            followEffectPair.Key.transform.position = followEffectPair.Value.transform.position;
        }

        if (removeObjects != null)
        {
            foreach (var removeObject in removeObjects)
            {
                followEffectObjects.Remove(removeObject);
            }
        }*/

    }

    public GameObject ShowEffect(Transform follower, string effect, float spawnTime)
    {
        if (activeEffectObjects == null)
        {
            activeEffectObjects = new Dictionary<string, List<GameObject>>();
            //followEffectObjects = new Dictionary<Transform, Transform>();
        }

        if (!activeEffectObjects.TryGetValue(effect, out var effectObjects))
        {
            effectObjects = new List<GameObject>();
            activeEffectObjects[effect] = effectObjects;
        }

        GameObject findEffectObject = null;

        foreach (GameObject effectObject in effectObjects)
        {
            if (!effectObject.activeSelf)
            {
                findEffectObject = effectObject;
                break;
            }
        }
        if (findEffectObject == null)
        {
            findEffectObject = UtilModel.Resources.Instantiate<GameObject>(effect);

            effectObjects.Add(findEffectObject);

            findEffectObject.SafeSetParent(effectParent);
        }

       // followEffectObjects[findEffectObject.transform] = follower;

        findEffectObject.SetActive(true);

      //  WaitDespawn(findEffectObject, spawnTime).Forget();

        return findEffectObject;
    }

    public static string Capitalize(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return char.ToUpper(input[0]) + input.Substring(1).ToLower();
    }

    public async Task<GameObject> LoadSingleCharacter(string characterId)
    {
        // Address로 특정 캐릭터 1개 로드
        string address = $"{characterId}";




        var handle = Addressables.LoadAssetAsync<GameObject>(address);

        return await handle.Task;

    }


    public async UniTask<BattleActor> CreateBattleActor( BattleCharInfoNew battleCharInfo )
    {
        string path = battleCharInfo.GetPrefabPath();
        string prefabName = battleCharInfo.GetPrefabName();


        var sw = System.Diagnostics.Stopwatch.StartNew();

        GameObject newObjectPrefab = await GameCore.Addressables.AddressableManager.Instance.LoadCharacterAsync( battleCharInfo.GetAddressableKey());

        if (newObjectPrefab == null)
        {
            Debug.LogError($"[CreateBattleActor] Failed to load prefab: {path}");
            return null;
        }

        Debug.LogError($"[CreateBattleActor] Load time for {battleCharInfo.GetAddressableKey()}: {sw.ElapsedMilliseconds}ms");


        // 인스턴스 생성 (기존 코드와 동일)
        GameObject newObjectInstance = GameObject.Instantiate(newObjectPrefab);
        newObjectInstance.transform.SetParent(actorParent);
        newObjectInstance.layer = battleCharInfo.IsAlly ?
            LayerMask.NameToLayer("Character") : LayerMask.NameToLayer("Enemy");
        newObjectInstance.name = (battleCharInfo.IsAlly ? "ALLY>" : "ENEMY>") +
                                prefabName + " / " + battleCharInfo.SlotIndex;

        BattleActor battleActor = newObjectInstance.GetComponent<BattleActor>();

        if (battleActor != null)
        {
            battleActor.SetBattleActorInfo(battleCharInfo);

            if (battleCharInfo.IsMonster == false)
                await battleCharInfo.LoadCharacterDataAsync();
            else
                await battleCharInfo.LoadMonsterDataAsync();
        }

        return battleActor;

    }


}
