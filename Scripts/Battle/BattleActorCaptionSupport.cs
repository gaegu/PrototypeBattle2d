//=========================================================================================================
#pragma warning disable CS1998
using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using TMPro;                        // TextMeshPro 관련 클래스 모음 ( UnityEngine.UI.Text 대신 이걸 사용 )
//=========================================================================================================

public class BattleActorCaptionSupport : MonoBehaviour
{
    [SerializeField]
    private TextMeshPro text = null;

    public void SetText(string text)
    {
        this.text.SafeSetText(text);
    }

}
