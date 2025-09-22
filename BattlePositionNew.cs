using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;

/// <summary>
/// 진형 타입 정의
/// </summary>
public enum FormationType
{
    Offensive,        // 공격형: 앞열 1, 뒷열 4
    Defensive,        // 방어형: 앞열 4, 뒷열 1  
    OffensiveBalance, // 공격밸런스형: 앞열 2, 뒷열 3
    DefensiveBalance  // 방어밸런스형: 앞열 3, 뒷열 2
}

/// <summary>
/// 캐릭터 직업 타입 (버프 적용용)
/// </summary>
public enum CharacterJobType
{
    None,
    Attacker,    // 공격형
    Defender,    // 방어형
    Support,     // 지원형
    Special      // 특수형
}

/// <summary>
/// 진형 시스템이 통합된 전투 위치 관리자
/// FormationSystem의 기능을 모두 포함
/// </summary>
public class BattlePositionNew : MonoBehaviour
{
    [Header("진형 데이터 - 통합")]
    [SerializeField] private FormationData[] formationDataList = new FormationData[4];

    [Header("현재 진형")]
    [SerializeField] private FormationType currentFormationType = FormationType.OffensiveBalance;

    [Header("진형 미리보기")]
    [SerializeField] private bool previewFormation = true;
    [SerializeField] private Color allyPreviewColor = new Color(0, 0, 1, 0.5f);
    [SerializeField] private Color enemyPreviewColor = new Color(1, 0, 0, 0.5f);

    // 진형별 위치 캐시
    private Dictionary<int, Vector3> cachedAllyStandPositions = new Dictionary<int, Vector3>();
    private Dictionary<int, Vector3> cachedAllyAttackPositions = new Dictionary<int, Vector3>();
    private Dictionary<int, Vector3> cachedEnemyStandPositions = new Dictionary<int, Vector3>();
    private Dictionary<int, Vector3> cachedEnemyAttackPositions = new Dictionary<int, Vector3>();

    // 진형 Dictionary (빠른 접근용)
    private Dictionary<FormationType, FormationData> formationDict;

    // 현재 활성 진형 데이터
    private FormationData currentFormation;

    // 진형 변경 이벤트
    public event Action<FormationType, bool> OnFormationChanged;

    private void Awake()
    {
        InitializeFormations();
        InitializeFormationDictionary();
    }

    private void Start()
    {
        // 초기 진형 설정
        SetFormation(currentFormationType);
    }

    /// <summary>
    /// 진형 Dictionary 초기화
    /// </summary>
    private void InitializeFormationDictionary()
    {
        formationDict = new Dictionary<FormationType, FormationData>();

        for (int i = 0; i < formationDataList.Length; i++)
        {
            if (formationDataList[i] != null)
            {
                formationDict[(FormationType)i] = formationDataList[i];
            }
        }
    }

    /// <summary>
    /// 진형 초기화 (데이터가 없으면 기본값 생성)
    /// </summary>
    private void InitializeFormations()
    {
        for (int i = 0; i < 4; i++)
        {
            if (formationDataList[i] == null)
            {
                Debug.LogWarning($"Creating default formation for {(FormationType)i}");
                formationDataList[i] = CreateDefaultFormationData((FormationType)i);
            }
        }
    }

    /// <summary>
    /// 기본 진형 데이터 생성 (런타임용)
    /// </summary>
    private FormationData CreateDefaultFormationData(FormationType type)
    {
        var data = ScriptableObject.CreateInstance<FormationData>();
        data.formationType = type;
        data.formationName = type.ToString();
        data.InitializePositions(true);  // 아군
        data.InitializePositions(false); // 적군
        return data;
    }

    /// <summary>
    /// 진형 설정 및 변경
    /// </summary>
    public void SetFormation(FormationType formationType)
    {
        if (!formationDict.TryGetValue(formationType, out FormationData formationData))
        {
            Debug.LogError($"Formation data not found for {formationType}");
            return;
        }

        currentFormationType = formationType;
        currentFormation = formationData;

        UpdateCachedPositions(true);  // 아군 캐시 업데이트
        UpdateCachedPositions(false); // 적군 캐시 업데이트

        // 이벤트 발생
        OnFormationChanged?.Invoke(formationType, true);
        OnFormationChanged?.Invoke(formationType, false);

        Debug.Log($"Formation changed to {formationType}");
    }

