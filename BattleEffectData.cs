using System;
using UnityEngine;

// 이펙트 타입 정의 (기존과 동일)
public enum EffectType
{
    None,
    AttackMelee,
    AttackRanged,
    Hit,
    Buff,
    Debuff,
    Heal,
    Special,
    ChargeBP
}

public enum ProjectileTrajectory
{
    Straight,
    Arc,
    Homing,
    Spiral,
    Bounce,
    Custom
}

// 새로운 enum 추가
public enum EffectOwnerType
{
    Common,           // 공용 이펙트
    Character         // 캐릭터 전용 이펙트
}

[CreateAssetMenu(fileName = "BattleEffectData", menuName = "Battle/EffectData")]
public class BattleEffectData : ScriptableObject
{
    [Header("기본 정보")]
    public string effectId;
    public string effectName;
    public EffectType effectType;

    [Header("이펙트 소유자")]
    public EffectOwnerType ownerType = EffectOwnerType.Common;

    [HideInInspector]
    public string characterName = "";  // 캐릭터 전용일 때 캐릭터 이름

    [HideInInspector]
    public string selectedEffectPrefab = "";  // 선택된 이펙트 프리팹 이름 (확장자 없이)

    [HideInInspector]
    public string characterBasePath = "Character2D/Battle";  // Resources 내 캐릭터 기본 경로

    // 나머지 필드들은 기존과 동일
    [Header("타이밍")]
    public float spawnDelay = 0f;
    public float hitTiming = 0.5f;

    public bool loop = false;
    public float duration = 2f;

    [Header("위치/회전")]
    public Vector3 positionOffset;
    public bool flipWithCharacter = true;
    public bool attachToTarget = false;

    [Header("스케일")]
    public bool scaleWithTarget = false;
    public float scaleMultiplier = 1f;

    [Header("프로젝타일 설정")]
    public bool isProjectile = false;
    public ProjectileTrajectory trajectory = ProjectileTrajectory.Straight;
    public float projectileSpeed = 10f;
    public AnimationCurve trajectoryHeightCurve;
    public float homingStrength = 5f;

    [Header("연결된 이펙트")]
    public string hitEffectId;
    public string[] additionalEffectIds;

    // 실제 프리팹 경로를 반환하는 메서드 (Resources.Load용 - 확장자 없이)
    public string GetEffectPrefabPath()
    {
        if (string.IsNullOrEmpty(selectedEffectPrefab))
            return null;

        // selectedEffectPrefab에 이미 확장자가 없으므로 그대로 사용
        string prefabName = selectedEffectPrefab;

        if (ownerType == EffectOwnerType.Common)
        {
            return $"Character2D/Battle/Common/Effect/{prefabName}";
        }
        else if (!string.IsNullOrEmpty(characterName))
        {
            // 저장된 캐릭터 기본 경로 사용
            return $"{characterBasePath}/{characterName}/Effect/{prefabName}";
        }

        return null;
    }

    // 파일 시스템 경로를 반환하는 메서드 (파일 복사용)
    public string GetEffectPrefabFilePath()
    {
        if (string.IsNullOrEmpty(selectedEffectPrefab))
            return null;

        // 파일 경로에는 .prefab 확장자 필요
        string prefabFileName = selectedEffectPrefab + ".prefab";

        if (ownerType == EffectOwnerType.Common)
        {
            return $"Assets/Resources/Battle/Common/Effect/{prefabFileName}";
        }
        else if (!string.IsNullOrEmpty(characterName))
        {
            return $"Assets/Resources/{characterBasePath}/{characterName}/Effect/{prefabFileName}";
        }

        return null;
    }
}

// 이펙트 인스턴스 정보
public class EffectInstance
{
    public GameObject effectObject;
    public BattleEffectData effectData;
    public BattleActor source;           // 시전자
    public BattleActor target;           // 대상
    public Vector3 startPosition;
    public Vector3 targetPosition;
    public float spawnTime;
    public bool isActive;
    public Action<BattleActor> onHit;    // 피격 시 콜백

    // 프로젝타일 관련
    public float trajectoryProgress = 0f;
    public Vector3 currentVelocity;
}

