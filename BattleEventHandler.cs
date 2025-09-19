using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DG.Tweening;
using Cinemachine;
using Cysharp.Threading.Tasks;
using SkillSystem;

/// <summary>
/// Battle Timeline Event를 처리하는 핸들러
/// TimelineEventHandler와 연동하여 전투 이벤트 실행
/// </summary>
public class BattleEventHandler : MonoBehaviour
{
    #region Fields

    [Header("Battle Context")]
    private BattleActor caster;
    private List<BattleActor> targets = new List<BattleActor>();
    private AdvancedSkillData currentSkill;

    [Header("Damage Calculation")]
    private float totalCalculatedDamage = 0f;
    private Dictionary<BattleActor, float> individualDamages = new Dictionary<BattleActor, float>();
    private float accumulatedDamagePercent = 0f;  // 누적 데미지 퍼센트 체크용

    [Header("Position Management")]
    private Dictionary<BattleActor, Vector3> originalPositions = new Dictionary<BattleActor, Vector3>();
    private Dictionary<BattleActor, Coroutine> moveCoroutines = new Dictionary<BattleActor, Coroutine>();

    [Header("Camera Management")]
    private Dictionary<string, CinemachineVirtualCamera> virtualCameras = new Dictionary<string, CinemachineVirtualCamera>();
    private CinemachineVirtualCamera previousCamera;
    private CinemachineBrain cinemachineBrain;

    [Header("Tween Management")]
    private Dictionary<BattleActor, List<Tween>> activeTweens = new Dictionary<BattleActor, List<Tween>>();

    [Header("Effect Management")]
    private Dictionary<string, GameObject> activeEffects = new Dictionary<string, GameObject>();

    [Header("Settings")]
    [SerializeField] private bool debugMode = false;

    #endregion

    #region Initialization

    private void Awake()
    {
        cinemachineBrain = Camera.main?.GetComponent<CinemachineBrain>();
    }

    /// <summary>
    /// Battle Event Handler 초기화
    /// </summary>
    public void Initialize(BattleActor skillCaster, List<BattleActor> skillTargets)
    {
        caster = skillCaster;
        targets = skillTargets ?? new List<BattleActor>();

        // 현재 스킬 가져오기
        GetCurrentSkillData();

        // 원위치 저장
        SaveOriginalPositions();

        // 카메라 캐싱
        CacheVirtualCameras();

        // 총 데미지 계산
        CalculateTotalDamage();

        // 누적 퍼센트 리셋
        accumulatedDamagePercent = 0f;

        if (debugMode)
        {
            Debug.Log($"[BattleEventHandler] Initialized - Caster: {caster?.name}, Targets: {targets.Count}, Skill: {currentSkill?.skillName}");
        }
    }

    private void GetCurrentSkillData()
    {
        // BattleProcessManagerNew에서 현재 스킬 가져오기
        var battleManager = BattleProcessManagerNew.Instance;
        if (battleManager != null)
        {
            currentSkill = battleManager.GetSelectedSkill();
        }

        // 없으면 caster의 스킬에서 가져오기
        if (currentSkill == null && caster != null)
        {
            var availableSkill = caster.GetAvailableActiveSkill();
            if (availableSkill != null)
            {
                currentSkill = availableSkill.SkillData;
            }
        }
    }

    private void SaveOriginalPositions()
    {
        originalPositions.Clear();

        if (caster != null)
        {
            originalPositions[caster] = caster.transform.position;
        }

        foreach (var target in targets)
        {
            if (target != null)
            {
                originalPositions[target] = target.transform.position;
            }
        }
    }

    private void CacheVirtualCameras()
    {
        virtualCameras.Clear();

        var cameras = FindObjectsOfType<CinemachineVirtualCamera>();
        foreach (var cam in cameras)
        {
            virtualCameras[cam.name] = cam;

            // 현재 활성 카메라 찾기
            if (cam.Priority == 10)
            {
                previousCamera = cam;
            }
        }

        if (debugMode)
        {
            Debug.Log($"[BattleEventHandler] Cached {virtualCameras.Count} virtual cameras");
        }
    }

