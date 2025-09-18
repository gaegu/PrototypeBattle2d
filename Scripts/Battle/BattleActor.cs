using Cysharp.Threading.Tasks;
using Febucci.UI.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;



 public partial class BattleActor : MonoBehaviour
{
    public bool ReferenceEquals(BattleActor other_)
    {
        return this == other_;
    }


    public Vector3 position
    {
        get
        {
            return transform.position;
        }

        set
        {
            transform.position = value;
        }
    }

    public bool isFrontFormation { set; get; }

    public bool isCompletedAttack { set; get; }


    public virtual bool IsDead
    {
        get
        {
            // return BattleActorInfo != null && BattleActorInfo.IsDead;
            return false;
        }
    }
    public bool NeverDieMode { get; protected set; }

    public float moveSpeed = 3f;


    [Header("포지션")]
    [SerializeField]
    protected Transform floorPosition = null;
    [SerializeField]
    protected Transform tagPosition = null;
    [SerializeField]
    protected Transform captionPosition = null;


    public TransformWrapper RootTransform { get => TransformWrapper.Get(floorPosition); }
    public TransformWrapper TagPostion { get => TransformWrapper.Get(tagPosition); }

    public TransformWrapper CaptionPostion { get => TransformWrapper.Get(captionPosition); }


    [SerializeField]
    protected BattleCharInfoNew battleCharInfo = null;

    [SerializeField]
    protected BattleActorDirection initLookDirection = BattleActorDirection.Left;
    [SerializeField]
    private CharacterSpriteAnimator spriteSheetAnimation = null;
    [SerializeField]
    protected ResourcesLoader captionLoader = null;

    private StateBehaviour actorBehaviour = new StateBehaviour();

    protected  Transform MoveTarget { get => spriteSheetAnimation.transform; }
    public  VectorWrapper Size { get => VectorWrapper.Get(spriteSheetAnimation.GetSize()); }
    public  bool IsShowRenderer { get => spriteSheetAnimation.isActiveAndEnabled; }

    public BattleActorState PrevState { get; protected set; }

    public BattleActorState State { get; private set; }
    public BattleActorDirection LookDirection { get; private set; }

    public Vector3 StartLocalPosition { get; protected set; }


    [Header("idle 최소 시간")]
    [SerializeField]
    private float waitIdleMinTime = 2f;

    [Header("idle 최대 시간")]
    [SerializeField]
    private float waitIdleMaxTime = 5f;

    [Header("태그 UI")]
    private GameObject actorTagObject = null;  // 생성된 태그 오브젝트
    private BattleActorTagUI actorTag = null;  // 태그 UI 컴포넌트 참조


    // 움직임 기반 애니메이션 관련 필드
    [Header("Movement Animation Settings")]
    [SerializeField] private bool enableMovementAnimation = true;  // 기능 on/off
    [SerializeField] private float idleResetTime = 0.5f;          // Idle 전환 대기 시간
    [SerializeField] private float movementThreshold = 0.001f;     // 움직임 감지 임계값

    // 내부 상태 추적
    private Vector3 lastFramePosition;
    private float idleTimer = 0f;
    private bool wasMoving = false;



    // 원래 방향을 저장할 변수 추가 (클래스 상단에)
    private bool originalFlipX;

    public bool isMoving { get; private set; }

    // 피격 색상 관련 필드 추가
    private SpriteRenderer spriteRenderer;
    private Color originalColor = Color.white;
    private Coroutine hitColorCoroutine;


    // GuardianStone 시스템 추가
    private GuardianStoneSystem guardianStoneSystem;

    // GuardianStone 시스템 getter
    public GuardianStoneSystem GuardianStone => guardianStoneSystem;


    virtual public BattleCharInfoNew BattleActorInfo
    {
        get
        {
            return battleCharInfo;
        }

        set
        {
            battleCharInfo = value;

         

        }
    }

