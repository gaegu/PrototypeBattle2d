using Cysharp.Threading.Tasks;
using UnityEngine;



public partial class BattleActor : MonoBehaviour
{

    // SetState 메서드 수정
    public void SetState(BattleActorState onState)
    {
        // 이전 상태 종료
        if (actorBehaviour.IsPlay && !actorBehaviour.IsEnded)
        {
            actorBehaviour.End();
        }

        PrevState = State;
        State = onState;

        // 새로운 상태 설정
        SetupStateBehaviour(onState);
    }

    private void UpdateStateBehaviour()
    {
        if (!actorBehaviour.IsPlay)
            return;

        if (!actorBehaviour.IsStarted)
        {
            actorBehaviour.Start();
        }

        if (actorBehaviour.CheckComplete())
        {
            if (!actorBehaviour.IsEnded)
            {
                actorBehaviour.End();
            }
        }
        else
        {
            actorBehaviour.Process(Time.deltaTime);
        }
    }



    // 새로운 메서드 추가 - 상태별 Behaviour 설정
    private void SetupStateBehaviour(BattleActorState state)
    {
        actorBehaviour.Reset();

        switch (state)
        {
            case BattleActorState.Idle:
                SetupIdleBehaviour();
                break;

            case BattleActorState.MoveToAttackPoint:
                SetupMoveToAttackBehaviour();
                break;

            case BattleActorState.ReturnToStartPoint:
                SetupReturnToStartPointBehaviour();
                break;


            case BattleActorState.Attack:
                SetupAttackBehaviour();
                break;

            case BattleActorState.Hit:
                SetupHitBehaviour();
                break;

            case BattleActorState.Skill:
                SetupSkillBehaviour();
                break;

            case BattleActorState.Dead:
                SetupDeadBehaviour();
                break;

            default:
                actorBehaviour.SetPlay(false);
                return;
        }

        actorBehaviour.SetPlay(true);
    }

    // 각 상태별 Behaviour 설정 메서드
    private void SetupIdleBehaviour()
    {
        float idleTime = UnityEngine.Random.Range(waitIdleMinTime, waitIdleMaxTime);
        actorBehaviour.SetEndTime(idleTime);

        actorBehaviour.SetEventStart(() => {
            SetAnimation(BattleActorAnimation.Idle);
        });

        actorBehaviour.SetEventProcess((parameters) => {
            // Idle 중 처리 (필요시)
        });

        actorBehaviour.SetEventEnd(() => {
            Debug.Log($"[BattleActor] {name} finished Idle state");
        });
    }

    private void SetupAttackBehaviour()
    {
        actorBehaviour.SetEndTime(2.0f); // 공격 애니메이션 시간

        actorBehaviour.SetEventStart(() => {
            SetAnimation(BattleActorAnimation.Attack);
            Debug.Log($"[BattleActor] {name} started Attack");
        });

        actorBehaviour.SetEventProcess((parameters) => {
          
        });

        actorBehaviour.SetEventEnd(() => {
            isCompletedAttack = false;
            SetState(BattleActorState.Idle);
        });
    }

    private void SetupSkillBehaviour()
    {
        actorBehaviour.SetEventStart(() => {
          //  Debug.LogError($"[BattleActor] {name} SetupSkillBehaviour!!!! ");
        });

        actorBehaviour.SetEventEnd(() => {
            SetState(BattleActorState.Idle);
        });
    }

    private void SetupHitBehaviour()
    {
        actorBehaviour.SetEventStart(() => {
            //SetAnimation(BattleActorAnimation.Hit);
            PlayHitColorEffect();
        });

        actorBehaviour.SetEventEnd(() => {
            SetState(BattleActorState.Idle);
        });
    }


    private void SetupDeadBehaviour()
    {
        actorBehaviour.SetEndTime(0); // 무한

        actorBehaviour.SetEventStart(() => {
            SetAnimation(BattleActorAnimation.Dead);
            Debug.Log($"[BattleActor] {name} died");
        });

        actorBehaviour.SetEventCheckComplete((parameters) => {
            return false; // 죽음 상태는 계속 유지
        });
    }

    // 이동 관련 필드 추가
    private Vector3 moveTargetPosition;
    private bool moveReturnToOriginal;
    private bool hasMoveCompleted;


