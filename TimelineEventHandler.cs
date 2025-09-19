using System;
using System.Collections.Generic;
using UnityEngine;
using BattleCharacterSystem.Timeline;
using UnityEditor;

namespace Cosmos.Timeline.Playback
{
    /// <summary>
    /// Timeline 이벤트 실제 처리 구현
    /// </summary>
    public class TimelineEventHandler : MonoBehaviour, ITimelineEventHandler
    {
        #region Fields

        // 타겟 참조
        private Animator targetAnimator;
        public Animator TargetAnimator => targetAnimator;

        private Transform targetTransform;
        private GameObject targetObject;

        // 리소스 프로바이더
        private IResourceProvider resourceProvider;

        // ===== Battle 모드 추가 =====
        private BattleActor battleActor = null;


        // 활성 이펙트 관리
        private Dictionary<string, List<GameObject>> activeEffects = new Dictionary<string, List<GameObject>>();
        private Dictionary<string, Coroutine> effectCoroutines = new Dictionary<string, Coroutine>();

        // 사운드 관리
        private AudioSource audioSource;
        private Dictionary<string, AudioSource> activeSounds = new Dictionary<string, AudioSource>();

        // 이벤트
        public event Action<TimelineDataSO.ITimelineEvent> OnEventTriggered;
        public event Action<TimelineDataSO.ITimelineEvent> OnEventCompleted;


        // 클래스 필드에 추가
        private RuntimeAnimatorController genericController;
        private AnimatorOverrideController overrideController;
        private Dictionary<string, AnimationClip> clipCache = new Dictionary<string, AnimationClip>();


        // 설정
        [SerializeField] private bool debugMode = true;

        private bool initialize = false;

        #endregion

        #region Initialization

        private void Awake()
        {
            Initialize();
        }
        #region Battle Mode Initialization (새로 추가)

        /// <summary>
        /// Battle 모드로 초기화
        /// </summary>
        public void InitializeForBattle(BattleActor actor)
        {
            if (actor == null) return;

            battleActor = actor;

            // Battle 모드에서는 BattleActor의 컴포넌트 사용
            targetObject = actor.gameObject;
            targetTransform = actor.transform;

            if (targetAnimator == null)
            {
                targetAnimator = actor.GetComponent<Animator>();
                if (targetAnimator == null)
                    targetAnimator = actor.gameObject.AddComponent<Animator>();
            }

            // Resource Provider 찾기
            resourceProvider = GetComponent<IResourceProvider>();
            if (resourceProvider == null)
            {
                // 없으면 생성
                resourceProvider = gameObject.AddComponent<AddressableResourceProvider>();
                Debug.Log("[EventHandler] Created new ResourceProvider");
            }

            // Generic Controller 로드
            LoadGenericController();

            if (debugMode)
                Debug.Log($"[TimelineEventHandler] Initialized for Battle: {actor.name}");

            initialize = true;
        }

        #endregion


        public void Initialize()
        {
            if (initialize == true) return;

            if (targetAnimator == null)
            {
                // Animator 찾기
                targetAnimator = GetComponent<Animator>();
                if (targetAnimator == null)
                {
                    targetAnimator = GetComponentInChildren<Animator>();
                }

                if (targetAnimator == null)
                    targetAnimator = this.gameObject.AddComponent<Animator>();
            }

            targetTransform = transform;
            targetObject = gameObject;

            // Resource Provider 찾기
            resourceProvider = GetComponent<IResourceProvider>();
            if (resourceProvider == null)
            {
                // 없으면 생성
                resourceProvider = gameObject.AddComponent<AddressableResourceProvider>();
                Debug.Log("[EventHandler] Created new ResourceProvider");
            }

            // Generic Controller 로드
            LoadGenericController();
        }

        public void SetTarget(GameObject target)
        {
            if (target == null) return;

            targetObject = target;
            targetTransform = target.transform;
            targetAnimator = target.GetComponent<Animator>() ?? target.GetComponentInChildren<Animator>();

            if (debugMode)
                Debug.Log($"[EventHandler] Target set: {target.name}");
        }

        public void SetResourceProvider(IResourceProvider provider)
        {
            resourceProvider = provider;
        }

