using UnityEngine;
using System;
using Cysharp.Threading.Tasks;

/// <summary>
/// 프로젝타일 이펙트에 붙이는 컴포넌트
/// 더 정교한 제어가 필요한 경우 사용
/// </summary>
public class BattleProjectile : MonoBehaviour
{
    [Header("프로젝타일 설정")]
    public ProjectileTrajectory trajectoryType = ProjectileTrajectory.Straight;
    public float speed = 10f;
    public float homingStrength = 5f;
    public AnimationCurve heightCurve;
    public float spiralRadius = 1f;
    public float spiralSpeed = 10f;

    [Header("충돌 설정")]
    public float hitRadius = 0.5f;
    public LayerMask targetLayer;
    public bool destroyOnHit = true;
    public bool penetrating = false;  // 관통 여부
    public int maxPenetration = 1;    // 최대 관통 수

    [Header("시각 효과")]
    public bool rotateTowardsDirection = true;
    public TrailRenderer trailRenderer;
    public ParticleSystem particleSystem;

    // 내부 변수
    private Transform target;
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private Vector3 currentVelocity;
    private float trajectoryProgress = 0f;
    private int penetrationCount = 0;
    private bool isActive = false;

    // 이벤트
    public Action<GameObject> OnHit;
    public Action OnDestroy;

    /// <summary>
    /// 프로젝타일 초기화
    /// </summary>
    public void Initialize(Transform targetTransform, float projectileSpeed = -1)
    {
        target = targetTransform;
        startPosition = transform.position;

        if (target != null)
        {
            targetPosition = target.position;
            Vector3 direction = (targetPosition - startPosition).normalized;
            currentVelocity = direction * (projectileSpeed > 0 ? projectileSpeed : speed);
        }

        trajectoryProgress = 0f;
        penetrationCount = 0;
        isActive = true;

        // 트레일 초기화
        if (trailRenderer != null)
        {
            trailRenderer.Clear();
        }
    }

    private void Update()
    {
        if (!isActive) return;

        // 타겟이 사라졌으면 비활성화
        if (target == null)
        {
            if (trajectoryType == ProjectileTrajectory.Homing)
            {
                // 유도 미사일은 타겟이 없으면 직진
                trajectoryType = ProjectileTrajectory.Straight;
            }
            else if (trajectoryType != ProjectileTrajectory.Straight)
            {
                DestroyProjectile();
                return;
            }
        }
        else
        {
            // 타겟 위치 업데이트
            targetPosition = target.position;
        }

        // 궤적에 따른 이동
        UpdateMovement(Time.deltaTime);

        // 충돌 체크
        CheckCollision();

        // 화면 밖으로 나가면 제거
        if (IsOutOfBounds())
        {
            DestroyProjectile();
        }
    }