    // MoveToAttackPoint StateBehaviour 설정
    private void SetupMoveToAttackBehaviour()
    {
        hasMoveCompleted = false;
        float moveToSpeed = moveSpeed * attackMoveSpeedMultiplier;

        actorBehaviour.SetEventStart(() => {
            isMoving = true;

            SetAnimation(BattleActorAnimation.Walk);

            // 방향 설정
            if (moveTargetPosition.x > transform.position.x)
            {
                spriteSheetAnimation.SetFlip(true, false); // 오른쪽
            }
            else
            {
                spriteSheetAnimation.SetFlip(false, false); // 왼쪽
            }
            SetLookDirection();
        });

        actorBehaviour.SetEventProcess((parameters) => {
            if (!hasMoveCompleted)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    moveTargetPosition,
                    moveToSpeed * Time.deltaTime
                );

                if (Vector3.Distance(transform.position, moveTargetPosition) <= 0.1f)
                {
                    transform.position = moveTargetPosition;
                    hasMoveCompleted = true;
                }
            }
        });

        actorBehaviour.SetEventCheckComplete((parameters) => {
            return hasMoveCompleted;
        });

        actorBehaviour.SetEventEnd(() => {
            SetAnimation(BattleActorAnimation.Idle);
            isMoving = false;
        });
    }

    // ReturnToStartPoint StateBehaviour 설정
    private void SetupReturnToStartPointBehaviour()
    {
        hasMoveCompleted = false;
        float returnSpeed = moveSpeed * returnMoveSpeedMultiplier;

        actorBehaviour.SetEventStart(() => {
            isMoving = true;

            SetAnimation(BattleActorAnimation.Walk);

            // 복귀 방향 설정
            if (moveTargetPosition.x > transform.position.x)
            {
                spriteSheetAnimation.SetFlip(true, false);
            }
            else
            {
                spriteSheetAnimation.SetFlip(false, false);
            }
            SetLookDirection();
        });

        actorBehaviour.SetEventProcess((parameters) => {
            if (!hasMoveCompleted)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    moveTargetPosition,
                    returnSpeed * Time.deltaTime
                );

                if (Vector3.Distance(transform.position, moveTargetPosition) <= 0.1f)
                {
                    transform.position = moveTargetPosition;
                    hasMoveCompleted = true;
                }
            }
        });

        actorBehaviour.SetEventCheckComplete((parameters) => {
            return hasMoveCompleted;
        });

        actorBehaviour.SetEventEnd(() => {
            // 원래 방향으로 복구
            if (moveReturnToOriginal)
            {
                spriteSheetAnimation.SetFlip(originalFlipX, false);
            }

            SetAnimation(BattleActorAnimation.Idle);
            isMoving = false;

            // Idle 상태로 전환
            SetState(BattleActorState.Idle);
        });
    }

    // 클래스 상단에 속도 배율 변수 추가
    public float attackMoveSpeedMultiplier = 2.5f;  // 공격 이동 속도 배율
    public float returnMoveSpeedMultiplier = 5f;  // 복귀 이동 속도 배율

    // 기존 MoveToPosition 메서드를 StateBehaviour 사용하도록 수정
    public async UniTask MoveToPosition(Vector3 targetPosition, bool returnToOriginalDirection = false)
    {
        if (isMoving) return;

        // 이동 정보 설정
        moveTargetPosition = targetPosition;
        moveReturnToOriginal = returnToOriginalDirection;

        // 상태 설정 (StateBehaviour가 처리)
        if (returnToOriginalDirection)
        {
            SetState(BattleActorState.ReturnToStartPoint);
        }
        else
        {
            SetState(BattleActorState.MoveToAttackPoint);
        }

        // 이동 완료 대기
        await UniTask.WaitUntil(() => !isMoving);
    }

    // MoveToAttackPosition 메서드 추가 (호환성)
    public async UniTask MoveToAttackPosition(Vector3 attackPosition)
    {
        await MoveToPosition(attackPosition, false);
    }

    // ReturnToStartPosition 메서드 추가 (호환성)
    public async UniTask ReturnToStartPosition()
    {
        await MoveToPosition(StartLocalPosition, true);
    }







}