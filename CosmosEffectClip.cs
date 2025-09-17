using System;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;



[Serializable]
public class CosmosEffectClip : PlayableAsset
{
    // Prefab 직접 참조 (Timeline 에디터에서 미리보기용)
    public GameObject effectPrefab;

    // Addressable 키 (런타임용)
    public string effectAddressableKey;

    // 나머지 설정들...
    public Vector3 positionOffset;
    public Vector3 rotationOffset;
    public float scale = 1f;
    public bool attachToActor = false;
    public string attachBoneName = "";

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<EffectPlayableBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();

        // 데이터 전달
        behaviour.effectPrefab = effectPrefab;
        behaviour.positionOffset = positionOffset;
        behaviour.rotationOffset = rotationOffset;
        behaviour.scale = scale;
        behaviour.attachToActor = attachToActor;
        behaviour.attachBoneName = attachBoneName;

        return playable;
    }
}

public class EffectPlayableBehaviour : PlayableBehaviour
{
    public GameObject effectPrefab;
    public Vector3 positionOffset;
    public Vector3 rotationOffset;
    public float scale;
    public bool attachToActor;
    public string attachBoneName;

    private GameObject spawnedEffect;


    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        // 정지 시 이펙트 제거
        if (spawnedEffect != null)
        {
            if (Application.isPlaying)
                GameObject.Destroy(spawnedEffect);
            else
                GameObject.DestroyImmediate(spawnedEffect);
        }
    }

    private void ApplyTransform(GameObject parent)
    {
        if (spawnedEffect == null) return;

        if (parent != null)
        {
            spawnedEffect.transform.position = parent.transform.position + positionOffset;
            spawnedEffect.transform.rotation = parent.transform.rotation * Quaternion.Euler(rotationOffset);

            if (attachToActor)
            {
                spawnedEffect.transform.SetParent(parent.transform);
            }
        }

        spawnedEffect.transform.localScale = Vector3.one * scale;
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        if (effectPrefab == null) return;

        // GameObject 바인딩 (Track에 연결된 오브젝트)
        GameObject trackBinding = playerData as GameObject;

        // 이펙트가 없으면 생성
        if (spawnedEffect == null && Application.isEditor)
        {
            spawnedEffect = GameObject.Instantiate(effectPrefab);
            ApplyTransform(trackBinding);
        }

        // ParticleSystem 시간 동기화
        if (spawnedEffect != null)
        {
            var particleSystems = spawnedEffect.GetComponentsInChildren<ParticleSystem>();
            float clipTime = (float)playable.GetTime();

            foreach (var ps in particleSystems)
            {
                // ParticleSystem을 Timeline 시간과 동기화
                ps.Simulate(clipTime, true, true);
            }

            // Animator가 있으면 동기화
            var animator = spawnedEffect.GetComponent<Animator>();
            if (animator != null)
            {
                animator.playbackTime = clipTime;
            }
        }
    }

}





