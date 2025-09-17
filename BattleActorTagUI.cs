using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class BattleActorTagUI : MonoBehaviour, IPointerClickHandler
{
    [Header("UI 요소")]
    public Image elementIcon;
    public Slider sliderHP;
    public CustomTextMeshProUGUI textHP;

    [Header("설정")]
    public bool isClickable = true;
    [SerializeField] private Vector3 worldOffset = new Vector3(0, 0.5f, 0); // 태그 위치 오프셋

    // 연결된 BattleActor
    private BattleActor linkedActor;
    private BattleCharInfoNew linkedCharInfo;
    private Transform tagPosition; // BattleActor의 tagPosition
    private RectTransform rectTransform;
    private Canvas parentCanvas;
    private Camera mainCamera;

    // 원소 아이콘 스프라이트 (Inspector에서 설정)
    [Header("Element Icons")]
    public Sprite powerIcon;
    public Sprite plasmaIcon;
    public Sprite bioIcon;
    public Sprite chemicalIcon;
    public Sprite electricalIcon;
    public Sprite networkIcon;
    public Sprite noneIcon;



    public GameObject panelGuardianStone;

    public Image[] guardianStoneElementIcon;

    // 원소 아이콘 스프라이트 (Inspector에서 설정)
    [Header("GuardianStone Icons")]
    public Sprite closedGuardianStoneIcon;

    public CustomTextMeshProUGUI textBreak;


    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        UpdateDisplay();
        SetupCamera();
    }

    private void LateUpdate()
    {
        // HP 업데이트
        UpdateHP();

        // 위치 업데이트 (좌표 변환)
        UpdatePosition();
    }

    /// <summary>
    /// BattleActor와 연결 설정
    /// </summary>
    public void Initialize(BattleActor actor, BattleCharInfoNew charInfo)
    {
        linkedActor = actor;
        linkedCharInfo = charInfo;

        // tagPosition 가져오기
        tagPosition = actor.TagPostion.Transform;
        if (tagPosition == null)
        {
            // tagPosition이 없으면 actor의 transform 사용
            tagPosition = actor.transform;
            Debug.LogWarning($"[BattleActorTagUI] {actor.name} has no tagPosition, using transform");
        }

        // Canvas 찾기
        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null && BattleUIManager.Instance != null)
        {
            parentCanvas = BattleUIManager.Instance.GetComponent<Canvas>();
        }

        // 카메라 설정
        SetupCamera();

        // 적군의 태그만 클릭 가능
        isClickable = !charInfo.IsAlly;


        // GuardianStone UI 초기 설정
        if (charInfo.GuardianStoneElement == null || charInfo.GuardianStoneElement.Length == 0)
        {
            if (panelGuardianStone != null)
                panelGuardianStone.SetActive(false);
        }

        if (textBreak != null)
            textBreak.gameObject.SetActive(false);



        // 초기 표시 업데이트
        UpdateDisplay();

        // 초기 위치 설정
        UpdatePosition();
    }

    /// <summary>
    /// 카메라 설정
    /// </summary>
    private void SetupCamera()
    {
        // 메인 카메라 찾기
        mainCamera = Camera.main;
        if (mainCamera == null && BattleCameraManager.Instance != null)
        {
            mainCamera = BattleCameraManager.Instance.MainCamera;
        }
    }

    /// <summary>
    /// 태그 위치 업데이트 (World to UI 좌표 변환)
    /// </summary>
    private void UpdatePosition()
    {
        if (tagPosition == null || rectTransform == null || mainCamera == null)
            return;

        // World 좌표 계산 (tagPosition + offset)
        Vector3 worldPos = tagPosition.position + worldOffset;

        // World 좌표를 Screen 좌표로 변환
        Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

        // 카메라 뒤에 있으면 숨기기
        if (screenPos.z < 0)
        {
            gameObject.SetActive(false);
            return;
        }

        // 활성화
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        // Screen 좌표를 Canvas 좌표로 변환
        if (parentCanvas != null)
        {
            RectTransform canvasRect = parentCanvas.GetComponent<RectTransform>();

            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPos,
                parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera,
                out localPoint
            );

            // 태그 위치 설정
            rectTransform.anchoredPosition = localPoint;
        }
        else
        {
            // Canvas가 없으면 Screen 좌표 직접 사용
            rectTransform.position = screenPos;
        }
    }

    /// <summary>
    /// 전체 UI 업데이트
    /// </summary>
    public void UpdateDisplay()
    {
        if (linkedCharInfo == null) return;

        UpdateElement();
        UpdateHP();
        UpdateClickability();
    }

    /// <summary>
    /// 원소 아이콘 업데이트
    /// </summary>
    private void UpdateElement()
    {
        if (elementIcon == null || linkedCharInfo == null) return;

        // 원소에 따른 아이콘 설정
        Sprite iconSprite = GetElementSprite(linkedCharInfo.Element);
        if (iconSprite != null)
        {
            elementIcon.sprite = iconSprite;
            elementIcon.enabled = true;
        }
        else
        {
            // 아이콘이 없으면 색상으로 표시
            elementIcon.enabled = true;
            elementIcon.color = GetElementColor(linkedCharInfo.Element);
        }
    }

    /// <summary>
    /// HP 정보 업데이트
    /// </summary>
    private void UpdateHP()
    {
        if (linkedCharInfo == null) return;

        if (sliderHP != null)
        {
            sliderHP.maxValue = linkedCharInfo.MaxHp;
            sliderHP.value = linkedCharInfo.Hp;
        }

        if (textHP != null)
        {
            textHP.text = $"{linkedCharInfo.Hp}/{linkedCharInfo.MaxHp}";
        }

        // 죽었으면 태그 숨기기 또는 어둡게
        if (linkedCharInfo.IsDead)
        {
            CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0.3f;
            isClickable = false;
        }
    }

    /// <summary>
    /// 클릭 가능 여부 업데이트
    /// </summary>
    private void UpdateClickability()
    {
        // 아군 태그는 클릭 불가
        if (linkedCharInfo != null && linkedCharInfo.IsAlly)
        {
            isClickable = false;
        }

        // UI 클릭은 OnPointerClick으로 처리되므로 3D 콜라이더 불필요
        // Image의 Raycast Target만 사용
        Image backgroundImage = GetComponent<Image>();
        if (backgroundImage != null)
        {
            backgroundImage.raycastTarget = isClickable;
        }
    }

    /// <summary>
    /// Actor 참조 반환
    /// </summary>
    public BattleActor GetLinkedActor()
    {
        return linkedActor;
    }

    /// <summary>
    /// 위치 오프셋 설정
    /// </summary>
    public void SetWorldOffset(Vector3 offset)
    {
        worldOffset = offset;
        UpdatePosition();
    }

    private void OnDestroy()
    {
        // 참조 정리
        linkedActor = null;
        linkedCharInfo = null;
        tagPosition = null;
    }

    /// <summary>
    /// 태그 클릭 시 처리 (UI 이벤트)
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isClickable || linkedActor == null || linkedActor.IsDead)
            return;

        // BattleTargetSystem이 타겟 선택 중일 때만 처리
        if (BattleTargetSystem.Instance != null && BattleTargetSystem.Instance.IsSelectingTarget)
        {
            BattleTargetSystem.Instance.SelectTargetByTag(linkedActor);

            // 선택 효과
            PlaySelectionEffect();
        }
    }

    /// <summary>
    /// 선택 효과 재생
    /// </summary>
    private void PlaySelectionEffect()
    {
        // 간단한 스케일 애니메이션
        StartCoroutine(ScaleEffect());
    }

    private IEnumerator ScaleEffect()
    {
        Vector3 originalScale = transform.localScale;
        float duration = 0.2f;
        float elapsed = 0;

        // 확대
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            transform.localScale = Vector3.Lerp(originalScale, originalScale * 1.2f, t);
            yield return null;
        }

        // 축소
        elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            transform.localScale = Vector3.Lerp(originalScale * 1.2f, originalScale, t);
            yield return null;
        }

        transform.localScale = originalScale;
    }

    /// <summary>
    /// 원소별 스프라이트 반환
    /// </summary>
    private Sprite GetElementSprite(EBattleElementType element)
    {
        switch (element)
        {
            case EBattleElementType.Power:
                return powerIcon;
            case EBattleElementType.Plasma:
                return plasmaIcon;
            case EBattleElementType.Bio:
                return bioIcon;
            case EBattleElementType.Chemical:
                return chemicalIcon;
            case EBattleElementType.Electrical:
                return electricalIcon;
            case EBattleElementType.Network:
                return networkIcon;
            default:
                return noneIcon;
        }
    }

    /// <summary>
    /// 원소별 색상 반환 (아이콘이 없을 때 사용)
    /// </summary>
    private Color GetElementColor(EBattleElementType element)
    {
        switch (element)
        {
            case EBattleElementType.Power:
                return new Color(1f, 0.3f, 0f);      // 주황색
            case EBattleElementType.Plasma:
                return new Color(0f, 0.7f, 1f);      // 하늘색
            case EBattleElementType.Bio:
                return new Color(0f, 1f, 0.3f);      // 녹색
            case EBattleElementType.Chemical:
                return new Color(0.7f, 0f, 1f);      // 보라색
            case EBattleElementType.Electrical:
                return new Color(1f, 1f, 0f);        // 노란색
            case EBattleElementType.Network:
                return new Color(0.2f, 0f, 0.3f);    // 검은색
            default:
                return Color.white;
        }
    }

    /// <summary>
    /// 타겟 선택 가능 상태 설정
    /// </summary>
    public void SetSelectable(bool selectable)
    {
        if (linkedCharInfo != null && linkedCharInfo.IsAlly)
        {
            // 아군은 항상 선택 불가
            isClickable = false;
            return;
        }

        isClickable = selectable && !linkedActor.IsDead;

        // 시각적 피드백
        if (isClickable)
        {
            // 선택 가능할 때 강조 효과
            Image backgroundImage = GetComponent<Image>();
            if (backgroundImage != null)
            {
                backgroundImage.color = new Color(1f, 1f, 1f, 1f);
            }
        }
        else
        {
            // 선택 불가능할 때 어둡게
            Image backgroundImage = GetComponent<Image>();
            if (backgroundImage != null)
            {
                backgroundImage.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
            }
        }
    }
}