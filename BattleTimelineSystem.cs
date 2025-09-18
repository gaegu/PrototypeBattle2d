using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using System.Collections;
using System;

public class BattleTimelineSystem : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private BattleTimelineConfig config;

    [Header("UI Layout")]
    [SerializeField] private Transform slotContainer;
    [SerializeField] private Transform deadSlotContainer;
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private GameObject dividerPrefab;

    [Header("Positioning")]
    [SerializeField] private Transform currentTurnPosition;  // 현재 턴 캐릭터 위치
    [SerializeField] private float startX = 100f;           // 시작 X 위치

    // 슬롯 풀
    private List<BattleTimelineSlot> allSlots = new List<BattleTimelineSlot>();
    private Queue<BattleTimelineSlot> slotPool = new Queue<BattleTimelineSlot>();

    // 활성 슬롯 관리
    private List<BattleTimelineSlot> currentRoundSlots = new List<BattleTimelineSlot>();
    private List<BattleTimelineSlot> previewSlots = new List<BattleTimelineSlot>();
    private List<BattleTimelineSlot> deadSlots = new List<BattleTimelineSlot>();
    private List<BattleTimelineSlot> counterSlots = new List<BattleTimelineSlot>();
    private BattleTimelineSlot currentTurnSlot;
    private GameObject divider;

    // 시스템 참조
    private TurnOrderSystem turnOrderSystem;
    private BattleProcessManagerNew battleManager;

    

    private bool isInitialized = false;

    private void Awake()
    {
    }
    public void Initialize(TurnOrderSystem turnSystem)
    {
        if (isInitialized)
        {
            Debug.LogWarning("[Timeline] Already initialized, skipping...");
            return;
        }

        if (turnSystem == null)
        {
            Debug.LogError("[Timeline] TurnOrderSystem is null!");
            return;
        }

        // 시스템 참조 설정
        turnOrderSystem = turnSystem;
        battleManager = BattleProcessManagerNew.Instance;

        // 컴포넌트 초기화
        InitializeSlotPool();
        SubscribeEvents();

        // 초기 상태 클리어
        ClearAll();

        // 초기화 완료 플래그
        isInitialized = true;

        Debug.Log("[Timeline] Initialization completed successfully");
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    /// <summary>
    /// 슬롯 풀 초기화
    /// </summary>
    private void InitializeSlotPool()
    {
        int totalSlots = config.maxCurrentRoundSlots + config.maxPreviewSlots +
                        config.maxDeadSlots + config.maxCounterSlots + 5; // 여유분

        for (int i = 0; i < totalSlots; i++)
        {
            GameObject slotObj = Instantiate(slotPrefab, slotContainer);
            BattleTimelineSlot slot = slotObj.GetComponent<BattleTimelineSlot>();

            if (slot == null)
                slot = slotObj.AddComponent<BattleTimelineSlot>();

            slot.Initialize(this, config);
            slot.gameObject.SetActive(false);

            allSlots.Add(slot);
            slotPool.Enqueue(slot);
        }

        // 구분선 생성
        if (dividerPrefab != null)
        {
            divider = Instantiate(dividerPrefab, slotContainer);
            divider.SetActive(false);
        }
    }

    /// <summary>
    /// 이벤트 구독
    /// </summary>
    private void SubscribeEvents()
    {
        if (BattleEventManager.Instance != null)
        {
            var em = BattleEventManager.Instance;
            em.Subscribe(BattleEventType.TurnStart, OnTurnStart);
            em.Subscribe(BattleEventType.TurnEnd, OnTurnEnd);
            em.Subscribe(BattleEventType.CharacterDeath, OnCharacterDeath);
            em.Subscribe(BattleEventType.CharacterRevive, OnCharacterRevive);
            em.Subscribe(BattleEventType.RoundEnd, OnRoundEnd);
        }
    }

    private void UnsubscribeEvents()
    {
        if (BattleEventManager.Instance != null)
        {
            var em = BattleEventManager.Instance;
            em.Unsubscribe(BattleEventType.TurnStart, OnTurnStart);
            em.Unsubscribe(BattleEventType.TurnEnd, OnTurnEnd);
            em.Unsubscribe(BattleEventType.CharacterDeath, OnCharacterDeath);
            em.Unsubscribe(BattleEventType.CharacterRevive, OnCharacterRevive);
            em.Unsubscribe(BattleEventType.RoundEnd, OnRoundEnd);
        }
    }


    /// <summary>
    /// 타임라인 업데이트 (메인 메서드)
    /// </summary>
    public async void UpdateTimeline(
        TurnOrderSystem.TurnOrderInfo currentTurn,
        List<TurnOrderSystem.TurnOrderInfo> remainingTurns,
        List<TurnOrderSystem.TurnOrderInfo> nextRoundPreview)
    {
        if (!isInitialized) return;

        // 현재 턴 처리
        await UpdateCurrentTurn(currentTurn);

        // 대기 중인 턴들 처리
        await UpdateCurrentRound(remainingTurns);

        // 다음 라운드 미리보기 처리
        await UpdateNextRoundPreview(nextRoundPreview);

        // 슬롯 위치 재배치
        await RepositionAllSlots();
    }

    /// <summary>
    /// 현재 턴 업데이트
    /// </summary>
    private async UniTask UpdateCurrentTurn(TurnOrderSystem.TurnOrderInfo turnInfo)
    {
        if (turnInfo == null || turnInfo.Actor == null) return;

        // 기존 현재 턴 슬롯이 있으면 대기 슬롯으로 복귀
        if (currentTurnSlot != null && currentTurnSlot.Actor != turnInfo.Actor)
        {
            currentTurnSlot.SetData(currentTurnSlot.Actor, BattleTimelineSlot.SlotType.Normal);
            currentRoundSlots.Add(currentTurnSlot);
            currentTurnSlot = null;
        }

        // 새 현재 턴 슬롯 찾기 또는 생성
        BattleTimelineSlot slot = FindSlotByActor(turnInfo.Actor);

        if (slot == null)
        {
            slot = GetSlotFromPool();
            if (slot == null) return;
        }
        else
        {
            // 기존 리스트에서 제거
            currentRoundSlots.Remove(slot);
        }

        // 현재 턴으로 설정
        currentTurnSlot = slot;
        // bool isBoss = turnInfo.CharInfo?.IsBoss ?? false;

        bool isBoss = false;
        slot.SetData(turnInfo.Actor, BattleTimelineSlot.SlotType.Current, isBoss);
        slot.gameObject.SetActive(true);

        // 브레이크 상태면 처리
        if (!turnInfo.Actor.CanAct())
        {
            await HandleBreakTurn(turnInfo.Actor);
        }

        // 특별 위치로 이동
        if (currentTurnPosition != null)
        {
            //await slot.MoveTo(currentTurnPosition.position);

            RectTransform slotRect = slot.GetComponent<RectTransform>();
            slotRect.localPosition = currentTurnPosition.position;

        }
    }

    /// <summary>
    /// 현재 라운드 슬롯 업데이트
    /// </summary>
    private async UniTask UpdateCurrentRound(List<TurnOrderSystem.TurnOrderInfo> turnInfos)
    {
        // 기존 슬롯 정리
        foreach (var slot in currentRoundSlots.ToList())
        {
            if (slot != currentTurnSlot && !turnInfos.Any(t => t.Actor == slot.Actor))
            {
                ReturnSlotToPool(slot);
                currentRoundSlots.Remove(slot);
            }
        }

        // 새 슬롯 추가 또는 업데이트
        int displayCount = Mathf.Min(turnInfos.Count, config.maxCurrentRoundSlots);

        for (int i = 0; i < displayCount; i++)
        {
            var info = turnInfos[i];
            if (info.Actor == null || info.Actor == currentTurnSlot?.Actor) continue;

            BattleTimelineSlot slot = FindSlotByActor(info.Actor);

            if (slot == null)
            {
                slot = GetSlotFromPool();
                if (slot == null) break;
                currentRoundSlots.Add(slot);
            }

            //bool isBoss = info.CharInfo?.IsBoss ?? false;
            bool isBoss = false;
            slot.SetData(info.Actor, BattleTimelineSlot.SlotType.Normal, isBoss);
            slot.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// 다음 라운드 미리보기 업데이트
    /// </summary>
    private async UniTask UpdateNextRoundPreview(List<TurnOrderSystem.TurnOrderInfo> preview)
    {
        // 기존 미리보기 슬롯 정리
        foreach (var slot in previewSlots)
        {
            ReturnSlotToPool(slot);
        }
        previewSlots.Clear();

        if (preview == null || preview.Count == 0)
        {
            if (divider != null) divider.SetActive(false);
            return;
        }

        // 구분선 표시
        if (divider != null)
        {
            divider.SetActive(true);
        }

        // 미리보기 슬롯 생성
        int previewCount = Mathf.Min(preview.Count, config.maxPreviewSlots);

        for (int i = 0; i < previewCount; i++)
        {
            var info = preview[i];
            if (info.Actor == null) continue;

            var slot = GetSlotFromPool();
            if (slot == null) break;

            //bool isBoss = info.CharInfo?.IsBoss ?? false;
            bool isBoss = false;
            slot.SetData(info.Actor, BattleTimelineSlot.SlotType.Preview, isBoss);
            slot.gameObject.SetActive(true);

            previewSlots.Add(slot);
        }
    }

    /// <summary>
    /// 모든 슬롯 위치 재배치
    /// </summary>
    private async UniTask RepositionAllSlots()
    {
        float currentX = startX;
        List<UniTask> moveTasks = new List<UniTask>();

        // 현재 라운드 슬롯 배치
        foreach (var slot in currentRoundSlots)
        {

            Vector3 localPos = new Vector3(currentX, slotContainer.position.y, 0);

            RectTransform slotRect = slot.GetComponent<RectTransform>();
            slotRect.localPosition = localPos;

            currentX += config.slotSpacing;
        }

        // 구분선 위치
        if (divider != null && divider.activeSelf)
        {
            divider.transform.localPosition = new Vector3(currentX, slotContainer.position.y, 0);
            currentX += config.dividerWidth;
        }

        // 미리보기 슬롯 배치
        foreach (var slot in previewSlots)
        {
            Vector3 localPos = new Vector3(currentX, slotContainer.position.y, 0);

            RectTransform slotRect = slot.GetComponent<RectTransform>();
            //moveTasks.Add(slot.MoveTo(pos));
            slotRect.localPosition = localPos;

            currentX += config.slotSpacing;
        }

        // 죽은 캐릭터 슬롯 배치
        if (deadSlotContainer != null)
        {
            float deadX = 0;
            foreach (var slot in deadSlots)
            {
                Vector3 pos = new Vector3(deadX, 0, 0);
                slot.transform.localPosition = pos;
                deadX += config.slotSpacing * 0.7f; // 죽은 슬롯은 좀 더 좁게
            }
        }

        if (moveTasks.Count > 0)
        {
            await UniTask.WhenAll(moveTasks);
        }
    }

    /// <summary>
    /// 브레이크 턴 처리
    /// </summary>
    private async UniTask HandleBreakTurn(BattleActor actor)
    {
        await UniTask.Delay((int)(config.breakWaitTime * 1000));
        // 다음 턴으로 자동 진행은 BattleProcessManagerNew에서 처리
    }

    /// <summary>
    /// 반격 슬롯 추가
    /// </summary>
    public async void InsertCounterSlot(BattleActor actor)
    {
        if (counterSlots.Count >= config.maxCounterSlots) return;

        var slot = GetSlotFromPool();
        if (slot == null) return;

        slot.SetData(actor, BattleTimelineSlot.SlotType.Counter);
        slot.gameObject.SetActive(true);
        counterSlots.Add(slot);

        // 현재 턴 바로 뒤에 배치
        float posX = currentTurnSlot != null ?
            currentTurnSlot.transform.position.x + config.slotSpacing :
            startX;

        Vector3 localPos = new Vector3(posX, slotContainer.position.y, 0);
       
        
        slot.transform.localPosition = localPos;
        //await slot.MoveTo(pos);



        // 반격 후 자동 제거
        RemoveCounterSlotDelayed(slot, 2f).Forget();
    }

    private async UniTaskVoid RemoveCounterSlotDelayed(BattleTimelineSlot slot, float delay)
    {
        await UniTask.Delay((int)(delay * 1000));

        if (counterSlots.Contains(slot))
        {
            counterSlots.Remove(slot);
            await slot.Fade(false);
            ReturnSlotToPool(slot);
        }
    }

    /// <summary>
    /// TurnSpeed 변경 시 재정렬
    /// </summary>
    public void OnTurnSpeedChanged(BattleActor actor)
    {
        if (currentTurnSlot?.Actor == actor) return; // 현재 턴은 변경 없음

        // BattleProcessManagerNew에 재정렬 요청
        if (battleManager != null)
        {
            battleManager.RequestTimelineUpdate();
        }
    }

    /// <summary>
    /// 이벤트 핸들러들
    /// </summary>
    private void OnTurnStart(BattleEventData data)
    {
        if (data is TurnEventData turnData)
        {
            // 타임라인 업데이트는 BattleProcessManagerNew에서 호출
        }
    }

    private void OnTurnEnd(BattleEventData data)
    {
        // 현재 턴 슬롯을 완료 상태로 변경
        if (currentTurnSlot != null)
        {
            currentTurnSlot.SetData(currentTurnSlot.Actor, BattleTimelineSlot.SlotType.Normal);
        }
    }

    private async void OnCharacterDeath(BattleEventData data)
    {
        if (data is StatusEventData statusData)
        {
            var actor = statusData.Actor;
            var slot = FindSlotByActor(actor);

            if (slot != null)
            {
                // 죽은 슬롯으로 이동
                currentRoundSlots.Remove(slot);
                previewSlots.Remove(slot);

                slot.SetData(actor, BattleTimelineSlot.SlotType.Dead);
                deadSlots.Add(slot);

                if (deadSlotContainer != null)
                {
                    slot.transform.SetParent(deadSlotContainer);
                }

                await RepositionAllSlots();
            }
        }
    }

    private async void OnCharacterRevive(BattleEventData data)
    {
        if (data is StatusEventData statusData)
        {
            var actor = statusData.Actor;
            var slot = deadSlots.FirstOrDefault(s => s.Actor == actor);

            if (slot != null)
            {
                // 죽은 슬롯에서 제거
                deadSlots.Remove(slot);
                slot.transform.SetParent(slotContainer);

                // 타임라인에 복귀
                slot.SetData(actor, BattleTimelineSlot.SlotType.Normal);
                currentRoundSlots.Add(slot);

                await RepositionAllSlots();
            }
        }
    }

    private void OnRoundEnd(BattleEventData data)
    {
        // 라운드 전환 애니메이션
        TransitionToNewRound().Forget();
    }

    private async UniTaskVoid TransitionToNewRound()
    {
        // 미리보기 슬롯을 현재 라운드로 전환
        foreach (var slot in previewSlots)
        {
            slot.SetData(slot.Actor, BattleTimelineSlot.SlotType.Normal);
            currentRoundSlots.Add(slot);
        }
        previewSlots.Clear();

        await RepositionAllSlots();
    }

    /// <summary>
    /// 유틸리티 메서드들
    /// </summary>
    private BattleTimelineSlot GetSlotFromPool()
    {
        if (slotPool.Count > 0)
        {
            return slotPool.Dequeue();
        }

        // 비활성 슬롯 찾기 (최대 횟수 제한)
        int maxAttempts = allSlots.Count;
        for (int i = 0; i < maxAttempts; i++)
        {
            var slot = allSlots[i];
            if (!slot.gameObject.activeSelf && !IsSlotInUse(slot))
            {
                return slot;
            }
        }
        return null;
    }


    /// <summary>
    /// 슬롯이 현재 사용 중인지 확인
    /// </summary>
    private bool IsSlotInUse(BattleTimelineSlot slot)
    {
        if (slot == null) return true;

        // 1. 현재 턴 슬롯인지 확인
        if (currentTurnSlot == slot)
            return true;

        // 2. 현재 라운드 슬롯에 포함되어 있는지
        if (currentRoundSlots.Contains(slot))
            return true;

        // 3. 미리보기 슬롯에 포함되어 있는지
        if (previewSlots.Contains(slot))
            return true;

        // 4. 죽은 캐릭터 슬롯에 포함되어 있는지
        if (deadSlots.Contains(slot))
            return true;

        // 5. 반격 슬롯에 포함되어 있는지
        if (counterSlots.Contains(slot))
            return true;

        // 6. 슬롯이 애니메이션 중인지 (선택사항)
      //  if (slot.IsAnimating)  // BattleTimelineSlot에 IsAnimating 프로퍼티 필요
       //     return true;

        // 7. 슬롯에 Actor가 할당되어 있는지
        if (slot.Actor != null)
            return true;

        return false;
    }


    private void ReturnSlotToPool(BattleTimelineSlot slot)
    {
        if (slot == null) return;

        slot.Clear();
        slot.gameObject.SetActive(false);

        if (!slotPool.Contains(slot))
        {
            slotPool.Enqueue(slot);
        }
    }

    private BattleTimelineSlot FindSlotByActor(BattleActor actor)
    {
        if (currentTurnSlot?.Actor == actor) return currentTurnSlot;

        foreach (var slot in currentRoundSlots)
        {
            if (slot.Actor == actor) return slot;
        }

        foreach (var slot in previewSlots)
        {
            if (slot.Actor == actor) return slot;
        }

        foreach (var slot in deadSlots)
        {
            if (slot.Actor == actor) return slot;
        }

        return null;
    }

    private void ClearAll()
    {
        currentTurnSlot = null;

        foreach (var slot in currentRoundSlots)
            ReturnSlotToPool(slot);
        foreach (var slot in previewSlots)
            ReturnSlotToPool(slot);
        foreach (var slot in deadSlots)
            ReturnSlotToPool(slot);
        foreach (var slot in counterSlots)
            ReturnSlotToPool(slot);

        currentRoundSlots.Clear();
        previewSlots.Clear();
        deadSlots.Clear();
        counterSlots.Clear();

        if (divider != null)
            divider.SetActive(false);
    }

    public void OnSlotClicked(BattleTimelineSlot slot)
    {
        // 클릭 이벤트 처리
        Debug.Log($"[Timeline] Slot clicked: {slot.Actor?.name}");
    }
}