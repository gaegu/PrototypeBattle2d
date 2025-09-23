using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BecameVisibleTester : MonoBehaviour
{
    // Start is called before the first frame update

    public Animator parentAnimator; 

    private void OnBecameVisible()
    {
       IronJade.Debug.LogError("����!");

        if( parentAnimator != null )
        {
            parentAnimator.applyRootMotion = true;
        }
    }

    private void OnBecameInvisible()
    {
       IronJade.Debug.LogError("�ø�!");

        if (parentAnimator != null)
        {
            parentAnimator.applyRootMotion = false;
        }


    }

    private void OnWillRenderObject()
    {
        //�������Ӹ��� ���¤� üũ
        //IronJade.Debug.LogError("" + Camera.current.name );
    }

}