        #endregion

        #region Animation Events

        public void HandleAnimationEvent(TimelineDataSO.AnimationEvent animEvent)
        {
            if (targetAnimator == null)
            {
                Debug.LogWarning($"[EventHandler] No Animator found for animation: {animEvent.animationStateName}");
                return;
            }

            // extractedClip이 있으면 Override Controller 사용
            if (animEvent.extractedClip != null)
            {
                ApplyAnimationClipOverride(animEvent.extractedClip, animEvent.animationStateName);
            }
            else
            {
                OnEventTriggered?.Invoke(animEvent);

                // Play Mode에 따른 처리
                switch (animEvent.playMode)
                {
                    case TimelineDataSO.AnimationPlayMode.Play:
                        PlayAnimation(animEvent);
                        break;

                    case TimelineDataSO.AnimationPlayMode.CrossFade:
                        CrossFadeAnimation(animEvent);
                        break;

                    case TimelineDataSO.AnimationPlayMode.CrossFadeQueued:
                        CrossFadeQueuedAnimation(animEvent);
                        break;

                    case TimelineDataSO.AnimationPlayMode.PlayQueued:
                        PlayQueuedAnimation(animEvent);
                        break;
                }

                // Animator 속도 설정
                if (animEvent.clipSpeed > 0)
                {
                    targetAnimator.speed = animEvent.clipSpeed;
                }

                OnEventCompleted?.Invoke(animEvent);

                if (debugMode)
                    Debug.Log($"[EventHandler] Animation played: {animEvent.animationStateName}");
            }
        }


        // Awake나 Initialize에서 로드
        private void LoadGenericController()
        {
            if (genericController == null)
            {
                genericController = Resources.Load<RuntimeAnimatorController>("AnimatorControllers/GenericRuntimeAnimatorController");
                // 또는 Addressable로 로드
            }
        }



        // 새 메서드 추가 - Track 애니메이션 처리
        public void HandleTrackAnimation(TimelineDataSO.TrackAnimation trackAnim)
        {
            if (trackAnim.animationClip == null || targetAnimator == null)
            {
                Debug.LogWarning($"[EventHandler] Cannot play track animation: clip or animator is null {targetAnimator} / {trackAnim.animationClip}");
                return;
            }


            // Animator 상태 확인
            Debug.LogError($"[HandleTrackAnimation] Animator enabled: {targetAnimator.enabled}");
            Debug.LogError($"[HandleTrackAnimation] Current Controller: {targetAnimator.runtimeAnimatorController?.name}");


            ApplyAnimationClipOverride(trackAnim.animationClip, trackAnim.trackName);

        }
        // 공통 Override 적용 메서드
        private void ApplyAnimationClipOverride(AnimationClip clip, string stateName)
        {
            if (clip == null || targetAnimator == null) return;

            // 디버깅 정보
            Debug.Log($"[EventHandler] Trying to play clip: {clip.name}, state: {stateName}");

            // Generic Controller 확인
            if (genericController == null)
            {
                LoadGenericController();
                if (genericController == null)
                {
                    Debug.LogError("[EventHandler] GenericController is still null");
                    return;
                }
            }

            
            // Override Controller 생성
          
            overrideController = new AnimatorOverrideController(genericController);

            // Override 가능한 클립 목록 가져오기
             
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
              
            overrideController.GetOverrides(overrides);

            int stateCount = overrides.Count;
              
            Debug.LogError($"[EventHandler] Override Controller has {stateCount} states");

            // "GenericClip" State에 클립 할당
            overrideController["GenericClip"] = clip;

            // Animator에 적용
            targetAnimator.runtimeAnimatorController = overrideController;

            // 재생
            targetAnimator.Play("GenericClip", 0, 0f);
            
            // 강제 업데이트
            targetAnimator.Update(0f);

            clipCache[stateName] = clip;

            DebugAnimationClip(clip);

        }