    /// <summary>
    /// 이동 업데이트
    /// </summary>
    private void UpdateMovement(float deltaTime)
    {
        Vector3 oldPosition = transform.position;

        switch (trajectoryType)
        {
            case ProjectileTrajectory.Straight:
                UpdateStraight(deltaTime);
                break;

            case ProjectileTrajectory.Arc:
                UpdateArc(deltaTime);
                break;

            case ProjectileTrajectory.Homing:
                UpdateHoming(deltaTime);
                break;

            case ProjectileTrajectory.Spiral:
                UpdateSpiral(deltaTime);
                break;

            case ProjectileTrajectory.Bounce:
                UpdateBounce(deltaTime);
                break;

            case ProjectileTrajectory.Custom:
                UpdateCustom(deltaTime);
                break;
        }

        // 진행 방향으로 회전
        if (rotateTowardsDirection && transform.position != oldPosition)
        {
            Vector3 direction = transform.position - oldPosition;
            if (direction != Vector3.zero)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }
        }
    }

    private void UpdateStraight(float deltaTime)
    {
        transform.position += currentVelocity * deltaTime;
    }

    private void UpdateArc(float deltaTime)
    {
        if (target == null) return;

        float distance = Vector3.Distance(startPosition, targetPosition);
        trajectoryProgress += deltaTime * speed / distance;
        trajectoryProgress = Mathf.Clamp01(trajectoryProgress);

        Vector3 currentPos = Vector3.Lerp(startPosition, targetPosition, trajectoryProgress);

        // 높이 추가
        if (heightCurve != null)
        {
            float height = heightCurve.Evaluate(trajectoryProgress) * distance * 0.5f;
            currentPos.y += height;
        }

        transform.position = currentPos;
    }

    private void UpdateHoming(float deltaTime)
    {
        if (target != null)
        {
            Vector3 direction = (targetPosition - transform.position).normalized;
            currentVelocity = Vector3.Lerp(
                currentVelocity.normalized,
                direction,
                homingStrength * deltaTime
            ).normalized * speed;
        }

        transform.position += currentVelocity * deltaTime;
    }

    private void UpdateSpiral(float deltaTime)
    {
        trajectoryProgress += deltaTime;

        Vector3 direction = target != null ?
            (targetPosition - transform.position).normalized :
            currentVelocity.normalized;

        Vector3 forward = direction * speed * deltaTime;

        // 나선 회전
        Vector3 perpendicular = new Vector3(-direction.y, direction.x, 0);
        float spiralX = Mathf.Sin(trajectoryProgress * spiralSpeed) * spiralRadius;
        float spiralY = Mathf.Cos(trajectoryProgress * spiralSpeed) * spiralRadius;
        Vector3 spiral = perpendicular * spiralX + Vector3.up * spiralY;

        transform.position += forward + spiral * deltaTime;
    }

    private void UpdateBounce(float deltaTime)
    {
        transform.position += currentVelocity * deltaTime;

        // 화면 경계 체크 및 반사
        Vector3 viewportPos = Camera.main.WorldToViewportPoint(transform.position);

        if (viewportPos.x <= 0 || viewportPos.x >= 1)
        {
            currentVelocity.x *= -1;
            transform.position = Camera.main.ViewportToWorldPoint(
                new Vector3(Mathf.Clamp01(viewportPos.x), viewportPos.y, viewportPos.z)
            );
        }

        if (viewportPos.y <= 0 || viewportPos.y >= 1)
        {
            currentVelocity.y *= -1;
            transform.position = Camera.main.ViewportToWorldPoint(
                new Vector3(viewportPos.x, Mathf.Clamp01(viewportPos.y), viewportPos.z)
            );
        }
    }

    private void UpdateCustom(float deltaTime)
    {
        // 커스텀 궤적은 상속받아서 구현
        OnUpdateCustomTrajectory(deltaTime);
    }

    /// <summary>
    /// 커스텀 궤적 업데이트 (오버라이드용)
    /// </summary>
    protected virtual void OnUpdateCustomTrajectory(float deltaTime)
    {
        // 상속받아서 구현
        UpdateStraight(deltaTime);
    }

    /// <summary>
    /// 충돌 체크
    /// </summary>
    private void CheckCollision()
    {
        // 구체 충돌 체크
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, hitRadius, targetLayer);

        foreach (var hit in hits)
        {
            // 자기 자신은 제외
            if (hit.transform == transform) continue;

            // 타겟 확인
            BattleActor actor = hit.GetComponentInParent<BattleActor>();
            if (actor != null && !actor.IsDead)
            {
                // 피격 이벤트
                OnHit?.Invoke(hit.gameObject);

                if (penetrating)
                {
                    penetrationCount++;
                    if (penetrationCount >= maxPenetration)
                    {
                        DestroyProjectile();
                        return;
                    }
                }
                else if (destroyOnHit)
                {
                    DestroyProjectile();
                    return;
                }
            }
        }

        // 3D 충돌도 체크 (필요한 경우)
        Collider[] hits3D = Physics.OverlapSphere(transform.position, hitRadius, targetLayer);
        foreach (var hit in hits3D)
        {
            if (hit.transform == transform) continue;

            BattleActor actor = hit.GetComponentInParent<BattleActor>();
            if (actor != null && !actor.IsDead)
            {
                OnHit?.Invoke(hit.gameObject);

                if (!penetrating && destroyOnHit)
                {
                    DestroyProjectile();
                    return;
                }
            }
        }
    }

    /// <summary>
    /// 화면 밖 체크
    /// </summary>
    private bool IsOutOfBounds()
    {
        Vector3 viewportPos = Camera.main.WorldToViewportPoint(transform.position);

        // 화면 밖으로 충분히 나갔는지 체크 (여유 마진 0.2)
        return viewportPos.x < -0.2f || viewportPos.x > 1.2f ||
               viewportPos.y < -0.2f || viewportPos.y > 1.2f;
    }

    /// <summary>
    /// 프로젝타일 파괴
    /// </summary>
    private void DestroyProjectile()
    {
        isActive = false;
        OnDestroy?.Invoke();

        // 파티클 중지
        if (particleSystem != null)
        {
            particleSystem.Stop();
        }

        // 트레일이 끝날 때까지 대기 후 제거
        if (trailRenderer != null && trailRenderer.time > 0)
        {
            DestroyAfterTrail().Forget();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private async UniTaskVoid DestroyAfterTrail()
    {
        await UniTask.Delay(TimeSpan.FromSeconds(trailRenderer.time));
        gameObject.SetActive(false);
    }

    private void OnDrawGizmosSelected()
    {
        // 충돌 범위 표시
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, hitRadius);

        // 궤적 미리보기
        if (target != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, target.position);
        }
    }
}