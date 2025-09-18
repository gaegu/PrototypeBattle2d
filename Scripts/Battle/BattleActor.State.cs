using UnityEngine;



public partial class BattleActor : MonoBehaviour
{
    private void SetBehaviour()
    {
        actorBehaviour.Reset();

        switch (State)
        {
            case BattleActorState.Idle:
                {
                    actorBehaviour.SetEventStart(OnEventIdleStart);
                    actorBehaviour.SetEventEnd(OnEventIdleEnd);
                    actorBehaviour.SetPlay(true);
                }
                break;


            case BattleActorState.MoveToAttackPoint:
                {
                    actorBehaviour.SetEventStart(OnEventMoveToAttackPointStart);
                    actorBehaviour.SetEventCheckComplete(OnEventMoveToAttackPointCheckComplete);
                    actorBehaviour.SetEventProcess(OnEventMoveToAttackPointProcess);
                    actorBehaviour.SetPlay(true);
                }
                break;

            case BattleActorState.BackToStartPoint:
 
                {
                    actorBehaviour.SetEventStart(OnEventBackToStartPointStart);
                    actorBehaviour.SetEventCheckComplete(OnEventBackToStartPointComplete);
                    actorBehaviour.SetEventProcess(OnEventBackToStartPointProcess);
                    actorBehaviour.SetPlay(true);
                }
                break;



            case BattleActorState.Attack:
                {
                    actorBehaviour.SetEventStart(OnEventAttackStart);
                    actorBehaviour.SetEventCheckComplete(OnEventAttackCheckComplete);
                    actorBehaviour.SetEventProcess(OnEventAttackProcess);
                    actorBehaviour.SetEventEnd(OnEventAttackEnd);
                    actorBehaviour.SetPlay(true);
                }
                break;

            default:
                {
                    actorBehaviour.SetPlay(false);
                }
                break;
        }
    }


    private void OnEventIdleStart()
    {
        float randomWaitTime = Random.Range(waitIdleMinTime, waitIdleMaxTime);

        actorBehaviour.SetEndTime(randomWaitTime);
    }

    private void OnEventIdleEnd()
    {
        //SetState(GetRandomState(State));
    }


    private void OnEventMoveToAttackPointStart()
    {
       /* Vector3 randomLocalPosition = GetAreaRandomLocalPosition();

        if (initLookDirection == HousingDirection.Left)
            spriteSheetAnimation.SetFlip(randomLocalPosition.x > StartLocalPosition.x, false);
        else
            spriteSheetAnimation.SetFlip(randomLocalPosition.x < StartLocalPosition.x, false);*/


        SetLookDirection();

        //actorBehaviour.SetParameters(randomLocalPosition);
    }


    private bool OnEventMoveToAttackPointCheckComplete(object[] parameters)
    {
        Vector3 endLocalPosition = (Vector3)parameters[0];

        return Vector3.Distance(endLocalPosition, StartLocalPosition) <= float.Epsilon;
    }



    private void OnEventMoveToAttackPointProcess(object[] parameters)
    {
        Vector3 endLocalPosition = (Vector3)parameters[0];

        StartLocalPosition = Vector3.MoveTowards(StartLocalPosition, endLocalPosition, Time.deltaTime * moveSpeed);
    }



    private void OnEventBackToStartPointStart()
    {

    }
    private bool OnEventBackToStartPointComplete(object[] parameters)
    {
        Vector3 endLocalPosition = (Vector3)parameters[0];

        return Vector3.Distance(endLocalPosition, StartLocalPosition) <= float.Epsilon;
    }


    private void OnEventBackToStartPointProcess(object[] parameters)
    {
        Vector3 endLocalPosition = (Vector3)parameters[0];

        StartLocalPosition = Vector3.MoveTowards(StartLocalPosition, endLocalPosition, Time.deltaTime * moveSpeed);
    }


    private void OnEventAttackStart()
    {

    }



    private bool OnEventAttackCheckComplete(object[] parameters)
    {
        Vector3 endLocalPosition = (Vector3)parameters[0];

        return Vector3.Distance(endLocalPosition, StartLocalPosition) <= float.Epsilon;
    }


    private void OnEventAttackProcess(object[] parameters)
    {
        Vector3 endLocalPosition = (Vector3)parameters[0];

        StartLocalPosition = Vector3.MoveTowards(StartLocalPosition, endLocalPosition, Time.deltaTime * moveSpeed);
    }


    private void OnEventAttackEnd()
    {

    }





}