    private void CalculateTotalDamage()
    {
        if (caster == null || currentSkill == null)
        {
            totalCalculatedDamage = 0f;
            return;
        }

        individualDamages.Clear();
        totalCalculatedDamage = 0f;

        // 스킬의 첫 번째 데미지 효과 찾기
        float skillPower = 100f;  // 기본값
        var damageEffect = currentSkill.effects.FirstOrDefault(e => e.IsDamageType());
        if (damageEffect != null)
        {
            skillPower = damageEffect.GetSkillPower();
        }

        // 각 타겟별로 데미지 계산
        foreach (var target in targets)
        {
            if (target == null || target.BattleActorInfo == null) continue;

            // BattleFormularHelper의 Context 생성
            var context = new BattleFormularHelper.BattleContext
            {
                Attacker = caster,
                Defender = target,
                SkillPower = (int)skillPower,  // AdvancedSkillEffect의 value 또는 customSkillPower 사용
                IsSkillAttack = true,
                AttackType = GetAttackType(damageEffect),
                UsedBP = caster.BattleActorInfo?.UsedBPThisTurn ?? 0
            };

            // 데미지 계산 (판정 제외, 순수 데미지만)
            var damageResult = BattleFormularHelper.CalculateDamage(context);

            // 판정 무시하고 기본 데미지만 저장 (실제 적용시 개별 판정)
            float baseDamage = damageResult.OriginalDamage * (skillPower / 100f);
            individualDamages[target] = baseDamage;
            totalCalculatedDamage += baseDamage;
        }

        if (debugMode)
        {
            Debug.Log($"[BattleEventHandler] Total calculated damage: {totalCalculatedDamage} for {targets.Count} targets");
            Debug.Log($"[BattleEventHandler] Skill Power: {skillPower}%");
        }
    }

    private EAttackType GetAttackType(AdvancedSkillEffect effect)
    {
        if (effect == null) return EAttackType.Physical;

        // SkillEffect의 damageType을 EAttackType으로 변환
       /* if (effect.damageType == DamageType.Physical)
            return EAttackType.Physical;
        else if (effect.damageType == DamageType.Magical)
            return EAttackType.Magic;
        else if (effect.damageType == DamageType.True)
            return EAttackType.Special;  // True damage는 Special로 처리
        else*/

            return EAttackType.Physical;
    }

    #endregion

    #region Public Event Methods (TimelineEventHandler에서 호출)

    /// <summary>
    /// 데미지 적용 (퍼센트)
    /// </summary>
    public void ApplyDamagePercent(float damagePercent)
    {
        if (targets == null || targets.Count == 0) return;

        // 누적 체크 (100% 초과 방지)
        accumulatedDamagePercent += damagePercent;
        if (accumulatedDamagePercent > 100f)
        {
            Debug.LogWarning($"[BattleEventHandler] Total damage exceeds 100%! ({accumulatedDamagePercent}%)");
            damagePercent = 100f - (accumulatedDamagePercent - damagePercent);
        }

        foreach (var target in targets)
        {
            if (target == null || target.BattleActorInfo.IsDead) continue;

            float baseDamage = individualDamages.ContainsKey(target)
                ? individualDamages[target]
                : totalCalculatedDamage / targets.Count;

            float damage = baseDamage * (damagePercent / 100f);

            // 개별 판정 (크리티컬, 회피 등)
            var damageEffect = currentSkill?.effects.FirstOrDefault(e => e.IsDamageType());
            var context = new BattleFormularHelper.BattleContext
            {
                Attacker = caster,
                Defender = target,
                SkillPower = 100,  // 이미 계산된 데미지 사용
                IsSkillAttack = true,
                AttackType = GetAttackType(damageEffect),
                UsedBP = caster.BattleActorInfo?.UsedBPThisTurn ?? 0
            };

            // 판정만 체크
            var judgement = BattleFormularHelper.CheckJudgement(context);

            // Miss나 Dodge면 스킵
            if ((judgement & BattleFormularHelper.JudgementType.Miss) != 0 ||
                (judgement & BattleFormularHelper.JudgementType.Dodge) != 0)
            {
                ShowMissEffect(target);
                continue;
            }

            // Critical 체크
            bool isCritical = (judgement & BattleFormularHelper.JudgementType.Critical) != 0;
            if (isCritical)
            {
                float critMultiplier = caster.BattleActorInfo.CriDmg;
                damage *= critMultiplier;
            }

            // Block 체크
            bool isBlocked = (judgement & BattleFormularHelper.JudgementType.Block) != 0;
            if (isBlocked)
            {
                damage *= 0.5f;  // Block은 50% 감소
            }

            // 최종 데미지 적용
            int finalDamage = Mathf.Max(1, Mathf.RoundToInt(damage));
            target.BattleActorInfo.TakeDamage(finalDamage);

            // 데미지 UI 표시
            ShowDamageNumber(target, finalDamage, isCritical, isBlocked);

            if (debugMode)
            {
                Debug.Log($"[BattleEventHandler] Applied {finalDamage} damage to {target.name} ({damagePercent}% of total)");
            }
        }
    }

