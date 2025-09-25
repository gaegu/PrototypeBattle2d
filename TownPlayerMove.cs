using UnityEngine;
using UnityEngine.AI;

public class TownPlayerMove : MonoBehaviour
{
    private enum AnimState
    {
        Idle,
        Walk,
        Blink,
    }

    private ITownObject owner;
    private NavMeshAgent navMeshAgent;
    private SpriteSheetAnimationSupport spriteAnimation;
    private PlayerState playerState;
    private AnimState currentAnimState = AnimState.Idle;

    [SerializeField]
    private float maxMoveSpeed = 4.0f;

    [SerializeField]
    private float sprintSpeedMultiplier = 1.25f;

    private Vector3 currentVelocity;

    public void SetOwner()
    {
        owner = gameObject.GetComponentInChildren<ITownObject>(); // TownMyCharacter가 ITownObject를 구현한다고 가정
        TryCacheComponents();
        InitializePlayerState();
    }

    private void Awake()
    {
        SetOwner();
    }

    private void TryCacheComponents()
    {
        if (navMeshAgent == null)
            navMeshAgent = GetComponentInChildren<NavMeshAgent>();
        if (spriteAnimation == null)
            spriteAnimation = GetComponentInChildren<SpriteSheetAnimationSupport>();
        if (spriteAnimation)
            spriteAnimation.LoadAnimationsAsync();
    }

    private void InitializePlayerState()
    {
        if (owner != null && playerState == null)
        {
            playerState = new PlayerState();
            playerState.Initialize(owner);
        }
    }

    private void Update()
    {
        // PlayerState 업데이트
        if (playerState != null)
        {
            playerState.Update();
        }

        if (spriteAnimation == null)
            TryCacheComponents();

        // PathFind가 활성화되어 있으면 조이스틱 입력 무시
        if (owner.PathfindData != null && owner.PathfindData.IsPathfind)
        {
            // NavMeshAgent가 경로찾기 중일 때는 조이스틱 입력을 차단
            if (navMeshAgent != null)
            {
                navMeshAgent.isStopped = false;
                SetAnimState(AnimState.Walk);
            }
            return;
        }

        MoveData moveData = owner.MoveData;
        if (moveData == null)
        {
            Debug.LogWarning("TownPlayerMove: MoveData is null!");
            return;
        }

        // 움직임 입력이 없거나 움직임이 잠겨있으면 정지
        if (!moveData.IsMoveInput || moveData.IsMoveLock)
        {
            StopMovement();
            return;
        }

        // 조이스틱 입력 방향 가져오기
        Vector3 inputDirection = moveData.InputDirection;
        inputDirection.y = 0f;

        // 입력이 너무 작으면 정지
        if (inputDirection.sqrMagnitude < 0.0001f)
        {
            StopMovement();
            return;
        }

        // 카메라 회전을 기준으로 월드 방향 계산
        Quaternion cameraRotation = moveData.CameraRotation;
        Vector3 worldDirection = cameraRotation * inputDirection;
        worldDirection.y = 0f;
        worldDirection.Normalize();

        // 속도 계산
        float speedFactor = Mathf.Clamp01(moveData.MoveThreshoid);
        float sprintMultiplier = moveData.IsSprint ? sprintSpeedMultiplier : 1.0f;

        float targetSpeed = maxMoveSpeed * speedFactor * sprintMultiplier;

        // inputDirection의 크기를 고려한 속도 조정
        float inputMagnitude = Mathf.Clamp01(inputDirection.magnitude);
        targetSpeed *= inputMagnitude;

        Vector3 targetVelocity = worldDirection * targetSpeed;

        if( spriteAnimation != null )
            spriteAnimation.SetFlip(inputDirection.x > 0 ? true : false, false);

        // 위치 업데이트
        if (currentVelocity.sqrMagnitude > 0.0001f)
        {
            transform.position += currentVelocity * Time.deltaTime;
        }

        // NavMeshAgent 설정 (조이스틱 입력 중에는 비활성화)
        if (navMeshAgent != null)
        {
            navMeshAgent.isStopped = true;
            navMeshAgent.velocity = Vector3.zero;
        }

        SetAnimState(AnimState.Walk);
    }

    private void SetAnimState(AnimState state)
    {
        if (currentAnimState == state)
            return;

        currentAnimState = state;
        SetAnimation(currentAnimState);
    }

    private void SetAnimation(AnimState state)
    {
        if( spriteAnimation != null )
            spriteAnimation.SetAnimation(state.ToString());
    }

    private void StopMovement()
    {
        if (currentVelocity.sqrMagnitude > 0.0001f)
        {
            transform.position += currentVelocity * Time.deltaTime;
        }

        // NavMeshAgent 재활성화 준비
        if (navMeshAgent != null && currentVelocity.sqrMagnitude < 0.01f)
        {
            navMeshAgent.isStopped = false;
        }

        SetAnimState(AnimState.Idle);
    }
}