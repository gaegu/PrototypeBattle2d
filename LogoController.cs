//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class LogoController : BaseController
{
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.LogoView; } }
    private LogoView View { get { return base.BaseView as LogoView; } }
    protected LogoViewModel Model { get; private set; }
    public LogoController() { Model = GetModel<LogoViewModel>(); }
    public LogoController(BaseModel baseModel) : base(baseModel) { }

    #region Coding rule : Property
    #endregion Coding rule : Property

    #region Coding rule : Value
    #endregion Coding rule : Value

    #region Coding rule : Function
    public override async UniTask Process()
    {
    }

    public override async UniTask PlayShowAsync()
    {
        await View.LoadAsync();
        await View.ShowAsync();
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Logo/LogoView";
    }
    #endregion Coding rule : Function
}