        private void DebugAnimationClip(AnimationClip clip)
        {
            if (clip == null)
            {
                Debug.LogError("Clip is null!");
                return;
            }

            /*Debug.LogError($"=== AnimationClip Info: {clip.name} ===");
            Debug.LogError($"Length: {clip.length} seconds");
            Debug.LogError($"Frame Rate: {clip.frameRate}");
            Debug.LogError($"Legacy: {clip.legacy}");
            Debug.LogError($"Loop: {clip.isLooping}");

            // 바인딩 정보
            var bindings = AnimationUtility.GetCurveBindings(clip);
            Debug.LogError($"Curve Count: {bindings.Length}");


            foreach( var b in bindings )
            {
                Debug.LogError($"  - Path: {b.path}, Property: {b.propertyName}");

            }*/
        }


        private void PlayAnimation(TimelineDataSO.AnimationEvent animEvent)
        {
            // State name으로 재생 시도
            targetAnimator.Play(animEvent.animationStateName, 0, 0f);

            // Trigger로도 시도 (파라미터가 있는지 확인)
            if (HasAnimatorParameter(targetAnimator, animEvent.animationStateName))
            {
                targetAnimator.SetTrigger(animEvent.animationStateName);
            }
        }
        private bool HasAnimatorParameter(Animator animator, string parameterName)
        {
            if (animator == null || string.IsNullOrEmpty(parameterName)) return false;

            foreach (var parameter in animator.parameters)
            {
                if (parameter.name == parameterName)
                {
                    return true;
                }
            }

            return false;
        }
        private void CrossFadeAnimation(TimelineDataSO.AnimationEvent animEvent)
        {
            targetAnimator.CrossFade(
                animEvent.animationStateName,
                animEvent.crossFadeDuration,
                0,
                0f
            );
        }

        private void CrossFadeQueuedAnimation(TimelineDataSO.AnimationEvent animEvent)
        {
            targetAnimator.CrossFadeInFixedTime(
                animEvent.animationStateName,
                animEvent.crossFadeDuration
            );
        }

        private void PlayQueuedAnimation(TimelineDataSO.AnimationEvent animEvent)
        {
            targetAnimator.PlayInFixedTime(animEvent.animationStateName);
        }

        #endregion

        #region Effect Events

        public void HandleEffectEvent(TimelineDataSO.EffectEvent effectEvent)
        {   
            
            // Editor 모드에서는 Effect 생성 건너뛰기
            if (!Application.isPlaying)
                return;


            if (resourceProvider == null)
            {
                Debug.LogWarning($"[EventHandler] No ResourceProvider for effect: {effectEvent.effectAddressableKey}");
                return;
            }

            try
            {
                OnEventTriggered?.Invoke(effectEvent);

                // 위치 계산
                Vector3 spawnPosition = CalculateEffectPosition(effectEvent);
                Quaternion spawnRotation = CalculateEffectRotation(effectEvent);

                // 이펙트 생성
                resourceProvider.LoadResourceAsync<GameObject>(
                    effectEvent.effectAddressableKey,
                    prefab => SpawnEffect(prefab, effectEvent, spawnPosition, spawnRotation)
                );

                if (debugMode)
                    Debug.Log($"[EventHandler] Effect spawning: {effectEvent.effectAddressableKey}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[EventHandler] Effect event failed: {e.Message}");
            }
        }

        private void SpawnEffect(GameObject prefab, TimelineDataSO.EffectEvent effectEvent,
                                 Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return;

            // 인스턴스 생성
            GameObject effectInstance = Instantiate(prefab, position, rotation);
            effectInstance.transform.localScale = Vector3.one * effectEvent.scale;

            // 부착 처리
            if (effectEvent.attachToActor)
            {
                AttachEffectToTarget(effectInstance, effectEvent);
            }

            // 활성 이펙트 등록
            RegisterActiveEffect(effectEvent.effectAddressableKey, effectInstance);

            // Duration 처리
            HandleEffectDuration(effectEvent, effectInstance);

            OnEventCompleted?.Invoke(effectEvent);
        }

        private void AttachEffectToTarget(GameObject effectInstance, TimelineDataSO.EffectEvent effectEvent)
        {
            Transform attachTarget = targetTransform;

            // 특정 본에 부착
            if (!string.IsNullOrEmpty(effectEvent.attachBoneName))
            {
                attachTarget = FindBone(effectEvent.attachBoneName) ?? targetTransform;
            }

            effectInstance.transform.SetParent(attachTarget);
            effectInstance.transform.localPosition = effectEvent.positionOffset;
            effectInstance.transform.localRotation = Quaternion.Euler(effectEvent.rotationOffset);
        }

