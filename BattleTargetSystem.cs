using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System;
using Random = UnityEngine.Random;

/// <summary>
/// 전투 타겟 선택 시스템
/// </summary>
public class BattleTargetSystem : MonoBehaviour
{
    private static BattleTargetSystem instance;
    public static BattleTargetSystem Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<BattleTargetSystem>();
                if (instance == null)
                {
                    GameObject go = new GameObject("BattleTargetSystem");
                    instance = go.AddComponent<BattleTargetSystem>();
                }
            }
            return instance;
        }
    }

    [Header("타겟 선택 설정")]
    [SerializeField] private bool isSelectingTarget = false;
    [SerializeField] private BattleActor selectedTarget = null;
    [SerializeField] private LayerMask enemyLayer = -1; // 적 레이어 마스크

    [Header("AI 타겟 설정")]
    [SerializeField] private float frontLineTargetProbability = 0.7f; // 전열 공격 확률 70%
    [SerializeField] private float backLineTargetProbability = 0.3f;  // 후열 공격 확률 30%

    [Header("시각 효과")]
    [SerializeField] private GameObject targetIndicatorPrefab; // 타겟 표시 프리팹
    private GameObject currentTargetIndicator;

    // 전열/후열 구분 인덱스
    private readonly int[] frontLineIndices = { 0, 1 };        // 전열 슬롯
    private readonly int[] backLineIndices = { 2, 3, 4 };      // 후열 슬롯
                                                               // IsSelectingTarget 속성 추가 (public getter)
    #region Constants

    // 어그로 상수
    private const int FRONT_FORMATION_AGGRO = 400;        // 전열 기본 어그로
    private const int BACK_FORMATION_AGGRO = 20;          // 후열 기본 어그로
    private const int MONSTER_BONUS_AGGRO = 200;          // 몬스터 추가 어그로
    private static readonly int[] AGGRO_RANK_VALUES = { 100, 90, 80, 70, 60 };

    #endregion

    /// <summary>
    /// 타겟팅 모드
    /// </summary>
    public enum TargetingMode
    {
        Aggro,          // 어그로 기반
        LowestHP,       // 최저 HP
        HighestHP,      // 최고 HP
        Random,         // 랜덤
        Front,          // 전열 우선
        Back            // 후열 우선
    }

    public bool IsSelectingTarget
    {
        get { return isSelectingTarget; }
    }


    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

   
    /// <summary>
    /// AI(적)의 타겟 선택 로직
    /// </summary>
    public BattleActor GetTargetForEnemy(List<BattleActor> allies)
    {
        if (allies == null || allies.Count == 0)
            return null;

        // 살아있는 아군만 필터링
        var aliveAllies = allies.Where(a => a != null && !a.IsDead).ToList();
        if (aliveAllies.Count == 0)
            return null;

        // 전열과 후열 분류
        var frontLineTargets = new List<BattleActor>();
        var backLineTargets = new List<BattleActor>();

        foreach (var ally in aliveAllies)
        {
            if (ally.BattleActorInfo != null)
            {
                int slotIndex = ally.BattleActorInfo.SlotIndex;

                if (frontLineIndices.Contains(slotIndex))
                {
                    frontLineTargets.Add(ally);
                }
                else if (backLineIndices.Contains(slotIndex))
                {
                    backLineTargets.Add(ally);
                }
            }
        }

        // 타겟 선택
        BattleActor target = null;

        // 전열이 있는 경우
        if (frontLineTargets.Count > 0)
        {
            float random = Random.Range(0f, 1f);

            // 후열도 있고 30% 확률에 당첨되면 후열 공격
            if (backLineTargets.Count > 0 && random > frontLineTargetProbability)
            {
                target = backLineTargets[Random.Range(0, backLineTargets.Count)];
                Debug.Log($"[AI Target] Attacking back line (30% chance)");
            }
            else
            {
                // 70% 확률로 전열 공격
                target = frontLineTargets[Random.Range(0, frontLineTargets.Count)];
                Debug.Log($"[AI Target] Attacking front line (70% chance)");
            }
        }
        // 전열이 없고 후열만 있는 경우
        else if (backLineTargets.Count > 0)
        {
            target = backLineTargets[Random.Range(0, backLineTargets.Count)];
            Debug.Log($"[AI Target] Only back line remains, attacking back line");
        }
        // 분류되지 않은 캐릭터가 있는 경우 (예외 처리)
        else
        {
            target = aliveAllies[Random.Range(0, aliveAllies.Count)];
            Debug.LogWarning($"[AI Target] No categorized targets, selecting random");
        }

        if (target != null && target.BattleActorInfo != null)
        {
            Debug.Log($"[AI Target Selected] {target.name} at slot {target.BattleActorInfo.SlotIndex}");
        }

        return target;
    }





    /// <summary>
    /// 공격 가능한 타겟 필터링
    /// </summary>
    public List<BattleActor> GetValidTargets(BattleActor attacker, List<BattleActor> potentialTargets)
    {
        if (attacker?.BattleActorInfo == null)
            return potentialTargets;

        List<BattleActor> validTargets = new List<BattleActor>();

        foreach (var target in potentialTargets)
        {
            if (attacker.BattleActorInfo.CanAttackTarget(target, potentialTargets))
            {
                validTargets.Add(target);
            }
        }

        // 유효한 타겟이 없으면 원본 리스트 반환 (방어 로직)
        if (validTargets.Count == 0)
        {
            Debug.LogWarning($"[TargetSystem] No valid targets for {attacker.name}, returning all targets");
            return potentialTargets;
        }

        return validTargets;
    }




    #region Main Target Selection

    /// <summary>
    /// 메인 타겟 선정 메서드
    /// </summary>
    public BattleActor FindTarget(List<BattleActor> candidates, TargetingMode mode = TargetingMode.Aggro)
    {
        if (candidates == null || candidates.Count == 0)
            return null;

        // 살아있는 캐릭터만 필터링
        var aliveTargets = candidates.Where(c => c != null && !c.IsDead).ToList();
        if (aliveTargets.Count == 0)
            return null;

        // 타겟팅 모드에 따른 처리
        switch (mode)
        {
            case TargetingMode.Aggro:
                return FindTargetByAggro(aliveTargets);

            case TargetingMode.LowestHP:
                return FindTargetByLowestHP(aliveTargets);

            case TargetingMode.HighestHP:
                return FindTargetByHighestHP(aliveTargets);

            case TargetingMode.Random:
                return FindRandomTarget(aliveTargets);

            case TargetingMode.Front:
                return FindFrontTarget(aliveTargets);

            case TargetingMode.Back:
                return FindBackTarget(aliveTargets);

            default:
                return FindTargetByAggro(aliveTargets);
        }
    }

    /// <summary>
    /// 어그로 기반 타겟 찾기 (Lua FindTarget 포팅)
    /// </summary>
    public BattleActor FindTargetByAggro(List<BattleActor> characters)
    {
        if (characters == null || characters.Count == 0)
            return null;

        // 도발 상태인 캐릭터가 있으면 우선 타겟
        var provoker = characters.FirstOrDefault(c =>
            c.SkillManager != null && c.SkillManager.IsBattleSkillBase(SkillSystem.StatusType.Taunt));
        if (provoker != null)
        {
            Debug.Log($"[Target] Provoked! Forced target: {provoker.name}");
            return provoker;
        }

        // 은신 상태인 캐릭터 제외
        //var visibleTargets = characters.Where(c =>
        //  c.SkillManager == null || !c.SkillManager.IsBattleSkillBase(SkillSystem.StatusType.Hide)).ToList();
        var visibleTargets = characters.ToList();

        if (visibleTargets.Count == 0)
        {
            Debug.Log("[Target] No visible targets!");
            return null;
        }


        // 어그로 할당
        float totalAggro = AllocateAggro(visibleTargets);

        // 어그로 기준 정렬
        visibleTargets = visibleTargets.OrderByDescending(c => c.BattleActorInfo.Aggro).ToList();

        Debug.Log($"[Target] Total Aggro: {totalAggro}");

        if (totalAggro <= 0)
        {
            Debug.LogWarning("[Target] Total aggro is zero! Returning first target.");
            return visibleTargets[0];
        }

        // 어그로 기반 가중치 선택
        float randomValue = UnityEngine.Random.value * totalAggro;
        float currentAggro = 0;

        foreach (var character in visibleTargets)
        {
            currentAggro += character.BattleActorInfo.Aggro;

            Debug.Log($"[Target] {character.BattleActorInfo.Name} - Aggro: {character.BattleActorInfo.Aggro:F0} " +
                     $"(Total: {currentAggro:F0}/{totalAggro:F0})");

            if (randomValue <= currentAggro)
            {
                Debug.Log($"[Target] Selected: {character.BattleActorInfo.Name}");
                return character;
            }
        }

        return visibleTargets[visibleTargets.Count - 1];
    }

    #endregion

    #region Specialized Target Selection

    /// <summary>
    /// HP가 가장 낮은 타겟 선택
    /// </summary>
    public BattleActor FindTargetByLowestHP(List<BattleActor> characters)
    {
        return characters
            .Where(c => c != null && !c.IsDead)
            .OrderBy(c => c.BattleActorInfo.Hp)
            .FirstOrDefault();
    }

    /// <summary>
    /// HP가 가장 높은 타겟 선택
    /// </summary>
    public BattleActor FindTargetByHighestHP(List<BattleActor> characters)
    {
        return characters
            .Where(c => c != null && !c.IsDead)
            .OrderByDescending(c => c.BattleActorInfo.Hp)
            .FirstOrDefault();
    }

    /// <summary>
    /// 랜덤 타겟 선택
    /// </summary>
    public BattleActor FindRandomTarget(List<BattleActor> characters)
    {
        var aliveTargets = characters.Where(c => c != null && !c.IsDead).ToList();
        if (aliveTargets.Count == 0) return null;

        int randomIndex = UnityEngine.Random.Range(0, aliveTargets.Count);
        return aliveTargets[randomIndex];
    }

    /// <summary>
    /// 전열 타겟 선택
    /// </summary>
    public BattleActor FindFrontTarget(List<BattleActor> characters)
    {
        var frontTargets = characters
            .Where(c => c != null && !c.IsDead && c.BattleActorInfo.IsFrontFormation)
            .ToList();

        // 전열이 없으면 아무나
        if (frontTargets.Count == 0)
            return FindRandomTarget(characters);

        return FindTargetByAggro(frontTargets);
    }

    /// <summary>
    /// 후열 타겟 선택
    /// </summary>
    public BattleActor FindBackTarget(List<BattleActor> characters)
    {
        var backTargets = characters
            .Where(c => c != null && !c.IsDead && !c.BattleActorInfo.IsFrontFormation)
            .ToList();

        // 후열이 없으면 아무나
        if (backTargets.Count == 0)
            return FindRandomTarget(characters);

        return FindTargetByAggro(backTargets);
    }

    #endregion

    #region Area Target Selection

    /// <summary>
    /// 범위 타겟 선택 (타겟 타입에 따라)
    /// </summary>
    public List<BattleActor> GetTargetsByType(List<BattleActor> candidates, ETargetType targetType, BattleActor primaryTarget = null)
    {
        var result = new List<BattleActor>();
        var aliveTargets = candidates.Where(c => c != null && !c.IsDead).ToList();

        switch (targetType)
        {
            case ETargetType.Single:
                if (primaryTarget != null && !primaryTarget.IsDead)
                    result.Add(primaryTarget);
                break;

            case ETargetType.FrontRow:
                result = aliveTargets.Where(c => c.BattleActorInfo.IsFrontFormation).ToList();
                break;

            case ETargetType.BackRow:
                result = aliveTargets.Where(c => !c.BattleActorInfo.IsFrontFormation).ToList();
                break;

            case ETargetType.All:
                result = aliveTargets;
                break;
        }

        return result;
    }

    /// <summary>
    /// 주변 타겟 가져오기 (인접한 슬롯)
    /// </summary>
    public List<BattleActor> GetAdjacentTargets(BattleActor centerTarget, List<BattleActor> candidates, int range = 1)
    {
        if (centerTarget == null) return new List<BattleActor>();

        var result = new List<BattleActor> { centerTarget };
        int centerSlot = centerTarget.BattleActorInfo.SlotIndex;

        foreach (var candidate in candidates)
        {
            if (candidate == null || candidate.IsDead || candidate == centerTarget)
                continue;

            int slotDiff = Math.Abs(candidate.BattleActorInfo.SlotIndex - centerSlot);
            if (slotDiff <= range)
            {
                result.Add(candidate);
            }
        }

        return result;
    }

    #endregion

    #region Aggro Management

    /// <summary>
    /// 어그로 할당 (Lua AllocateAggro 포팅)
    /// </summary>
    private float AllocateAggro(List<BattleActor> characters)
    {
        float totalAggro = 0;

        // 1. 기본 어그로 (진형 기반)
        foreach (var character in characters)
        {
            float aggroValue;

            if (character.BattleActorInfo.IsFrontFormation)
            {
                aggroValue = FRONT_FORMATION_AGGRO;
            }
            else
            {
                aggroValue = BACK_FORMATION_AGGRO;
            }

            // 몬스터 보너스 (보스가 아닌 적 몬스터)
            if (character.BattleActorInfo.IsMonster && !character.BattleActorInfo.IsAlly && !character.BattleActorInfo.IsBoss)
            {
                aggroValue += MONSTER_BONUS_AGGRO;
            }

            character.BattleActorInfo.SetAggro( (int)aggroValue );
            totalAggro += aggroValue;
        }

        // 2. HP 기반 어그로 추가 (낮을수록 높은 어그로)
        totalAggro += AddAggroByStatDescending(
            characters.OrderBy(c => c.BattleActorInfo.Hp).ToList(),
            c => c.BattleActorInfo.Hp
        );

        // 3. 방어력 기반 어그로 추가 (Lua에서는 주석처리됨)
        // totalAggro += AddAggroByStatDescending(
        //     characters.OrderBy(c => c.BattleActorInfo.Def).ToList(),
        //     c => c.BattleActorInfo.Def
        // );

        return totalAggro;
    }

    /// <summary>
    /// 스탯 기반 어그로 추가 (내림차순)
    /// </summary>
    private float AddAggroByStatDescending(List<BattleActor> sortedCharacters, Func<BattleActor, float> statGetter)
    {
        float totalAggro = 0;
        int index = 0;
        int equalCount = 0;
        float lowestValue = 0f;

        for (int i = 0; i < sortedCharacters.Count; i++)
        {
            float currentValue = statGetter(sortedCharacters[i]);

            if (lowestValue <= 0f || currentValue < lowestValue)
            {
                index = index + equalCount;
                equalCount = 1;
            }
            else
            {
                equalCount++;
            }

            int aggroValue = 1;
            if (index < AGGRO_RANK_VALUES.Length)
            {
                aggroValue = AGGRO_RANK_VALUES[index];
            }

            // 기본 어그로에 추가
            int newAggro = sortedCharacters[i].BattleActorInfo.Aggro + aggroValue;
            sortedCharacters[i].BattleActorInfo.SetAggro(newAggro );

            lowestValue = currentValue;
            totalAggro += aggroValue;
        }

        return totalAggro;
    }



    /// <summary>
    /// 어그로 수정 (버프/디버프로 인한 어그로 변경)
    /// </summary>
    public void ModifyAggro(BattleActor target, float multiplier)
    {
        if (target == null || target.BattleActorInfo == null) return;

        int newAggro = Mathf.RoundToInt(target.BattleActorInfo.Aggro * multiplier);
        target.BattleActorInfo.SetAggro( Mathf.Max(1, newAggro) ); 

        Debug.Log($"[Aggro] {target.name} aggro modified: x{multiplier} = {target.BattleActorInfo.Aggro}");
    }

    /// <summary>
    /// 어그로 리셋
    /// </summary>
    public void ResetAggro(BattleActor target)
    {
        if (target == null || target.BattleActorInfo == null) return;

        target.BattleActorInfo.SetAggro( (int)target.BattleActorInfo.DefaultAggro );
        Debug.Log($"[Aggro] {target.name} aggro reset to {target.BattleActorInfo.Aggro}");
    }

    #endregion



    #region Target Validation

    /// <summary>
    /// 타겟 유효성 검증
    /// </summary>
    public bool IsValidTarget(BattleActor target, BattleActor attacker = null)
    {
        if (target == null || target.IsDead)
            return false;

        // 은신 상태 체크 나중에 추가. 
        /*if (target.SkillManager != null && target.SkillManager.IsBattleSkillBase(SkillSystem.StatusType.Hide))
        {
            // 특정 스킬은 은신 무시 가능
            if (attacker?.SkillManager != null)
            {
                // TODO: 은신 감지 스킬 체크
            }
            return false;
        }


        // 무적 상태 체크
        if (target.SkillManager != null && target.SkillManager.IsBattleSkillBase(SkillSystem.StatusType.Invincible))
        {
            return false;
        }*/

        return true;
    }

    /// <summary>
    /// 기본 타겟 가져오기 (아군용)
    /// </summary>
    public BattleActor GetDefaultTargetForAlly(List<BattleActor> enemies, int allyIndex)
    {
        // 같은 인덱스의 적을 우선 타겟
        var sameIndexEnemy = enemies.FirstOrDefault(e =>
            e != null && !e.IsDead && e.BattleActorInfo.SlotIndex == allyIndex);

        if (sameIndexEnemy != null && IsValidTarget(sameIndexEnemy))
            return sameIndexEnemy;

        // 없으면 어그로 기반 선택
        return FindTargetByAggro(enemies);
    }

    /// <summary>
    /// 기본 타겟 가져오기 (적군용)
    /// </summary>
    public BattleActor GetDefaultTargetForEnemy(List<BattleActor> allies, int enemyIndex)
    {
        // 어그로 기반 선택
        return FindTargetByAggro(allies);
    }

    #endregion



    /// <summary>
    /// 터치로 타겟 선택 시작
    /// </summary>
    /// <returns>선택된 타겟, 취소 시 null 반환</returns>
    public async UniTask<BattleActor> StartTargetSelection(List<BattleActor> enemies, BattleActor defaultTarget,
        BattleActor attacker = null)
    {

        // 공격자가 있으면 유효한 타겟만 필터링
        if (attacker != null)
        {
            enemies = GetValidTargets(attacker, enemies);

            // 기본 타겟이 유효하지 않으면 첫 번째 유효한 타겟으로 변경
            if (defaultTarget != null && !enemies.Contains(defaultTarget))
            {
                defaultTarget = enemies.FirstOrDefault(t => !t.IsDead);
                Debug.Log($"[TargetSystem] Default target invalid, switching to {defaultTarget?.name}");
            }
        }


        isSelectingTarget = true;
        selectedTarget = defaultTarget;
        bool isCancelled = false;

        // 기본 타겟에 인디케이터 표시
        ShowTargetIndicator(defaultTarget);

        // 모든 적에게 클릭 가능하도록 설정
        EnableEnemySelection(enemies, true);

        // 태그 UI들도 선택 가능하게 설정
        EnableTagSelection(enemies, true);

        bool targetConfirmed = false;
        while (!targetConfirmed  && !isCancelled)
        {
            // ESC 키 또는 뒤로가기 버튼으로 취소
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Mouse1))
            {
                isCancelled = true;
                Debug.Log("[Target Selection] Cancelled by user");
                break;
            }

            // 터치 입력 체크 (3D 콜라이더)
            if (CheckTouchInput(enemies))
            {
                targetConfirmed = true;
            }

            await UniTask.Yield();
        }

        // 선택 종료
        isSelectingTarget = false;
        EnableEnemySelection(enemies, false);
        EnableTagSelection(enemies, false);

        // 취소된 경우
        if (isCancelled)
        {
            HideTargetIndicator();
            return null;
        }

        Debug.Log($"[Target Confirmed] Final target: {selectedTarget.name}");

        return selectedTarget ?? defaultTarget;

    }

    /// <summary>
    /// Melee 공격자를 위한 자동 타겟 선택
    /// </summary>
    public BattleActor GetAutoTargetForMelee(List<BattleActor> enemies)
    {
        // 전열 적 우선 탐색
        var frontRowEnemies = new List<BattleActor>();
        var backRowEnemies = new List<BattleActor>();

        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy.IsDead)
                continue;

            if (IsInFrontRow(enemy))
                frontRowEnemies.Add(enemy);
            else
                backRowEnemies.Add(enemy);
        }

        // 전열에 적이 있으면 전열 중 랜덤 선택
        if (frontRowEnemies.Count > 0)
        {
            return frontRowEnemies[UnityEngine.Random.Range(0, frontRowEnemies.Count)];
        }

        // 전열이 비어있으면 후열 중 랜덤 선택
        if (backRowEnemies.Count > 0)
        {
            return backRowEnemies[UnityEngine.Random.Range(0, backRowEnemies.Count)];
        }

        return null;
    }

    private bool IsInFrontRow(BattleActor actor)
    {
        if (BattleProcessManagerNew.Instance?.battlePosition != null)
        {
            var frontSlots = BattleProcessManagerNew.Instance.battlePosition
                .GetFrontRowSlots(actor.BattleActorInfo.IsAlly);
            return frontSlots.Contains(actor.BattleActorInfo.SlotIndex);
        }

        return actor.BattleActorInfo.SlotIndex <= 1;
    }


    /// <summary>
    /// 태그 선택 가능 상태 설정
    /// </summary>
    private void EnableTagSelection(List<BattleActor> enemies, bool enable)
    {
        foreach (var enemy in enemies)
        {
            if (enemy != null && !enemy.IsDead)
            {
                // BattleActor에서 태그 가져오기
                BattleActorTagUI tag = enemy.GetComponentInChildren<BattleActorTagUI>();
                if (tag != null)
                {
                    tag.SetSelectable(enable);
                }
            }
        }
    }

 

    /// <summary>
    /// 터치 입력 체크
    /// </summary>
    private bool CheckTouchInput(List<BattleActor> enemies)
    {
        bool targetChanged = false;

        // 모바일 터치
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                targetChanged = CheckRaycast(touch.position, enemies);
            }
        }
        // PC 마우스 (테스트용)
        else if (Input.GetMouseButtonDown(0))
        {
            targetChanged = CheckRaycast(Input.mousePosition, enemies);
        }

        return targetChanged;
    }

    /// <summary>
    /// 레이캐스트로 적 선택
    /// </summary>
    private bool CheckRaycast(Vector3 screenPosition, List<BattleActor> enemies)
    {
        Ray ray = Camera.main.ScreenPointToRay(screenPosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 100f, enemyLayer))
        {
            // 히트한 오브젝트에서 BattleActor 컴포넌트 찾기
            BattleActor hitActor = hit.collider.GetComponentInParent<BattleActor>();

            if (hitActor != null && enemies.Contains(hitActor) && !hitActor.IsDead)
            {
                // 이미 선택된 타겟이면 변경 없음 /한명일때 디폴트랑 같을때도 true
                if (selectedTarget == hitActor)
                    return true;

                // 새로운 타겟 선택
                selectedTarget = hitActor;
                ShowTargetIndicator(hitActor);

                Debug.Log($"[Target Changed] New target: {hitActor.name}");

                // 선택 효과음 재생
                PlayTargetSelectionSound();

                return true;  // 타겟이 변경됨
            }
        }

        return false;  // 타겟 변경 없음
    }

    /// <summary>
    /// 적 선택 가능 상태 설정
    /// </summary>
    private void EnableEnemySelection(List<BattleActor> enemies, bool enable)
    {
        foreach (var enemy in enemies)
        {
            if (enemy != null && !enemy.IsDead)
            {
                // 3D 콜라이더 활성화/비활성화
                Collider collider = enemy.GetComponentInChildren<Collider>();
                if (collider == null && enable)
                {
                    GameObject colliderObj = new GameObject("TargetCollider");
                    colliderObj.transform.SetParent(enemy.transform);
                    colliderObj.transform.localPosition = Vector3.zero;

                    BoxCollider boxCollider = colliderObj.AddComponent<BoxCollider>();
                    boxCollider.size = new Vector3(2f, 3f, 0.5f);
                    colliderObj.layer = LayerMask.NameToLayer("Enemy");
                }
                else if (collider != null)
                {
                    collider.enabled = enable;
                }

                // 선택 가능 상태 시각적 표시
                ShowSelectableHighlight(enemy, enable);

                // 태그도 표시
                enemy.ShowTag(true);
            }
        }
    }

    /// <summary>
    /// 타겟 인디케이터 표시
    /// </summary>
    private void ShowTargetIndicator(BattleActor target)
    {
        if (target == null) return;

        // 기존 인디케이터 제거
        HideTargetIndicator();

        // 새 인디케이터 생성 또는 이동
        if (currentTargetIndicator == null)
            currentTargetIndicator = Instantiate(targetIndicatorPrefab);
            
        currentTargetIndicator.transform.position = target.CaptionPostion.Position;
        currentTargetIndicator.SetActive(true);
    }

    /// <summary>
    /// 타겟 인디케이터 숨기기
    /// </summary>
    public void HideTargetIndicator()
    {
        if (currentTargetIndicator != null)
        {
            currentTargetIndicator.SetActive(false);
        }
    }

    /// <summary>
    /// 선택 가능 하이라이트 표시
    /// </summary>
    private void ShowSelectableHighlight(BattleActor actor, bool show)
    {
        // SpriteRenderer의 색상 변경으로 표시
        SpriteRenderer spriteRenderer = actor.GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            if (show)
            {
                // 약간 밝게 표시
                Color color = spriteRenderer.color;
                color.r = Mathf.Min(color.r + 0.3f, 1f);
                color.g = Mathf.Min(color.g + 0.3f, 1f);
                color.b = Mathf.Min(color.b + 0.3f, 1f);
                spriteRenderer.color = color;
            }
            else
            {
                // 원래 색상으로 복구
                spriteRenderer.color = Color.white;
            }
        }
    }

    /// <summary>
    /// 태그 UI를 통한 타겟 선택
    /// </summary>
    public void SelectTargetByTag(BattleActor target)
    {
        if (!isSelectingTarget || target == null || target.IsDead)
            return;

        // 이전 선택 타겟과 같으면 무시
        if (selectedTarget == target)
            return;

        // 새 타겟 선택
        selectedTarget = target;
        ShowTargetIndicator(target);

        Debug.Log($"[Target Selected by Tag] {target.name}");

        // 선택 효과음 재생
        PlayTargetSelectionSound();

    }




    /// <summary>
    /// 타겟 선택 효과음 재생
    /// </summary>
    private void PlayTargetSelectionSound()
    {
        // TODO: 효과음 재생 구현
        // AudioManager.Instance?.PlaySFX("target_select");
    }

    /// <summary>
    /// 타겟 선택 취소
    /// </summary>
    public void CancelTargetSelection()
    {
        isSelectingTarget = false;
    }
}