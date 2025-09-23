using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BecameVisibleTester : MonoBehaviour
{
    // Start is called before the first frame update

    public Animator parentAnimator; 

    private void OnBecameVisible()
    {
       IronJade.Debug.LogError("뷰잉!");

        if( parentAnimator != null )
        {
            parentAnimator.applyRootMotion = true;
        }
    }

    private void OnBecameInvisible()
    {
       IronJade.Debug.LogError("컬링!");

        if (parentAnimator != null)
        {
            parentAnimator.applyRootMotion = false;
        }


    }

    private void OnWillRenderObject()
    {
        //매프레임마다 상태ㅔ 체크
        //IronJade.Debug.LogError("" + Camera.current.name );
    }

}
