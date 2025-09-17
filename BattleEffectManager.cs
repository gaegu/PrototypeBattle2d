using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using System;

public class BattleEffectManager : MonoBehaviour
{
    // 싱글톤
    private static BattleEffectManager instance;
    public static BattleEffectManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<BattleEffectManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("BattleEffectManager");
                    instance = go.AddComponent<BattleEffectManager>();
                }
            }
            return instance;
        }
    }

    [Header("이펙트 데이터")]
    [SerializeField] private List<BattleEffectData> effectDataList = new List<BattleEffectData>();
    private Dictionary<string, BattleEffectData> effectDataDict = new Dictionary<string, BattleEffectData>();

    [Header("이펙트 컨테이너")]
    [SerializeField] private Transform effectContainer;
    private List<EffectInstance> activeEffects = new List<EffectInstance>();

    // 이펙트 풀링
    private Dictionary<string, Queue<GameObject>> effectPools = new Dictionary<string, Queue<GameObject>>();
    private const int POOL_SIZE = 5;

    // 캐릭터별 이펙트 매핑 - 캐릭터명과 이펙트 타입별 ID 매핑
    private Dictionary<string, Dictionary<BattleActorEffectType, string>> characterEffectMapping =
        new Dictionary<string, Dictionary<BattleActorEffectType, string>>();

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeContainer();
        LoadAllEffectData();
        InitializeCharacterEffectMappings();
    }

    private void Update()
    {
        UpdateActiveEffects(Time.deltaTime);
    }

    private void InitializeContainer()
    {
        if (effectContainer == null)
        {
            GameObject container = new GameObject("EffectContainer");
            container.transform.SetParent(transform);
            effectContainer = container.transform;
        }
    }

    private void LoadAllEffectData()
    {
        effectDataList.Clear();
        effectDataDict.Clear();

        // Resources 폴더에서 모든 BattleEffectData 로드
        BattleEffectData[] loadedData = Resources.LoadAll<BattleEffectData>("ScriptableObjects/BattleEffects");

        if (loadedData != null && loadedData.Length > 0)
        {
            effectDataList.AddRange(loadedData);
            Debug.Log($"[BattleEffectManager] Loaded {loadedData.Length} effect data from Resources");
        }
        else
        {
            // 다른 경로에서도 시도
            loadedData = Resources.LoadAll<BattleEffectData>("");
            if (loadedData != null && loadedData.Length > 0)
            {
                effectDataList.AddRange(loadedData);
                Debug.Log($"[BattleEffectManager] Loaded {loadedData.Length} effect data from all Resources");
            }
        }

        // Dictionary 초기화
        InitializeEffectData();
    }

    private void InitializeEffectData()
    {
        effectDataDict.Clear();
        foreach (var data in effectDataList)
        {
            if (data != null && !string.IsNullOrEmpty(data.effectId))
            {
                effectDataDict[data.effectId] = data;
                Debug.Log($"[BattleEffectManager] Registered effect: {data.effectId}");
            }
        }
    }

    /// <summary>
    /// 캐릭터별 이펙트 매핑 초기화
    /// </summary>
    private void InitializeCharacterEffectMappings()
    {
        characterEffectMapping.Clear();

        // 모든 이펙트 데이터를 순회하면서 캐릭터별로 분류
        foreach (var effectData in effectDataList)
        {
            if (effectData == null) continue;

            // 캐릭터 전용 이펙트인 경우
            if (effectData.ownerType == EffectOwnerType.Character &&
                !string.IsNullOrEmpty(effectData.characterName))
            {
                if (!characterEffectMapping.ContainsKey(effectData.characterName))
                {
                    characterEffectMapping[effectData.characterName] = new Dictionary<BattleActorEffectType, string>();
                }

                // 이펙트 타입에 따라 매핑
                BattleActorEffectType effectType = ConvertToActorEffectType(effectData.effectType, effectData.effectId);
                characterEffectMapping[effectData.characterName][effectType] = effectData.effectId;

                Debug.Log($"[EffectManager] Mapped {effectData.characterName} - {effectType}: {effectData.effectId}");
            }
        }

        Debug.Log($"[BattleEffectManager] Initialized {characterEffectMapping.Count} character effect mappings");
    }

    /// <summary>
    /// EffectType을 BattleActorEffectType으로 변환
    /// </summary>
    private BattleActorEffectType ConvertToActorEffectType(EffectType effectType, string effectId)
    {
        // effectId에 따른 추가 분류
        string lowerEffectId = effectId.ToLower();

        if (lowerEffectId.Contains("attack") || effectType == EffectType.AttackMelee || effectType == EffectType.AttackRanged)
            return BattleActorEffectType.Attack1;
        if (lowerEffectId.Contains("skill") || effectType == EffectType.Special)
            return BattleActorEffectType.Skill1;
        if (lowerEffectId.Contains("hit") || effectType == EffectType.Hit)
            return BattleActorEffectType.Hit;
        if (lowerEffectId.Contains("heal") || effectType == EffectType.Heal || effectType == EffectType.Buff)
            return BattleActorEffectType.Heal;

        if (effectType == EffectType.ChargeBP)
            return BattleActorEffectType.ChargeBP1;

        // 기본값
        return BattleActorEffectType.Attack1;
    }

    /// <summary>
    /// BattleActor를 위한 이펙트 ID 딕셔너리 가져오기
    /// </summary>
    public Dictionary<BattleActorEffectType, string> GetCharacterEffectIds(string characterName)
    {
        if (string.IsNullOrEmpty(characterName))
        {
            Debug.LogWarning("[BattleEffectManager] Character name is empty, returning default effects");
            return GetCommonEffectIds();
        }

        // 캐릭터별 이펙트가 있는 경우
        if (characterEffectMapping.TryGetValue(characterName, out var effectIds))
        {
            Debug.Log($"[BattleEffectManager] Found character-specific effects for {characterName}");
            return new Dictionary<BattleActorEffectType, string>(effectIds);
        }

        Debug.Log($"[BattleEffectManager] No character-specific effects for {characterName}, using defaults");
        // 기본 이펙트 반환
        return GetCommonEffectIds();
    }

    /// <summary>
    /// 기본 이펙트 ID 딕셔너리
    /// </summary>
    public Dictionary<BattleActorEffectType, string> GetCommonEffectIds()
    {
        var defaultIds = new Dictionary<BattleActorEffectType, string>();

        // Common 이펙트 중에서 기본값 찾기
        foreach (var effectData in effectDataList)
        {
            if (effectData.ownerType == EffectOwnerType.Common)
            {
                var effectType = ConvertToActorEffectType(effectData.effectType, effectData.effectId);
                if (!defaultIds.ContainsKey(effectType))
                {
                    defaultIds[effectType] = effectData.effectId;
                }
            }
        }

        // 하드코딩된 기본값 (Common 이펙트가 없는 경우)
        if (!defaultIds.ContainsKey(BattleActorEffectType.Attack1))
            defaultIds[BattleActorEffectType.Attack1] = "BasicAttack";
        if (!defaultIds.ContainsKey(BattleActorEffectType.Skill1))
            defaultIds[BattleActorEffectType.Skill1] = "BasicSkill";
        if (!defaultIds.ContainsKey(BattleActorEffectType.Hit))
            defaultIds[BattleActorEffectType.Hit] = "BasicHit";
        if (!defaultIds.ContainsKey(BattleActorEffectType.Heal))
            defaultIds[BattleActorEffectType.Heal] = "BasicHeal";

        return defaultIds;
    }

    /// <summary>
    /// 이펙트 재생
    /// </summary>
    public async UniTask<EffectInstance> PlayEffect(
        string effectId,
        BattleActor source,
        BattleActor target = null,
        Action<BattleActor> onHitCallback = null)
    {
        if (!effectDataDict.TryGetValue(effectId, out BattleEffectData effectData))
        {
            Debug.LogError($"[BattleEffectManager] Effect data not found: {effectId}");
            return null;
        }

        // 생성 지연
        if (effectData.spawnDelay > 0)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(effectData.spawnDelay));
        }

        // 이펙트 인스턴스 생성
        EffectInstance instance = await CreateEffectInstance(effectData, source, target);
        if (instance == null) return null;

        instance.onHit = onHitCallback;
        activeEffects.Add(instance);

        // 근접 공격인 경우 타이밍에 맞춰 피격 처리
        if (effectData.effectType == EffectType.AttackMelee && !effectData.isProjectile)
        {
            ProcessMeleeHit(instance, effectData).Forget();
        }
        // 버프/디버프인 경우
        else if (effectData.effectType == EffectType.Buff || effectData.effectType == EffectType.Debuff)
        {
            ProcessBuffDebuff(instance, effectData).Forget();
        }

        return instance;
    }

    /// <summary>
    /// 피격 이펙트 재생
    /// </summary>
    public async UniTask PlayHitEffect(string attackEffectId, BattleActor target, BattleActor attacker = null)
    {
        if (!effectDataDict.TryGetValue(attackEffectId, out BattleEffectData attackData))
        {
            Debug.LogWarning($"[BattleEffectManager] Attack effect not found: {attackEffectId}");
            return;
        }

        // 연결된 피격 이펙트 ID 확인
        string hitEffectId = attackData.hitEffectId;
        if (string.IsNullOrEmpty(hitEffectId))
        {
            hitEffectId = "BasicHit"; // 기본 피격 이펙트
        }

        await PlayEffect(hitEffectId, target, null, null);
    }

    /// <summary>
    /// 이펙트 인스턴스 생성
    /// </summary>
    private async UniTask<EffectInstance> CreateEffectInstance(
        BattleEffectData effectData,
        BattleActor source,
        BattleActor target)
    {
        // 이펙트 프리팹 경로 결정
        string prefabPath = GetEffectPrefabPath(effectData);
        if (string.IsNullOrEmpty(prefabPath))
        {
            Debug.LogError($"[BattleEffectManager] No prefab path for effect: {effectData.effectId}");
            return null;
        }

        // 이펙트 오브젝트 가져오기 (풀링)
        GameObject effectObject = await GetOrCreateEffect(prefabPath);
        if (effectObject == null)
        {
            Debug.LogError($"[BattleEffectManager] Failed to create effect object: {prefabPath}");
            return null;
        }

        // 스폰 위치 계산
        Vector3 spawnPosition = CalculateSpawnPosition(effectData, source, target);
        effectObject.transform.position = spawnPosition;

        // BattleActor의 방향에 따른 스케일 조정
        ApplyDirectionScale(effectObject, effectData, source, target);

        effectObject.SetActive(true);

        // 인스턴스 생성
        EffectInstance instance = new EffectInstance
        {
            effectData = effectData,
            effectObject = effectObject,
            source = source,
            target = target,
            startPosition = spawnPosition,
            targetPosition = target != null ? target.transform.position : spawnPosition,
            spawnTime = Time.time,
            isActive = true
        };

        // 프로젝타일 초기화
        if (effectData.isProjectile)
        {
            InitializeProjectile(instance, effectData);
        }

        return instance;
    }



    /// <summary>
    /// BattleActor의 방향에 따른 이펙트 스케일 조정
    /// </summary>
    private void ApplyDirectionScale(GameObject effectObject, BattleEffectData effectData, BattleActor source, BattleActor target)
    {
        // flipWithCharacter 옵션이 꺼져있으면 스케일 조정하지 않음
        if (!effectData.flipWithCharacter)
        {
            return;
        }

        // 참조할 Actor 결정 (attachToTarget이면 target, 아니면 source)
        BattleActor referenceActor = effectData.attachToTarget ? target : source;

        if (referenceActor == null)
        {
            return;
        }

        // 현재 스케일 가져오기
        Vector3 currentScale = effectObject.transform.localScale;

        // BattleActorDirection.Left 경우 X 스케일을 -1로 변경
        if (referenceActor.LookDirection == BattleActorDirection.Left)
        {
            currentScale.x = Mathf.Abs(currentScale.x) * -1f;
        }
        else // BattleActorDirection.Left일 경우 원래 스케일 유지
        {
            currentScale.x = Mathf.Abs(currentScale.x);
        }

        effectObject.transform.localScale = currentScale;
    }


    /// <summary>
    /// 이펙트 프리팹 경로 가져오기
    /// </summary>
    private string GetEffectPrefabPath(BattleEffectData effectData)
    {
        // BattleEffectData의 GetEffectPrefabPath 메서드 사용
        string path = effectData.GetEffectPrefabPath();
        if (!string.IsNullOrEmpty(path))
        {
            return path;
        }

        // Fallback
        if (effectData.ownerType == EffectOwnerType.Common)
        {
            return $"Battle/CommonEffect/{effectData.effectId}";
        }
        else if (effectData.ownerType == EffectOwnerType.Character && !string.IsNullOrEmpty(effectData.characterName))
        {
            return $"BattleActor/{effectData.characterName}/Effects/{effectData.effectId}";
        }

        return null;
    }

    /// <summary>
    /// 이펙트 오브젝트 가져오기 (풀링)
    /// </summary>
    private async UniTask<GameObject> GetOrCreateEffect(string prefabPath)
    {
        if (string.IsNullOrEmpty(prefabPath)) return null;

        // 풀에서 확인
        if (effectPools.TryGetValue(prefabPath, out Queue<GameObject> pool) && pool.Count > 0)
        {
            GameObject pooledObj = pool.Dequeue();
            if (pooledObj != null)
            {
                return pooledObj;
            }
        }

        // 새로 로드
        GameObject prefab = await UtilModel.Resources.LoadAsync<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[BattleEffectManager] Failed to load effect prefab: {prefabPath}");
            return null;
        }

        GameObject newEffect = Instantiate(prefab, effectContainer);
        return newEffect;
    }

    /// <summary>
    /// 스폰 위치 계산
    /// </summary>
    private Vector3 CalculateSpawnPosition(BattleEffectData effectData, BattleActor source, BattleActor target)
    {
        Vector3 basePosition = source != null ? source.transform.position : Vector3.zero;

        // 타겟 위치 사용
        if (target != null && effectData.attachToTarget)
        {
            basePosition = target.transform.position;
        }

        // 오프셋 적용 - 방향을 고려한 오프셋
        Vector3 finalOffset = effectData.positionOffset;

        // 소스의 방향에 따라 오프셋 X값 조정
        BattleActor referenceActor = effectData.attachToTarget ? target : source;
        if (referenceActor != null && referenceActor.LookDirection == BattleActorDirection.Right)
        {
            finalOffset.x *= -1f;
        }

        basePosition += finalOffset;

        return basePosition;
    }

    /// <summary>
    /// 프로젝타일 초기화 (수정)
    /// </summary>
    private void InitializeProjectile(EffectInstance instance, BattleEffectData effectData)
    {
        if (instance.target == null) return;

        // 타겟 방향 계산
        Vector3 direction = (instance.target.transform.position - instance.effectObject.transform.position).normalized;

        // 회전 설정 - 프로젝타일은 방향 뒤집기 대신 회전으로 처리
        if (!effectData.flipWithCharacter) // 프로젝타일은 일반적으로 회전으로 방향 설정
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            instance.effectObject.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }

        // 속도 초기화
        instance.currentVelocity = direction * effectData.projectileSpeed;
    }

    /// <summary>
    /// 근접 공격 피격 처리
    /// </summary>
    private async UniTaskVoid ProcessMeleeHit(EffectInstance instance, BattleEffectData effectData)
    {
        // 히트 타이밍 대기
        await UniTask.Delay(TimeSpan.FromSeconds(effectData.hitTiming));

        if (instance.isActive && instance.target != null && instance.onHit != null)
        {
            instance.onHit.Invoke(instance.target);
        }
    }

    /// <summary>
    /// 버프/디버프 처리
    /// </summary>
    private async UniTaskVoid ProcessBuffDebuff(EffectInstance instance, BattleEffectData effectData)
    {
        // 버프/디버프 적용 로직
        if (instance.target != null)
        {
            Debug.Log($"[BattleEffectManager] Applying {effectData.effectType} to {instance.target.name}");
        }

        // 지속 시간 후 제거
        if (effectData.duration > 0 && instance.effectData.loop == false)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(effectData.duration));
            RemoveEffect(instance);
        }
    }

    /// <summary>
    /// 활성 이펙트 업데이트
    /// </summary>
    private void UpdateActiveEffects(float deltaTime)
    {
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            var instance = activeEffects[i];
            if (!instance.isActive)
            {
                RemoveEffect(instance);
                activeEffects.RemoveAt(i);
                continue;
            }

            // 프로젝타일 이동
            if (instance.effectData.isProjectile && instance.target != null)
            {
                UpdateProjectile(instance, deltaTime);
            }

            // 수명 체크
            if (instance.effectData.duration > 0 && instance.effectData.loop == false )
            {
                float elapsed = Time.time - instance.spawnTime;
                if (elapsed >= instance.effectData.duration)
                {
                    RemoveEffect(instance);
                    activeEffects.RemoveAt(i);
                }
            }
        }
    }

    /// <summary>
    /// 프로젝타일 업데이트
    /// </summary>
    private void UpdateProjectile(EffectInstance instance, float deltaTime)
    {
        if (instance.effectObject == null || instance.target == null) return;

        Vector3 targetPosition = instance.target.transform.position;
        Vector3 currentPosition = instance.effectObject.transform.position;

        // 궤적에 따른 이동
        switch (instance.effectData.trajectory)
        {
            case ProjectileTrajectory.Straight:
                // 직선 이동
                Vector3 direction = (targetPosition - currentPosition).normalized;
                instance.effectObject.transform.position += direction * instance.effectData.projectileSpeed * deltaTime;
                break;

            case ProjectileTrajectory.Arc:
                // 포물선 이동
                instance.trajectoryProgress += deltaTime * instance.effectData.projectileSpeed /
                    Vector3.Distance(instance.startPosition, instance.targetPosition);

                if (instance.effectData.trajectoryHeightCurve != null)
                {
                    float height = instance.effectData.trajectoryHeightCurve.Evaluate(instance.trajectoryProgress);
                    Vector3 linearPos = Vector3.Lerp(instance.startPosition, targetPosition, instance.trajectoryProgress);
                    instance.effectObject.transform.position = linearPos + Vector3.up * height;
                }
                break;

            case ProjectileTrajectory.Homing:
                // 유도 미사일
                Vector3 homingDirection = (targetPosition - currentPosition).normalized;
                instance.currentVelocity = Vector3.Lerp(instance.currentVelocity,
                    homingDirection * instance.effectData.projectileSpeed,
                    instance.effectData.homingStrength * deltaTime);
                instance.effectObject.transform.position += instance.currentVelocity * deltaTime;
                break;
        }

        // 도착 체크
        float distanceToTarget = Vector3.Distance(currentPosition, targetPosition);
        if (distanceToTarget < 0.1f || instance.trajectoryProgress >= 1f)
        {
            // 피격 처리
            if (instance.onHit != null)
            {
                instance.onHit.Invoke(instance.target);
            }

            // 피격 이펙트 재생
            if (!string.IsNullOrEmpty(instance.effectData.hitEffectId))
            {
                PlayHitEffect(instance.effectData.effectId, instance.target, instance.source).Forget();
            }

            instance.isActive = false;
        }
    }

    /// <summary>
    /// 이펙트 제거
    /// </summary>
    private void RemoveEffect(EffectInstance instance)
    {
        if (instance.effectObject != null)
        {
            // 풀에 반환
            string prefabPath = GetEffectPrefabPath(instance.effectData);
            if (!string.IsNullOrEmpty(prefabPath))
            {
                if (!effectPools.ContainsKey(prefabPath))
                {
                    effectPools[prefabPath] = new Queue<GameObject>();
                }

                instance.effectObject.SetActive(false);

                if (effectPools[prefabPath].Count < POOL_SIZE)
                {
                    effectPools[prefabPath].Enqueue(instance.effectObject);
                }
                else
                {
                    Destroy(instance.effectObject);
                }
            }
            else
            {
                Destroy(instance.effectObject);
            }
        }

        instance.isActive = false;
    }

    /// <summary>
    /// 모든 이펙트 제거
    /// </summary>
    public void ClearAllEffects()
    {
        foreach (var instance in activeEffects)
        {
            if (instance.effectObject != null)
            {
                Destroy(instance.effectObject);
            }
        }
        activeEffects.Clear();

        // 풀 정리
        foreach (var pool in effectPools.Values)
        {
            foreach (var obj in pool)
            {
                if (obj != null)
                {
                    Destroy(obj);
                }
            }
        }
        effectPools.Clear();
    }
}