    /// <summary>
    /// 캐시된 위치 업데이트
    /// </summary>
    private void UpdateCachedPositions(bool isAlly)
    {
        if (currentFormation == null) return;

        var standCache = isAlly ? cachedAllyStandPositions : cachedEnemyStandPositions;
        var attackCache = isAlly ? cachedAllyAttackPositions : cachedEnemyAttackPositions;
        var positions = isAlly ? currentFormation.allyPositions : currentFormation.enemyPositions;

        standCache.Clear();
        attackCache.Clear();

        for (int i = 0; i < positions.Length; i++)
        {
            if (positions[i] != null)
            {
                standCache[i] = transform.TransformPoint(positions[i].standPosition);
                attackCache[i] = transform.TransformPoint(currentFormation.GetAttackPosition(i, isAlly));
            }
        }
    }

    /// <summary>
    /// 현재 진형 데이터 가져오기
    /// </summary>
    public FormationData GetCurrentFormation()
    {
        return currentFormation;
    }

    /// <summary>
    /// 현재 진형 타입 가져오기
    /// </summary>
    public FormationType GetCurrentFormationType()
    {
        return currentFormationType;
    }

    /// <summary>
    /// 슬롯의 대기 위치 가져오기
    /// </summary>
    public Vector3 GetStandPosition(int slotIndex, bool isAlly)
    {
        var cache = isAlly ? cachedAllyStandPositions : cachedEnemyStandPositions;

        if (cache.TryGetValue(slotIndex, out Vector3 position))
        {
            return position;
        }

        // 캐시에 없으면 실시간 계산
        if (currentFormation != null)
        {
            var positions = isAlly ? currentFormation.allyPositions : currentFormation.enemyPositions;
            if (positions != null && slotIndex < positions.Length && positions[slotIndex] != null)
            {
                return transform.TransformPoint(positions[slotIndex].standPosition);
            }
        }

        Debug.LogWarning($"Stand position not found for slot {slotIndex} (isAlly: {isAlly})");
        return transform.position;
    }

    /// <summary>
    /// 슬롯의 공격 위치 가져오기
    /// </summary>
    public Vector3 GetAttackPosition(int slotIndex, bool isAlly)
    {
        var cache = isAlly ? cachedAllyAttackPositions : cachedEnemyAttackPositions;

        if (cache.TryGetValue(slotIndex, out Vector3 position))
        {
            return position;
        }

        // 캐시에 없으면 실시간 계산
        if (currentFormation != null)
        {
            return transform.TransformPoint(currentFormation.GetAttackPosition(slotIndex, isAlly));
        }

        Debug.LogWarning($"Attack position not found for slot {slotIndex} (isAlly: {isAlly})");
        return transform.position;
    }

    /// <summary>
    /// 특정 슬롯이 앞열인지 확인
    /// </summary>
    public bool IsFrontRow(int slotIndex, bool isAlly)
    {
        if (currentFormation != null)
        {
            var positions = isAlly ? currentFormation.allyPositions : currentFormation.enemyPositions;
            if (positions != null && slotIndex < positions.Length && positions[slotIndex] != null)
            {
                return positions[slotIndex].isFrontRow;
            }
        }

        return false;
    }

    /// <summary>
    /// 앞열 슬롯 목록 가져오기
    /// </summary>
    public List<int> GetFrontRowSlots(bool isAlly)
    {
        var frontSlots = new List<int>();

        if (currentFormation != null)
        {
            var positions = isAlly ? currentFormation.allyPositions : currentFormation.enemyPositions;

            for (int i = 0; i < positions.Length; i++)
            {
                if (positions[i] != null && positions[i].isFrontRow)
                {
                    frontSlots.Add(i);
                }
            }
        }

        return frontSlots;
    }