        private Transform FindBone(string boneName)
        {
            if (targetAnimator == null) return null;

            // HumanBodyBones로 찾기
            if (targetAnimator.isHuman)
            {
                if (Enum.TryParse<HumanBodyBones>(boneName, true, out var bone))
                {
                    return targetAnimator.GetBoneTransform(bone);
                }
            }

            // Transform 이름으로 찾기
            return targetTransform.Find(boneName);
        }

        // HandleEffectDuration 메서드 수정
        private void HandleEffectDuration(TimelineDataSO.EffectEvent effectEvent, GameObject effectInstance)
        {
            string key = $"{effectEvent.effectAddressableKey}_{Time.time}";

            switch (effectEvent.playMode)
            {
                case TimelineDataSO.EffectPlayMode.OneShot:
                    var particleSystem = effectInstance.GetComponent<ParticleSystem>();
                    if (particleSystem != null)
                    {
                        var coroutine = StartCoroutine(DestroyAfterParticles(effectInstance, particleSystem, key));
                        effectCoroutines[key] = coroutine;
                    }
                    else
                    {
                        // Editor에서는 DestroyImmediate 사용
#if UNITY_EDITOR
                        if (!Application.isPlaying)
                            StartCoroutine(DestroyAfterDelay(effectInstance, 2f));  // 이것도 문제! 지연 불가
                        else
#endif
                            Destroy(effectInstance, 2f);
                    }
                    break;

                case TimelineDataSO.EffectPlayMode.Duration:
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        StartCoroutine(DestroyAfterDelay(effectInstance, effectEvent.duration));
                    else
#endif
                        Destroy(effectInstance, effectEvent.duration);
                    break;
            }
        }


        private System.Collections.IEnumerator DestroyAfterDelay(GameObject obj, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (obj != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(obj);
                else
#endif
                    Destroy(obj);
            }
        }


        private System.Collections.IEnumerator DestroyAfterParticles(GameObject effect, ParticleSystem ps, string key)
        {
            // ParticleSystem이 완료될 때까지 대기
            yield return new WaitWhile(() => ps != null && ps.IsAlive());

            if (effect != null)
            {
                UnregisterActiveEffect(key, effect);
                Destroy(effect);
            }

            if (effectCoroutines.ContainsKey(key))
            {
                effectCoroutines.Remove(key);
            }
        }

        private Vector3 CalculateEffectPosition(TimelineDataSO.EffectEvent effectEvent)
        {
            if (effectEvent.attachToActor && targetTransform != null)
            {
                return targetTransform.position + targetTransform.TransformDirection(effectEvent.positionOffset);
            }
            else
            {
                return targetTransform != null
                    ? targetTransform.position + effectEvent.positionOffset
                    : effectEvent.positionOffset;
            }
        }

        private Quaternion CalculateEffectRotation(TimelineDataSO.EffectEvent effectEvent)
        {
            Quaternion baseRotation = targetTransform != null ? targetTransform.rotation : Quaternion.identity;
            return baseRotation * Quaternion.Euler(effectEvent.rotationOffset);
        }

        #endregion

        #region Sound Events

        public void HandleSoundEvent(TimelineDataSO.SoundEvent soundEvent)
        {
            try
            {
                OnEventTriggered?.Invoke(soundEvent);

                if (string.IsNullOrEmpty(soundEvent.soundEventPath))
                {
                    Debug.LogWarning("[EventHandler] Sound event path is empty");
                    return;
                }

                // FMOD 이벤트인지 AudioClip인지 구분
                if (soundEvent.soundEventPath.StartsWith("event:/"))
                {
                    // FMOD 처리 (확장 가능)
                    PlayFMODSound(soundEvent);
                }
                else
                {
                    // Unity AudioClip 처리
                    PlayUnitySound(soundEvent);
                }

                OnEventCompleted?.Invoke(soundEvent);

                if (debugMode)
                    Debug.Log($"[EventHandler] Sound played: {soundEvent.soundEventPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[EventHandler] Sound event failed: {e.Message}");
            }
        }

