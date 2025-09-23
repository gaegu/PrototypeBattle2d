//=========================================================================================================
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
using Cysharp.Threading.Tasks;
using UnityEngine;
//=========================================================================================================

/// <summary>
/// ExtraStory 관련 메인 Manager입니다.
/// 전체 State 컨트롤, Flow 전환, 저장 데이터 관리 및 하위 매니저에 적용 관련 기능 처리합니다.
/// </summary>
public class ExtraStoryManager : MonoBehaviour
{	
    private static ExtraStoryManager instance;
    public static ExtraStoryManager Instance
    {
        get
        {
            if (instance == null)
                instance = new ExtraStoryManager();

            return instance;
        }
    }

    private ExtraStoryManager() { }
	
    //=================================================================
    // 불필요한 부분은 지우고 사용하시면 됩니다.
    //=================================================================
    //============================================================
    //=========    Coding rule에 맞춰서 작업 바랍니다.   =========
    //========= Coding rule region은 절대 지우지 마세요. =========
    //=========    문제 시 '김철옥'에게 문의 바랍니다.   =========
    //============================================================

    #region Coding rule : Property
    public ExtraStoryState State { get; private set; }

    #endregion Coding rule : Property
    
    #region Coding rule : Value
    #endregion Coding rule : Value
    
    #region Coding rule : Function
    public void SetState(ExtraStoryState state)
    {
        State = state;
    }

    public void OnClickExit()
    {
       ExitExtraStory().Forget();
    }

    private async UniTask ExitExtraStory()
    {
        //저장
        await RequestSaveExtraStory();

        Time.timeScale = 1f;
        await TransitionManager.In(TransitionType.Rotation);
        await FlowManager.Instance.ChangeFlow(FlowType.TownFlow, new TownFlowModel(), isStack: false);
    }

    private async UniTask RequestSaveExtraStory()
    {

    }

    private async UniTask RequestLoadExtraStory()
    {


        //===================================================
        //추후 서버작업 완료 후 서버에서 받은 데이터 로드하는 부분으로 옮기기
        ExtraStoryQuestUnitModel questModel = new ExtraStoryQuestUnitModel();
        //===================================================
    }
    #endregion Coding rule : Function
}
