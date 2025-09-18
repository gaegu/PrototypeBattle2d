using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class BattleDamageNumberUI : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private CustomTextMeshProUGUI[] textDamages;

    [Header("Animation Settings")]
    [SerializeField] private float lifetime = 1.5f;              // 전체 생존 시간
    [SerializeField] private float bounceHeight = 1.5f;          // 튀는 높이
    [SerializeField] private float bounceSpeed = 8f;             // 튀는 속도
    [SerializeField] private float horizontalSpread = 0.5f;      // 좌우 퍼짐 정도
    [SerializeField] private float gravity = 15f;                // 중력 강도
    [SerializeField] private int bounceCount = 2;                // 바운스 횟수
    [SerializeField] private float bounceDamping = 0.6f;         // 바운스 감쇠율 (0~1)

    [Header("Scale Animation")]
    [SerializeField] private float scaleInDuration = 0.1f;       // 스케일 인 시간
    [SerializeField] private float scaleInSize = 1.5f;           // 초기 크기 배율
    [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

    [Header("Fade Settings")]
    [SerializeField] private float fadeStartTime = 0.8f;         // 페이드 시작 시점
    [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.Linear(0, 1, 1, 0);

    private Vector3 originalScale;
    private Vector3 velocity;
    private Vector3 startPosition;
    private float elapsedTime;
    private int currentBounces;
    private float currentBounceHeight;
    private Color[] originalColors;
    private bool isAnimating;

    private void OnEnable()
    {
        // 활성화될 때마다 애니메이션 시작
        if (isAnimating)
        {
            StartAnimation();
        }
    }

    /// <summary>
    /// BattleActor와 연결 설정
    /// </summary>
    public void Set( BattleActor owner, int damage)
    {
        //캡션 포지션 
        Vector3 worldPosition = owner.CaptionPostion.Position;

        // World 좌표를 Screen 좌표로 변환
        Vector3 screenPosition = Camera.main.WorldToScreenPoint(worldPosition);

        // Canvas의 RectTransform 가져오기
        Canvas canvas = this.transform.parent.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[BattleUIManager] Canvas not found!");
            return;
        }

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();

        // Screen 좌표를 Canvas의 로컬 좌표로 변환
        Vector2 localPoint;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPosition,
                canvas.worldCamera,
                out localPoint
            );


        // RectTransform 가져오기
        RectTransform damageRect = GetComponent<RectTransform>();
        if (damageRect == null)
        {
            Debug.LogError("[BattleUIManager] BattleDamageNumberUI must have RectTransform!");
            return;
        }


        damageRect.anchoredPosition = localPoint;

        startPosition = damageRect.position;

        // 텍스트 설정
        foreach (var item in textDamages)
        {
            item.text = damage.ToString();
        }

        // 원본 색상 저장
        SaveOriginalColors();


        this.gameObject.SetActive(true);

        // 애니메이션 시작
        StartAnimation();
    }

    /// <summary>
    /// 애니메이션 시작
    /// </summary>
    private void StartAnimation()
    {
        isAnimating = true;
        elapsedTime = 0f;
        currentBounces = 0;
        currentBounceHeight = bounceHeight;

        // 초기 속도 설정 (위쪽으로 튀어오르며 약간 옆으로)
        float randomX = Random.Range(-horizontalSpread, horizontalSpread);
        velocity = new Vector3(randomX, bounceSpeed, 0);

        // 초기 스케일 설정
        if (originalScale == Vector3.zero)
        {
            originalScale = transform.localScale;
        }
        transform.localScale = originalScale * scaleInSize;

        // 코루틴 시작
        StopAllCoroutines();
        StartCoroutine(AnimatePopup());
    }

    /// <summary>
    /// 팝콘 애니메이션 코루틴
    /// </summary>
    private IEnumerator AnimatePopup()
    {
        while (elapsedTime < lifetime)
        {
            float deltaTime = Time.deltaTime;
            elapsedTime += deltaTime;

            // 1. 위치 업데이트 (포물선 운동)
            UpdatePosition(deltaTime);

            // 2. 스케일 애니메이션
            UpdateScale();

            // 3. 페이드 아웃
            UpdateFade();

            yield return null;
        }

        // 애니메이션 종료
        isAnimating = false;

        StopAllCoroutines();
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 위치 업데이트 (중력과 바운스 적용)
    /// </summary>
    private void UpdatePosition(float deltaTime)
    {
        // 중력 적용
        velocity.y -= gravity * deltaTime;

        // 위치 이동
        Vector3 newPosition = transform.position + velocity * deltaTime;

        // 바닥 충돌 검사 (바운스)
        if (newPosition.y < startPosition.y && velocity.y < 0 && currentBounces < bounceCount)
        {
            newPosition.y = startPosition.y;
            velocity.y = Mathf.Abs(velocity.y) * bounceDamping; // 반발력 감쇠
            velocity.x *= 0.8f; // 수평 속도도 감쇠
            currentBounces++;

            // 바운스 높이 감소
            currentBounceHeight *= bounceDamping;
        }

        // 최종 위치 적용
        transform.position = newPosition;
    }

    /// <summary>
    /// 스케일 애니메이션 업데이트
    /// </summary>
    private void UpdateScale()
    {
        if (elapsedTime < scaleInDuration)
        {
            // 스케일 인 애니메이션
            float t = elapsedTime / scaleInDuration;
            float scaleMultiplier = Mathf.Lerp(scaleInSize, 1f, scaleCurve.Evaluate(t));
            transform.localScale = originalScale * scaleMultiplier;
        }
        else if (currentBounces > 0 && currentBounces <= bounceCount)
        {
            // 바운스할 때마다 살짝 찌그러지는 효과
            float bounceScale = 1f + (Mathf.Sin(Time.time * 20f) * 0.05f);
            transform.localScale = originalScale * bounceScale;
        }
    }

    /// <summary>
    /// 페이드 아웃 업데이트
    /// </summary>
    private void UpdateFade()
    {
        if (elapsedTime > fadeStartTime)
        {
            float fadeProgress = (elapsedTime - fadeStartTime) / (lifetime - fadeStartTime);
            float alpha = fadeCurve.Evaluate(1f - fadeProgress);

            for (int i = 0; i < textDamages.Length; i++)
            {
                if (textDamages[i] != null && i < originalColors.Length)
                {
                    Color newColor = originalColors[i];
                    newColor.a = alpha;
                    textDamages[i].color = newColor;
                }
            }
        }
    }

    /// <summary>
    /// 원본 색상 저장
    /// </summary>
    private void SaveOriginalColors()
    {
        originalColors = new Color[textDamages.Length];
        for (int i = 0; i < textDamages.Length; i++)
        {
            if (textDamages[i] != null)
            {
                originalColors[i] = textDamages[i].color;
            }
        }
    }

    public void SetColor(Color newColor)
    {
        // Set Color
        for (int i = 0; i < textDamages.Length; i++)
        {
            if (textDamages[i] != null)
            {
                textDamages[i].color = newColor;
                if (originalColors != null && i < originalColors.Length)
                {
                    originalColors[i] = newColor;
                }
            }
        }
    }

    public void SetScale(float newScale)
    {
        originalScale = new Vector3(newScale, newScale, newScale);
        transform.localScale = originalScale;
    }

    /// <summary>
    /// 크리티컬 데미지용 특별 애니메이션 설정
    /// </summary>
    public void SetCritical()
    {
        // 크리티컬은 더 크고 높게 튀어오름
        bounceHeight *= 1.5f;
        bounceSpeed *= 1.3f;
        scaleInSize = 2f;
        SetScale(1.3f);
    }

    /// <summary>
    /// 데미지 타입별 설정
    /// </summary>
    public void SetDamageType(string damageType)
    {
        switch (damageType.ToLower())
        {
            case "critical":
                SetCritical();
                SetColor(Color.yellow);
                break;
            case "heal":
                SetColor(Color.green);
                bounceCount = 1;
                gravity *= 0.7f;
                break;
            case "poison":
                SetColor(new Color(0.5f, 0f, 0.5f));
                horizontalSpread *= 2f;
                break;
            case "fire":
                SetColor(new Color(1f, 0.5f, 0f));
                break;
            case "ice":
                SetColor(new Color(0.5f, 0.8f, 1f));
                gravity *= 0.5f; // 얼음은 천천히 떨어짐
                break;
            default:
                // 기본 설정 유지
                break;
        }
    }

    private void OnDisable()
    {
        // 비활성화 시 초기화
        StopAllCoroutines();
        isAnimating = false;
    }
}