        private void PlayFMODSound(TimelineDataSO.SoundEvent soundEvent)
        {
            // FMOD 통합 시 구현
            Debug.Log($"[EventHandler] FMOD event would play: {soundEvent.soundEventPath}");
        }

        private void PlayUnitySound(TimelineDataSO.SoundEvent soundEvent)
        {
            if (resourceProvider == null) return;

            resourceProvider.LoadResourceAsync<AudioClip>(
                soundEvent.soundEventPath,
                clip => PlayAudioClip(clip, soundEvent)
            );
        }

        private void PlayAudioClip(AudioClip clip, TimelineDataSO.SoundEvent soundEvent)
        {
            if (clip == null) return;

            AudioSource source = audioSource;

            // 3D 사운드 처리
            if (soundEvent.is3D)
            {
                // 위치에 AudioSource 생성
                GameObject soundObject = new GameObject($"Sound_{clip.name}");
                soundObject.transform.position = targetTransform.position + soundEvent.positionOffset;

                source = soundObject.AddComponent<AudioSource>();
                source.clip = clip;
                source.spatialBlend = 1f; // 3D
                source.volume = soundEvent.volume;
                source.Play();

                // 재생 완료 후 제거
                Destroy(soundObject, clip.length);
            }
            else
            {
                // 2D 사운드
                source.PlayOneShot(clip, soundEvent.volume);
            }
        }

        #endregion

        #region Camera Events

        public void HandleCameraEvent(TimelineDataSO.CameraEvent cameraEvent)
        {
            try
            {
                OnEventTriggered?.Invoke(cameraEvent);

                switch (cameraEvent.actionType)
                {
                    case TimelineDataSO.CameraActionType.Shake:
                        StartCameraShake(cameraEvent);
                        break;

                    case TimelineDataSO.CameraActionType.Zoom:
                        StartCameraZoom(cameraEvent);
                        break;

                    case TimelineDataSO.CameraActionType.SlowMotion:
                        StartSlowMotion(cameraEvent);
                        break;

                    case TimelineDataSO.CameraActionType.Flash:
                        StartCameraFlash(cameraEvent);
                        break;

                    case TimelineDataSO.CameraActionType.Fade:
                        StartCameraFade(cameraEvent);
                        break;
                }

                OnEventCompleted?.Invoke(cameraEvent);

                if (debugMode)
                    Debug.Log($"[EventHandler] Camera action: {cameraEvent.actionType}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[EventHandler] Camera event failed: {e.Message}");
            }
        }

        private void StartCameraShake(TimelineDataSO.CameraEvent cameraEvent)
        {
            // Camera Shake 구현 (Camera Controller 연동 필요)
            if (Camera.main != null)
            {
                StartCoroutine(CameraShakeCoroutine(cameraEvent.duration, cameraEvent.intensity));
            }
        }

        private System.Collections.IEnumerator CameraShakeCoroutine(float duration, float intensity)
        {
            Camera mainCam = Camera.main;
            Vector3 originalPos = mainCam.transform.localPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float x = UnityEngine.Random.Range(-1f, 1f) * intensity;
                float y = UnityEngine.Random.Range(-1f, 1f) * intensity;

                mainCam.transform.localPosition = originalPos + new Vector3(x, y, 0);

                elapsed += Time.deltaTime;
                yield return null;
            }

            mainCam.transform.localPosition = originalPos;
        }

        private void StartCameraZoom(TimelineDataSO.CameraEvent cameraEvent)
        {
            // Camera Zoom 구현
            Debug.Log($"[EventHandler] Camera zoom: intensity={cameraEvent.intensity}, duration={cameraEvent.duration}");
        }

        private void StartSlowMotion(TimelineDataSO.CameraEvent cameraEvent)
        {
            // Time scale 조정
            Time.timeScale = 1f - cameraEvent.intensity;
            StartCoroutine(ResetTimeScale(cameraEvent.duration));
        }

        private System.Collections.IEnumerator ResetTimeScale(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            Time.timeScale = 1f;
        }