    /// <summary>
    /// 뒷열 슬롯 목록 가져오기
    /// </summary>
    public List<int> GetBackRowSlots(bool isAlly)
    {
        var backSlots = new List<int>();

        if (currentFormation != null)
        {
            var positions = isAlly ? currentFormation.allyPositions : currentFormation.enemyPositions;

            for (int i = 0; i < positions.Length; i++)
            {
                if (positions[i] != null && !positions[i].isFrontRow)
                {
                    backSlots.Add(i);
                }
            }
        }

        return backSlots;
    }

    /// <summary>
    /// 진형 버프 계산 (캐릭터 직업과 위치 기반)
    /// </summary>
    public float GetFormationBuff(CharacterJobType jobType, int slotIndex, bool isAlly)
    {
        if (currentFormation == null) return 1f;

        bool isFrontRow = IsFrontRow(slotIndex, isAlly);
        float buffMultiplier = 1f;

        // 앞열: 지원형, 방어형 버프
        // 뒷열: 공격형, 특수형 버프
        bool isOptimalPosition = false;

        if (isFrontRow)
        {
            isOptimalPosition = (jobType == CharacterJobType.Support || jobType == CharacterJobType.Defender);
        }
        else
        {
            isOptimalPosition = (jobType == CharacterJobType.Attacker || jobType == CharacterJobType.Special);
        }

        if (isOptimalPosition)
        {
            buffMultiplier = 1f + (currentFormation.GetActualBuffPercentage() / 100f);
        }

        return buffMultiplier;
    }

    /// <summary>
    /// 진형 버프 계산 (별칭 - 호환성)
    /// </summary>
    public float CalculateFormationBuff(CharacterJobType jobType, int slotIndex, bool isAlly)
    {
        return GetFormationBuff(jobType, slotIndex, isAlly);
    }

    /// <summary>
    /// 진형 레벨 증가
    /// </summary>
    public void UpgradeFormationLevel(FormationType formationType)
    {
        if (formationDict.TryGetValue(formationType, out FormationData formation))
        {
            if (formation.level < 10)
            {
                formation.level++;
                Debug.Log($"Formation {formationType} upgraded to level {formation.level}");

                // 현재 진형이면 캐시 업데이트
                if (formationType == currentFormationType)
                {
                    UpdateCachedPositions(true);
                    UpdateCachedPositions(false);
                }
            }
        }
    }

    /// <summary>
    /// 런타임 중 진형 변경 (스킬 등)
    /// </summary>
    public void ChangeFormationDuringBattle(FormationType newFormation, float transitionTime = 1f)
    {
        StartCoroutine(TransitionFormation(newFormation, transitionTime));
    }

    /// <summary>
    /// 진형 전환 애니메이션
    /// </summary>
    private IEnumerator TransitionFormation(FormationType newFormation, float duration)
    {
        // 이전 위치 저장
        var oldAllyPositions = new Dictionary<int, Vector3>();
        var oldEnemyPositions = new Dictionary<int, Vector3>();

        for (int i = 0; i < 5; i++)
        {
            oldAllyPositions[i] = GetStandPosition(i, true);
            oldEnemyPositions[i] = GetStandPosition(i, false);
        }

        // 새 진형 설정
        SetFormation(newFormation);

        // 부드러운 이동
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = Mathf.SmoothStep(0, 1, t);

            // 아군 위치 보간
            var allyActors = BattleProcessManagerNew.Instance.GetAllyActors();
            foreach (var actor in allyActors)
            {
                if (actor != null && !actor.IsDead)
                {
                    int slot = actor.BattleActorInfo.SlotIndex;
                    Vector3 newPos = GetStandPosition(slot, true);
                    Vector3 currentPos = Vector3.Lerp(oldAllyPositions[slot], newPos, t);
                    actor.transform.position = currentPos;
                }
            }

            // 적군 위치 보간
            var enemyActors = BattleProcessManagerNew.Instance.GetEnemyActors();
            foreach (var actor in enemyActors)
            {
                if (actor != null && !actor.IsDead)
                {
                    int slot = actor.BattleActorInfo.SlotIndex;
                    Vector3 newPos = GetStandPosition(slot, false);
                    Vector3 currentPos = Vector3.Lerp(oldEnemyPositions[slot], newPos, t);
                    actor.transform.position = currentPos;
                }
            }

            yield return null;
        }

