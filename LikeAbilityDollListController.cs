//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using System.Collections.Generic;
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.Table.Data;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
using UnityEngine;
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class LikeAbilityDollListController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.LikeAbilityDollListView; } }
    public override void SetModel() { SetModel(new LikeAbilityDollListViewModel()); }
    private LikeAbilityDollListView View { get { return base.BaseView as LikeAbilityDollListView; } }
    private LikeAbilityDollListViewModel Model { get { return GetModel<LikeAbilityDollListViewModel>(); } }
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
        Model.SetCurrentLicenseType(LicenseType.None);

        Model.SetEventChangeTab(OnEventChangeTab);
        Model.SetEventDetail(OnEventDetail);

        if (IsTestMode)
            return;

        Model.SetDollLists(PlayerManager.Instance.MyPlayer.User.CharacterModel, GetFavoriteList());
    }

    public override void InitializeTestModel(params object[] parameters)
    {
        Model.SetEventDetail(OnEventDetail);

        CharacterTable characterTable = TableManager.Instance.GetTable<CharacterTable>();

        CharacterModel testModel = new CharacterModel();
        GoodsGeneratorModel testGenerator = new GoodsGeneratorModel();
        
        for (int i = 0; i < characterTable.GetDataTotalCount(); i++)
        {
            CharacterTableData characterData = characterTable.GetDataByIndex(i);

            if ((CharacterType)characterData.GetCHARACTER_TYPE() != CharacterType.PlayerCharacter)
                continue;

            if (characterData.GetLIKEABILITY_USE() == 0)
                continue;

            Character character = testGenerator.CreateCharacterByGoodsValue(characterData.GetID());
            character.SetDoll(System.DateTime.Now, 1, 0);

            testModel.AddGoods(character);
        }

        Model.SetDollLists(testModel, GetFavoriteList());

        base.InitializeTestModel();
    }

    public override async UniTask LoadingProcess()
    {
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();
    }

    public override void Refresh()
    {
        Model.UpdateDollLists(GetFavoriteList());
        View.ShowAsync().Forget();
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "LikeAbility/LikeAbilityDollListView";
    }

    private void OnEventChangeTab(LicenseType licenseType)
    {
        Model.ChangeCategory(licenseType);
        View.ShowAsync().Forget();
    }

    private void OnEventDetail(int characterDataId)
    {
        BaseController controller = UIManager.Instance.GetController(UIType.LikeAbilityDollDetailView);
        LikeAbilityDollDetailViewModel model = controller.GetModel<LikeAbilityDollDetailViewModel>();
        model.SetTargetDoll(characterDataId, Model.GetDollDataIdList());
        UIManager.Instance.EnterAsync(controller).Forget();
    }

    private List<int> GetFavoriteList()
    {
        List<int> favoriteDollDataIds = new List<int>();

        string favoriteDollData = PlayerPrefs.GetString(StringDefine.KEY_PLAYER_PREFS_FAVORITE_DOLL_LIST);
        if (string.IsNullOrEmpty(favoriteDollData))
            return favoriteDollDataIds;

        string[] favoriteDollDatas = favoriteDollData.Split(',');

        for (int i = 0; i < favoriteDollDatas.Length; i++)
            favoriteDollDataIds.Add(int.Parse(favoriteDollDatas[i]));

        return favoriteDollDataIds;
    }
    #endregion Coding rule : Function
}