        private void StartCameraFlash(TimelineDataSO.CameraEvent cameraEvent)
        {
            // UI Flash 효과 구현 필요
            Debug.Log($"[EventHandler] Camera flash: intensity={cameraEvent.intensity}");
        }

        private void StartCameraFade(TimelineDataSO.CameraEvent cameraEvent)
        {
            // UI Fade 효과 구현 필요
            Debug.Log($"[EventHandler] Camera fade: intensity={cameraEvent.intensity}, duration={cameraEvent.duration}");
        }

        #endregion

        #region Custom Events

        public void HandleCustomEvent(TimelineDataSO.CustomEvent customEvent)
        {
            try
            {
                OnEventTriggered?.Invoke(customEvent);

                // Custom event는 외부에서 처리하도록 이벤트만 발생
                SendMessage($"OnCustomTimelineEvent_{customEvent.eventName}",
                           customEvent.parameters,
                           SendMessageOptions.DontRequireReceiver);

                OnEventCompleted?.Invoke(customEvent);

                if (debugMode)
                    Debug.Log($"[EventHandler] Custom event: {customEvent.eventName}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[EventHandler] Custom event failed: {e.Message}");
            }
        }

        #endregion

        #region Effect Management

        private void RegisterActiveEffect(string key, GameObject effect)
        {
            if (!activeEffects.ContainsKey(key))
            {
                activeEffects[key] = new List<GameObject>();
            }

            activeEffects[key].Add(effect);
        }

        private void UnregisterActiveEffect(string key, GameObject effect)
        {
            if (activeEffects.ContainsKey(key))
            {
                activeEffects[key].Remove(effect);

                if (activeEffects[key].Count == 0)
                {
                    activeEffects.Remove(key);
                }
            }
        }

        public void CleanupEffect(string effectKey)
        {
            if (activeEffects.ContainsKey(effectKey))
            {
                foreach (var effect in activeEffects[effectKey])
                {
                    if (effect != null)
                    {
                        Destroy(effect);
                    }
                }

                activeEffects[effectKey].Clear();
                activeEffects.Remove(effectKey);
            }
        }

        public void CleanupAllEffects()
        {
            foreach (var kvp in activeEffects)
            {
                foreach (var effect in kvp.Value)
                {
                    if (effect != null)
                    {
#if UNITY_EDITOR
                        if (!Application.isPlaying)
                            DestroyImmediate(effect);
                        else
#endif
                            Destroy(effect);
                    }
                }
            }

            activeEffects.Clear();

            // Coroutine 정리
            foreach (var coroutine in effectCoroutines.Values)
            {
                if (coroutine != null)
                {
                    StopCoroutine(coroutine);
                }
            }

            effectCoroutines.Clear();
        }

        #endregion

        #region Cleanup

        public void CleanupEffectsOutOfRange(float currentTime, TimelineDataSO timeline)
        {
            if (timeline == null) return;

            List<string> keysToRemove = new List<string>();

            foreach (var kvp in activeEffects)
            {
                bool shouldKeep = false;

                // 현재 시간에 활성화되어야 하는 Effect인지 확인
                foreach (var effectEvent in timeline.effectEvents)
                {
                    if (kvp.Key.Contains(effectEvent.effectAddressableKey))
                    {
                        switch (effectEvent.playMode)
                        {
                            case TimelineDataSO.EffectPlayMode.Duration:
                                shouldKeep = currentTime >= effectEvent.triggerTime &&
                                           currentTime <= effectEvent.triggerTime + effectEvent.duration;
                                break;
                            case TimelineDataSO.EffectPlayMode.Loop:
                                shouldKeep = currentTime >= effectEvent.triggerTime;
                                break;
                            default:
                                shouldKeep = false;
                                break;
                        }

                        if (shouldKeep) break;
                    }
                }

                if (!shouldKeep)
                    keysToRemove.Add(kvp.Key);
            }

            // 범위를 벗어난 Effect 제거
            foreach (var key in keysToRemove)
            {
                CleanupEffect(key);
            }
        }

        private void OnDestroy()
        {
            CleanupAllEffects();
        }

        #endregion
    }
}