        // 최종 위치 확정
        UpdateActorPositions();
    }

    /// <summary>
    /// 액터들의 위치 업데이트
    /// </summary>
    private void UpdateActorPositions()
    {
        // 아군
        var allyActors = BattleProcessManagerNew.Instance.GetAllyActors();
        foreach (var actor in allyActors)
        {
            if (actor != null && !actor.IsDead)
            {
                actor.SetPosition(GetStandPosition(actor.BattleActorInfo.SlotIndex, true));
            }
        }

        // 적군
        var enemyActors = BattleProcessManagerNew.Instance.GetEnemyActors();
        foreach (var actor in enemyActors)
        {
            if (actor != null && !actor.IsDead)
            {
                actor.SetPosition(GetStandPosition(actor.BattleActorInfo.SlotIndex, false));
            }
        }
    }

    /// <summary>
    /// 다중 타겟 선택 헬퍼 메서드들
    /// </summary>
    public List<BattleActor> GetFrontRowActors(List<BattleActor> actors, bool isAlly)
    {
        var frontSlots = GetFrontRowSlots(isAlly);
        return actors.Where(a => a != null && !a.IsDead && frontSlots.Contains(a.BattleActorInfo.SlotIndex)).ToList();
    }

    public List<BattleActor> GetBackRowActors(List<BattleActor> actors, bool isAlly)
    {
        var backSlots = GetBackRowSlots(isAlly);
        return actors.Where(a => a != null && !a.IsDead && backSlots.Contains(a.BattleActorInfo.SlotIndex)).ToList();
    }

    public List<BattleActor> GetAllActors(List<BattleActor> actors)
    {
        return actors.Where(a => a != null && !a.IsDead).ToList();
    }

    private void OnDrawGizmos()
    {
        if (!previewFormation) return;

        // 에디터에서 진형 미리보기
        DrawFormationPreview(true);
        DrawFormationPreview(false);
    }

    private void DrawFormationPreview(bool isAlly)
    {
        FormationData formation = null;

        if (Application.isPlaying)
        {
            formation = currentFormation;
        }
        else
        {
            // 에디터 모드에서는 배열에서 직접 가져오기
            if (formationDataList != null && (int)currentFormationType < formationDataList.Length)
            {
                formation = formationDataList[(int)currentFormationType];
            }
        }

        if (formation == null) return;

        var positions = isAlly ? formation.allyPositions : formation.enemyPositions;
        if (positions == null) return;

        Color color = isAlly ? allyPreviewColor : enemyPreviewColor;

        for (int i = 0; i < positions.Length; i++)
        {
            var pos = positions[i];
            if (pos == null) continue;

            Vector3 worldStandPos = transform.TransformPoint(pos.standPosition);
            Vector3 worldAttackPos = transform.TransformPoint(formation.GetAttackPosition(i, isAlly));

            // 대기 위치
            Gizmos.color = color;
            if (pos.isFrontRow)
            {
                Gizmos.DrawCube(worldStandPos, Vector3.one * 0.8f);
            }
            else
            {
                Gizmos.DrawSphere(worldStandPos, 0.4f);
            }

            // 공격 위치
            Gizmos.color = color * 0.5f;
            Gizmos.DrawWireSphere(worldAttackPos, 0.3f);

            // 연결선
            Gizmos.DrawLine(worldStandPos, worldAttackPos);

            // 슬롯 번호 표시
#if UNITY_EDITOR
            UnityEditor.Handles.Label(worldStandPos + Vector3.up,
                $"{(isAlly ? "A" : "E")}{i}\n{(pos.isFrontRow ? "Front" : "Back")}");
#endif
        }
    }
}