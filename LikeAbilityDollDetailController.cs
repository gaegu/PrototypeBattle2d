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
using Febucci.UI.Core;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
using UnityEngine;
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class LikeAbilityDollDetailController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.LikeAbilityDollDetailView; } }
    public override void SetModel() { SetModel(new LikeAbilityDollDetailViewModel()); }
    private LikeAbilityDollDetailView View { get { return base.BaseView as LikeAbilityDollDetailView; } }
    private LikeAbilityDollDetailViewModel Model { get { return GetModel<LikeAbilityDollDetailViewModel>(); } }
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
        Model.SetEventToggleFavorite(OnEventToggleFavorite);
        Model.SetEventChangeDoll(OnEventChangeDoll);
        Model.SetEventGift(OnEventGift);
        Model.SetEventNaviiChat(OnEventNaviiChat);
        Model.SetEventAbility(OnEventAbility);
    }

    public override async UniTask LoadingProcess()
    {
        Model.SetDollDetailModel(GetFavoriteList(), PlayerManager.Instance.MyPlayer.User.CharacterModel);
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();
    }

    public override void Refresh()
    {
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "LikeAbility/LikeAbilityDollDetailView";
    }

    public void OnEventToggleFavorite()
    {
        List<int> favoriteLists = GetFavoriteList();

        if (favoriteLists.Contains(Model.TargetDollCharacterDataId))
            favoriteLists.Remove(Model.TargetDollCharacterDataId);
        else
            favoriteLists.Add(Model.TargetDollCharacterDataId);

        string favoriteDollData = string.Join(",", favoriteLists);
        PlayerPrefsWrapper.SetString(StringDefine.KEY_PLAYER_PREFS_FAVORITE_DOLL_LIST, favoriteDollData);

        Model.SetFavorite(!Model.IsFavorite);
        View.ShowFavorite();
    }

    public void OnEventChangeDoll(bool isNext)
    {
        int prevIndex = Model.DollDataIdList.IndexOf(Model.TargetDollCharacterDataId);
        int nextIndex;
        if (isNext)
            nextIndex = prevIndex + 1 >= Model.DollDataIdList.Count ? 0 : prevIndex + 1;
        else
            nextIndex = prevIndex - 1 < 0 ? Model.DollDataIdList.Count - 1 : prevIndex - 1;

        Model.SetTargetDollCharacter(Model.DollDataIdList[nextIndex]);
        Model.SetDollDetailInfo(GetFavoriteList());

        View.ShowAsync().Forget();
    }

    public void OnEventGift()
    {
        if (Model.IsMaxLevel)
        {
            MessageBoxManager.ShowToastMessage("@Already Max Level");
            return;
        }
        BaseController baseController = UIManager.Instance.GetController(UIType.GiftPopup);
        GiftPopupModel giftPopupModel = baseController.GetModel<GiftPopupModel>();
        giftPopupModel.SetDataCharacterId(Model.TargetDollCharacterDataId);
        giftPopupModel.SetGiftTargetType(CharacterLikeAbilityType.Doll);
        UIManager.Instance.EnterAsync(baseController).Forget();
    }

    public void OnEventNaviiChat()
    {

    }

    public void OnEventAbility()
    {
        BaseController controller = UIManager.Instance.GetController(UIType.LikeAbilityStatPopup);
        LikeAbilityStatPopupModel model = controller.GetModel<LikeAbilityStatPopupModel>();
        model.SetStatInfoModel(Model.TargetCharacter, CharacterLikeAbilityType.Doll);
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