    public void Init( BattleCharInfoNew battleCharInfo)
    {
        spriteSheetAnimation.SafeSetActive(true);
        captionLoader.SafeSetActive(false);

        // 움직임 추적 초기화 추가
        lastFramePosition = transform.position;


        BattleActorInfo = battleCharInfo;

        battleCharInfo.Initialize(this);


        spriteSheetAnimation.Initialize( "Char_" + battleCharInfo.GetRootName() );

        //방향 교체 
        spriteSheetAnimation.SetFlip(!battleCharInfo.IsAlly, false);

        SetLookDirection();

        originalFlipX = !battleCharInfo.IsAlly; // 원래 방향 저장

        if (spriteSheetAnimation)
        {
            // 미리 로드함
            spriteSheetAnimation.LoadAnimationsAsync().Forget();
        }

        // 이펙트 ID 초기화 자동 호출
        InitializeEffectIds();


        // SpriteRenderer 캐싱
        InitializeSpriteRenderer();


        // 스킬 시스템 초기화 추가
        InitializeSkillSystem();
        InitializeBPSystem();

        InitializeTimelineSystem();


        // BP 테스트 모드 체크 및 적용
        if (BattleActorInfo != null && BattleActorInfo.EnableBPSkillTest)
        {
            InitializeBPTestMode();
        }



        // GuardianStone 시스템 초기화
        if (guardianStoneSystem == null)
        {
            guardianStoneSystem = new GuardianStoneSystem();
        }

        // ===== 태그 UI 생성 =====
        CreateTagUI();

        if (actorTag != null)
        {
            guardianStoneSystem.Initialize(this, battleCharInfo, actorTag);
        }

    }

