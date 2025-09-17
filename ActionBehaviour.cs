using DG.Tweening;
using UnityEngine;
using UnityEngine.Playables;
using Cysharp.Threading.Tasks;

namespace IronJade.ResourcesAddressable._2DRenewal.PortraitNew
{
    public class ActionBehaviour : PlayableBehaviour
    {
        public ActionClipData actionData;
        public ExposedReference<GameObject> bodyObject;   // 움직일 본체
        public ExposedReference<GameObject> targetObject; // 도달할 목표
        
        // 커브 설정 (ActionClip에서 전달받음)
        public bool useCustomEase = false;
        public DG.Tweening.Ease easeType = DG.Tweening.Ease.Linear;
        public AnimationCurve customCurve = AnimationCurve.Linear(0, 0, 1, 1);
        public float moveSpeed = 1f;
        public bool useSpeedInsteadOfDuration = false;
        public bool useCustomCurve = false;  // 커스텀 커브 사용 여부
        public bool useOffset = false; // 오프셋 사용 여부
        public float offsetDistance = 0f; // 오프셋 거리
        
        private bool isFirstFrame = true;
        private Transform bodyTransform;
        private Transform targetTransform;
        
        // 이동 관련 변수들
        private Vector3 startPosition;
        private Vector3 endPosition;
        private bool isMoving = false;
        private float moveStartTime = 0f;
        private float moveDuration = 0f;
        private float moveDistance = 0f;
        
        // 애니메이션 관련 변수들
        private SpriteSheetAnimationSupport spriteSheetAnimation;
        private bool wasMoving = false;
        private bool isAnimationLoaded = false;
        private bool walkAnimationStarted = false;  // Walk 애니메이션 시작 여부 추적

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            isFirstFrame = true;
            isMoving = false;
            walkAnimationStarted = false;  // 애니메이션 시작 상태 초기화
            moveDuration = (float)playable.GetDuration();
        }

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (isFirstFrame)
            {
                var resolver = playable.GetGraph().GetResolver();
                GameObject resolvedBody = bodyObject.Resolve(resolver);
                GameObject resolvedTarget = targetObject.Resolve(resolver);

                bodyTransform = resolvedBody != null ? resolvedBody.transform : null;
                targetTransform = resolvedTarget != null ? resolvedTarget.transform : null;

                // SpriteSheetAnimationSupport 컴포넌트 찾기
                if (resolvedBody != null)
                {
                    // CharacterBody 하위의 캐릭터 오브젝트에서 SpriteSheetAnimationSupport 찾기
                    spriteSheetAnimation = resolvedBody.GetComponentInChildren<SpriteSheetAnimationSupport>();
                    
                    // 애니메이션이 로드되지 않았다면 로드 시도
                    if (spriteSheetAnimation != null && !spriteSheetAnimation.IsLoaded)
                    {
                        LoadAnimationsAsync().Forget();
                    }
                    else
                    {
                        isAnimationLoaded = true;
                    }
                }

                if (actionData.actionType == ActionType.Move && bodyTransform != null && targetTransform != null)
                {
                    InitializeMoveAction(playable);
                }

                isFirstFrame = false;
            }

