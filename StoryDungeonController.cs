//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.Table.Data;          // 데이터 테이블
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class StoryDungeonController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.StoryDungeonPopup; } }
    private StoryDungeonPopup View { get { return base.BaseView as StoryDungeonPopup; } }
    protected StoryDungeonPopupModel Model { get; private set; }
    public StoryDungeonController() { Model = GetModel<StoryDungeonPopupModel>(); }
    public StoryDungeonController(BaseModel baseModel) : base(baseModel) { }
    //=========================================================================================================
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
    public override void Enter()
    {
    }

    public override async UniTask LoadingProcess()
    {
        SetButtonEvent();
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();
    }

    public override void Refresh()
    {
    }

    private void SetButtonEvent()
    {
        Model.SetEventStart(OnEventStart);
    }

    private void OnEventStart(int itemIndex)
    {
        int dungeonID = Model.DungeonIDs[itemIndex];

        DungeonTable dungeonTable = TableManager.Instance.GetTable<DungeonTable>();
        DungeonTableData dungeonTableData = dungeonTable.GetDataByID(dungeonID);

        PlayStory(dungeonTableData).Forget();
    }

    private async UniTask PlayStory(DungeonTableData dungeonData)
    {
        // 해당 UI를 사용하지 않고, 스토리 로직이 변경되어서 주석 처리 합니다.(2023.12.03)
        //await StoryCutManager.PlayStory(dungeonData.GetSTART_STORY_SCENE());

        //await StoryCutManager.PlayStory(dungeonData.GetEND_STORY_SCENE());
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Story/StoryDungeonPopup";
    }
    #endregion Coding rule : Function
}