using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Cysharp.Threading.Tasks;

public class BattleTimelineSlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI Components")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private Image borderImage;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private GameObject deathMark;  // X 표시

    [Header("State")]
    private SlotType slotType = SlotType.Empty;
    private BattleActor actor;
    private BattleTimelineConfig config;
    private BattleTimelineSystem timeline;

    // 애니메이션 관련
    private Coroutine moveCoroutine;
    private Coroutine pulseCoroutine;
    private Vector3 originalScale;
    private bool isAnimating = false;

    // 상태 관련
    private bool isBoss = false;
    private bool isBreak = false;
    private List<string> statusEffects = new List<string>();

    public enum SlotType
    {
        Empty,
        Normal,
        Current,
        Preview,
        Dead,
        Counter,
        Divider
    }

    // Properties
    public BattleActor Actor => actor;
    public SlotType Type => slotType;
    public bool IsEmpty => slotType == SlotType.Empty;


    public void Initialize(BattleTimelineSystem system, BattleTimelineConfig configuration)
    {

        originalScale = transform.localScale;
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();


        timeline = system;
        config = configuration;
    }

    /// <summary>
    /// 슬롯 데이터 설정
    /// </summary>
    public void SetData(BattleActor battleActor, SlotType type, bool boss = false)
    {
        actor = battleActor;
        slotType = type;
        isBoss = boss;

        if (actor != null)
        {
            // 포트레이트 설정
            SetPortrait();

            // 브레이크 상태 체크
            isBreak = !actor.CanAct();

            // 상태 이상 체크
            UpdateStatusEffects();
        }


        //this.gameObject.name = slotType + " / " + battleActor.BattleActorInfo.Name + "/ Is Ally " + battleActor.BattleActorInfo.IsAlly;

        UpdateVisual();
    }

    /// <summary>
    /// 비주얼 업데이트
    /// </summary>
    private void UpdateVisual()
    {
        // 타입별 스케일 설정
        float targetScale = GetTargetScale();
        transform.localScale = originalScale * targetScale;

        // 타입별 알파 설정
        canvasGroup.alpha = GetTargetAlpha();

        // 테두리 색상 설정
        UpdateBorderColor();

        // 브레이크 상태 처리
        if (isBreak)
        {
            transform.rotation = Quaternion.Euler(0, 0, config.breakRotationAngle);
        }
        else
        {
            transform.rotation = Quaternion.identity;
        }

        // 죽음 표시
        if (deathMark != null)
        {
            deathMark.SetActive(slotType == SlotType.Dead);
        }

        // 현재 턴 펄스 애니메이션
        if (slotType == SlotType.Current)
        {
            StartPulse();
        }
        else
        {
            StopPulse();
        }

        // 상태 텍스트 업데이트
        UpdateStatusText();
    }

    private float GetTargetScale()
    {
        switch (slotType)
        {
            case SlotType.Current: return config.currentTurnScale;
            case SlotType.Preview: return config.previewScale;
            case SlotType.Dead: return config.deadScale;
            case SlotType.Counter: return config.counterScale;
            default: return config.normalScale;
        }
    }

    private float GetTargetAlpha()
    {
        switch (slotType)
        {
            case SlotType.Preview: return config.previewAlpha;
            case SlotType.Dead: return config.deadAlpha;
            default: return isBreak ? config.breakAlpha : config.normalAlpha;
        }
    }

    private void UpdateBorderColor()
    {
        if (borderImage == null || actor == null) return;

        Color color = Color.white;

        if (slotType == SlotType.Counter)
        {
            color = config.counterBorderColor;
        }
        else if (isBoss)
        {
            color = config.bossBorderColor;
        }
        else if (actor.BattleActorInfo.IsAlly)
        {
            color = config.allyBorderColor;
        }
        else
        {
            color = config.enemyBorderColor;
        }

        borderImage.color = color;
    }

    /// <summary>
    /// 포트레이트 설정
    /// </summary>
    private void SetPortrait()
    {
        if (portraitImage == null || actor == null) return;

        // BattleActorTagUI의 아이콘을 참조하거나
        // Resources에서 로드
        string portraitPath = $"Portraits/{actor.BattleActorInfo.Name}";
        Sprite portrait = Resources.Load<Sprite>(portraitPath);

        if (portrait != null)
        {
            portraitImage.sprite = portrait;
        }
    }


    /// <summary>
    /// 페이드 인/아웃
    /// </summary>
    public async UniTask Fade(bool fadeIn, float duration = -1)
    {
        if (duration < 0) duration = config.fadeAnimDuration;

        float startAlpha = canvasGroup.alpha;
        float targetAlpha = fadeIn ? GetTargetAlpha() : 0;
        float elapsed = 0;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            await UniTask.Yield();
        }

        canvasGroup.alpha = targetAlpha;

        if (!fadeIn)
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 펄스 애니메이션
    /// </summary>
    private void StartPulse()
    {
        if (pulseCoroutine != null) return;
        pulseCoroutine = StartCoroutine(PulseAnimation());
    }

    private void StopPulse()
    {
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
            transform.localScale = originalScale * GetTargetScale();
        }
    }

    private IEnumerator PulseAnimation()
    {
        float baseScale = GetTargetScale();

        while (pulseCoroutine != null)  // 조건 추가
        {
            if (!gameObject.activeInHierarchy)  // 비활성 체크
            {
                yield break;
            }

            float scale = baseScale + Mathf.Sin(Time.time * config.pulseSpeed) * 0.1f;
            transform.localScale = originalScale * scale;
            yield return null;
        }
    }

    /// <summary>
    /// 상태 이상 업데이트
    /// </summary>
    private void UpdateStatusEffects()
    {
        statusEffects.Clear();

        if (actor?.SkillManager != null)
        {
            if (actor.SkillManager.IsBattleSkillBase(SkillSystem.StatusType.Stun))
                statusEffects.Add("스턴");
            if (actor.SkillManager.IsBattleSkillBase(SkillSystem.StatusType.Silence))
                statusEffects.Add("침묵");
            if (actor.SkillManager.IsBattleSkillBase(SkillSystem.StatusType.Poison))
                statusEffects.Add("중독");
        }
    }

    private void UpdateStatusText()
    {
        if (statusText != null)
        {
            statusText.text = string.Join(" ", statusEffects);
        }
    }

    /// <summary>
    /// 클릭 이벤트
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (actor == null || slotType == SlotType.Divider) return;

        // 타겟 선택 상황이면
        if (BattleTargetSystem.Instance?.IsSelectingTarget == true)
        {
            BattleTargetSystem.Instance.SelectTargetByTag(actor);
        }

        timeline?.OnSlotClicked(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // 툴팁 표시 (추후 구현)
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // 툴팁 숨김 (추후 구현)
    }

    /// <summary>
    /// 슬롯 초기화
    /// </summary>
    public void Clear()
    {
        actor = null;
        slotType = SlotType.Empty;
        statusEffects.Clear();
        isBreak = false;
        isBoss = false;

        StopPulse();
        transform.rotation = Quaternion.identity;
        transform.localScale = originalScale;
        canvasGroup.alpha = 1f;

        if (deathMark != null)
            deathMark.SetActive(false);
        if (statusText != null)
            statusText.text = "";
    }
}