            if (isMoving && actionData.actionType == ActionType.Move)
            {
                Debug.Log($"[ActionBehaviour] ProcessFrame - calling UpdateMoveAction");
                UpdateMoveAction(playable);
            }
            else
            {
                Debug.Log($"[ActionBehaviour] ProcessFrame - NOT calling UpdateMoveAction - isMoving: {isMoving}, actionType: {actionData.actionType}");
            }
        }

        private void InitializeMoveAction(Playable playable)
        {
            startPosition = bodyTransform.position;
            
            // 목표 위치 설정 (오프셋은 이동 중에 거리 체크로 처리)
            endPosition = targetTransform.position;
            
            // 디버그 로그 추가
            Debug.Log($"[ActionBehaviour] InitializeMoveAction - useOffset: {useOffset}, offsetDistance: {offsetDistance:F2}");
            Debug.Log($"[ActionBehaviour] Start: {startPosition}, End: {endPosition}");
            
            moveDistance = Vector3.Distance(startPosition, endPosition);
            
            // 이동 방향에 따른 스케일 조정
            if (startPosition.x > endPosition.x)
            {
                bodyTransform.DOScaleX(-1, 0.1f);
            }
            
            // 이동 시간 계산
            if (useSpeedInsteadOfDuration)
            {
                moveDuration = moveDistance / moveSpeed;
            }
            else
            {
                moveDuration = (float)playable.GetDuration();
            }
            
            isMoving = true;
            moveStartTime = 0f;
            
            // Walk 애니메이션 시작 (애니메이션이 로드된 후에만, 한 번만)
            if (spriteSheetAnimation != null && isAnimationLoaded && !walkAnimationStarted)
            {
                TrySetAnimation("Walk");
                wasMoving = true;
                walkAnimationStarted = true;  // Walk 애니메이션 시작 상태로 설정
            }
            else if (spriteSheetAnimation != null && !walkAnimationStarted)
            {
                // 애니메이션 로딩 완료 후 Walk 애니메이션 시작
                LoadAnimationsAndStartWalk().Forget();
            }
        }

        private async UniTaskVoid LoadAnimationsAsync()
        {
            if (spriteSheetAnimation == null) return;
            
            try
            {
                await spriteSheetAnimation.LoadAnimationsAsync();
                isAnimationLoaded = true;
                
                // 이동 중이고 Walk 애니메이션이 아직 시작되지 않았다면 시작
                if (isMoving && !walkAnimationStarted)
                {
                    TrySetAnimation("Walk");
                    wasMoving = true;
                    walkAnimationStarted = true;  // Walk 애니메이션 시작 상태로 설정
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ActionBehaviour] Failed to load animations: {e.Message}");
            }
        }

        private async UniTaskVoid LoadAnimationsAndStartWalk()
        {
            if (spriteSheetAnimation == null) return;
            
            try
            {
                await spriteSheetAnimation.LoadAnimationsAsync();
                isAnimationLoaded = true;
                
                if (isMoving && !walkAnimationStarted)
                {
                    TrySetAnimation("Walk");
                    wasMoving = true;
                    walkAnimationStarted = true;  // Walk 애니메이션 시작 상태로 설정
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ActionBehaviour] Failed to load animations for Walk: {e.Message}");
            }
        }

        private void TrySetAnimation(string animationKey)
        {
            if (spriteSheetAnimation == null || !isAnimationLoaded) return;
            
            try
            {
                spriteSheetAnimation.SetAnimation(animationKey, true);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ActionBehaviour] Failed to set animation {animationKey}: {e.Message}");
            }
        }

        private void UpdateMoveAction(Playable playable)
        {
            Debug.Log($"[ActionBehaviour] UpdateMoveAction called - isMoving: {isMoving}, bodyTransform: {bodyTransform != null}, targetTransform: {targetTransform != null}");
            
            if (!isMoving || bodyTransform == null || targetTransform == null) return;

            // 오프셋 체크: 목표와의 거리가 설정된 오프셋 거리보다 작으면 멈춤 (Z값 제외)
            if (useOffset)
            {
                // X, Y 좌표만으로 거리 계산 (Z값 제외)
                Vector2 currentPos2D = new Vector2(bodyTransform.position.x, bodyTransform.position.y);
                Vector2 targetPos2D = new Vector2(targetTransform.position.x, targetTransform.position.y);
                float currentDistance = Vector2.Distance(currentPos2D, targetPos2D);
                
                // 디버그 로그 추가
                Debug.Log($"[ActionBehaviour] Offset Check - useOffset: {useOffset}, currentDistance: {currentDistance:F2}, offsetDistance: {offsetDistance:F2}");
                
                if (currentDistance <= offsetDistance)
                {
                    Debug.Log($"[ActionBehaviour] Stopping due to offset distance reached!");
                    isMoving = false;
                    
                    // Walk 애니메이션 정지 (기본 애니메이션으로 복귀) - 한 번만
                    if (spriteSheetAnimation != null && wasMoving && walkAnimationStarted)
                    {
                        TrySetAnimation("Idle");
                        wasMoving = false;
                        walkAnimationStarted = false;  // Walk 애니메이션 시작 상태 초기화
                    }
                    return;
                }
            }

            float t = (float)(playable.GetTime() / moveDuration);
            
            // 커브 적용
            float curveValue = t;
            if (useCustomEase)
            {
                if (useCustomCurve)
                {
                    // 커스텀 커브 사용
                    curveValue = customCurve.Evaluate(t);
                }
                else
                {
                    // DOTween Ease 사용
                    curveValue = DG.Tweening.DOVirtual.EasedValue(0f, 1f, t, easeType);
                }
            }
            
            // 위치 업데이트
            bodyTransform.position = Vector3.Lerp(startPosition, endPosition, curveValue);

            if (t >= 1f)
            {
                isMoving = false;
                bodyTransform.position = endPosition;
                
                // Walk 애니메이션 정지 (기본 애니메이션으로 복귀) - 한 번만
                if (spriteSheetAnimation != null && wasMoving && walkAnimationStarted)
                {
                    TrySetAnimation("Idle");
                    wasMoving = false;
                    walkAnimationStarted = false;  // Walk 애니메이션 시작 상태 초기화
                }
            }
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            // Walk 애니메이션 정지 (기본 애니메이션으로 복귀) - 한 번만
            if (spriteSheetAnimation != null && wasMoving && walkAnimationStarted)
            {
                TrySetAnimation("Idle");
                wasMoving = false;
                walkAnimationStarted = false;  // Walk 애니메이션 시작 상태 초기화
            }
            
            isMoving = false;
        }
    }
}
