using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum BattleTextType
{
    Miss,
    Dodge,
    Block,
    Resist,
    Immune,
    Critical,
    Weak,       // 약점 공격
    Break,      // 브레이크
    Counter,    // 반격
    Reflect,     // 반사
    HealBlocked,
    Silence,
    CooldownUp,
    CooldownReset,
    SkillSeal,

}

public class BattleTextUI : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private CustomTextMeshProUGUI textDisplay;
    [SerializeField] private CanvasGroup canvasGroup;


    [Header("Animation Profiles")]
    [SerializeField] private TextAnimationProfile[] animationProfiles;

    // 현재 애니메이션 설정
    private TextAnimationProfile currentProfile;
    private Vector3 startPosition;
    private Vector3 originalScale;
    private float elapsedTime;
    private bool isAnimating;

    // 애니메이션 상태
    private Vector3 currentVelocity;
    private float rotationSpeed;
    private float shakeIntensity;

    [System.Serializable]
    public class TextAnimationProfile
    {
        public BattleTextType textType;
        public string displayText;
        public Color textColor = Color.white;
        public Color outlineColor = Color.black;
        public float outlineWidth = 2f;

        [Header("Movement")]
        public AnimationType animationType = AnimationType.FloatUp;
        public float moveSpeed = 2f;
        public float moveDistance = 1.5f;
        public AnimationCurve movementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Scale")]
        public bool useScaleAnimation = true;
        public float initialScale = 1.5f;
        public float finalScale = 1f;
        public AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

        [Header("Rotation")]
        public bool useRotation = false;
        public float rotationAmount = 15f;
        public float rotationFrequency = 2f;

        [Header("Effects")]
        public bool useShake = false;
        public float shakeAmount = 0.1f;
        public float shakeSpeed = 30f;

        [Header("Timing")]
        public float duration = 1.5f;
        public float fadeStartTime = 0.7f;
        public AnimationCurve fadeCurve = AnimationCurve.Linear(0, 1, 1, 0);
    }

    public enum AnimationType
    {
        FloatUp,        // 위로 떠오름
        BounceUp,       // 튀어오름
        SlideIn,        // 옆에서 슬라이드
        Expand,         // 확대되며 나타남
        Spiral,         // 나선형
        Wave,           // 파도 움직임
        ElasticPop,     // 탄성 팝
        FallDown        // 떨어짐
    }



    private void InitializeDefaultProfiles()
    {
        if (animationProfiles == null || animationProfiles.Length == 0)
        {
            animationProfiles = new TextAnimationProfile[]
            {
                // MISS - 옆으로 슬라이드하며 사라짐
                new TextAnimationProfile
                {
                    textType = BattleTextType.Miss,
                    displayText = "MISS",
                    textColor = Color.gray,
                    animationType = AnimationType.SlideIn,
                    moveSpeed = 3f,
                    useRotation = true,
                    rotationAmount = 10f,
                    duration = 1.2f
                },
                
                // DODGE - 빠르게 옆으로 회피하는 움직임
                new TextAnimationProfile
                {
                    textType = BattleTextType.Dodge,
                    displayText = "DODGE",
                    textColor = Color.cyan,
                    animationType = AnimationType.Wave,
                    moveSpeed = 4f,
                    useShake = true,
                    shakeAmount = 0.15f,
                    duration = 1.3f
                },
                
                // BLOCK - 단단하게 튀어오르는 움직임
                new TextAnimationProfile
                {
                    textType = BattleTextType.Block,
                    displayText = "BLOCK",
                    textColor = new Color(0.3f, 0.5f, 1f), // 파란색
                    animationType = AnimationType.BounceUp,
                    initialScale = 1.8f,
                    useShake = true,
                    shakeAmount = 0.2f,
                    shakeSpeed = 40f,
                    duration = 1.4f
                },
                
                // RESIST - 저항하는 듯한 진동
                new TextAnimationProfile
                {
                    textType = BattleTextType.Resist,
                    displayText = "RESIST",
                    textColor = new Color(0.8f, 0.3f, 0.8f), // 보라색
                    animationType = AnimationType.ElasticPop,
                    useShake = true,
                    shakeAmount = 0.1f,
                    useRotation = true,
                    rotationAmount = 5f,
                    duration = 1.5f
                },
                
                // IMMUNE - 강력한 보호막 효과
                new TextAnimationProfile
                {
                    textType = BattleTextType.Immune,
                    displayText = "IMMUNE",
                    textColor = Color.yellow,
                    outlineColor = new Color(1f, 0.8f, 0f),
                    outlineWidth = 3f,
                    animationType = AnimationType.Expand,
                    initialScale = 2f,
                    finalScale = 1.2f,
                    duration = 1.6f
                },
                
                // CRITICAL - 강렬한 임팩트
                new TextAnimationProfile
                {
                    textType = BattleTextType.Critical,
                    displayText = "CRITICAL!",
                    textColor = new Color(1f, 0.2f, 0.2f), // 빨간색
                    animationType = AnimationType.ElasticPop,
                    initialScale = 2.5f,
                    finalScale = 1.3f,
                    useShake = true,
                    shakeAmount = 0.25f,
                    duration = 1.8f
                }
            };
        }
    }

    /// <summary>
    /// 텍스트 표시 초기화 및 시작
    /// </summary>
    public void Set(BattleActor owner, BattleTextType textType)
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (textDisplay == null)
            textDisplay = GetComponentInChildren<CustomTextMeshProUGUI>();


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
        RectTransform uiRect = GetComponent<RectTransform>();
        if (uiRect == null)
        {
            Debug.LogError("[BattleUIManager] BattleDamageNumberUI must have RectTransform!");
            return;
        }


        uiRect.anchoredPosition = localPoint;

        startPosition = uiRect.position;

        originalScale = transform.localScale;

        // 기본 프로필 초기화
        InitializeDefaultProfiles();



        elapsedTime = 0f;

        // 해당 타입의 프로필 찾기
        currentProfile = GetProfile(textType);
        if (currentProfile == null)
        {
            Debug.LogWarning($"No profile found for {textType}, using default");
            currentProfile = animationProfiles[0];
        }



        // 텍스트 설정
        SetupText();

        this.gameObject.SetActive(true);

        // 애니메이션 시작
        StartAnimation();
    }

    /// <summary>
    /// 커스텀 텍스트로 초기화
    /// </summary>
    public void Initialize(Vector3 position, string customText, Color color)
    {
        startPosition = position;
        elapsedTime = 0f;

        // 기본 프로필 사용
        currentProfile = new TextAnimationProfile
        {
            displayText = customText,
            textColor = color,
            animationType = AnimationType.FloatUp,
            duration = 1.5f
        };

        SetupText();
        StartAnimation();
    }

    private TextAnimationProfile GetProfile(BattleTextType type)
    {
        foreach (var profile in animationProfiles)
        {
            if (profile.textType == type)
                return profile;
        }
        return null;
    }

    private void SetupText()
    {
        if (textDisplay != null)
        {
            textDisplay.text = currentProfile.displayText;
            textDisplay.color = currentProfile.textColor;

            // 외곽선 설정 (TMP 사용 시)
            if (currentProfile.outlineWidth > 0)
            {
             // //  textDisplay.outlineWidth = currentProfile.outlineWidth;
             //   textDisplay.outlineColor = currentProfile.outlineColor;
            }
        }

        // 초기 스케일 설정
        if (currentProfile.useScaleAnimation)
        {
            transform.localScale = originalScale * currentProfile.initialScale;
        }

        // 초기 알파값 설정
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }

        // 애니메이션별 초기 설정
        SetupAnimationType();
    }

    private void SetupAnimationType()
    {
        switch (currentProfile.animationType)
        {
            case AnimationType.SlideIn:
                transform.position = startPosition + Vector3.right * 2f;
                break;

            case AnimationType.FallDown:
                transform.position = startPosition + Vector3.up * 2f;
                currentVelocity = Vector3.zero;
                break;

            case AnimationType.Spiral:
                rotationSpeed = 360f;
                break;

            default:
                transform.position = startPosition;
                break;
        }
    }

    private void StartAnimation()
    {
        if (isAnimating)
        {
            StopAllCoroutines();
        }

        isAnimating = true;
        StartCoroutine(AnimateText());
    }

    private IEnumerator AnimateText()
    {
        while (elapsedTime < currentProfile.duration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = elapsedTime / currentProfile.duration;

            // 이동 애니메이션
            UpdateMovement(normalizedTime);

            // 스케일 애니메이션
            if (currentProfile.useScaleAnimation)
            {
                UpdateScale(normalizedTime);
            }

            // 회전 애니메이션
            if (currentProfile.useRotation)
            {
                UpdateRotation(normalizedTime);
            }

            // 흔들림 효과
            if (currentProfile.useShake)
            {
                UpdateShake();
            }

            // 페이드 아웃
            if (elapsedTime >= currentProfile.fadeStartTime)
            {
                UpdateFade(normalizedTime);
            }

            yield return null;
        }

        // 애니메이션 종료
        isAnimating = false;
        gameObject.SetActive(false);
    }

    private void UpdateMovement(float normalizedTime)
    {
        Vector3 targetPosition = startPosition;
        float curveValue = currentProfile.movementCurve.Evaluate(normalizedTime);

        switch (currentProfile.animationType)
        {
            case AnimationType.FloatUp:
                targetPosition = startPosition + Vector3.up * (currentProfile.moveDistance * curveValue);
                break;

            case AnimationType.BounceUp:
                float bounce = Mathf.Abs(Mathf.Sin(normalizedTime * Mathf.PI * 3f)) * (1f - normalizedTime);
                targetPosition = startPosition + Vector3.up * (currentProfile.moveDistance + bounce);
                break;

            case AnimationType.SlideIn:
                float slideX = Mathf.Lerp(2f, -1f, curveValue);
                targetPosition = startPosition + Vector3.right * slideX;
                break;

            case AnimationType.Expand:
                // 확대는 스케일로만 처리
                targetPosition = startPosition;
                break;

            case AnimationType.Spiral:
                float angle = normalizedTime * Mathf.PI * 4f;
                float radius = currentProfile.moveDistance * (1f - normalizedTime);
                targetPosition = startPosition + new Vector3(
                    Mathf.Cos(angle) * radius,
                    curveValue * currentProfile.moveDistance,
                    0
                );
                break;

            case AnimationType.Wave:
                float waveX = Mathf.Sin(normalizedTime * Mathf.PI * 4f) * 0.5f * (1f - normalizedTime);
                targetPosition = startPosition + new Vector3(waveX, curveValue * currentProfile.moveDistance, 0);
                break;

            case AnimationType.ElasticPop:
                float elastic = 1f + Mathf.Sin(normalizedTime * Mathf.PI * 6f) * 0.3f * (1f - normalizedTime);
                targetPosition = startPosition + Vector3.up * (currentProfile.moveDistance * curveValue * elastic);
                break;

            case AnimationType.FallDown:
                currentVelocity += Vector3.down * 9.8f * Time.deltaTime;
                targetPosition = transform.position + currentVelocity * Time.deltaTime;

                // 바운스
                if (targetPosition.y < startPosition.y && currentVelocity.y < 0)
                {
                    targetPosition.y = startPosition.y;
                    currentVelocity.y *= -0.5f;
                }
                break;
        }

        transform.position = targetPosition;
    }

    private void UpdateScale(float normalizedTime)
    {
        float scaleCurveValue = currentProfile.scaleCurve.Evaluate(normalizedTime);
        float targetScale = Mathf.Lerp(currentProfile.initialScale, currentProfile.finalScale, scaleCurveValue);

        // ElasticPop 타입은 특별한 스케일 애니메이션
        if (currentProfile.animationType == AnimationType.ElasticPop)
        {
            float elastic = 1f + Mathf.Sin(normalizedTime * Mathf.PI * 8f) * 0.2f * (1f - normalizedTime);
            targetScale *= elastic;
        }

        transform.localScale = originalScale * targetScale;
    }

    private void UpdateRotation(float normalizedTime)
    {
        float rotation = Mathf.Sin(normalizedTime * Mathf.PI * currentProfile.rotationFrequency) * currentProfile.rotationAmount;

        // Spiral 타입은 계속 회전
        if (currentProfile.animationType == AnimationType.Spiral)
        {
            rotation = normalizedTime * rotationSpeed;
        }

        transform.rotation = Quaternion.Euler(0, 0, rotation);
    }

    private void UpdateShake()
    {
        if (currentProfile.shakeAmount > 0)
        {
            float shakeX = Mathf.Sin(Time.time * currentProfile.shakeSpeed) * currentProfile.shakeAmount;
            float shakeY = Mathf.Cos(Time.time * currentProfile.shakeSpeed * 0.7f) * currentProfile.shakeAmount;

            transform.position += new Vector3(shakeX, shakeY, 0) * (1f - (elapsedTime / currentProfile.duration));
        }
    }

    private void UpdateFade(float normalizedTime)
    {
        float fadeProgress = (elapsedTime - currentProfile.fadeStartTime) / (currentProfile.duration - currentProfile.fadeStartTime);
        float alpha = currentProfile.fadeCurve.Evaluate(1f - fadeProgress);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = alpha;
        }
    }

    /// <summary>
    /// 특정 프로필로 설정 변경
    /// </summary>
    public void SetProfile(TextAnimationProfile profile)
    {
        currentProfile = profile;
    }

    /// <summary>
    /// 텍스트 색상 변경
    /// </summary>
    public void SetColor(Color color)
    {
        if (textDisplay != null)
        {
            textDisplay.color = color;
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        isAnimating = false;
        elapsedTime = 0f;

        // 초기 상태로 리셋
        transform.localScale = originalScale;
        transform.rotation = Quaternion.identity;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }
    }
}