    /// <summary>
    /// 히트 이펙트 표시
    /// </summary>
    public void ShowHitEffect(string effectKey)
    {
        if (string.IsNullOrEmpty(effectKey)) return;

        foreach (var target in targets)
        {
            if (target == null) continue;

            SpawnEffect(effectKey, target.transform.position, target.transform);
        }
    }

    /// <summary>
    /// 카메라 전환
    /// </summary>
    public void SwitchCamera(string cameraName, float blendTime)
    {
        if (string.IsNullOrEmpty(cameraName)) return;

        if (virtualCameras.TryGetValue(cameraName, out var targetCamera))
        {
            // 이전 카메라 저장
            foreach (var cam in virtualCameras.Values)
            {
                if (cam.Priority == 10)
                {
                    previousCamera = cam;
                    cam.Priority = 0;
                }
            }

            // 타겟 카메라 활성화
            targetCamera.Priority = 10;

            // 블렌드 시간 설정
            if (cinemachineBrain != null)
            {
                cinemachineBrain.m_DefaultBlend.m_Time = blendTime;
            }

            if (debugMode)
            {
                Debug.Log($"[BattleEventHandler] Switched to camera: {cameraName}");
            }
        }
        else
        {
            Debug.LogWarning($"[BattleEventHandler] Camera '{cameraName}' not found!");
        }
    }

    /// <summary>
    /// 타겟 이동
    /// </summary>
    public void MoveTargetsToPosition(string positionName, bool instant)
    {
        Vector3 targetPos = GetPositionByName(positionName);

        foreach (var target in targets)
        {
            if (target == null) continue;

            if (instant)
            {
                target.transform.position = targetPos;
            }
            else
            {
                // 기존 이동 중지
                if (moveCoroutines.ContainsKey(target) && moveCoroutines[target] != null)
                {
                    StopCoroutine(moveCoroutines[target]);
                }

                // DOTween으로 이동
                target.transform.DOMove(targetPos, 0.5f)
                    .SetEase(Ease.InOutQuad)
                    .OnComplete(() => moveCoroutines.Remove(target));
            }
        }

        if (debugMode)
        {
            Debug.Log($"[BattleEventHandler] Moving targets to: {positionName}");
        }
    }

    /// <summary>
    /// 트윈 효과 적용
    /// </summary>
    public void ApplyTweenToTargets(string tweenType, float duration)
    {
        foreach (var target in targets)
        {
            if (target == null) continue;

            // 기존 트윈 제거
            if (activeTweens.ContainsKey(target))
            {
                foreach (var tween in activeTweens[target])
                {
                    tween?.Kill();
                }
                activeTweens[target].Clear();
            }
            else
            {
                activeTweens[target] = new List<Tween>();
            }

            Tween newTween = null;

            switch (tweenType.ToLower())
            {
                case "scale":
                    newTween = target.transform.DOScale(Vector3.one * 1.2f, duration)
                        .SetEase(Ease.OutElastic)
                        .SetLoops(2, LoopType.Yoyo);
                    break;

                case "shake":
                    newTween = target.transform.DOShakePosition(duration, 0.5f, 10, 90, false, true);
                    break;

                case "rotate":
                    newTween = target.transform.DORotate(new Vector3(0, 360, 0), duration, RotateMode.LocalAxisAdd)
                        .SetEase(Ease.Linear);
                    break;

                case "color":
                    var renderer = target.GetComponent<SpriteRenderer>();
                    if (renderer != null)
                    {
                        newTween = renderer.DOColor(Color.red, duration * 0.5f)
                            .SetLoops(2, LoopType.Yoyo);
                    }
                    break;

                case "punch":
                    newTween = target.transform.DOPunchScale(Vector3.one * 0.3f, duration, 3);
                    break;
            }

            if (newTween != null)
            {
                activeTweens[target].Add(newTween);
            }
        }

        if (debugMode)
        {
            Debug.Log($"[BattleEventHandler] Applied tween: {tweenType}");
        }
    }

    #endregion

    #region Helper Methods