    private void InitializeSpriteRenderer()
    {
        if (spriteSheetAnimation != null)
        {
            spriteRenderer = spriteSheetAnimation.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                originalColor = spriteRenderer.color;
            }
        }
    }

    /// <summary>
    /// 태그 UI 생성 - BattleUIManager의 프리팹 활용
    /// </summary>
    private void CreateTagUI()
    {
        // BattleUIManager 확인
        if (BattleUIManager.Instance == null)
        {
            Debug.LogError("[BattleActor] BattleUIManager.Instance is null!");
            return;
        }

        // 태그 프리팹 가져오기
        GameObject tagPrefab = BattleUIManager.Instance.BattleActorTagUI;
        if (tagPrefab == null)
        {
            Debug.LogError("[BattleActor] BattleActorTagUI prefab not found in BattleUIManager!");
            return;
        }

        GameObject tagObject = Instantiate(tagPrefab, BattleUIManager.Instance.rootComponents );
        actorTag = tagObject.GetComponent<BattleActorTagUI>();

        if (actorTag == null)
        {
            Debug.LogError("[BattleActor] BattleActorTagUI component not found!");
            Destroy(tagObject);
            return;
        }

        // BattleActorTagUI 초기화 (태그가 자체적으로 위치 업데이트)
        actorTag.Initialize(this, BattleActorInfo);
        tagObject.SetActive(true);

        Debug.Log($"[BattleActor] Tag UI created for {name}");
    }


    /// <summary>
    /// 태그 표시/숨기기
    /// </summary>
    public void ShowTag(bool show)
    {
        if (actorTagObject != null)
        {
            actorTagObject.SetActive(show);
        }
    }

    /// <summary>
    /// 태그 업데이트
    /// </summary>
    public void UpdateTag()
    {
        if (actorTag != null)
        {
            actorTag.UpdateDisplay();
        }
    }

    /// <summary>
    /// 태그 선택 가능 상태 설정
    /// </summary>
    public void SetTagSelectable(bool selectable)
    {
        if (actorTag != null)
        {
            actorTag.SetSelectable(selectable);
        }
    }

    /// <summary>
    /// 태그 가져오기
    /// </summary>
    public BattleActorTagUI GetTag()
    {
        return actorTag;
    }



    private void OnDestroy()
    {
        if (hitColorCoroutine != null)
        {
            StopCoroutine(hitColorCoroutine);
            hitColorCoroutine = null;
        }

        // 태그 오브젝트 제거
        if (actorTagObject != null)
        {
            Destroy(actorTagObject);
        }

        CleanupSkillSystem();

        CleanupTimeline();

    }


    // 피격 색상 효과 메서드
    private void PlayHitColorEffect()
    {
        if (hitColorCoroutine != null)
        {
            StopCoroutine(hitColorCoroutine);
        }
        hitColorCoroutine = StartCoroutine(HitColorAnimation());
    }

    // 피격 색상 애니메이션 코루틴
    private IEnumerator HitColorAnimation()
    {
        if (spriteRenderer == null)
        {
            // SpriteRenderer가 없으면 다시 시도
            InitializeSpriteRenderer();
            if (spriteRenderer == null)
                yield break;
        }

        // 피격 색상 (빨간색)
        Color hitColor = new Color(1f, 0.3f, 0.3f, 1f);

        // 빠른 플래시 효과 (3번 반복)
        for (int i = 0; i < 3; i++)
        {
            spriteRenderer.color = hitColor;
            yield return new WaitForSeconds(0.1f);
            spriteRenderer.color = originalColor;
            yield return new WaitForSeconds(0.05f);
        }

        // 최종적으로 원래 색상으로 복구
        spriteRenderer.color = originalColor;
        hitColorCoroutine = null;
    }


    public void SetPosition(Vector3 position)
    {
        transform.position = position;

        Vector3 localPosition = transform.localPosition;
        localPosition.y -= floorPosition.localPosition.y;
        transform.localPosition = localPosition;
    }


    private void Start()
    {
        // Start에서도 한 번 더 확인 (BattleActorInfo가 늦게 설정되는 경우)
        if (!effectsInitialized)
        {
            InitializeEffectIds();
        }
    }


    /// <summary>
    /// BattleActorInfo 설정 시 이펙트도 초기화
    /// </summary>
    public void SetBattleActorInfo(BattleCharInfoNew info)
    {
        BattleActorInfo = info;

        // BattleActorInfo 설정 후 이펙트 재초기화
        effectsInitialized = false;
        InitializeEffectIds();
    }


    private void SetLookDirection()
    {
        LookDirection = spriteSheetAnimation.GetFlip().flipX ? BattleActorDirection.Left : BattleActorDirection.Right;
    }



    public void SetState(BattleActorState onState)
    {
        State = onState;

        
       // SetAnimation(onState);
    }

    private void Update()
    {
        StartUpdateState();

        // 스킬 매니저 업데이트 추가
        UpdateSkillSystem();

        // 움직임 기반 애니메이션 업데이트 추가
        if (enableMovementAnimation && !isMoving)  // MoveToPosition 중이 아닐 때만
        {
            HandleMovementAnimation();
        }
    }

    /// <summary>
    /// 움직임 감지 및 애니메이션 자동 전환
    /// </summary>
    private void HandleMovementAnimation()
    {
        // 전투 중이거나 특정 상태에서는 무시
        if (State == BattleActorState.Attack ||
            State == BattleActorState.Dead)
        {
            return;
        }

        Vector3 currentPosition = transform.position;
        float horizontalMovement = Mathf.Abs(currentPosition.x - lastFramePosition.x);
        bool isCurrentlyMoving = horizontalMovement > movementThreshold;

        // 움직임 시작
        if (isCurrentlyMoving && !wasMoving)
        {
            idleTimer = 0f;
            SetAnimation(BattleActorAnimation.Walk);
            wasMoving = true;
        }
        // 움직임 중지
        else if (!isCurrentlyMoving && wasMoving)
        {
            idleTimer = 0f;
            wasMoving = false;
        }
        // 정지 상태 지속
        else if (!isCurrentlyMoving && !wasMoving)
        {
            idleTimer += Time.deltaTime;

            // Idle로 전환
            if (idleTimer >= idleResetTime && State != BattleActorState.Idle)
            {
                SetState(BattleActorState.Idle);
                SetAnimation(BattleActorAnimation.Idle);
            }
        }

        // 이동 중 방향 전환 처리 (기존 flip 로직 활용)
        if (isCurrentlyMoving)
        {
            float direction = currentPosition.x - lastFramePosition.x;
            if (Mathf.Abs(direction) > movementThreshold)
            {
                bool shouldFlipX = BattleActorInfo.IsAlly ?
                    (direction > 0) : (direction < 0);

                if (spriteSheetAnimation.GetFlip().flipX != shouldFlipX)
                {
                    spriteSheetAnimation.SetFlip(shouldFlipX, false);
                    SetLookDirection();
                }
            }
        }

        lastFramePosition = currentPosition;
    }



    public virtual void SetAnimation(BattleActorAnimation animationState)
    {
        if (spriteSheetAnimation == null)
        {
            IronJade.Debug.LogError("SpriteSheetAnimation 컴포넌트가 없습니다.");
            return;
        }

        spriteSheetAnimation.SetAnimation(animationState.ToString());

        // 애니메이션 변경 시 타이머 리셋
        if (enableMovementAnimation)
        {
            if (animationState == BattleActorAnimation.Walk)
            {
                wasMoving = true;
                idleTimer = 0f;
            }
            else if (animationState == BattleActorAnimation.Idle)
            {
                wasMoving = false;
            }
        }
    }

    private void StartUpdateState()
    {
        if (!actorBehaviour.IsPlay)
            return;

        if (actorBehaviour.IsEnded)
            SetBehaviour();

        actorBehaviour.Start();

        if (actorBehaviour.CheckComplete())
            actorBehaviour.End();
        else
            actorBehaviour.Process(Time.deltaTime);
    }


    public async UniTask ShowCaption(string text, float duration)
    {
        if (captionLoader == null)
            return;

        captionLoader.SafeSetActive(true);

        BattleActorCaptionSupport caption = await captionLoader.LoadAsync<BattleActorCaptionSupport>();

        caption.SetText(text);

        caption.SafeSetActive(true);

        // WaitForSeconds 대신 UniTask.Delay 사용 (밀리초 단위)
        await UniTask.Delay(TimeSpan.FromSeconds(duration));



        captionLoader.SafeSetActive(true);
    }


    // 클래스 상단에 속도 배율 변수 추가
    public float attackMoveSpeedMultiplier = 2.5f;  // 공격 이동 속도 배율
    public float returnMoveSpeedMultiplier = 5f;  // 복귀 이동 속도 배율

    // 특정 위치로 이동
    public async UniTask MoveToPosition(Vector3 targetPosition, bool returnToOriginalDirection = false)
    {
        if (isMoving) return;

        isMoving = true;

        // 움직임 애니메이션 시스템 일시 정지
        wasMoving = false;
        idleTimer = 0f;


        SetState(BattleActorState.MoveToAttackPoint);
        SetAnimation(BattleActorAnimation.Walk);

        // 방향 설정
        if (targetPosition.x > transform.position.x)
        {
            spriteSheetAnimation.SetFlip(true, false); // 오른쪽 보기
        }
        else
        {
            spriteSheetAnimation.SetFlip(false, false); // 왼쪽 보기
        }
        SetLookDirection();

        float moveToSpeed = moveSpeed * attackMoveSpeedMultiplier;
        if ( returnToOriginalDirection == true )
        {
            moveToSpeed = moveSpeed * returnMoveSpeedMultiplier;
        }


        // 이동 처리
        while (Vector3.Distance(transform.position, targetPosition) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                moveToSpeed * Time.deltaTime
            );
            await UniTask.Yield();
        }

        transform.position = targetPosition;

        // 원위치로 돌아갈 때는 원래 방향으로 복구
        if (returnToOriginalDirection)
        {
            spriteSheetAnimation.SetFlip(originalFlipX, false);
        }

        SetAnimation(BattleActorAnimation.Idle);
        isMoving = false;

        // 위치 업데이트 (움직임 감지 초기화)
        lastFramePosition = transform.position;
    }

}