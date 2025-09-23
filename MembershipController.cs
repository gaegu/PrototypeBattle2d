//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.LowLevel.Server.Web;
using IronJade.Server.Web.Core;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
using UnityEngine;
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class MembershipController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.MembershipView; } }
    public override void SetModel() { SetModel(new MembershipViewModel()); }
    private MembershipView View { get { return base.BaseView as MembershipView; } }
    private MembershipViewModel Model { get { return GetModel<MembershipViewModel>(); } }
    //=========================================================================================================
    //============================================================
    //=========    Coding rule에 맞춰서 작업 바랍니다.   =========
    //========= Coding rule region은 절대 지우지 마세요. =========
    //=========    문제 시 '김철옥'에게 문의 바랍니다.   =========
    //============================================================

    #region Coding rule : Property
    #endregion Coding rule : Property

    #region Coding rule : Value
    public bool isTest = false;
    #endregion Coding rule : Value

    #region Coding rule : Function
    public override void Enter()
    {
        Model.SetEventShowEffectDescriptionPopup(OnEventShowEffectDescriptionPopup);
        Model.SetEventClose(OnEventClose);
        Model.SetEventRefreshAll(OnEventRefreshAll);

        SetGradeModels();
    }

    public override async UniTask LoadingProcess()
    {
        if (isTest)
            await RequestMembershipGet();
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();
    }

    public override void Refresh()
    {
        View.RefreshAsync().Forget();
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Membership/MembershipView";
    }

    public override void InitializeTestModel(params object[] parameters)
    {
        isTest = true;
    }

    private void SetGradeModels()
    {
        User user = PlayerManager.Instance.MyPlayer.User;
        StageDungeonGroupModel stageDungeonModel = DungeonManager.Instance.GetDungeonGroupModel<StageDungeonGroupModel>();

        Model.SetGradeModels(user, stageDungeonModel, user.MembershipModel);
    }

    private void ShowEffectDescriptionPopup()
    {
        UIManager.Instance.EnterAsync(UIType.MembershipGradeEffectPopup).Forget();
    }

    private void OnEventShowEffectDescriptionPopup()
    {
        ShowEffectDescriptionPopup();
    }

    private void OnEventClose()
    {
        Exit().Forget();
    }

    private async UniTask RequestMembershipGet()
    {
        var membershipGetProcess = NetworkManager.Web.GetProcess(WebProcess.MembershipGet);

        if (await membershipGetProcess.OnNetworkAsyncRequest())
            membershipGetProcess.OnNetworkResponse();
    }

    private void OnEventRefreshAll()
    {
        SetGradeModels();

        View.ShowAsync().Forget();
    }

    public override async UniTask TutorialStepAsync(System.Enum type)
    {
        TutorialExplain stepType = (TutorialExplain)type;

        switch (stepType)
        {
            case TutorialExplain.MembershipGradeInfoSlot:
                {
                    View.GetMembershipGradeInfoUnit(0).OnClickToggleDetail();
                    break;
                }

            case TutorialExplain.MembershipGradeInfoDetailSlot:
                {
                    MembershipActiveUnit unit = await View.GetMembershipGradeInfoUnit(0).GetMembershipActiveUnit(0);
                    unit.OnClickToggleDetail();
                    break;
                }

            case TutorialExplain.MembershipGradeInfoDetailActive:
                {
                    MembershipActiveUnit unit = await View.GetMembershipGradeInfoUnit(0).GetMembershipActiveUnit(0);
                    unit.OnClickActivate();
                    await TutorialManager.WaitUntilEnterUI(UIType.MembershipUnlockPopup);
                    break;
                }
        }
    }

    public override async UniTask<GameObject> GetTutorialFocusObject(string stepKey)
    {
        TutorialExplain stepType = (TutorialExplain)System.Enum.Parse(typeof(TutorialExplain), stepKey);

        switch (stepType)
        {
            case TutorialExplain.MembershipGradeInfoSlot:
                {
                    return View.GetMembershipGradeInfoUnit(0).gameObject;
                }

            case TutorialExplain.MembershipGradeInfoDetailSlot:
                {
                    MembershipActiveUnit unit = await View.GetMembershipGradeInfoUnit(0).GetMembershipActiveUnit(0);
                    return unit.gameObject;
                }

            case TutorialExplain.MembershipGradeInfoDetailActive:
                {
                    MembershipActiveUnit unit = await View.GetMembershipGradeInfoUnit(0).GetMembershipActiveUnit(0);
                    return unit.ActiveButton;
                }

            case TutorialExplain.MembershipReward:
                {
                    return View.GetMembershipGradeInfoUnit(0).RewardGroup;
                }

            default: return await base.GetTutorialFocusObject(stepKey);
        }
    }
    #endregion Coding rule : Function
}