    private Vector3 GetPositionByName(string name)
    {
        switch (name.ToLower())
        {
            case "center":
                return Vector3.zero;

            case "front":
                return new Vector3(0, 0, 5);

            case "back":
                return new Vector3(0, 0, -5);

            case "original":
                if (targets.Count > 0 && originalPositions.ContainsKey(targets[0]))
                {
                    return originalPositions[targets[0]];
                }
                return Vector3.zero;

            case "caster":
                return caster != null ? caster.transform.position : Vector3.zero;

            case "between":
                // 시전자와 타겟 사이
                if (caster != null && targets.Count > 0)
                {
                    return Vector3.Lerp(caster.transform.position, targets[0].transform.position, 0.5f);
                }
                return Vector3.zero;

            default:
                // 숫자가 포함되어 있으면 오프셋으로 해석
                if (name.Contains(","))
                {
                    string[] coords = name.Split(',');
                    if (coords.Length >= 3)
                    {
                        float.TryParse(coords[0], out float x);
                        float.TryParse(coords[1], out float y);
                        float.TryParse(coords[2], out float z);
                        return new Vector3(x, y, z);
                    }
                }
                return Vector3.zero;
        }
    }

    private void ShowDamageNumber(BattleActor target, int damage, bool isCritical, bool isBlocked)
    {
        var damageUI = FindObjectOfType<BattleDamageNumberUI>();
        if (damageUI != null)
        {
            Color color = Color.white;
            float scale = 1f;

            if (isCritical)
            {
                color = Color.yellow;
                scale = 1.5f;
            }
            else if (isBlocked)
            {
                color = Color.gray;
                scale = 0.8f;
            }

            damageUI.ShowDamage(target.transform.position, damage, isCritical, false);
        }

        // 데미지 텍스트 UI가 없으면 로그
        if (damageUI == null && debugMode)
        {
            Debug.Log($"[BattleEventHandler] Damage: {damage} to {target.name} (Crit:{isCritical}, Block:{isBlocked})");
        }
    }

    private void ShowMissEffect(BattleActor target)
    {
        var damageUI = FindObjectOfType<BattleDamageNumberUI>();
        if (damageUI != null)
        {
            damageUI.ShowDamage(target.transform.position, 0, false, true);  // Miss 표시
        }

        if (debugMode)
        {
            Debug.Log($"[BattleEventHandler] MISS on {target.name}");
        }
    }

    private async void SpawnEffect(string effectKey, Vector3 position, Transform parent = null)
    {
        try
        {
            // BattleEffectManager가 있으면 사용
            if (BattleEffectManager.Instance != null)
            {
                //await BattleEffectManager.Instance.PlayEffect(effectKey, position, Quaternion.identity);
            }
            else
            {
                // 직접 Addressable 로드
                var handle = UnityEngine.AddressableAssets.Addressables.InstantiateAsync(effectKey, position, Quaternion.identity);
                var effect = await handle;

                if (effect != null)
                {
                    if (parent != null)
                    {
                        effect.transform.SetParent(parent);
                    }

                    activeEffects[effectKey] = effect;

                    // 파티클 시스템이면 자동 제거
                    var ps = effect.GetComponent<ParticleSystem>();
                    if (ps != null)
                    {
                        Destroy(effect, ps.main.duration + ps.main.startLifetime.constantMax);
                    }
                    else
                    {
                        Destroy(effect, 2f);
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[BattleEventHandler] Failed to spawn effect '{effectKey}': {e.Message}");
        }
    }

    #endregion

    #region Cleanup

    /// <summary>
    /// 모든 상태 리셋
    /// </summary>
    public void ResetAll()
    {
        // 위치 복원
        RestoreOriginalPositions();

        // 트윈 정리
        CleanupTweens();

        // 이펙트 정리
        CleanupEffects();

        // 카메라 복원
        RestoreCamera();

        // 누적 데미지 리셋
        accumulatedDamagePercent = 0f;
    }

    private void RestoreOriginalPositions()
    {
        foreach (var kvp in originalPositions)
        {
            if (kvp.Key != null)
            {
                kvp.Key.transform.position = kvp.Value;
            }
        }
    }

    private void CleanupTweens()
    {
        foreach (var kvp in activeTweens)
        {
            foreach (var tween in kvp.Value)
            {
                tween?.Kill();
            }
        }
        activeTweens.Clear();
    }

    private void CleanupEffects()
    {
        foreach (var effect in activeEffects.Values)
        {
            if (effect != null)
            {
                Destroy(effect);
            }
        }
        activeEffects.Clear();
    }

    private void RestoreCamera()
    {
        if (previousCamera != null)
        {
            // 모든 카메라 우선순위 리셋
            foreach (var cam in virtualCameras.Values)
            {
                cam.Priority = 0;
            }

            // 이전 카메라 복원
            previousCamera.Priority = 10;
        }
    }

    private void OnDestroy()
    {
        ResetAll();
    }

    #endregion
}