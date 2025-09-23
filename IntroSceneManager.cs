//=========================================================================================================
//using System;
//using System.Collections;
//using System.Collections.Generic;
using UnityEngine;
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

[DisallowMultipleComponent]
public class IntroSceneManager : BaseSceneManager
{
    private static IntroSceneManager instance;
    public static IntroSceneManager Instance
    {
        get
        {
            if (instance == null)
            {
                string className = typeof(IntroSceneManager).Name;
                GameObject manager = GameObject.Find(className);
                instance = manager.GetComponent<IntroSceneManager>();

                if (instance == null)
                    instance = new IntroSceneManager();
            }

            return instance;
        }
    }

    private IntroSceneManager() { }
    //=================================================================
    // 불필요한 부분은 지우고 사용하시면 됩니다.
    //=================================================================
    //============================================================
    //=========    Coding rule에 맞춰서 작업 바랍니다.   =========
    //========= Coding rule region은 절대 지우지 마세요. =========
    //=========    문제 시 '김철옥'에게 문의 바랍니다.   =========
    //============================================================

    #region Coding rule : Property
    #endregion Coding rule : Property

    #region Coding rule : Value
    #endregion Coding rule : Value

    #region Coding rule : Function
    #endregion Coding rule : Function
}
